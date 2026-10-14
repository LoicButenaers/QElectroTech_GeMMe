using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace GeMMeElec;

internal static class CoverTests
{
    private static void Render(Project p,Sheet s,string path)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new(0,0,s.Width,s.Height));Drawing.Render(dc,Drawing.Scene(p,s));}var bmp=new RenderTargetBitmap((int)s.Width,(int)s.Height,96,96,PixelFormats.Pbgra32);bmp.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(path);encoder.Save(file);
    }
    public static void Run(Action<string,Action> test,Action<bool,string> assert,Action<Action> reject,string dir)
    {
        test("Page de garde : conversion sans perdre les objets, sans zones, cartouche facultatif",()=>{
            var e=new Editor();Examples.Motor(e);string before=e.ExportSnapshot;var ids=e.Sheet.Components.Select(c=>c.Id).ToArray();e.SetSheetKind("cover");assert(e.Sheet.Kind=="cover"&&e.Sheet.TitleBlockId==""&&SheetZones.Frame(e.Project,e.Sheet).Count()==0,"Zones ou cartouche encore présents");assert(e.Sheet.Components.Select(c=>c.Id).SequenceEqual(ids),"Contenu supprimé");assert(SheetZones.Zone(e.Project,e.Sheet,200,200)=="page de garde","Adresse inventée");e.Change(()=>e.Sheet.TitleBlockId="technical");assert(DocumentModel.Scene(e.Project,e.Sheet).Count>0&&SheetZones.Frame(e.Project,e.Sheet).Count()==0,"Cartouche réactive les zones");e.Undo();e.Undo();assert(e.ExportSnapshot==before,"Annulation conversion");
        });
        test("Page de garde : ajout en tête, une annulation, page réellement blanche",()=>{
            var e=new Editor();string previous=e.ExportSnapshot;var first=e.Sheet.Id;var cover=e.AddCoverSheet("Page de garde");assert(e.Project.Sheets[0].Id==cover.Id&&e.Sheet.Id==cover.Id&&e.Project.Sheets[1].Id==first,"Ordre ou folio actif");assert(Drawing.Scene(e.Project,cover).Count==0,"Cadre / repérage sur la garde blanche");var pdf=PdfNavigation.Pages(e.Project);assert(pdf[0].Sheet.Id==cover.Id&&pdf[1].Title.StartsWith("Sommaire"),"Le sommaire précède la garde");e.Undo();assert(e.ExportSnapshot==previous&&e.Sheet.Id==first,"Ajout non atomique");e.Redo();assert(e.Project.Sheets[0].Kind=="cover","Redo garde");
        });
        test("Dessins libres : validation et mutation atomique",()=>{
            var e=new Editor();var g=e.PutGraphic(new(){X=100,Y=200,Width=300,Height=80,Fill="#E7EDF3"});string before=e.ExportSnapshot;reject(()=>e.PutGraphic(new(){X=200,Y=200,Width=-50}));reject(()=>e.PutGraphic(new(){X=200,Y=200,Kind="wire"}));reject(()=>e.PutGraphic(new(){X=200,Y=200,Color="rouge"}));reject(()=>e.PutNote(new(){X=300,Y=300,FontSize=0}));assert(e.ExportSnapshot==before,"Refus partiel");e.Delete([g.Id]);assert(e.Sheet.Graphics.Count==0,"Suppression");e.Undo();assert(e.ExportSnapshot==before,"Undo suppression");
        });
        test("Dessins libres : rectangle, ellipse, ligne inverse, texte lié et exports",()=>{
            var e=new Editor();e.SetSheetKind("cover");e.Change(()=>{e.Project.Title="Automatisation de la ligne";e.Project.Customer="Client · exemple";e.Project.Author="Bureau d’études";e.Project.DocumentNumber="GE-001";e.Project.Date="14/10/2026";e.Project.Revision="A";});
            e.PutGraphic(new(){X=60,Y=90,Width=1280,Height=210,Fill="#203348",Color="#203348"});e.PutGraphic(new(){X=60,Y=370,Width=1280,Height=340,Fill="#F3F6FA",Color="#CBD5E1"});e.PutGraphic(new(){Kind="line",X=1340,Y=760,Width=-1280,Height=0,Color="#007F86",Thickness=2});e.PutGraphic(new(){Kind="ellipse",X=1180,Y=800,Width=120,Height=70,Fill="#E7F5F4",Color="#007F86"});
            foreach(var n in new[]{new Annotation{X=60,Y=45,Text="PAGE DE GARDE",FontSize=22,Color="#64748B"},new Annotation{X=700,Y=125,Text="{project.title}",FontSize=52,Color="#FFFFFF",Align="center",ResolveFields=true},new Annotation{X=700,Y=230,Text="{project.customer}",FontSize=26,Color="#FFFFFF",Align="center",ResolveFields=true},new Annotation{X=100,Y=420,Text="Document : {project.documentNumber}",FontSize=24,ResolveFields=true},new Annotation{X=100,Y=480,Text="Auteur : {project.author}",FontSize=24,ResolveFields=true},new Annotation{X=100,Y=540,Text="Date : {project.date}",FontSize=24,ResolveFields=true},new Annotation{X=100,Y=600,Text="Révision : {project.revision}",FontSize=24,ResolveFields=true},new Annotation{X=60,Y=815,Text="Dossier électrique · schémas et raccordements",FontSize=22,Color="#64748B"}})e.PutNote(n);
            var scene=Drawing.Scene(e.Project,e.Sheet);assert(scene.Any(i=>i.Kind=="ellipse"&&i.Fill=="#E7F5F4")&&scene.Any(i=>i.Text==e.Project.Title&&i.Size==52),"Styles ou champs");var cached=new CanvasScene(e.Project,e.Sheet,new());assert(JsonSerializer.Serialize(cached.SceneForTest())==JsonSerializer.Serialize(scene),"Cache différent");ProjectStore.Save(e.Project,Path.Combine(dir,"self-test-cover.gemelec"));Export.Svg(e.Project,e.Sheet,Path.Combine(dir,"self-test-cover.svg"));Export.Pdf(e.Project,Path.Combine(dir,"self-test-cover.pdf"));Render(e.Project,e.Sheet,Path.Combine(dir,"self-test-cover.png"));var svg=XDocument.Load(Path.Combine(dir,"self-test-cover.svg"));assert(svg.Descendants().Any(x=>x.Name.LocalName=="ellipse")&&svg.Descendants().Any(x=>x.Name.LocalName=="rect"&&x.Attribute("fill")?.Value=="#203348"),"SVG sans forme/remplissage");assert(ProjectStore.Serialize(ProjectStore.Load(Path.Combine(dir,"self-test-cover.gemelec")))==e.ExportSnapshot,"Relecture");
            e.Change(()=>e.Project.Title="Nouveau titre");assert(Drawing.Scene(e.Project,e.Sheet).Any(i=>i.Text=="Nouveau titre"),"Champ titre figé");
        });
        test("Dessins libres : gestes glisser / deux clics et Échap",()=>{
            var e=new Editor();var canvas=new SheetCanvas(e);canvas.SetMode("graphicRect");canvas.GraphicClickForTest(new(100,100));canvas.GraphicReleaseForTest(new(300,250));assert(e.Sheet.Graphics.Count==1&&e.Sheet.Graphics[0].Width==200,"Glisser rectangle");canvas.SetMode("graphicEllipse");canvas.GraphicClickForTest(new(400,200));canvas.GraphicReleaseForTest(new(400,200));assert(e.InteractionInProgress,"Premier clic oublié");canvas.GraphicClickForTest(new(600,350));assert(e.Sheet.Graphics.Count==2,"Second clic");string before=e.ExportSnapshot;canvas.SetMode("graphicLine");canvas.GraphicClickForTest(new(700,200));canvas.SetMode("select");assert(e.ExportSnapshot==before&&!e.InteractionInProgress,"Échap modifie le document");
        });
        test("Dessins libres : déplacement et redimensionnement annulables et cache partiel",()=>{
            var e=new Editor();var g=e.PutGraphic(new(){X=100,Y=100,Width=200,Height=100});var canvas=new SheetCanvas(e);canvas.Measure(new(1000,760));canvas.Arrange(new Rect(0,0,1000,760));canvas.Select(g.Id);canvas.BeginSelectionMove(new(100,100));canvas.MoveSelection(new(200,200));assert(e.Sheet.Graphics[0].X==200,"Déplacement");canvas.EndPreviewMove();e.Undo();assert(e.Sheet.Graphics[0].X==100,"Undo déplacement");canvas.Select(g.Id);assert(canvas.ResizeStartForTest(new(300,200)),"Poignée");canvas.ResizeForTest(new(450,350));canvas.EndPreviewMove();assert(e.Sheet.Graphics[0].Width==350&&e.Sheet.Graphics[0].Height==250,"Redimensionnement");e.Undo();assert(e.Sheet.Graphics[0].Width==200,"Undo taille");
            var scene=new CanvasScene(e.Project,e.Sheet,new());var live=e.Sheet.Graphics[0];live.X+=30;scene.GraphicsChanged([live.Id]);assert(JsonSerializer.Serialize(scene.SceneForTest())==JsonSerializer.Serialize(Drawing.Scene(e.Project,e.Sheet,new())),"Cache déplacement dessin");
        });
        test("Page de garde : anciens formats et styles de texte par défaut",()=>{
            for(int version=1;version<=8;version++){var p=new Project();p.Sheets[0].Notes.Add(new(){X=100,Y=100,Text="Ancienne note"});var json=JsonNode.Parse(ProjectStore.Serialize(p))!;json["version"]=version;var s=json["sheets"]![0]!.AsObject();s.Remove("kind");s.Remove("graphics");foreach(string key in new[]{"fontSize","color","rotation","align","resolveFields"})s["notes"]![0]!.AsObject().Remove(key);var migrated=ProjectStore.Deserialize(json.ToJsonString());assert(migrated.Version==Project.CurrentVersion&&migrated.Sheets[0].Kind=="drawing"&&migrated.Sheets[0].Graphics.Count==0&&migrated.Sheets[0].Notes[0].FontSize==16,"Migration "+version);}
        });
        test("Page de garde : gabarits, blocs et formes avec nouveaux identifiants",()=>{
            var e=new Editor();e.SetSheetKind("cover");var g=e.PutGraphic(new(){X=100,Y=100,Width=200,Height=100});var note=e.PutNote(new(){X=100,Y=240,Text="Titre",FontSize=36});var copy=ProjectTemplates.Instantiate(e.Project,"Copie",true);assert(copy.Sheets[0].Kind=="cover"&&copy.Sheets[0].Graphics[0].Id!=g.Id,"Gabarit");var empty=ProjectTemplates.Instantiate(e.Project,"Vide",false);assert(empty.Sheets[0].Kind=="cover"&&empty.Sheets[0].Graphics.Count==0,"Gabarit vide");var block=CircuitBlocks.Extract(e.Project,e.Sheet,[g.Id,note.Id],"Encadré");var target=new Editor();target.InsertBlock(block,new(300,300));assert(target.Sheet.Graphics.Count==1&&target.Sheet.Notes[0].FontSize==36&&target.Sheet.Graphics[0].Id!=g.Id,"Bloc graphique");
        });
        test("Page de garde : plans générés protégés",()=>{
            var e=new Editor();var c=e.AddComponent("terminal",200,200);var strip=e.PutTerminalStrip(new(){Terminals=[new(){ComponentId=c.Id}]});e.GenerateTerminalPlan(strip.Id);string before=e.ExportSnapshot;reject(()=>e.SetSheetKind("cover"));reject(()=>e.PutGraphic(new(){X=100,Y=100}));assert(e.ExportSnapshot==before,"Plan altéré");
        });
        test("Page de garde : interface type, propriétés et commandes accessibles",()=>{
            var window=new MainWindow();try{int dialogs=0;window.DocumentTestProbe=dialog=>{dialogs++;var root=(FrameworkElement)dialog.Content;root.Measure(new(900,780));root.Arrange(new(0,0,900,780));root.UpdateLayout();foreach(var combo in Descendants(root).OfType<ComboBox>().Where(c=>c.Name=="SheetKind"))combo.SelectedValue="cover";};window.WorkspaceCommandForTest("sheetInfo").Execute(null,window);assert(window.Editor.Sheet.Kind=="cover"&&window.Editor.Sheet.TitleBlockId=="","Choix type inaccessible");var g=window.Editor.PutGraphic(new(){X=100,Y=100});window.Canvas.Select(g.Id);assert(window.PropertyField("graphicWidth") is not null,"Propriétés dessin");window.WorkspaceCommandForTest("duplicate").Execute(null,window);assert(window.Editor.Sheet.Graphics.Count==2,"Copie dessin");assert(dialogs==1,"Dialogue");}finally{window.DocumentTestProbe=null;window.DisposeTestBridge();}
        });
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent){yield return parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(parent,i)))yield return child;}
}
