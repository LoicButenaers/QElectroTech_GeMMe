using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace GeMMeElec;

public static class OfficialLibraryTests
{
    public static void Run(Action<string,Action> test, Action<bool,string> assert, Action<Action> reject, string dir)
    {
        test("Sources : intégrité des originaux et séparation EPLAN / fabricants",()=>{
            assert(Catalog.Search(library:"manufacturer").Count()==3 && Catalog.Search(library:"eplan").Count()==4,"Sélection documentée");
            foreach(var e in Catalog.Entries) { var data=Catalog.ReadChecked(e.Asset,e.AssetSha256);ImageAssets.Validate(Convert.ToBase64String(data)); reject(()=>Catalog.ReadChecked(e.Asset,new string('0',64)));if(e.Document.Length>0)assert(Catalog.ReadChecked(e.Document,e.DocumentSha256).Length>1000,"Document source"); }
            foreach(var e in Catalog.Search(library:"eplan")) {assert(!e.CanPlace && e.Ports.Count==0,"Aucune borne inventée"); var editor=new Editor();var before=ProjectStore.Serialize(editor.Project);reject(()=>editor.AddComponent("",200,200,e.Id));assert(ProjectStore.Serialize(editor.Project)==before,"Rejet atomique");}
        });
        test("Fabricants : bornes distinctes, dessins inchangés, raccordement et copie",()=>{
            var editor=new Editor();
            foreach(var entry in Catalog.Search(library:"manufacturer"))
            {
                var c=editor.AddComponent(entry.Symbol,350,200+editor.Sheet.Components.Count*210,entry.Id);
                var d=c.Definition;CustomSymbols.Validate(d);
                assert(d.Shapes.Count==1 && Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(d.Shapes[0].Text)))==entry.AssetSha256,"Dessin identique au fichier contrôlé");
                assert(!c.ShowPortLabels && d.PrintedTerminals,"Pas de bornes imprimées en double");
            }
            var se=editor.Sheet.Components[0];var abb=editor.Sheet.Components[1];var eaton=editor.Sheet.Components[2];
            assert(se.Definition.Ports.Count==12 && abb.Definition.Ports.Count==10 && eaton.Definition.Ports.Count==10,"12 / 10 / 10 bornes");
            assert(abb.Definition.Ports.Any(p=>p.Id=="21")&&!abb.Definition.Ports.Any(p=>p.Id=="13"),"ABB variante NC");
            assert(eaton.Definition.Ports.Any(p=>p.Id=="13")&&!eaton.Definition.Ports.Any(p=>p.Id=="21"),"Eaton variante NO");
            var wire=editor.Connect(new(se.Id,"A2"),new(abb.Id,"A1"));editor.Change(()=>abb.Rotation=90);
            assert(wire.Route(editor.Sheet)[^1]==abb.PortPoint("A1"),"Raccordement après rotation");
            // The existing router does not avoid device bodies. Route this
            // demonstration around the rotated ABB symbol explicitly.
            editor.Change(()=>wire.Waypoints=[new(221,297),new(490,297),new(490,308.8)]);
            Export.Svg(editor.Project,editor.Sheet,Path.Combine(dir,"official-library.svg"));Export.Pdf(editor.Project,Path.Combine(dir,"official-library.pdf"));
            var images=XDocument.Load(Path.Combine(dir,"official-library.svg")).Descendants().Where(x=>x.Name.LocalName=="image").ToArray();assert(images.Length==3 && images[1].Attribute("transform")!=null && images.All(x=>x.Attribute("href")!.Value.StartsWith("data:image/png;base64,")),"SVG PNG + rotation");
            assert(System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(dir,"official-library.pdf"))).Contains("/FlateDecode"),"PDF sans JPEG destructif");
            assert(BomReport.Html(editor.Project,new()).Contains("data:image/png;base64,"),"Dessins officiels dans les images de BOM HTML");
            var json=ProjectStore.Serialize(editor.Project);assert(ProjectStore.Serialize(ProjectStore.Deserialize(json))==json,"Sauvegarde/relecture");
            var copy=editor.SaveSymbolCopy(se.Definition,se.Id);assert(se.CatalogId=="" && copy.Id.StartsWith("custom-"),"Copie non officielle");
            editor.Undo();assert(editor.Sheet.Components[0].CatalogId=="mfr-schneider-LC1D09BD","Annulation copie");editor.Redo();assert(editor.Project.CustomSymbols.Count==1,"Rétablissement");
            var path=Path.Combine(dir,"official-copy.gemsymbol");CustomSymbols.Export(copy,path);assert(CustomSymbols.Load(path).Shapes[0].Text==copy.Shapes[0].Text,"Copie PNG portable");
            var legacy=new Editor();var old=legacy.AddComponent("contactor",200,200);old.CatalogId="f07-1";old.Brand="Schneider Electric";old.Range="Ancien projet";assert(ProjectStore.Deserialize(ProjectStore.Serialize(legacy.Project)).Sheets[0].Components[0].Range=="Ancien projet","Ancien catalogue non réassigné");
        });
        test("Fluidité : 1 000 dessins fabricants avec images réutilisées",()=>{
            var p=new Project();var sheet=p.Sheets[0];for(int i=0;i<1000;i++)sheet.Components.Add(new(){Symbol="official-eaton-276705",Tag="K"+i,X=200+i%20*40,Y=200+i/20*10,ShowPortLabels=false});
            ProjectStore.ValidateStructure(p); var watch=System.Diagnostics.Stopwatch.StartNew();var scene=Drawing.Scene(p,sheet);var visual=new DrawingVisual();using(var dc=visual.RenderOpen())Drawing.Render(dc,scene);watch.Stop();
            assert(scene.Count(s=>s.Kind=="image")==1000,"Dessins présents");
            System.IO.File.WriteAllText(Path.Combine(dir,"official-library-performance.json"),System.Text.Json.JsonSerializer.Serialize(new{components=1000,sceneAndDrawingMs=watch.Elapsed.TotalMilliseconds},ProjectStore.Json));
        });
        test("Bibliothèques : fiches de provenance hors écran",()=>{
            var old=Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION");Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION","1");_ = Application.Current??new Application();var window=new MainWindow();
            try { foreach(var entry in new[]{Catalog.Entries[0],Catalog.Entries[^1]}) {window.DocumentTestProbe=dialog=>{var content=(FrameworkElement)dialog.Content;content.Measure(new(920,740));content.Arrange(new(0,0,920,740));content.UpdateLayout();var bitmap=new RenderTargetBitmap(920,740,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(dir,"ui-library-"+entry.Library+".png"));encoder.Save(file);};window.OfficialLibraryDetails(entry);} }
            finally {window.DocumentTestProbe=null;window.DisposeTestBridge();Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION",old);}
        });
    }
}
