using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

public static class ImportEditingTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> assert,Action<Action> reject,string dir)
    {
        List<Ink> Scene(Editor e)=>Drawing.Scene(e.Project,e.Sheet);
        test("Jonction dynamique : borne commune, divergence, déplacement et suppression",()=>{
            var e=new Editor();var a=e.AddComponent("coil",200,150);var b=e.AddComponent("coil",500,500);var c=e.AddComponent("coil",800,500);
            var one=e.Connect(new(a.Id,"A2"),new(b.Id,"A1"),[new(200,300),new(500,300)]);assert(!Scene(e).Any(x=>x.Kind=="dot"),"Pas de point pour un seul fil");
            var two=e.Connect(new(a.Id,"A2"),new(c.Id,"A1"),[new(200,300),new(500,300),new(500,400),new(800,400)]);
            assert(Scene(e).Any(x=>x.Kind=="dot"&&x.X==500&&x.Y==400),"Point à la divergence réelle du trajet commun");assert(e.Sheet.Components.Count==3,"Aucun composant de jonction ajouté");
            e.Change(()=>two.Waypoints=[new(200,250),new(800,250)]);assert(Scene(e).Any(x=>x.Kind=="dot"&&x.X==200&&x.Y==250),"Point suit le tracé");
            e.Delete([two.Id]);assert(!Scene(e).Any(x=>x.Kind=="dot"),"Point disparu avec le deuxième fil");e.Undo();assert(Scene(e).Any(x=>x.Kind=="dot"),"Undo restaure la connexion et son point");
            Export.Pdf(e.Project,Path.Combine(dir,"dynamic-junctions.pdf"));Render(e.Project,e.Sheet,Path.Combine(dir,"dynamic-junctions.png"));
        });
        test("Dérivation : dissolution et déplacement des nœuds automatiques",()=>{
            var e=new Editor();var a=e.AddComponent("coil",200,150);var b=e.AddComponent("coil",200,600);var c=e.AddComponent("coil",600,300);var wire=e.Connect(new(a.Id,"A2"),new(b.Id,"A1"));var original=wire.Route(e.Sheet);
            var branch=e.Branch(new(c.Id,"A1"),wire.Id,new(200,350));var j=e.Sheet.Components.Single(Junctions.Automatic);var before=ProjectStore.Serialize(e.Project);var route=wire.Route(e.Sheet);
            e.Change(()=>Junctions.ShiftSegment(e.Sheet,wire.Id,route,route.Count-1,new(250,350)));assert(j.X==250,"Nœud suit le segment");assert(CrossReferences.Trace(e.Project,new(a.Id,"A2")).Contains(new Endpoint(c.Id,"A1")),"Continuité après déplacement");e.Undo();assert(ProjectStore.Serialize(e.Project)==before,"Déplacement annulable");
            e.Delete([branch.Id]);assert(!e.Sheet.Components.Any(Junctions.Automatic)&&e.Sheet.Wires.Count==1,"Tronçons restants fusionnés");assert(e.Sheet.Wires[0].Route(e.Sheet).SequenceEqual(original),"Tracé initial conservé");e.Undo();assert(e.Sheet.Components.Any(Junctions.Automatic),"Restauration complète");
        });
        test("Reports : triangle noir, nom, destination et zone cliquable",()=>{
            var e=new Editor();var a=e.AddComponent("folio_next",200,200);e.ConfigureReference(a.Id,"DI1.4","pair",0,false);e.AddSheet("Arrivée");var b=e.AddComponent("folio_previous",600,300);e.ConfigureReference(b.Id,"DI1.4","pair",0,false);var refs=new CrossReferences(e.Project);var ink=ReferenceDisplay.Scene(e.Project,a,refs);
            assert(ink.Count(x=>x.Kind=="line")==4&&ink.Where(x=>x.Kind=="line").All(x=>x.Color=="#000000")&&ink.Any(x=>x.Kind=="text"&&x.Color=="#008000")&&ink.Any(x=>x.Text=="DI1.4"),"Présentation du report");var text=ink.Single(x=>x.Text=="DI1.4");assert(ReferenceDisplay.HitBounds(e.Project,a,refs).Contains(new Point(text.X+20,text.Y+5)),"Nom inclus dans le double-clic");
            Export.Pdf(e.Project,Path.Combine(dir,"inline-reports.pdf"));Render(e.Project,e.Project.Sheets[0],Path.Combine(dir,"inline-reports.png"));
        });
        test("Symboles personnels : copie indépendante, bornes, sauvegarde, BOM et rollback",()=>{
            var e=new Editor();var a=e.AddComponent("coil",200,200);var b=e.AddComponent("coil",400,400);e.Connect(new(a.Id,"A2"),new(b.Id,"A1"));var source=Symbols.All["coil"];var draft=source with{Name="Bobine personnalisée",Shapes=[..source.Shapes,new("text",-15,-5,10,Text:"TEST")]};var d=e.SaveSymbolCopy(draft,a.Id);
            assert(a.Symbol==d.Id&&Symbols.All["coil"].Shapes.Count==source.Shapes.Count,"Original intact");var before=ProjectStore.Serialize(e.Project);reject(()=>e.SaveSymbolCopy(d with{Ports=[]},a.Id));assert(ProjectStore.Serialize(e.Project)==before,"Borne raccordée protégée");
            e.Save(Path.Combine(dir,"custom-symbols.gemelec"));var loaded=ProjectStore.Load(e.FilePath!);assert(loaded.Sheets[0].Components[0].Definition.Name==draft.Name,"Dessin conservé");CustomSymbols.Export(d,Path.Combine(dir,"custom-symbol.gemsymbol"));assert(CustomSymbols.Load(Path.Combine(dir,"custom-symbol.gemsymbol")).Ports.Count==2,"Échange de symbole");assert(BomReport.Html(loaded,new()).Contains("TEST"),"Symbole personnel dans la BOM");e.GenerateBom(new());Export.Pdf(e.Project,Path.Combine(dir,"custom-symbol-bom.pdf"));
        });
        test("DXF : blocs transformés, arcs et absence de bornes inventées",()=>{
            var document=new netDxf.DxfDocument();var block=new netDxf.Blocks.Block("CONTACT");block.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(-10,0),new netDxf.Vector2(10,0)));block.Entities.Add(new netDxf.Entities.Circle(new netDxf.Vector3(0,0,0),5));document.Entities.Add(new netDxf.Entities.Insert(block,new netDxf.Vector3(30,20,0)){Rotation=90,Scale=new(2,2,2)});document.Entities.Add(new netDxf.Entities.Arc(new netDxf.Vector3(60,20,0),10,0,180));document.Entities.Add(new netDxf.Entities.Text("K1",new netDxf.Vector2(20,40),4));
            string file=Path.Combine(dir,"manufacturer-symbol-fixture.dxf");assert(document.Save(file),"Fixture DXF");var imported=DxfImport.Read(file);assert(imported.Symbol.Shapes.Count>=50&&imported.Symbol.Ports.Count==0,"Dessin sans bornes inventées");assert(imported.Symbol.Shapes.Any(s=>s.Kind=="line"&&Math.Abs(s.X-s.A)<.01&&Math.Abs(s.Y-s.B)>39),"Rotation et échelle du bloc");
            var e=new Editor();var d=e.SaveSymbolCopy(imported.Symbol);e.AddComponent(d.Id,400,300);e.Save(Path.Combine(dir,"import-dxf.gemelec"));Render(e.Project,e.Sheet,Path.Combine(dir,"import-dxf.png"));var result=ProjectImport.DxfPages([file,file]);assert(result.Project.Sheets.Count==2&&result.Project.Sheets.All(s=>s.Wires.Count==0),"Import multipage graphique");assert(BomReport.Rows(result.Project,new()).Count==0,"Aucun faux article BOM");
        });
        test("QET : connectivité, métadonnées et protection XML",()=>{
            string xml="""
            <project title="Conversion vérifiée" version="0.9"><diagram title="Puissance" author="Test" cols="20" colsize="60" rows="10" rowsize="80"><elements>
            <element type="embed://import/coil.elmt" x="200" y="100" orientation="0"><terminals><terminal id="0" x="0" y="-50"/><terminal id="1" x="0" y="50"/></terminals><elementInformations><elementInformation name="label">K1</elementInformation></elementInformations><inputs><input text="K1" x="20" y="0"/></inputs></element>
            <element type="embed://import/coil.elmt" x="200" y="400" orientation="1"><terminals><terminal id="2" x="0" y="-50"/><terminal id="3" x="0" y="50"/></terminals><inputs><input text="K2" x="20" y="0"/></inputs></element></elements>
            <conductors><conductor terminal1="1" terminal2="2" num="24V" conductor_color="#123456"><segment orientation="vertical" length="150"/><segment orientation="horizontal" length="50"/><segment orientation="vertical" length="100"/></conductor></conductors></diagram>
            <collection><category name="import"><element name="coil.elmt"><definition width="80" height="100"><names><name lang="fr">Bobine QET</name></names><description><rect x="-20" y="-15" width="40" height="30"/><line x1="0" y1="-50" x2="0" y2="-15"/><line x1="0" y1="15" x2="0" y2="50"/></description></definition></element></category></collection></project>
            """;string file=Path.Combine(dir,"qet-import-fixture.qet");File.WriteAllText(file,xml);var imported=ProjectImport.Qet(file);var p=imported.Project;var s=p.Sheets[0];assert(s.Components.Count==2&&s.Wires.Count==1&&s.Components[0].Tag=="K1"&&s.Author=="Test","Appareils et propriétés");assert(s.Wires[0].Color=="#123456"&&s.Components[1].Rotation==90,"Couleur et rotation");assert(CrossReferences.Trace(p,s.Wires[0].From).Contains(s.Wires[0].To),"Connexion aux bornes");ProjectStore.Save(p,Path.Combine(dir,"qet-import-fixture.gemelec"));Export.Pdf(p,Path.Combine(dir,"qet-import-fixture.pdf"));
            string bad=Path.Combine(dir,"qet-import-invalid.qet");File.WriteAllText(bad,"<!DOCTYPE project [<!ENTITY x SYSTEM 'file:///does-not-exist'>]><project>&x;</project>");reject(()=>ProjectImport.Qet(bad));File.WriteAllText(bad,"<project/>");reject(()=>ProjectImport.Qet(bad));
        });
        test("QET : conversion de quatre projets d’exemple installés",()=>{
            List<object> reports=[];foreach(var name in new[]{"schema_indus.qet","industrial.qet","perceuse.qet","convertisseur.qet"}){string file=Path.Combine(@"C:\Program Files\QElectroTech\examples",name);if(!File.Exists(file))continue;var timer=Stopwatch.StartNew();var converted=ProjectImport.Qet(file);timer.Stop();reports.Add(new{name,folios=converted.Project.Sheets.Count,components=converted.Project.Sheets.Sum(s=>s.Components.Count),wires=converted.Project.Sheets.Sum(s=>s.Wires.Count),elapsedMs=timer.Elapsed.TotalMilliseconds,converted.Warnings});ProjectStore.Save(converted.Project,Path.Combine(dir,"converted-"+Path.GetFileNameWithoutExtension(name)+".gemelec"));if(name=="schema_indus.qet"){Export.Pdf(converted.Project,Path.Combine(dir,"converted-qet.pdf"));Render(converted.Project,converted.Project.Sheets[0],Path.Combine(dir,"converted-qet.png"));}}File.WriteAllText(Path.Combine(dir,"conversion-validation.json"),JsonSerializer.Serialize(reports,ProjectStore.Json));
        });
        test("Éditeur de symbole : commandes et aperçu hors écran",()=>{
            string? old=Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION");Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION","1");var app=Application.Current??new Application();var window=new MainWindow();try{var c=window.Editor.AddComponent("coil",200,200);window.DocumentTestProbe=dialog=>{var body=(FrameworkElement)dialog.Content;body.Measure(new(1210,780));body.Arrange(new(0,0,1210,780));body.UpdateLayout();Save(body,Path.Combine(dir,"ui-symbol-editor.png"),1210,780);var buttons=Descendants(body).OfType<Button>().ToArray();buttons.Single(b=>b.Content is string s&&s=="+ Borne").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var canvas=Descendants(body).OfType<SymbolCanvas>().Single();canvas.Begin(new Point(40,0));canvas.End();buttons.Single(b=>b.Content is string s&&s=="Enregistrer la copie").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};window.EditSymbol(c.Definition,c.Id);window.Canvas.Select(c.Id);typeof(MainWindow).GetMethod("Duplicate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(window,null);assert(window.Editor.Sheet.Components.Count==2,"Duplication de la copie personnalisée");assert(window.Editor.Project.CustomSymbols.Count==1&&window.Editor.Sheet.Components[0].Definition.Ports.Count==3,"Copie enregistrée depuis le dialogue");}finally{window.DocumentTestProbe=null;window.DisposeTestBridge();Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION",old);}
        });
        test("Fluidité : jonctions sur 1 000 conducteurs",()=>{
            var e=new Editor();e.Change(()=>{var a=new Component{Symbol="coil",X=100,Y=100};e.Sheet.Components.Add(a);for(int i=0;i<1000;i++){var b=new Component{Symbol="coil",X=200+i%20*50,Y=200+i/20*12};e.Sheet.Components.Add(b);e.Sheet.Wires.Add(new(){From=new(a.Id,"A2"),To=new(b.Id,"A1")});}});var index=e.Sheet.Components.ToDictionary(c=>c.Id);var routes=e.Sheet.Wires.ToDictionary(w=>w.Id,w=>w.Route(e.Sheet,index));var watch=Stopwatch.StartNew();var dots=Junctions.Dots(e.Sheet,routes);watch.Stop();assert(dots.Count>0,"Points calculés");File.WriteAllText(Path.Combine(dir,"junction-performance.json"),JsonSerializer.Serialize(new{wires=1000,dots=dots.Count,elapsedMs=watch.Elapsed.TotalMilliseconds},ProjectStore.Json));
        });
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject p){yield return p;for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)foreach(var x in Descendants(VisualTreeHelper.GetChild(p,i)))yield return x;}
    private static void Render(Project p,Sheet s,string path){var v=new DrawingVisual();using(var dc=v.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new(0,0,s.Width,s.Height));Drawing.Render(dc,Drawing.Scene(p,s));}Save(v,path,(int)Math.Ceiling(s.Width),(int)Math.Ceiling(s.Height));}
    private static void Save(Visual v,string path,int width,int height){var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(Brushes.White,null,new(0,0,width,height));bitmap.Render(background);bitmap.Render(v);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);}
}
