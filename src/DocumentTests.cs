using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace GeMMeElec;

public static class DocumentTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> assert,Action<Action> reject,string dir)
    {
        test("Cartouches : liens, héritage, numérotation, champs personnalisés et ordre",()=>{
            var p=new Project{Author="Auteur projet",Customer="Client",Date="12/10/2026"};var s=p.Sheets[0];s.TitleBlockId="technical";s.Number="01-A";s.CheckedBy="Contrôle";s.CustomFields["armoire"]="AR1";
            var v=DocumentModel.Values(p,s);assert(v["sheet.author"]==p.Author&&v["sheet.date"]==p.Date&&v["sheet.number"]=="01-A","Héritage");assert(DocumentModel.Resolve("{sheet.custom.armoire} {inconnu}",v)=="AR1 {inconnu}","Champs");
            var next=new Sheet();p.Sheets.Insert(0,next);assert(DocumentModel.Values(p,s)["sheet.index"]=="2"&&DocumentModel.PageNumber(p,next)=="1","Ordre");s.Author="Auteur folio";assert(DocumentModel.Values(p,s)["sheet.author"]=="Auteur folio","Priorité folio");ProjectStore.ValidateStructure(p);
        });
        test("Cartouches : modifications atomiques, modèles partagés et sauvegarde",()=>{
            var e=new Editor();var template=TitleBlocksDefaults.Technical();template.Id="custom";e.Change(()=>{e.Project.TitleBlocks.Add(template);e.Sheet.TitleBlockId=template.Id;e.Sheet.Number="P-01";});var saved=ProjectStore.Serialize(e.Project);
            reject(()=>e.Change(()=>e.Project.TitleBlocks.Last().Cells[0].Width=110));assert(ProjectStore.Serialize(e.Project)==saved,"Rollback cellule");
            var loaded=ProjectStore.Deserialize(saved);assert(loaded.Sheets[0].Number=="P-01"&&loaded.TitleBlocks.Last().Cells.Count>10,"Persistance");e.Undo();assert(e.Sheet.TitleBlockId=="simple","Undo affectation");e.Redo();assert(e.Sheet.TitleBlockId=="custom","Redo affectation");
            reject(()=>e.Change(()=>e.Sheet.TitleBlockId="missing"));
        });
        test("Compatibilité projets 0.2 sans cartouches, images ni propriétés de folio",()=>{
            var json=JsonNode.Parse(ProjectStore.Serialize(new Project()))!;foreach(var key in new[]{"titleBlocks","images","bomOptions","customer","customFields","documentNumber","date"})json.AsObject().Remove(key);
            var s=json["sheets"]![0]!.AsObject();foreach(var key in new[]{"number","author","date","checkedBy","approvedBy","revision","location","function","customFields","titleBlockId"})s.Remove(key);
            var p=ProjectStore.Deserialize(json.ToJsonString());assert(p.TitleBlocks.Count==2&&p.Sheets[0].TitleBlockId=="simple","Valeurs initiales");assert(Drawing.Scene(p,p.Sheets[0]).Count>5,"Rendu ancien projet");
        });
        test("BOM : regroupement, colonnes ordonnées, filtres et protection CSV/HTML",()=>{
            var e=new Editor();var a=e.AddComponent("coil",100,100);var b=e.AddComponent("coil",200,100);a.Brand=b.Brand="Marque";a.Description=b.Description="<script>texte</script>";a.Reference=b.Reference="=FORMULE";e.AddComponent("earth",300,100);
            var o=new BomOptions();var rows=BomReport.Rows(e.Project,o);assert(rows.Count==1&&rows[0].Values["quantity"]=="2","Quantités");o.GroupArticles=false;assert(BomReport.Rows(e.Project,o).Count==2,"Sans regroupement");o.IncludeAuxiliary=true;assert(BomReport.Rows(e.Project,o).Count==3,"Auxiliaires");o.Brand="absent";assert(BomReport.Rows(e.Project,o).Count==0,"Filtre marque");o.Brand="";o.IncludeAuxiliary=false;
            o.Columns=[new(){Key="reference",Label="Réf."},new(){Key="description",Label="Description"}];var csv=BomReport.Csv(e.Project,o);assert(csv.StartsWith("\"Réf.\";")&&csv.Contains("'=FORMULE"),"Ordre et formule");var html=BomReport.Html(e.Project,o);assert(!html.Contains("<script>")&&html.Contains("&lt;script&gt;"),"Échappement HTML");
            o.Columns.ForEach(c=>c.Enabled=false);reject(o.Validate);
        });
        test("Images : import, déduplication, PDF/SVG et BOM autonomes",()=>{
            var png=Path.Combine(dir,"document-image-source.png");var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new Rect(0,0,180,90));dc.DrawRectangle(Brushes.Teal,null,new Rect(10,10,60,70));dc.DrawText(new FormattedText("GeMMe",System.Globalization.CultureInfo.GetCultureInfo("fr-BE"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),20,Brushes.Black,1),new Point(75,30));}SavePng(visual,png,180,90);
            var asset=ImageAssets.Import(png);assert(ImageAssets.Import(png).Id==asset.Id,"Identifiant dédupliqué");var e=new Editor();var c=e.AddComponent("coil",400,300);e.AddComponent("breaker1",400,100);
            e.Change(()=>{e.Project.Images[asset.Id]=asset.Data;c.ImageId=asset.Id;e.Project.Title="Projet exemple · armoire industrielle";e.Project.Author="GeMMe";e.Project.Customer="Client exemple";e.Project.DocumentNumber="ELEC-001";e.Sheet.Title="Puissance et commande";e.Sheet.Number="01-A";e.Sheet.CheckedBy="Vérificateur";e.Sheet.Date="12/10/2026";e.Sheet.TitleBlockId="technical";e.Project.TitleBlocks.Single(t=>t.Id=="technical").Cells[0].ImageId=asset.Id;});
            var round=ProjectStore.Deserialize(ProjectStore.Serialize(e.Project));assert(round.Images.Count==1&&round.Sheets[0].Components[0].ImageId==asset.Id,"Images sauvegardées");
            var svg=Path.Combine(dir,"document-preview.svg");Export.Svg(e.Project,e.Sheet,svg);assert(XDocument.Load(svg).Descendants().Any(el=>el.Name.LocalName=="image"&&el.Attribute("href")!.Value.StartsWith("data:image/jpeg;base64,")),"SVG autonome");
            Export.Pdf(e.Project,Path.Combine(dir,"document-preview.pdf"));var pdf=Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(dir,"document-preview.pdf")));assert(pdf.Contains("/Subtype /Image")&&pdf.Contains("/DCTDecode"),"Image PDF");
            var html=BomReport.Html(e.Project,new());assert(html.Contains("data:image/jpeg;base64,")&&html.Contains("<svg"),"Photo + symbole de remplacement");File.WriteAllText(Path.Combine(dir,"document-bom.html"),html);
            var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1400,990));Drawing.Render(dc,Drawing.Scene(e.Project,e.Sheet));}SavePng(drawing,Path.Combine(dir,"document-preview.png"),1400,990);
            var preview=new TitleBlockPreview{Width=1200,Height=220};preview.Set(e.Project,e.Sheet,DocumentModel.Template(e.Project,e.Sheet)!);preview.Measure(new Size(1200,220));preview.Arrange(new Rect(0,0,1200,220));SavePng(preview,Path.Combine(dir,"cartouche-editor-preview.png"),1200,220);
            var pack=new TitleBlockPackage{Template=DocumentModel.Template(e.Project,e.Sheet)!,Images=e.Project.Images};File.WriteAllText(Path.Combine(dir,"document-example.gemcartouche"),JsonSerializer.Serialize(pack,ProjectStore.Json));
            e.Save(Path.Combine(dir,"document-example.gemelec"));reject(()=>e.Change(()=>e.Sheet.Components[0].ImageId="missing"));assert(e.Sheet.Components[0].ImageId==asset.Id,"Rollback image");
        });
        test("Rendu et BOM : mesure sur 1 000 composants / 999 conducteurs",()=>{
            var p=new Project();var s=p.Sheets[0];for(int i=0;i<1000;i++)s.Components.Add(new(){Symbol="coil",Tag="K"+i,X=80+(i%25)*48,Y=70+(i/25)*18});for(int i=1;i<s.Components.Count;i++)s.Wires.Add(new(){From=new(s.Components[i-1].Id,"A2"),To=new(s.Components[i].Id,"A1"),Number="W"+i});
            ProjectStore.ValidateStructure(p);Drawing.Scene(p,s);var clock=Stopwatch.StartNew();var scene=Drawing.Scene(p,s);clock.Stop();double sceneMs=clock.Elapsed.TotalMilliseconds;clock.Restart();var bom=BomReport.Rows(p,new());clock.Stop();assert(scene.Count>5000&&bom.Sum(r=>int.Parse(r.Values["quantity"]))==1000,"Scène et quantités");
            File.WriteAllText(Path.Combine(dir,"document-performance.json"),JsonSerializer.Serialize(new{components=1000,wires=999,sceneMs,bomMs=clock.Elapsed.TotalMilliseconds,primitives=scene.Count},ProjectStore.Json));
        });
        test("Interfaces documentaires : création et rendu hors écran des quatre fenêtres",()=>{
            var oldSession=Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION");Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION","1");
            var app=Application.Current??new Application();var window=new MainWindow();
            try
            {
                window.Editor.Open(Path.Combine(dir,"document-example.gemelec"));
                foreach(var kind in new[]{"titleblock","bom","sheet","project"})
                {
                    window.DocumentTestProbe=dialog=>{
                        var content=(FrameworkElement)dialog.Content;double w=dialog.Width-16,h=dialog.Height-40;content.Measure(new Size(w,h));content.Arrange(new Rect(0,0,w,h));content.UpdateLayout();assert(content.ActualWidth>500&&content.ActualHeight>400,"Disposition hors écran");SavePng(content,Path.Combine(dir,"ui-"+kind+".png"),(int)w,(int)h);
                        void Click(string text)=>Descendants(content).OfType<System.Windows.Controls.Button>().Single(b=>b.Content is string s&&s==text).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        if(kind=="titleblock") {Click("Dupliquer modèle");Click("+ Cellule");Click("Enregistrer et affecter au folio");assert(window.Editor.Project.TitleBlocks.Count==3&&window.Editor.Sheet.TitleBlockId!="technical","Édition et affectation par les commandes du dialogue");}
                        if(kind=="bom") {Click("Actualiser l’aperçu");Click("Mémoriser ces options");assert(window.Editor.Project.BomOptions.Columns.Any(c=>c.Key=="image"&&c.Enabled),"Options BOM");}
                    };
                    window.TestDocumentDialog(kind);
                }
            }
            finally{window.DocumentTestProbe=null;window.DisposeTestBridge();Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION",oldSession);}
        });
    }
    private static void SavePng(Visual visual,string path,int width,int height)
    {var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(Brushes.White,null,new Rect(0,0,width,height));bitmap.Render(background);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {yield return parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(parent,i)))yield return child;}
}
