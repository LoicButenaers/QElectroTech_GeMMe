using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace GeMMeElec;

public static class WireAppearanceTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Numéro indépendant : protection et continuité électrique conservée", () => {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var b = e.AddComponent("terminal", 200, 400); var c = e.AddComponent("coil", 500, 400);
            var own = e.Connect(new(a.Id, "A1"), new(b.Id, "1"), number: "C1", numberIndependent: true); var common = e.Connect(new(b.Id, "2"), new(c.Id, "A1"), number: "26");
            assert(own.Number == "C1" && common.Number == "26", "Exception écrasée à la création"); e.SetWireNumber(common.Id, "27"); assert(own.Number == "C1", "Exception renommée avec le réseau");
            e.SetWireNumber(own.Id, "C2"); assert(common.Number == "27", "Renommage indépendant propagé");
            var load = e.AddComponent("coil", 700, 400); var next = e.Connect(new(a.Id, "A1"), new(load.Id, "A1")); assert(next.Number == "27", "Numéro indépendant adopté comme numéro commun");
            assert(CrossReferences.Trace(e.Project, new(a.Id, "A1")).Contains(new Endpoint(c.Id, "A1")), "Exception coupant la connexion électrique");
            e.Reconnect(own.Id, false, new(load.Id, "A2")); assert(own.Number == "C2" && own.NumberIndependent, "Reconnexion perdant le numéro propre");
        });
        test("Retour au numéro commun, annulation et persistance des styles", () => {
            var e = Fixture(); var own = e.Sheet.Wires[1]; string before = ProjectStore.Serialize(e.Project); e.SetWireNumberIndependent(own.Id, false);
            assert(!own.NumberIndependent && own.Number == "26", "Retour au numéro commun"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Retour non annulable"); e.Redo();
            var path = Path.Combine(dir, "self-test-wire-appearance.gemelec"); e.Save(path); var restored = ProjectStore.Load(path); assert(restored.Sheets[0].Wires[1] is { Thickness: 3.2, LineStyle: "dashed", Color: "#D32F2F" }, "Style perdu à la lecture");
            e.Undo(); e.Save(path); restored = ProjectStore.Load(path); assert(restored.Sheets[0].Wires[1].NumberIndependent && restored.Sheets[0].Wires[1].Number == "CABLE-01", "Exception perdue à la lecture");
            before = ProjectStore.Serialize(e.Project); reject(() => e.Change(() => e.Sheet.Wires[1].Thickness = 0)); assert(ProjectStore.Serialize(e.Project) == before, "Épaisseur invalide altérant le modèle"); reject(() => e.Change(() => e.Sheet.Wires[1].LineStyle = "invalid"));
        });
        test("Migration v3 : défauts de trait et exceptions de folio conservés", () => {
            var p = new Project { ZoneColumns = 16, ZoneRows = 8 }; p.Sheets[0].UseProjectZones = false; p.Sheets[0].ZoneColumns = 12;
            var e = Fixture(); p.Sheets[0].Components = e.Sheet.Components; p.Sheets[0].Wires = e.Sheet.Wires;
            var old = JsonNode.Parse(ProjectStore.Serialize(p))!; old["version"] = 3; foreach (var w in old["sheets"]![0]!["wires"]!.AsArray()) { w!.AsObject().Remove("numberIndependent"); w.AsObject().Remove("thickness"); w.AsObject().Remove("lineStyle"); }
            var migrated = ProjectStore.Deserialize(old.ToJsonString()); assert(migrated.Version == Project.CurrentVersion && migrated.ZoneColumns == 16 && !migrated.Sheets[0].UseProjectZones && migrated.Sheets[0].Wires.All(w => w.Thickness == 1.6 && w.LineStyle == "solid" && !w.NumberIndependent), "Migration modifiant le repérage ou les traits");
        });
        test("Dérivation : tronçons indépendants et styles conservés au nettoyage", () => {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var b = e.AddComponent("coil", 200, 500); var c = e.AddComponent("coil", 500, 300);
            var target = e.Connect(new(a.Id, "A2"), new(b.Id, "A1"), number: "C1", numberIndependent: true, thickness: 4, lineStyle: "dashdot");
            var branch = e.Branch(new(c.Id, "A1"), target.Id, new(200, 350)); var parts = e.Sheet.Wires.Where(w => w.Id != branch.Id).ToList(); assert(parts.All(w => w.Number == "C1" && w.NumberIndependent && w.Thickness == 4 && w.LineStyle == "dashdot") && !branch.NumberIndependent && branch.Number != "C1", "Dérivation perdant ses attributs");
            e.Change(() => parts[1].LineStyle = "dotted"); e.Delete([branch.Id]); assert(e.Sheet.Wires.Count == 2 && e.Sheet.Components.Any(Junctions.Automatic), "Fusion détruisant une différence de style");
        });
        test("Écran, SVG et PDF : traits personnalisés et numéros distincts", () => {
            var e = Fixture(); var scene = Drawing.Scene(e.Project, e.Sheet); assert(scene.Any(i => i.Kind == "line" && i.Thickness == 3.2 && i.LineStyle == "dashed" && i.Color == "#D32F2F"), "Style absent de la scène");
            foreach (string style in new[] { "solid", "dashed", "dotted", "dashdot" }) { e.Change(() => e.Sheet.Wires[1].LineStyle = style); string svg = Path.Combine(dir, "self-test-wire-" + style + ".svg"); Export.Svg(e.Project, e.Sheet, svg); var lines = XDocument.Load(svg).Descendants().Where(el => el.Name.LocalName == "line" && (string?)el.Attribute("stroke") == "#D32F2F").ToList(); assert(lines.Count > 0 && lines.All(el => (string?)el.Attribute("stroke-width") == "3.2" && (style == "solid" ? el.Attribute("stroke-dasharray") is null : el.Attribute("stroke-dasharray") is not null)), "SVG incohérent"); }
            Export.Pdf(e.Project, Path.Combine(dir, "self-test-wire-styles.pdf")); var pdf = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(dir, "self-test-wire-styles.pdf"))); assert(pdf.Contains("3.2 w [12.8 6.4 3.2 6.4] 0 d"), "PDF perdant les tirets");
            e.Change(() => e.Sheet.Wires[1].LineStyle = "dashed"); Render(scene = Drawing.Scene(e.Project, e.Sheet), Path.Combine(dir, "apercu-conducteur-independant.png"));
        });
        test("Inspecteur : numéro indépendant, trait et épaisseur immédiats", () => {
            string? oldSession = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1"); var window = new MainWindow();
            try {
                var e = Fixture(); window.Editor.ImportProject(e.Project); window.SetInterface("compact", "light", false); window.Canvas.Select(window.Editor.Sheet.Wires[0].Id); var panel = (FrameworkElement)window.Content; Arrange(panel);
                var own = Descendants(window.PropertyPanel).OfType<CheckBox>().Single(c => c.Name == "IndependentWireNumber"); own.IsChecked = true; window.PropertyField("number").Text = "PROPRE"; window.PropertyField("thickness").Text = "2,8";
                Descendants(window.PropertyPanel).OfType<ComboBox>().Single(c => c.Name == "WireLineStyle").SelectedValue = "dotted";
                assert(window.Editor.Sheet.Wires[0] is { NumberIndependent: true, Number: "PROPRE", Thickness: 2.8, LineStyle: "dotted" } && window.Editor.Sheet.Wires[2].Number == "26", "Contrôles non appliqués");
                window.PropertyField("thickness").Text = "99"; assert(window.Editor.Sheet.Wires[0].Thickness == 2.8, "Épaisseur invalide appliquée"); window.PropertyField("thickness").Text = "2,8";
                own.IsChecked = false; assert(window.PropertyField("number").Text == "26" && !window.Editor.Sheet.Wires[0].NumberIndependent, "Champ non actualisé au retour au numéro commun");
                Arrange(panel); var bitmap = new RenderTargetBitmap(1440, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(panel); Save(bitmap, Path.Combine(dir, "ui-wire-appearance.png"));
            } finally { window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", oldSession); }
        });
    }
    private static Editor Fixture()
    {
        var e = new Editor(); e.Change(() => { e.Sheet.Title = "Conducteur indépendant"; e.Sheet.TitleBlockId = "technical"; });
        var source = e.AddComponent("coil", 200, 200); var first = e.AddComponent("coil", 500, 500); var own = e.AddComponent("coil", 800, 500); var last = e.AddComponent("coil", 1100, 500);
        e.Connect(new(source.Id, "A2"), new(first.Id, "A1"), [new(200, 280), new(500, 280)], "26");
        e.Connect(new(source.Id, "A2"), new(own.Id, "A1"), [new(200, 330), new(800, 330)], "CABLE-01", "#D32F2F", true, 3.2, "dashed");
        e.Connect(new(source.Id, "A2"), new(last.Id, "A1"), [new(200, 380), new(1100, 380)]); return e;
    }
    private static void Arrange(FrameworkElement content) { content.Measure(new(1440, 900)); content.Arrange(new(0, 0, 1440, 900)); content.UpdateLayout(); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent) { yield return parent; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Render(List<Ink> scene, string path) { var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new(0, 0, 1400, 990)); Drawing.Render(dc, scene); } var bitmap = new RenderTargetBitmap(1400, 990, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); Save(bitmap, path); }
    private static void Save(BitmapSource bitmap, string path) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); png.Save(file); }
}
