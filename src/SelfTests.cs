using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace GeMMeElec;

public static class SelfTests
{
    public static int Run()
    {
        List<object> results = []; int failures = 0;
        void Test(string name, Action action) { try { action(); results.Add(new { name, passed = true }); } catch (Exception ex) { failures++; results.Add(new { name, passed = false, error = ex.ToString() }); } }
        void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        void Reject(Action action) { try { action(); } catch { return; } throw new Exception("Une entrée invalide a été acceptée."); }
        string dir = AppContext.BaseDirectory;
        Test("Symboles : bornes uniques et géométrie finie", () => { foreach (var d in Symbols.All.Values) { Assert(d.Ports.Select(p => p.Id).Distinct().Count() == d.Ports.Count, d.Id); foreach (var p in d.Ports) Assert(double.IsFinite(p.X) && double.IsFinite(p.Y), d.Id); } });
        Test("Connexions suivent déplacement, rotation et annulation", () => { var e = new Editor(); var a = e.AddComponent("breaker1", 200, 200); var b = e.AddComponent("coil", 200, 400); var w = e.Connect(new(a.Id, "2"), new(b.Id, "A1")); Assert(w.Route(e.Sheet)[0].Y == 250, "Position initiale"); e.Change(() => { a.X = 300; a.Rotation = 90; }); Assert(Math.Abs(w.Route(e.Sheet)[0].X - 250) < .01, "Rotation"); e.Undo(); Assert(e.Sheet.Components[0].X == 200 && e.Sheet.Components[0].Rotation == 0, "Undo"); e.Redo(); Assert(e.Sheet.Components[0].Rotation == 90, "Redo"); });
        Test("Suppression en cascade et restauration", () => { var e = new Editor(); var a = e.AddComponent("terminal", 200, 200); var b = e.AddComponent("terminal", 200, 300); e.Connect(new(a.Id, "2"), new(b.Id, "1")); e.Delete([a.Id]); Assert(e.Sheet.Wires.Count == 0 && e.Sheet.Components.Count == 1, "Cascade"); e.Undo(); Assert(e.Sheet.Wires.Count == 1 && e.Sheet.Components.Count == 2, "Restauration"); });
        Test("Mutations invalides atomiques", () => { var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var snapshot = ProjectStore.Serialize(e.Project); Reject(() => e.Change(() => a.X = -100)); Assert(ProjectStore.Serialize(e.Project) == snapshot, "Rollback"); Reject(() => e.Connect(new(a.Id, "A1"), new("absent", "1"))); Assert(ProjectStore.Serialize(e.Project) == snapshot, "Connexion invalide"); });
        Test("Sauvegarde, relecture, folios et caractères français", () => { var e = new Editor(); Examples.Motor(e); e.AddSheet("Commande · été"); string path = Path.Combine(dir, "self-test.gemelec"); e.Save(path); Assert(!e.Dirty, "État enregistré"); var loaded = ProjectStore.Load(path); Assert(ProjectStore.Serialize(loaded) == ProjectStore.Serialize(e.Project), "Round trip"); });
        Test("Rejet des fichiers mal formés sans perte du projet courant", () => { var e = new Editor(); var snapshot = ProjectStore.Serialize(e.Project); Reject(() => ProjectStore.Deserialize(snapshot.Replace($"\"version\":{Project.CurrentVersion}", "\"version\":99"))); var invalid = new Project(); invalid.Sheets.Add(invalid.Sheets[0]); Reject(() => ProjectStore.ValidateStructure(invalid)); var file = Path.Combine(dir, "self-test-invalid.gemelec"); File.WriteAllText(file, "{}"); Reject(() => e.Open(file)); Assert(ProjectStore.Serialize(e.Project) == snapshot, "Projet courant altéré"); });
        Test("Exports SVG, PDF multipage Unicode et nomenclature", () => { var e = new Editor(); Examples.Motor(e); e.AddSheet("Commande · été"); Export.Svg(e.Project, e.Project.Sheets[0], Path.Combine(dir, "self-test.svg")); Assert(XDocument.Load(Path.Combine(dir, "self-test.svg")).Root!.Name.LocalName == "svg", "SVG"); Export.Pdf(e.Project, Path.Combine(dir, "self-test.pdf")); var pdf = File.ReadAllBytes(Path.Combine(dir, "self-test.pdf")); var ascii = System.Text.Encoding.ASCII.GetString(pdf); Assert(ascii.StartsWith("%PDF-1.7") && ascii.Contains("/Count 2") && ascii.Contains("/ToUnicode"), "PDF"); Assert(Export.Bom(e.Project).Contains("KM1"), "BOM"); });
        Test("MCP : schémas, paramètres inconnus et types rejetés", () => { Assert(McpTools.Specs.Select(x => x.Name).Distinct().Count() == McpTools.Specs.Length, "Outils dupliqués"); McpTools.Check("component_add", JsonSerializer.SerializeToElement(new { symbol = "coil", x = 200, y = 200 })); Reject(() => McpTools.Check("component_add", JsonSerializer.SerializeToElement(new { x = "incorrect", y = 200 }))); Reject(() => McpTools.Check("project_get", JsonSerializer.SerializeToElement(new { command = "anything" }))); });
        Test("Déplacement de segments : ancrages et orthogonalité", () =>
        {
            var e = new Editor(); var a = e.AddComponent("terminal", 200, 200); var b = e.AddComponent("terminal", 200, 400); var w = e.Connect(new(a.Id, "2"), new(b.Id, "1"));
            var route = w.Route(e.Sheet); e.Change(() => w.Waypoints = WireRouting.ShiftSegment(route, 1, new(300, 300)));
            var moved = w.Route(e.Sheet); Assert(moved[0] == a.PortPoint("2") && moved[^1] == b.PortPoint("1"), "Ancrages modifiés"); Assert(moved.Any(p => p.X == 300), "Segment non déplacé");
            for (int i = 1; i < moved.Count; i++) Assert(moved[i].X == moved[i - 1].X || moved[i].Y == moved[i - 1].Y, "Segment diagonal");
            e.Undo(); Assert(e.Sheet.Wires[0].Waypoints.Count == 0, "Annulation tracé"); e.Redo(); Assert(e.Sheet.Wires[0].Waypoints.Count == 2, "Rétablissement tracé");
        });
        Test("Reconnexion : bornes valides, doublons et annulation", () =>
        {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var b = e.AddComponent("coil", 400, 400); var c = e.AddComponent("coil", 600, 400); var w = e.Connect(new(a.Id, "A2"), new(b.Id, "A1"));
            e.Reconnect(w.Id, false, new(c.Id, "A1")); Assert(e.Sheet.Wires[0].To.ComponentId == c.Id, "Reconnexion"); e.Undo(); Assert(e.Sheet.Wires[0].To.ComponentId == b.Id, "Undo reconnexion");
            var snapshot = ProjectStore.Serialize(e.Project); Reject(() => e.Reconnect(w.Id, false, new(c.Id, "absente"))); Assert(ProjectStore.Serialize(e.Project) == snapshot, "Rollback reconnexion");
            e.Connect(new(a.Id, "A2"), new(c.Id, "A1")); Reject(() => e.Reconnect(w.Id, false, new(c.Id, "A1"))); Assert(e.Sheet.Wires[0].To.ComponentId == b.Id, "Doublon accepté");
        });
        Test("Position du texte : côtés, centrage, précision et rotation", () =>
        {
            var c = new Component { Symbol = "coil", X = 300, Y = 300 }; var p = new LabelPlacement { Side = "top", Horizontal = "center", Vertical = "bottom", Gap = 12, OffsetX = 1.25, OffsetY = -2.5 };
            Assert(p.Origin(c, 40, 20) == new System.Windows.Point(281.25, 215.5), "Haut centré");
            p.Side = "custom"; p.Horizontal = "right"; p.Vertical = "center"; Assert(p.Origin(c, 40, 20) == new System.Windows.Point(261.25, 287.5), "Coordonnées précises");
            c.X += 100; Assert(p.Origin(c, 40, 20).X == 361.25, "Suivi du composant");
            p.Side = "right"; p.Horizontal = "left"; c.Rotation = 90; Assert(Math.Abs(p.Origin(c, 40, 20).X - 463.25) < .001, "Bord après rotation");
        });
        Test("Compatibilité anciens projets et persistance des textes", () =>
        {
            var e = new Editor(); var c = e.AddComponent("coil", 200, 200); var old = System.Text.Json.Nodes.JsonNode.Parse(ProjectStore.Serialize(e.Project))!; var oldComponent = old["sheets"]![0]!["components"]![0]!.AsObject(); oldComponent.Remove("tagPlacement"); oldComponent.Remove("detailPlacement"); oldComponent.Remove("descriptionPlacement");
            Assert(ProjectStore.Deserialize(old.ToJsonString()).Sheets[0].Components[0].TagPlacement.Side == "right", "Valeurs par défaut anciennes");
            e.Change(() => c.TagPlacement = new() { Side = "custom", OffsetX = 13.75, OffsetY = -19.25 }); var loaded = ProjectStore.Deserialize(ProjectStore.Serialize(e.Project)); Assert(loaded.Sheets[0].Components[0].TagPlacement.OffsetX == 13.75, "Précision sauvegardée");
            e.Undo(); Assert(e.Sheet.Components[0].TagPlacement.Side == "right", "Annulation position texte");
        });
        Test("Annulation d’une prévisualisation de tracé", () =>
        {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var b = e.AddComponent("coil", 200, 400); var w = e.Connect(new(a.Id, "A2"), new(b.Id, "A1")); var before = ProjectStore.Serialize(e.Project); e.InteractionInProgress = true; w.Waypoints = [new(350, 250), new(350, 350)]; e.CancelPreview(before); Assert(ProjectStore.Serialize(e.Project) == before && !e.InteractionInProgress, "Prévisualisation non restaurée");
        });
        Test("Scène commune et SVG respectent la position du texte", () =>
        {
            var e = new Editor(); var c = e.AddComponent("coil", 200, 200); c.Tag = "TAG_POSITION_TEST"; c.TagPlacement = new() { Side = "custom", Horizontal = "left", Vertical = "top", OffsetX = 12.5, OffsetY = -25.25 };
            var ink = Drawing.Scene(e.Project, e.Sheet).Single(x => x.Text == c.Tag); Assert(ink.X == 212.5 && ink.Y == 174.75, "Scène");
            var path = Path.Combine(dir, "self-test-labels.svg"); Export.Svg(e.Project, e.Sheet, path); var xml = XDocument.Load(path); var text = xml.Descendants().Single(x => x.Name.LocalName == "text" && x.Value == c.Tag); Assert(text.Attribute("x")!.Value == "212.5", "Export SVG");
            c.TagPlacement.Visible = false; Assert(!Drawing.Scene(e.Project, e.Sheet).Any(x => x.Text == c.Tag), "Texte masqué");
        });
        OfficialLibraryTests.Run(Test,Assert,Reject,dir);
        DocumentTests.Run(Test,Assert,Reject,dir);
        WorkflowTests.Run(Test,Assert,Reject,dir);
        ReferenceTests.Run(Test,Assert,Reject,dir);
        ImportEditingTests.Run(Test,Assert,Reject,dir);
        InteractionTests.Run(Test,Assert,Reject,dir);
        TextInteractionTests.Run(Test,Assert,Reject,dir);
        LiveTextTests.Run(Test,Assert,Reject,dir);
        WorkspaceUiTests.Run(Test,Assert,Reject,dir);
        CoilContactTests.Run(Test,Assert,Reject,dir);
        RequestedEditingTests.Run(Test,Assert,Reject,dir);
        WireAppearanceTests.Run(Test,Assert,Reject,dir);
        NavigationTests.Run(Test,Assert,Reject,dir);
        EngineeringTests.Run(Test,Assert,Reject,dir);
        File.WriteAllText(Path.Combine(dir, "self-test.json"), JsonSerializer.Serialize(new { passed = failures == 0, failures, results }, ProjectStore.Json)); return failures == 0 ? 0 : 1;
    }
}
