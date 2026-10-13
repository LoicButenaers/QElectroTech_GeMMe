using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public static class NavigationTests
{
    public static void Run(Action<string,Action> test, Action<bool,string> assert, Action<Action> reject, string dir)
    {
        test("Numéro de fil : Maj glisser, trajets conservés, annulation et lecture", () => {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var b = e.AddComponent("coil", 500, 500); var wire = e.Connect(new(a.Id,"A2"), new(b.Id,"A1"), number:"26"); var canvas = new SheetCanvas(e); var route = wire.Route(e.Sheet).ToList(); string before = ProjectStore.Serialize(e.Project);
            var ink = Drawing.WireLabel(wire, route)!; var point = Drawing.TextBounds(ink).TopLeft + new Vector(2,2); assert(canvas.BeginTextMove(point), "Numéro non sélectionnable"); canvas.MoveText(point + new Vector(73.5, -28.25)); canvas.EndPreviewMove();
            assert(wire.NumberPlacement.OffsetX == 73.5 && wire.NumberPlacement.OffsetY == -28.25 && route.SequenceEqual(wire.Route(e.Sheet)) && a.X == 200, "Câblage déplacé avec le texte"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Annulation incomplète"); e.Redo();
            var path = Path.Combine(dir, "self-test-wire-number-placement.gemelec"); e.Save(path); var restored = ProjectStore.Load(path); assert(restored.Sheets[0].Wires[0].NumberPlacement.OffsetX == 73.5, "Placement perdu");
            e.Change(() => { var placement = e.Sheet.Wires[0].NumberPlacement; placement.Rotation = 90; placement.FontSize = 18; placement.Color = "#112233"; }); ink = Drawing.WireLabel(e.Sheet.Wires[0], e.Sheet.Wires[0].Route(e.Sheet))!; assert(ink.Rotation == 90 && ink.Size == 18 && ink.Color == "#112233", "Style ignoré");
            e.Change(() => e.Sheet.Wires[0].NumberPlacement.Visible = false); assert(Drawing.WireLabel(e.Sheet.Wires[0], route) is null, "Visibilité ignorée"); reject(() => e.Change(() => e.Sheet.Wires[0].NumberPlacement.FontSize = 999));
        });
        test("Circuit interfolio : passage, exception de numéro et navigation", () => {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var terminal = e.AddComponent("terminal", 200, 350); var report = e.AddComponent("folio_next", 200, 500); var wire = e.Connect(new(a.Id,"A2"), new(terminal.Id,"1"), number:"26"); var local = e.Connect(new(terminal.Id,"2"),new(report.Id,"1"), number:"C1", numberIndependent:true); var first = e.Sheet;
            e.AddSheet("Retour"); var arrival = e.AddComponent("folio_previous", 200,200); var b = e.AddComponent("coil", 400,400); var other = e.Connect(new(arrival.Id,"1"),new(b.Id,"A1")); e.LinkReferencePair(report.Id,arrival.Id,"24V");
            var rows = ProjectNavigation.Circuit(e.Project,wire.Id); assert(rows.Count == 3 && rows.Any(r=>r.ObjectId==other.Id) && rows.Any(r=>r.ObjectId==local.Id), "Circuit coupé au report, passage ou exception");
            var canvas = new SheetCanvas(e); canvas.HighlightCircuit(wire.Id); assert(canvas.HighlightedWires.Count==3, "Surlignage incomplet"); e.SelectSheet(first.Id); assert(canvas.HighlightedWires.Contains(other.Id), "Circuit perdu en changeant de folio"); canvas.ClearCircuitHighlight(); assert(canvas.HighlightedWires.Count==0,"Surlignage persistant");
        });
        test("Recherche : accents, références, notes et folios", () => {
            var e = new Editor(); var c = e.AddComponent("coil",200,200); e.Change(()=> { c.Tag="KM12"; c.Reference="LC1D09BD"; c.Description="Sécurité générale"; c.AdditionalTexts.Add(new(){Text="Arrêt armoire"}); e.Sheet.Notes.Add(new(){Text="Réglage été",X=80,Y=60}); }); e.AddSheet("Distribution générale");
            assert(ProjectNavigation.Search(e.Project,"securite").Single().ObjectId==c.Id && ProjectNavigation.Search(e.Project,"lc1d09").Single().ObjectId==c.Id && ProjectNavigation.Search(e.Project,"arret").Single().ObjectId==c.Id,"Recherche composants"); assert(ProjectNavigation.Search(e.Project,"reglage").Single().Type=="Texte" && ProjectNavigation.Search(e.Project,"distribution").Single().Type=="Folio","Recherche notes/folios"); assert(ProjectNavigation.Search(e.Project,"introuvable").Count==0,"Résultat inventé");
        });
        test("Alignement et répartition : câblage, limites et undo", () => {
            var e = new Editor(); var a=e.AddComponent("coil",100,100); var b=e.AddComponent("coil",330,250); var c=e.AddComponent("coil",600,400); var wire=e.Connect(new(a.Id,"A2"),new(b.Id,"A1")); string before=ProjectStore.Serialize(e.Project); e.AlignSelection([a.Id,b.Id,c.Id],"distributeX"); assert(b.X==350&&a.X==100&&c.X==600,"Répartition incorrecte"); e.AlignSelection([a.Id,b.Id,c.Id],"top"); assert(e.Sheet.Components.All(c=>c.Y==100) && wire.Route(e.Sheet)[0]==a.PortPoint("A2"),"Bornes perdues"); e.Undo();e.Undo();assert(ProjectStore.Serialize(e.Project)==before,"Undo alignement"); reject(()=>e.AlignSelection([a.Id],"left")); reject(()=>e.AlignSelection([a.Id,b.Id],"distributeX"));
        });
        test("Anomalies : numéros manquants et continuités incohérentes", () => {
            var e=new Editor();var a=e.AddComponent("coil",200,200);var t=e.AddComponent("terminal",200,400);var b=e.AddComponent("coil",500,500);var first=e.Connect(new(a.Id,"A2"),new(t.Id,"1"));var second=e.Connect(new(t.Id,"2"),new(b.Id,"A1")); e.Change(()=>second.Number="AUTRE"); assert(e.Validate().Any(f=>f.ComponentId==first.Id&&f.Message.Contains("Numéros communs")),"Conflit absent"); e.SetWireNumberIndependent(second.Id,true);assert(!e.Validate().Any(f=>f.Message.Contains("Numéros communs")),"Exception signalée à tort");e.SetWireNumber(second.Id,"");assert(e.Validate().Any(f=>f.ComponentId==second.Id&&f.Message.Contains("manquant")),"Numéro vide absent");
        });
        test("Fenêtres de recherche, circuit, anomalies et alignement hors écran", () => {
            string? previous=Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION");Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION","1");var window=new MainWindow(); try { var a=window.Editor.AddComponent("coil",200,200);var b=window.Editor.AddComponent("coil",500,500);var wire=window.Editor.Connect(new(a.Id,"A2"),new(b.Id,"A1")); window.DocumentTestProbe=dialog=> { var root=(FrameworkElement)dialog.Content;root.Measure(new(1050,650));root.Arrange(new(0,0,1050,650));root.UpdateLayout();assert(root.ActualWidth>500,"Disposition absente"); }; foreach(string command in new[]{"projectSearch","validate","alignment","circuit"}) { window.Canvas.Select(command=="circuit"?wire.Id:a.Id); if(command=="alignment")window.Canvas.Selected.Add(b.Id);window.WorkspaceCommandForTest(command).Execute(null,window); } } finally { window.DocumentTestProbe=null;window.DisposeTestBridge();Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION",previous); }
        });
    }
}
