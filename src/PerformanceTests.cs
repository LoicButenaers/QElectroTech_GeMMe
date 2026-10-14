using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

internal static class PerformanceTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> assert,Action<Action> reject,string dir)
    {
        void Same(CanvasScene cached,Editor e,ReadingOptions reading)=>assert(JsonSerializer.Serialize(cached.SceneForTest(),ProjectStore.Json)==JsonSerializer.Serialize(Drawing.Scene(e.Project,e.Sheet,reading),ProjectStore.Json),"Le dessin optimisé diffère du moteur partagé.");
        test("Fluidité : texte seul, tracé et ordre de peinture identiques",()=>{
            var e=new Editor();Examples.Motor(e);var reading=new ReadingOptions();var scene=new CanvasScene(e.Project,e.Sheet,reading);Same(scene,e,reading);int builds=scene.RecordBuilds;var c=e.Sheet.Components[0];c.TagPlacement.OffsetX+=35;scene.TextChanged(c.Id,false);Same(scene,e,reading);assert(scene.RecordBuilds==builds+1,"Tout le folio a été reconstruit pour un texte.");var w=e.Sheet.Wires[0];w.NumberPlacement.OffsetY+=20;scene.TextChanged(w.Id,true);Same(scene,e,reading);
        });
        test("Fluidité : déplacement, fils attachés, points et numéros inchangés",()=>{
            var e=new Editor();Examples.Motor(e);var reading=new ReadingOptions{TextScale=1.5};var scene=new CanvasScene(e.Project,e.Sheet,reading);var c=e.Sheet.Components[0];c.X+=20;c.Y+=10;scene.GeometryChanged([c.Id],[]);Same(scene,e,reading);var note=new Annotation{X=400,Y=200,Text="Note"};e.Change(()=>e.Sheet.Notes.Add(note));scene=new(e.Project,e.Sheet,reading);note.X+=50;scene.GeometryChanged([],[note.Id]);Same(scene,e,reading);
        });
        test("Fluidité : bobines et contacts, références réciproques actualisées",()=>{
            var e=new Editor();var coil=e.AddComponent("coil",200,200);var contact=e.AddComponent("contact_no",400,400);e.AssociateContact(contact.Id,coil.Id);var reading=new ReadingOptions();var scene=new CanvasScene(e.Project,e.Sheet,reading);contact.Y=750;scene.GeometryChanged([contact.Id],[]);Same(scene,e,reading);coil.X=650;scene.GeometryChanged([coil.Id],[]);Same(scene,e,reading);
        });
        test("Fluidité : reports et réordonnancement pendant un déplacement",()=>{
            var e=new Editor();var reports=new[]{e.AddComponent("folio_next",200,200),e.AddComponent("folio_previous",200,400),e.AddComponent("folio_next",600,200),e.AddComponent("folio_previous",600,400)};e.Change(()=>{foreach(var c in reports)c.LinkName="R-CACHE";});var reading=new ReadingOptions{TextScale=1.5};var scene=new CanvasScene(e.Project,e.Sheet,reading);reports[0].Y=650;scene.GeometryChanged([reports[0].Id],[]);Same(scene,e,reading);
        });
        test("Fluidité : marquage de câble et bornier pendant déplacement",()=>{
            var e=new Editor();for(int i=0;i<3;i++){var a=e.AddComponent("terminal",200+i*100,150);var b=e.AddComponent("terminal",200+i*100,650);e.Connect(new(a.Id,"2"),new(b.Id,"1"));}e.InsertTerminalLine(new(160,300),new(440,300),"X1");e.MarkCableLine(new(160,450),new(440,450),"W1",2.5);var reading=new ReadingOptions();var scene=new CanvasScene(e.Project,e.Sheet,reading);var c=e.Sheet.Components[0];c.X+=70;scene.GeometryChanged([c.Id],[]);Same(scene,e,reading);
        });
        test("Fluidité : dessins de plans et filtres de lecture conservés",()=>{
            var e=new Editor();var a=e.AddComponent("terminal",200,200);var strip=e.PutTerminalStrip(new(){Name="X1",Terminals=[new(){ComponentId=a.Id}]});e.GenerateTerminalPlan(strip.Id,"graphic");var reading=new ReadingOptions{TextScale=2,Details=false,Ports=false,Notes=false,WireNumbers=false};Same(new(e.Project,e.Sheet,reading),e,reading);
        });
        test("Fluidité : mêmes bornes survolées, cache renouvelé après annulation",()=>{
            var e=new Editor();var c=e.AddComponent("coil",200,200);var canvas=new SheetCanvas(e);foreach(double zoom in new[]{.08,.6,1.5,4}){canvas.ViewportForBenchmark(zoom,new());foreach(var port in c.Definition.Ports)assert(canvas.HoverForBenchmark(c.PortPoint(port.Id))==new Endpoint(c.Id,port.Id),"Borne différente selon le zoom");}e.Change(()=>c.Rotation=90);assert(canvas.HoverForBenchmark(c.PortPoint("A1"))==new Endpoint(c.Id,"A1"),"Borne après rotation");e.Undo();var restored=e.Sheet.Components.Single();assert(canvas.HoverForBenchmark(restored.PortPoint("A1"))==new Endpoint(c.Id,"A1"),"Borne après undo");
        });
        test("Fluidité : jonction et tous ses fils suivent leur déplacement",()=>{
            var e=new Editor();var a=e.AddComponent("terminal",200,150);var b=e.AddComponent("terminal",200,650);var c=e.AddComponent("terminal",500,350);var w=e.Connect(new(a.Id,"2"),new(b.Id,"1"));e.Branch(new(c.Id,"1"),w.Id,new(200,350));var reading=new ReadingOptions();var scene=new CanvasScene(e.Project,e.Sheet,reading);var node=e.Sheet.Components.Single(Junctions.Automatic);node.X+=50;scene.GeometryChanged([node.Id],[]);Same(scene,e,reading);
        });
        test("Fluidité : aperçu optimisé annulable, JSON et exports identiques",()=>{
            var e=new Editor();Examples.Motor(e);var canvas=new SheetCanvas(e){GridVisible=false};canvas.Measure(new(1000,760));canvas.Arrange(new Rect(0,0,1000,760));var visual=new DrawingVisual();void Frame(){using(var dc=visual.RenderOpen())canvas.RenderForBenchmark(dc);}canvas.Fit();Frame();string before=e.ExportSnapshot;var c=e.Sheet.Components[0];var position=new Point(c.X,c.Y);canvas.Select(c.Id);canvas.BeginSelectionMove(position);for(int i=0;i<6;i++){canvas.MoveSelection(position+new Vector(10+i*10,20));Frame();}assert(e.ExportSnapshot==before,"Sérialisation pendant l’aperçu");canvas.EndPreviewMove();string after=e.ExportSnapshot;e.Undo();assert(e.ExportSnapshot==before,"Undo du déplacement");e.Redo();assert(e.ExportSnapshot==after,"Redo du déplacement");assert(ProjectStore.Serialize(e.Project)==after,"Cache du modèle altéré");Frame();canvas.SetReading(new(){TextScale=2,Details=false});Frame();assert(e.ExportSnapshot==after,"Le filtre a modifié le document");
        });
        test("Fluidité : grille répétée garde positions, intensité et taille",()=>{
            var e=new Editor();var canvas=new SheetCanvas(e){GridVisible=true,GridIntensity=35};canvas.Measure(new(1400,990));canvas.Arrange(new Rect(0,0,1400,990));canvas.ViewportForBenchmark(1,new(0,0));var visual=new DrawingVisual();using(var dc=visual.RenderOpen())canvas.RenderForBenchmark(dc);var bitmap=new RenderTargetBitmap(1400,990,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);byte[] pixels=new byte[1400*990*4];bitmap.CopyPixels(pixels,1400*4,0);int Ink(int x,int y)=>255-pixels[(y*1400+x)*4];
            assert(Ink(100,100)>0&&Ink(110,100)>0&&Ink(120,100)>0,"Points absents ou mauvaise fréquence");assert(Ink(105,100)==0&&Ink(101,101)==0,"La grille a épaissi ou changé de pas");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(dir,"self-test-performance-grid.png"));png.Save(file);
        });
    }
}
