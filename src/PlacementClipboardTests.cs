using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

internal static class PlacementClipboardTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Position page : conversions précises, bornes et dimensions signées", () =>
        {
            var sheet=new Sheet { Width=1600, Height=1000 };
            assert(PagePlacement.Units(sheet,"X",12.5,true)==200&&PagePlacement.Units(sheet,"Y",12.5,true)==125,"Axes confondus");
            assert(PagePlacement.Display(sheet,"X",200,true)==12.5&&PagePlacement.Units(sheet,"Height",-20,true)==-200,"Conversions");
            foreach(double value in new[]{-1,100.01,double.NaN,double.PositiveInfinity})reject(()=>PagePlacement.Units(sheet,"X",value,true));
            assert(PagePlacement.Units(sheet,"Y",100,true)==1000,"Limite 100 %");reject(()=>PagePlacement.Units(sheet,"Z",10,true));
        });
        test("Position page : symbole raccordé, annulation et sauvegarde", () =>
        {
            var e=new Editor();var a=e.AddComponent("coil",200,200);var b=e.AddComponent("coil",400,600);var w=e.Connect(new(a.Id,"A2"),new(b.Id,"A1"));string before=e.ExportSnapshot;
            e.SetElementCoordinate(a.Id,"X",50,true);assert(a.X==700&&w.Route(e.Sheet)[0]==a.PortPoint("A2"),"Connexion détachée");e.Undo();assert(e.ExportSnapshot==before,"Undo position");e.Redo();
            string positioned=e.ExportSnapshot;reject(()=>e.SetElementCoordinate(a.Id,"Y",101,true));assert(e.ExportSnapshot==positioned,"Refus partiel");
            string path=Path.Combine(dir,"self-test-position.gemelec");e.Save(path);assert(ProjectStore.Serialize(ProjectStore.Load(path))==e.ExportSnapshot,"Relecture");
        });
        test("Position page : texte, forme et dimensions en pourcentages", () =>
        {
            var e=new Editor();e.SetSheetKind("cover");var g=e.PutGraphic(new(){X=100,Y=100,Width=200,Height=100});var n=e.PutNote(new(){X=200,Y=300,Text="Titre",FontSize=30,Align="center"});
            e.SetElementCoordinate(g.Id,"X",25,true);e.SetElementCoordinate(g.Id,"Width",40,true);e.SetElementCoordinate(n.Id,"Y",50,true);
            assert(e.Sheet.Graphics[0].X==350&&e.Sheet.Graphics[0].Width==560&&e.Sheet.Notes[0].Y==495,"Position/dimensions");string before=e.ExportSnapshot;
            reject(()=>e.SetElementCoordinate(g.Id,"X",90,true));assert(e.ExportSnapshot==before,"Forme hors page acceptée");
            var line=e.PutGraphic(new(){Kind="line",X=900,Y=700,Width=-200,Height=0});e.SetElementCoordinate(line.Id,"Width",-20,true);assert(e.Sheet.Graphics.Last().Width==-280,"Ligne inverse");
        });
        test("Position page : centrage du groupe et conservation des coudes", () =>
        {
            var e=new Editor();e.SetSheetKind("cover");var a=e.AddComponent("coil",200,200);var b=e.AddComponent("coil",400,400);var wire=e.Connect(new(a.Id,"A2"),new(b.Id,"A1"),[new(200,300),new(400,300)]);var graphic=e.PutGraphic(new(){X=120,Y=120,Width=400,Height=400});var note=e.PutNote(new(){X=220,Y=500,Text="Groupe"});var ids=new[]{a.Id,b.Id,graphic.Id,note.Id};
            var bounds=PagePlacement.Bounds(e.Project,e.Sheet,ids);var route=wire.Route(e.Sheet);string before=e.ExportSnapshot;e.AlignToPage(ids,"center");var centered=PagePlacement.Bounds(e.Project,e.Sheet,ids);var delta=centered.TopLeft-bounds.TopLeft;
            assert(Math.Abs(centered.Left+centered.Width/2-e.Sheet.Width/2)<.001&&Math.Abs(centered.Top+centered.Height/2-e.Sheet.Height/2)<.001,"Centre du groupe");assert(wire.Route(e.Sheet).SequenceEqual(route.Select(p=>p+delta)),"Coudes perdus");e.Undo();assert(e.ExportSnapshot==before,"Undo alignement");
            reject(()=>e.AlignToPage(ids,"inconnu"));reject(()=>e.AlignToPage([],"center"));
        });
        test("Position page : bords et texte tourné selon ses limites visibles", () =>
        {
            var e=new Editor();e.SetSheetKind("cover");var g=e.PutGraphic(new(){X=100,Y=100,Width=240,Height=120});
            foreach(string op in new[]{"left","right","top","bottom"}){e.AlignToPage([g.Id],op);var bounds=e.Sheet.Graphics[0].Bounds();assert(op switch{"left"=>bounds.Left==0,"right"=>bounds.Right==1400,"top"=>bounds.Top==0,_=>bounds.Bottom==990},"Bord "+op);e.Undo();}
            var n=e.PutNote(new(){X=300,Y=300,Text="Rotation",FontSize=24,Rotation=45,Align="right"});e.AlignToPage([n.Id],"center");var text=FreeGraphics.NoteBounds(e.Project,e.Sheet,e.Sheet.Notes[0]);assert(Math.Abs(text.Left+text.Width/2-700)<.001&&Math.Abs(text.Top+text.Height/2-495)<.001,"Centrage texte tourné");
        });
        test("Presse-papiers : styles, images et symboles personnels entre projets", () =>
        {
            var logo=new BitmapImage(new Uri("pack://application:,,,/assets/logo.png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(logo));string imagePath=Path.Combine(dir,"self-test-clipboard-logo.png");using(var file=File.Create(imagePath))encoder.Save(file);
            var e=new Editor();var definition=e.SaveSymbolCopy(Symbols.All["coil"]);var c=e.AddComponent(definition.Id,300,300);var image=ImageAssets.Import(imagePath);e.Change(()=>e.Project.Images.Add(image.Id,image.Data));var g=e.PutGraphic(new(){Kind="image",ImageId=image.Id,X=500,Y=300,Width=100,Height=100});var shape=e.PutGraphic(new(){Kind="ellipse",X=200,Y=500,Width=180,Height=80,Fill="#E7F5F4",LineStyle="dotted"});var note=e.PutNote(new(){X=350,Y=550,Text="Texte été",Color="#007F86",FontSize=24,Rotation=15});string original=e.ExportSnapshot;
            var block=SelectionClipboard.Read(SelectionClipboard.Copy(e.Project,e.Sheet,[c.Id,g.Id,shape.Id,note.Id]));var target=new Editor();string before=target.ExportSnapshot;target.InsertBlock(block,new(100,100));assert(target.Project.CustomSymbols.Count==1&&target.Sheet.Components[0].Symbol!=definition.Id&&target.Sheet.Components[0].Definition.Ports.SequenceEqual(definition.Ports),"Définition personnelle");assert(target.Project.Images.Values.Single()==image.Data&&target.Sheet.Graphics.Single(x=>x.Kind=="image").ImageId!=image.Id,"Image non autonome");assert(target.Sheet.Notes[0].FontSize==24&&target.Sheet.Notes[0].Rotation==15&&target.Sheet.Graphics.Single(x=>x.Kind=="ellipse").LineStyle=="dotted","Styles perdus");assert(e.ExportSnapshot==original,"Source modifiée");string saved=target.ExportSnapshot;target.Undo();assert(target.ExportSnapshot==before,"Undo définitions/images");target.Redo();assert(target.ExportSnapshot==saved&&ProjectStore.Serialize(ProjectStore.Deserialize(saved))==saved,"Redo/relecture collage");
        });
        test("Presse-papiers : circuit interne et détachement des connexions extérieures", () =>
        {
            var e=new Editor();var a=e.AddComponent("coil",200,200);var b=e.AddComponent("coil",400,400);var outside=e.AddComponent("coil",600,600);var internalWire=e.Connect(new(a.Id,"A2"),new(b.Id,"A1"),[new(200,300),new(400,300)]);e.Connect(new(b.Id,"A2"),new(outside.Id,"A1"));e.SetWireNumberIndependent(internalWire.Id,true);e.SetWireNumber(internalWire.Id,"TEST");e.Change(()=>internalWire.LineStyle="dashed");string original=e.ExportSnapshot;
            var block=SelectionClipboard.Read(SelectionClipboard.Copy(e.Project,e.Sheet,[a.Id,b.Id]));assert(block.DetachedConnections==1&&block.Project.Sheets[0].Wires.Count==1,"Liaison extérieure");assert(e.ExportSnapshot==original,"Copier modifie la source");
            var target=new Editor();target.SetSheetKind("cover");string before=target.ExportSnapshot;var ids=target.InsertBlock(block,new(100,100));var s=target.Sheet;assert(ids.Count==3&&s.Components.Select(c=>c.Id).All(id=>id!=a.Id&&id!=b.Id)&&s.Wires[0].NumberIndependent&&s.Wires[0].LineStyle=="dashed","Copie/style");assert(s.Wires[0].From.ComponentId==s.Components[0].Id&&s.Wires[0].To.ComponentId==s.Components[1].Id,"Bornes non remappées");target.Undo();assert(target.ExportSnapshot==before,"Undo collage");target.Redo();ProjectStore.ValidateStructure(target.Project);
        });
        test("Presse-papiers : conducteur seul inclut ses deux extrémités", () =>
        {
            var e=new Editor();var a=e.AddComponent("terminal",200,200);var b=e.AddComponent("terminal",200,500);var w=e.Connect(new(a.Id,"2"),new(b.Id,"1"));var block=SelectionClipboard.Read(SelectionClipboard.Copy(e.Project,e.Sheet,[w.Id]));assert(block.Project.Sheets[0].Components.Count==2&&block.Project.Sheets[0].Wires.Count==1,"Fil sans bornes");
        });
        test("Presse-papiers : entrée invalide, placement hors page et plans protégés", () =>
        {
            var e=new Editor();var g=e.PutGraphic(new(){X=100,Y=100});var block=SelectionClipboard.Read(SelectionClipboard.Copy(e.Project,e.Sheet,[g.Id]));string before=e.ExportSnapshot;
            reject(()=>SelectionClipboard.Read("{}"));reject(()=>SelectionClipboard.Read("texte brut"));reject(()=>e.InsertBlock(block,new(1400,990)));assert(e.ExportSnapshot==before,"Refus détruit le projet");
            var terminal=e.AddComponent("terminal",200,200);var strip=e.PutTerminalStrip(new(){Terminals=[new(){ComponentId=terminal.Id}]});e.GenerateTerminalPlan(strip.Id);before=e.ExportSnapshot;reject(()=>e.InsertBlock(block,new(100,100)));reject(()=>e.AlignToPage([g.Id],"center"));assert(e.ExportSnapshot==before,"Plan généré modifié");
        });
        test("Interface : unités/% sans mutation, saisie française et centrage", () =>
        {
            var window=new MainWindow();try{var e=window.Editor;e.SetSheetKind("cover");var g=e.PutGraphic(new(){X=140,Y=99,Width=280,Height=198});window.Canvas.Select(g.Id);string before=e.ExportSnapshot;window.PropertyPanel.Measure(new(360,1100));window.PropertyPanel.Arrange(new Rect(0,0,360,1100));window.PropertyPanel.UpdateLayout();
                var mode=Descendants(window.PropertyPanel).OfType<ComboBox>().Single(c=>c.Name=="PageCoordinateMode");mode.SelectedValue="percent";assert(mode.SelectedItem is ComboBoxItem item&&item.Content as string=="Pourcentage de la page · %","Libellé du choix");assert(e.ExportSnapshot==before&&window.PropertyField("graphicX").Text=="10"&&window.PropertyField("graphicWidth").Text=="20","Changer l’unité déplace la forme");window.PropertyField("graphicX").Text="12,5";assert(e.Sheet.Graphics[0].X==175,"Virgule française");
                Descendants(window.PropertyPanel).OfType<Button>().Single(b=>b.Tag as string=="pageAlign:center").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));assert(e.Sheet.Graphics[0].X==560&&e.Sheet.Graphics[0].Y==396,"Bouton centre");
                var body=window.PropertyPanel;body.Measure(new(360,1100));body.Arrange(new Rect(0,0,360,1100));body.UpdateLayout();var bitmap=new RenderTargetBitmap(360,1100,96,96,PixelFormats.Pbgra32);bitmap.Render(body);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(dir,"self-test-page-position-ui.png"));encoder.Save(file);
                var c=e.AddComponent("coil",300,300);window.Canvas.Select(c.Id);window.PropertyField("X").Text="25";assert(c.X==350,"Symbole doublement lié");var n=e.PutNote(new(){X=100,Y=100,Text="Note"});window.Canvas.Select(n.Id);window.PropertyField("noteY").Text="25";assert(e.Sheet.Notes[0].Y==247.5,"Texte %");
            }finally{window.DisposeTestBridge();}
        });
        test("Interface : Copier/Coller entre onglets, Échap et collage annulable", () =>
        {
            var window=new MainWindow();try{string? clipboard=null;window.ClipboardWriteForTest=json=>clipboard=json;window.ClipboardReadForTest=()=>clipboard;var e=window.Editor;var g=e.PutGraphic(new(){X=100,Y=100,Fill="#203348"});window.Canvas.Select(g.Id);string source=e.ExportSnapshot;window.WorkspaceCommandForTest("copy").Execute(null,window);assert(clipboard is not null&&e.ExportSnapshot==source,"Copie source");
                var tab=window.AddProjectTab(title:"Destination");window.Editor.SetSheetKind("cover");string before=window.Editor.ExportSnapshot;window.WorkspaceCommandForTest("paste").Execute(null,window);assert(window.Canvas.Mode=="block"&&window.Editor.ExportSnapshot==before,"Ctrl+V insère avant le clic");window.Canvas.SetMode("select");assert(window.Canvas.PendingBlock is null&&window.Editor.ExportSnapshot==before,"Échap");window.WorkspaceCommandForTest("paste").Execute(null,window);assert(window.Canvas.PlaceBlockForTest(new(0,0)),"Clic collage");assert(window.Editor.Sheet.Graphics.Single().X==0&&window.Canvas.Selected.Count==1,"Copie sélectionnée au bord");window.Editor.Undo();assert(window.Editor.ExportSnapshot==before&&e.ExportSnapshot==source,"Undo/source");
                assert(MainWindow.TextClipboardTarget(new TextBox())&&MainWindow.TextClipboardTarget(new RichTextBox())&&!MainWindow.TextClipboardTarget(window.Canvas),"Raccourcis texte détournés");
                clipboard=null;window.WorkspaceCommandForTest("paste").Execute(null,window);assert(window.Editor.ExportSnapshot==before,"Presse-papiers vide");
            }finally{window.DisposeTestBridge();}
        });
        test("Fluidité : aperçu de collage conservé et libéré après Échap", () =>
        {
            var e=new Editor();e.Change(()=>{for(int i=0;i<400;i++)e.Sheet.Components.Add(new(){Symbol="coil",X=100+i%20*50,Y=100+i/20*35});});var canvas=new SheetCanvas(e);canvas.PendingBlock=SelectionClipboard.Read(SelectionClipboard.Copy(e.Project,e.Sheet,e.Sheet.Components.Select(c=>c.Id)));canvas.SetMode("block");var preview=canvas.BlockPreviewForTest();var watch=Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<120;i++){var visual=new DrawingVisual();using(var dc=visual.RenderOpen())canvas.RenderBlockAt(dc,new(200+i,200+i));assert(ReferenceEquals(preview,canvas.BlockPreviewForTest()),"Aperçu reconstruit");}watch.Stop();string before=e.ExportSnapshot;canvas.SetMode("select");assert(canvas.PendingBlock is null&&e.ExportSnapshot==before,"Échap conserve la copie");File.WriteAllText(Path.Combine(dir,"clipboard-preview-performance.json"),JsonSerializer.Serialize(new{components=400,frames=120,totalMs=watch.Elapsed.TotalMilliseconds,meanMs=watch.Elapsed.TotalMilliseconds/120,allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes},ProjectStore.Json));
        });
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent){yield return parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(parent,i)))yield return child;}
}
