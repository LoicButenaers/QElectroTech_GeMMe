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
        Test("Catalogue : 25 familles, 3 fabricants, sources HTTPS, symboles valides", () => { Assert(Catalog.Entries.Count == 75, "75 gammes attendues"); Assert(Catalog.Entries.GroupBy(x => x.Family).Count() == 25, "25 familles attendues"); foreach (var group in Catalog.Entries.GroupBy(x => x.Family)) Assert(group.Select(x => x.Brand).Distinct().Count() == 3, group.Key); foreach (var entry in Catalog.Entries) Assert(Uri.TryCreate(entry.Source, UriKind.Absolute, out var u) && u.Scheme == "https" && Symbols.All.ContainsKey(entry.Symbol), entry.Id); });
        Test("Symboles : bornes uniques et géométrie finie", () => { foreach (var d in Symbols.All.Values) { Assert(d.Ports.Select(p => p.Id).Distinct().Count() == d.Ports.Count, d.Id); foreach (var p in d.Ports) Assert(double.IsFinite(p.X) && double.IsFinite(p.Y), d.Id); } });
        Test("Connexions suivent déplacement, rotation et annulation", () => { var e = new Editor(); var a = e.AddComponent("breaker1", 200, 200); var b = e.AddComponent("coil", 200, 400); var w = e.Connect(new(a.Id, "2"), new(b.Id, "A1")); Assert(w.Route(e.Sheet)[0].Y == 250, "Position initiale"); e.Change(() => { a.X = 300; a.Rotation = 90; }); Assert(Math.Abs(w.Route(e.Sheet)[0].X - 250) < .01, "Rotation"); e.Undo(); Assert(e.Sheet.Components[0].X == 200 && e.Sheet.Components[0].Rotation == 0, "Undo"); e.Redo(); Assert(e.Sheet.Components[0].Rotation == 90, "Redo"); });
        Test("Suppression en cascade et restauration", () => { var e = new Editor(); var a = e.AddComponent("terminal", 200, 200); var b = e.AddComponent("terminal", 200, 300); e.Connect(new(a.Id, "2"), new(b.Id, "1")); e.Delete([a.Id]); Assert(e.Sheet.Wires.Count == 0 && e.Sheet.Components.Count == 1, "Cascade"); e.Undo(); Assert(e.Sheet.Wires.Count == 1 && e.Sheet.Components.Count == 2, "Restauration"); });
        Test("Mutations invalides atomiques", () => { var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var snapshot = ProjectStore.Serialize(e.Project); Reject(() => e.Change(() => a.X = -100)); Assert(ProjectStore.Serialize(e.Project) == snapshot, "Rollback"); Reject(() => e.Connect(new(a.Id, "A1"), new("absent", "1"))); Assert(ProjectStore.Serialize(e.Project) == snapshot, "Connexion invalide"); });
        Test("Sauvegarde, relecture, folios et caractères français", () => { var e = new Editor(); Examples.Motor(e); e.AddSheet("Commande · été"); string path = Path.Combine(dir, "self-test.gemelec"); e.Save(path); Assert(!e.Dirty, "État enregistré"); var loaded = ProjectStore.Load(path); Assert(ProjectStore.Serialize(loaded) == ProjectStore.Serialize(e.Project), "Round trip"); });
        Test("Rejet des fichiers mal formés sans perte du projet courant", () => { var e = new Editor(); var snapshot = ProjectStore.Serialize(e.Project); Reject(() => ProjectStore.Deserialize(snapshot.Replace("\"version\": 1", "\"version\": 99"))); var invalid = new Project(); invalid.Sheets.Add(invalid.Sheets[0]); Reject(() => ProjectStore.ValidateStructure(invalid)); var file = Path.Combine(dir, "self-test-invalid.gemelec"); File.WriteAllText(file, "{}"); Reject(() => e.Open(file)); Assert(ProjectStore.Serialize(e.Project) == snapshot, "Projet courant altéré"); });
        Test("Exports SVG, PDF multipage Unicode et nomenclature", () => { var e = new Editor(); Examples.Motor(e); e.AddSheet("Commande · été"); Export.Svg(e.Project, e.Project.Sheets[0], Path.Combine(dir, "self-test.svg")); Assert(XDocument.Load(Path.Combine(dir, "self-test.svg")).Root!.Name.LocalName == "svg", "SVG"); Export.Pdf(e.Project, Path.Combine(dir, "self-test.pdf")); var pdf = File.ReadAllBytes(Path.Combine(dir, "self-test.pdf")); var ascii = System.Text.Encoding.ASCII.GetString(pdf); Assert(ascii.StartsWith("%PDF-1.7") && ascii.Contains("/Count 2") && ascii.Contains("/ToUnicode"), "PDF"); Assert(Export.Bom(e.Project).Contains("Schneider Electric"), "BOM"); });
        Test("MCP : schémas, paramètres inconnus et types rejetés", () => { Assert(McpTools.Specs.Select(x => x.Name).Distinct().Count() == McpTools.Specs.Length, "Outils dupliqués"); McpTools.Check("component_add", JsonSerializer.SerializeToElement(new { symbol = "coil", x = 200, y = 200 })); Reject(() => McpTools.Check("component_add", JsonSerializer.SerializeToElement(new { x = "incorrect", y = 200 }))); Reject(() => McpTools.Check("project_get", JsonSerializer.SerializeToElement(new { command = "anything" }))); });
        File.WriteAllText(Path.Combine(dir, "self-test.json"), JsonSerializer.Serialize(new { passed = failures == 0, failures, results }, ProjectStore.Json)); return failures == 0 ? 0 : 1;
    }
}
