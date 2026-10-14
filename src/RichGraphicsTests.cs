using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace GeMMeElec;

internal static class RichGraphicsTests
{
    internal static Editor Example()
    {
        var e=new Editor();e.SetSheetKind("cover");e.Change(()=>e.Project.Title="Rectangles et tableaux");
        var labels=new[]{("DESIGNER : ","L.BUTENAERS"),("APPROVED BY : ","G.DISLAIRE"),("CREATION DATE : ","27/07/2026")};
        for(int r=0;r<labels.Length;r++){var pair=labels[r];e.PutGraphic(new(){Kind="textbox",X=60,Y=50+r*42,Width=360,Height=42,Color="#000000",Content=new(){Text=pair.Item1+pair.Item2,FontSize=16,Color="#000000",Runs=[new(){Text=pair.Item1,Bold=true},new(){Text=pair.Item2}]}});}
        var table=FreeTable.Create(8,6,120,28);table.ColumnWidths=[100,240,100,130,130,130];string[] values=["A","CREATION","27/07/2026","L.BUTENAERS","G.DISLAIRE","G.DISLAIRE"],heads=["INDICE","MODIFICATION","DATE","DESIGNED BY","VERIFIED BY","APPROVED BY"];
        for(int c=0;c<6;c++){table.Cells[6*6+c]=new(){Text=values[c],FontSize=15,Color="#000000",Align="center"};table.Cells[7*6+c]=new(){Text=heads[c],Bold=true,FontSize=14,Color="#000000",Align="center"};}
        e.PutGraphic(new(){Kind="table",X=500,Y=50,Width=table.ColumnWidths.Sum(),Height=table.RowHeights.Sum(),Table=table,Color="#000000"});
        e.PutNote(new(){X=60,Y=310,Text="Normal · gras · italique · gras italique",FontSize=20,Runs=[new(){Text="Normal · "},new(){Text="gras · ",Bold=true},new(){Text="italique · ",Italic=true},new(){Text="gras italique",Bold=true,Italic=true}]});return e;
    }
    public static void Run(Action<string,Action> test,Action<bool,string> assert,Action<Action> reject,string dir)
    {
        test("Texte formaté : rectangle et tableau inspirés de la référence, exports vectoriels",()=>
        {
            var e=Example();ProjectStore.ValidateStructure(e.Project);var scene=Drawing.Scene(e.Project,e.Sheet);assert(scene.Any(i=>i.Text=="DESIGNER : "&&i.Bold)&&scene.Any(i=>i.Text=="L.BUTENAERS"&&!i.Bold),"Gras partiel");assert(scene.Any(i=>i.Text=="DESIGNED BY"&&i.Bold),"En-tête tableau");
            Export.Svg(e.Project,e.Sheet,Path.Combine(dir,"self-test-rich-graphics.svg"));Export.Pdf(e.Project,Path.Combine(dir,"self-test-rich-graphics.pdf"));ProjectStore.Save(e.Project,Path.Combine(dir,"self-test-rich-graphics.gemelec"));SaveScene(scene,Path.Combine(dir,"self-test-rich-graphics.png"),1400,360);
            var svg=XDocument.Load(Path.Combine(dir,"self-test-rich-graphics.svg"));assert(svg.Descendants().Any(x=>x.Attribute("font-weight")?.Value=="bold")&&svg.Descendants().Any(x=>x.Attribute("font-style")?.Value=="italic")&&svg.Descendants().Any(x=>x.Name.LocalName=="clipPath"),"SVG sans styles/limites");var pdf=System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(dir,"self-test-rich-graphics.pdf")));assert(pdf.Contains("/SegoeUI-Bold")&&pdf.Contains("/SegoeUI-Italic")&&pdf.Contains("/SegoeUI-Bold-Italic")&&pdf.Contains(" re W n"),"Polices et limites PDF");assert(ProjectStore.Serialize(ProjectStore.Load(Path.Combine(dir,"self-test-rich-graphics.gemelec")))==e.ExportSnapshot,"Relecture rich");
        });
        test("Tableau : cellules, dimensions individuelles et redimensionnement proportionnel",()=>
        {
            var e=new Editor();var t=FreeTable.Create(3,4,100,40);t.ColumnWidths=[80,120,150,90];t.RowHeights=[30,40,50];var g=e.PutGraphic(new(){Kind="table",Table=t,X=100,Y=100,Width=440,Height=120});e.SetTableCell(g.Id,1,2,new(){Text="Cellule",Bold=true,Italic=true});string before=e.ExportSnapshot;e.SetElementCoordinate(g.Id,"Width",50,true);assert(e.Sheet.Graphics[0].Width==700&&e.Sheet.Graphics[0].Table!.ColumnWidths[0]==80*700.0/440&&e.Sheet.Graphics[0].Table!.Cells[6].Text=="Cellule","Taille/cellule");e.Undo();assert(e.ExportSnapshot==before,"Undo taille");
            var changed=DocumentModel.Copy(e.Sheet.Graphics[0]);changed.Table!.Reconfigure(4,5,110,35);changed.Width=changed.Table.ColumnWidths.Sum();changed.Height=changed.Table.RowHeights.Sum();e.PutGraphic(changed);assert(e.Sheet.Graphics[0].Table!.Cells[7].Text=="Cellule"&&e.Sheet.Graphics[0].Table!.Cells.Count==20,"Redimensionner grille déplace contenu");e.Undo();assert(e.ExportSnapshot==before,"Undo grille");
        });
        test("Tableau : déplacement, poignées et cache partiel identiques au dessin complet",()=>
        {
            var e=Example();var g=e.Sheet.Graphics.Last();var canvas=new SheetCanvas(e);canvas.Measure(new(1000,760));canvas.Arrange(new Rect(0,0,1000,760));canvas.Select(g.Id);string before=e.ExportSnapshot;canvas.BeginSelectionMove(new(g.X,g.Y));canvas.MoveSelection(new(g.X+20,g.Y+50));canvas.EndPreviewMove();assert(e.Sheet.Graphics.Last().X==520,"Déplacement");e.Undo();assert(e.ExportSnapshot==before,"Undo déplacement");g=e.Sheet.Graphics.Last();canvas.Select(g.Id);assert(canvas.ResizeStartForTest(g.Bounds().BottomRight),"Poignée tableau");canvas.ResizeForTest(new(g.X+500,g.Y+300));canvas.EndPreviewMove();assert(Math.Abs(e.Sheet.Graphics.Last().Table!.ColumnWidths.Sum()-e.Sheet.Graphics.Last().Width)<.001,"Grille non redimensionnée");e.Undo();assert(e.ExportSnapshot==before,"Undo poignées");
            var cache=new CanvasScene(e.Project,e.Sheet,new());e.Sheet.Graphics[0].X+=20;cache.GraphicsChanged([e.Sheet.Graphics[0].Id]);assert(JsonSerializer.Serialize(cache.SceneForTest())==JsonSerializer.Serialize(Drawing.Scene(e.Project,e.Sheet,new())),"Cache rich différent");
        });
        test("Texte : notes mixtes, ancrage et champs liés coupés entre styles",()=>
        {
            var e=new Editor();e.Change(()=>e.Project.Title="Titre du dossier");var n=e.PutNote(new(){X=700,Y=200,Text="{project.title}",Align="center",ResolveFields=true,Runs=[new(){Text="{project.",Bold=true},new(){Text="title}"}]});var ink=FreeGraphics.NoteInk(e.Project,e.Sheet,n);assert(ink.Text==e.Project.Title&&ink.Runs![0].Bold&&Math.Abs(ink.X+Drawing.Measure(ink.Text,ink.Size,ink.Bold,ink.Italic,ink.Runs).Width/2-700)<.001,"Champ/ancrage");string before=e.ExportSnapshot;e.SetTextStyle(n.Id,"",null,true);assert(e.Sheet.Notes[0].Runs[0].Bold&&e.Sheet.Notes[0].Runs.All(r=>r.Italic),"Italique efface gras partiel");e.Undo();assert(e.ExportSnapshot==before,"Undo style");
        });
        test("Textes existants : symboles, bornes, reports et numéros de conducteur",()=>
        {
            var e=new Editor();var c=e.AddComponent("coil",200,200);var terminal=e.AddComponent("terminal",400,500);var w=e.Connect(new(c.Id,"A2"),new(terminal.Id,"1"));e.SetTextStyle(c.Id,"tag",true,true);e.SetTextStyle(c.Id,"port:A1",true,null);e.SetTextStyle(w.Id,"",true,true);assert(ComponentTexts.Ink(e.Project,c,new(e.Project),"tag") is {Bold:true,Italic:true}&&ComponentTexts.Ink(e.Project,c,new(e.Project),"port:A1") is {Bold:true},"Symboles/ports");assert(Drawing.WireLabel(w,w.Route(e.Sheet)) is {Bold:true,Italic:true},"Numéro fil");var report=e.AddComponent("folio_next",600,300);e.SetTextStyle(report.Id,"detail",true,true);assert(ComponentTexts.Ink(e.Project,report,new(e.Project),"detail") is {Bold:true,Italic:true},"Destination calculée");ProjectStore.ValidateStructure(e.Project);
        });
        test("Cartouche : gras/italique et mise en forme partielle avec champs",()=>
        {
            var e=new Editor();var t=new TitleBlockTemplate{Id="rich-cartouche",Height=60,Cells=[new(){X=0,Y=0,Width=100,Height=100,Text="Auteur : {project.author}",FontSize=16,Runs=[new(){Text="Auteur : ",Bold=true},new(){Text="{project.author}",Italic=true}]}]};e.Change(()=>{e.Project.Author="Bureau d’études";e.Project.TitleBlocks.Add(t);e.Sheet.TitleBlockId=t.Id;});var scene=DocumentModel.Scene(e.Project,e.Sheet);assert(scene.Any(i=>i.Text.Contains("Auteur")&&i.Bold)&&scene.Any(i=>i.Text.Contains("Bureau")&&i.Italic),"Styles cartouche");assert(ProjectStore.Serialize(ProjectStore.Deserialize(e.ExportSnapshot))==e.ExportSnapshot,"Relecture cartouche");
        });
        test("Texte/tableau : validation atomique, tailles et cellules hors limites",()=>
        {
            var e=Example();string before=e.ExportSnapshot;reject(()=>e.PutNote(new(){Text="texte",Runs=[new(){Text="autre",Bold=true}]}));reject(()=>FreeTable.Create(1,1,0,40));reject(()=>FreeTable.Create(100,40,120,40));reject(()=>e.SetTableCell(e.Sheet.Graphics.Last().Id,99,0,new(){Text="Test"}));reject(()=>e.PutGraphic(new(){Kind="table",Table=FreeTable.Create(2,2,120,40),X=100,Y=100,Width=160,Height=80}));assert(e.ExportSnapshot==before,"Rollback rich/tableau");
        });
        test("Copier/coller : rectangles et tableaux formatés autonomes",()=>
        {
            var e=Example();string source=e.ExportSnapshot;var block=SelectionClipboard.Read(SelectionClipboard.Copy(e.Project,e.Sheet,e.Sheet.Graphics.Select(g=>g.Id)));var target=new Editor();target.SetSheetKind("cover");string before=target.ExportSnapshot;target.InsertBlock(block,new(30,30));assert(target.Sheet.Graphics.Count==4&&target.Sheet.Graphics[0].Content.Runs[0].Bold&&target.Sheet.Graphics.Last().Table!.Cells[42].Bold,"Copie styles/cellules");target.Change(()=>target.Sheet.Graphics.Last().Table!.Cells[42].Text="COPIE");assert(e.ExportSnapshot==source,"Contenu partagé avec la source");target.Undo();target.Undo();assert(target.ExportSnapshot==before,"Undo collage rich");
        });
        test("Compatibilité : formats 1 à 9 sans styles, blocs de la version précédente",()=>
        {
            for(int version=1;version<10;version++){var p=new Project();p.Sheets[0].Notes.Add(new(){X=100,Y=100,Text="Ancien texte"});var node=JsonNode.Parse(ProjectStore.Serialize(p))!;node["version"]=version;var note=node["sheets"]![0]!["notes"]![0]!.AsObject();note.Remove("bold");note.Remove("italic");note.Remove("runs");var migrated=ProjectStore.Deserialize(node.ToJsonString());assert(migrated.Version==10&&!migrated.Sheets[0].Notes[0].Bold&&migrated.Sheets[0].Notes[0].Runs.Count==0,"Migration "+version);}
            var e=new Editor();var g=e.PutGraphic(new(){X=100,Y=100});var block=SelectionClipboard.Read(SelectionClipboard.Copy(e.Project,e.Sheet,[g.Id]));block.Project.Version=9;CircuitBlocks.Validate(block);assert(block.Project.Version==10,"Ancien bloc refusé");
        });
        test("Éditeur riche : sélection partielle, boutons gras/italique et enregistrement",()=>
        {
            var window=new MainWindow();try{RichTextContent? result=null;window.DocumentTestProbe=dialog=>{Arrange((FrameworkElement)dialog.Content,760,650);var controls=Descendants((DependencyObject)dialog.Content).ToList();var box=controls.OfType<RichTextBox>().Single();var run=(Run)((Paragraph)box.Document.Blocks.FirstBlock).Inlines.FirstInline;box.Selection.Select(run.ContentStart,run.ContentStart.GetPositionAtOffset(8)!);controls.OfType<Button>().Single(b=>b.Name=="RichBoldButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));controls.OfType<Button>().Single(b=>b.Name=="RichItalicButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));controls.OfType<Button>().Single(b=>b.Content as string=="Enregistrer le texte").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};window.FormattedTextDialog("Texte",new(){Text="DESIGNER : NOM"},value=>result=value);assert(result is not null&&result.Text=="DESIGNER : NOM"&&result.Runs[0].Bold&&result.Runs[0].Italic&&result.Runs.Skip(1).All(r=>!r.Bold&&!r.Italic),"Sélection remplacée ou style global");
            }finally{window.DocumentTestProbe=null;window.DisposeTestBridge();}
        });
        test("Interface tableau : grille, dimensions individuelles, contenu et placement par clic",()=>
        {
            var window=new MainWindow();try{string before=window.Editor.ExportSnapshot;window.DocumentTestProbe=dialog=>{Arrange((FrameworkElement)dialog.Content,1110,800);var controls=Descendants((DependencyObject)dialog.Content).ToList();controls.OfType<TextBox>().Single(b=>b.Name=="TableRows").Text="2";controls.OfType<TextBox>().Single(b=>b.Name=="TableColumns").Text="3";controls.OfType<Button>().Single(b=>b.Content as string=="Appliquer le nombre de lignes/colonnes").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));controls.OfType<TextBox>().Single(b=>b.Name=="TableColumnWidths").Text="80 ; 150 ; 200";controls.OfType<TextBox>().Single(b=>b.Name=="TableRowHeights").Text="35 ; 70";controls.OfType<Button>().Single(b=>b.Content as string=="Appliquer les largeurs et hauteurs").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var grid=controls.OfType<DataGrid>().Single();((TableRowDraft)grid.Items[0]).Cells[1].Text="Cellule test";controls.OfType<Button>().Single(b=>b.Content as string=="Placer le tableau").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};window.TableDialog(null);assert(window.Editor.ExportSnapshot==before&&window.Canvas.Mode=="graphicTemplate","Insertion avant clic");assert(window.Canvas.GraphicClickForTest(new(100,100)),"Placement");var table=window.Editor.Sheet.Graphics.Single();assert(table.Width==430&&table.Height==105&&table.Table!.Cells[1].Text=="Cellule test","Grille/contenu");window.Editor.Undo();assert(window.Editor.ExportSnapshot==before,"Undo placement");
            }finally{window.DocumentTestProbe=null;window.DisposeTestBridge();}
        });
        test("Présentation : gras/italique pour tous les textes, y compris les repérages générés",()=>
        {
            var e=Example();e.SetSheetKind("drawing");string before=e.ExportSnapshot;e.Change(()=>{e.Project.Presentation.AllTextBold=true;e.Project.Presentation.AllTextItalic=true;});var scene=Drawing.Scene(e.Project,e.Sheet);assert(scene.Where(i=>i.Kind=="text").All(i=>i.Bold&&i.Italic&& (i.Runs is null||i.Runs.All(r=>r.Bold&&r.Italic))),"Texte oublié par le style global");assert(scene.Any(i=>i.Text=="A"&&i.Bold),"Repérage non formaté");var cached=new CanvasScene(e.Project,e.Sheet,new());assert(JsonSerializer.Serialize(cached.SceneForTest())==JsonSerializer.Serialize(scene),"Style global/cache différent");e.Undo();assert(e.ExportSnapshot==before&&e.Sheet.Notes[0].Runs[0].Bold==false,"Style individuel perdu");
        });
        test("Fluidité : déplacement de tableau formaté avec dessin conservé",()=>
        {
            var e=Example();var canvas=new SheetCanvas(e);canvas.Measure(new(1000,760));canvas.Arrange(new Rect(0,0,1000,760));canvas.Fit();void Frame(){var visual=new DrawingVisual();using(var dc=visual.RenderOpen())canvas.RenderForBenchmark(dc);}Frame();var g=e.Sheet.Graphics.Last();canvas.Select(g.Id);var start=new Point(g.X,g.Y);string before=e.ExportSnapshot;canvas.BeginSelectionMove(start);var watch=Stopwatch.StartNew();long allocation=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<120;i++){canvas.MoveSelection(start+new Vector(i%10,20));Frame();}watch.Stop();canvas.CancelDrag();assert(e.ExportSnapshot==before,"Aperçu muté le tableau");File.WriteAllText(Path.Combine(dir,"rich-graphics-performance.json"),JsonSerializer.Serialize(new{rows=8,columns=6,frames=120,totalMs=watch.Elapsed.TotalMilliseconds,meanMs=watch.Elapsed.TotalMilliseconds/120,allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocation},ProjectStore.Json));
        });
    }
    private static void Arrange(FrameworkElement element,double width,double height){element.Measure(new(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent){yield return parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(parent,i)))yield return child;}
    private static void SaveScene(List<Ink> scene,string path,int width,int height){var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new(0,0,width,height));Drawing.Render(dc,scene);}var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);}
}
