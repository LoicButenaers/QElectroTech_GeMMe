using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

public static class ReferenceTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Repérage : limites, cartouche, colonnes, lignes et numéros libres", () =>
        {
            var p = new Project(); var s = p.Sheets[0]; var a = SheetZones.Area(p, s);
            assert(SheetZones.Zone(p, s, 30, 30) == "A1" && SheetZones.Zone(p, s, a.Right, a.Bottom) == "J20", "Angles du cadre");
            assert(SheetZones.Zone(p, s, 29, 30) == "hors cadre" && SheetZones.Zone(p, s, 100, a.Bottom + 1) == "hors cadre", "Pas de fausse zone hors dessin");
            var c = new Component { X = 200, Y = 200 }; s.Number = "01-A"; assert(SheetZones.Address(p, s, c) == "01-A / B3", "Numéro libre et position");
            s.UseProjectZones = false; s.ZoneRows = 26; s.ZoneColumns = 40; assert(SheetZones.Zone(p, s, a.Right, a.Bottom) == "Z40", "Configuration maximale");
            s.ZoneColumns = 0; reject(() => ProjectStore.ValidateStructure(p));
        });
        test("BOM : association exacte repère / folio / zone, regroupement et déplacement", () =>
        {
            var e = new Editor(); var c1 = e.AddComponent("coil", 200, 200); var c2 = e.AddComponent("coil", 800, 600); var o = new BomOptions();
            var rows = BomReport.Rows(e.Project, o); assert(rows.Count == 1 && rows[0].Values["pages"].Contains(c1.Tag + " : 1 / B3") && rows[0].Values["pages"].Contains(c2.Tag + " : 1 / G12"), "Quantités groupées avec deux emplacements distincts");
            e.Change(() => c1.X = 400); assert(BomReport.Rows(e.Project, o)[0].Values["pages"].Contains("1 / B6"), "Position recalculée");
            e.AddComponent("folio_next", 500, 200); o.IncludeAuxiliary = true; assert(BomReport.Rows(e.Project, o).Count == 1, "Renvois exclus des articles");
            e.GenerateBom(o); assert(e.Sheet.BomPage!.Rows.SelectMany(r => r.Cells).SelectMany(x => x).Any(x => x.Contains("B6")), "Emplacement présent dans le folio BOM");
        });
        test("Renvois : lien réel entre folios, affichage dynamique, suppression et historique", () =>
        {
            var e = new Editor(); e.New("Renvois · exemple 24 V"); var first = e.Sheet; var a = e.AddComponent("terminal", 200, 300); var from = e.AddComponent("folio_next", 600, 300); e.Connect(new(a.Id, "2"), new(from.Id, "1")); e.ConfigureReference(from.Id, "+24V", "pair", 0, false);
            var second = e.AddSheet("Commande"); var to = e.AddComponent("folio_previous", 200, 300); var b = e.AddComponent("coil", 650, 300); e.Connect(new(to.Id, "1"), new(b.Id, "A1")); e.ConfigureReference(to.Id, "+24V", "pair", 0, false);
            var links = new CrossReferences(e.Project); assert(links.Targets(from.Id).Single().Component.Id == to.Id && links.Targets(to.Id).Single().Component.Id == from.Id, "Références réciproques");
            assert(CrossReferences.Trace(e.Project, new(a.Id, "2")).Contains(new Endpoint(b.Id, "A1")), "Continuité du réseau interfolio");
            assert(!CrossReferences.Trace(e.Project, new(a.Id, "2")).Contains(new Endpoint(b.Id, "A2")), "Ne pas court-circuiter l’appareil");
            string oldLabel = links.Label(e.Project, from); e.Change(() => { to.X = 400; second.Number = "CMD-02"; });
            string label = new CrossReferences(e.Project).Label(e.Project, from); assert(label != oldLabel && label.Contains("CMD-02 / D6"), "Renvoi suit numéro et déplacement");
            assert(Drawing.Scene(e.Project, first).Any(i => i.Text == label), "Scène partagée avec le PDF");
            e.Delete([to.Id]); assert(new CrossReferences(e.Project).Targets(from.Id).Count == 0 && e.Validate().Any(f => f.Level == "Renvoi"), "Renvoi cassé signalé"); e.Undo();
            e.Save(Path.Combine(dir, "references-example.gemelec")); var loaded = ProjectStore.Load(e.FilePath!); assert(CrossReferences.Trace(loaded, new(a.Id, "2")).Contains(new Endpoint(b.Id, "A1")), "Liaison conservée après sauvegarde");
            e.GenerateBom(new()); Export.Pdf(e.Project, Path.Combine(dir, "references-example.pdf")); Export.Svg(e.Project, e.Project.Sheets[0], Path.Combine(dir, "references-example.svg"));
            Render(e.Project, e.Project.Sheets[0], Path.Combine(dir, "reference-first.png")); Render(e.Project, e.Project.Sheets[1], Path.Combine(dir, "reference-second.png")); Render(e.Project, e.Sheet, Path.Combine(dir, "reference-bom.png"));
        });
        test("Renvois : tri des paires, étoile, source unique et entrées invalides", () =>
        {
            var e = new Editor(); List<string> ids = [];
            for (int i = 0; i < 4; i++) { var c = e.AddComponent(i % 2 == 0 ? "folio_next" : "folio_previous", 200 + i * 200, 200); ids.Add(c.Id); e.ConfigureReference(c.Id, "COM", "pair", i, false); }
            var links = new CrossReferences(e.Project); assert(links.Targets(ids[0]).Single().Component.Id == ids[1] && links.Targets(ids[2]).Single().Component.Id == ids[3], "Chaîne par paires");
            e.ConfigureReference(ids[3], "COM", "pair", 0, false); assert(new CrossReferences(e.Project).Targets(ids[0]).Single().Component.Id == ids[3], "Tri manuel prioritaire");
            e.ConfigureReference(ids[2], "COM", "star", 2, true); links = new(e.Project); assert(links.Targets(ids[2]).Count == 3 && links.Targets(ids[0]).Single().Component.Id == ids[2], "Étoile");
            e.ConfigureReference(ids[0], "COM", "star", 0, true); assert(e.Sheet.Components.Count(c => c.LinkSource) == 1 && new CrossReferences(e.Project).Targets(ids[0]).Count == 3, "Changement de source atomique");
            var before = ProjectStore.Serialize(e.Project); reject(() => e.ConfigureReference(ids[0], "COM", "invalid", 0, true)); assert(ProjectStore.Serialize(e.Project) == before, "Rejet atomique");
            e.ConfigureReference(ids[0], "", "pair", 0, false); assert(new CrossReferences(e.Project).Targets(ids[0]).Count == 0, "Délier un renvoi");
        });
        test("Bibliothèque industrielle : familles, connexions et atlas des symboles", () =>
        {
            assert(Symbols.All.Count >= 80 && Symbols.All.Values.Where(s=>!s.Id.StartsWith("official-")).Select(s => s.Category).Distinct().Count() == 10, "Bibliothèque organisée complète pour les familles de base");
            assert(Symbols.Search("sectionneur").Count() == 4 && Symbols.Search("relais").Any(), "Recherche sans accents");
            foreach (var s in Symbols.All.Values)
            {
                assert(s.Category != "Divers" && s.Ports.Count > 0 && s.Ports.Select(p => p.Id).Distinct().Count() == s.Ports.Count, "Bornes et catégorie " + s.Id);
                foreach (var shape in s.Shapes) assert(new[] { shape.X, shape.Y, shape.A, shape.B }.All(double.IsFinite), "Géométrie " + s.Id);
            }
            foreach (var category in Symbols.All.Values.GroupBy(s => s.Category))
            {
                var items = category.ToArray(); int height = 70 + (int)Math.Ceiling(items.Length / 5.0) * 180; var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new(0, 0, 1200, height)); Drawing.Render(dc, [new("text", 20, 15, Text: category.Key, Size: 22)]);
                    for (int i = 0; i < items.Length; i++)
                    {
                        var s = items[i]; double x = 120 + i % 5 * 240, y = 115 + i / 5 * 180;
                        dc.PushTransform(new TranslateTransform(x, y)); dc.PushTransform(new ScaleTransform(.65, .65)); Drawing.Render(dc, s.Shapes.Select(shape => new Ink(shape.Kind, shape.X, shape.Y, shape.A, shape.B, shape.Text, Size: shape.A))); dc.Pop(); dc.Pop();
                        int n = 0; foreach (var line in BomFolios.Wrap(s.Name, 220)) Drawing.Render(dc, [new("text", x - 110, y + 52 + n++ * 15, Text: line, Size: 11)]);
                    }
                }
                SavePng(visual, Path.Combine(dir, "symbols-" + category.Key[..2] + ".png"), 1200, height);
            }
            File.WriteAllText(Path.Combine(dir, "symbols-inventory.json"), JsonSerializer.Serialize(Symbols.All.Values.Select(s => new { s.Id, s.Name, s.Category, ports = s.Ports.Select(p => p.Id) }), ProjectStore.Json));
        });
        test("Compatibilité 0.4 et interfaces de repérage / renvois hors écran", () =>
        {
            var oldJson = JsonNode.Parse(ProjectStore.Serialize(new Project()))!; var sheet = oldJson["sheets"]![0]!.AsObject(); sheet.Remove("zoneRows"); sheet.Remove("zoneColumns"); assert(ProjectStore.Deserialize(oldJson.ToJsonString()).Sheets[0].ZoneColumns == 20, "Valeurs initiales ancien projet");
            var oldSession = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1"); var app = Application.Current ?? new Application(); var window = new MainWindow();
            try
            {
                window.Editor.Open(Path.Combine(dir, "references-example.gemelec"));
                foreach (string kind in new[] { "zones", "references" })
                {
                    window.DocumentTestProbe = dialog =>
                    {
                        var content = (FrameworkElement)dialog.Content; double w = dialog.Width - 16, h = dialog.Height - 40; content.Measure(new(w, h)); content.Arrange(new(0, 0, w, h)); content.UpdateLayout(); SavePng(content, Path.Combine(dir, "ui-" + kind + ".png"), (int)w, (int)h);
                        if (kind == "zones") { var boxes = Descendants(content).OfType<TextBox>().ToArray(); boxes[0].Text = "16"; boxes[1].Text = "8"; Descendants(content).OfType<Button>().Single(b => b.Content is string text && text == "Enregistrer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); assert(SheetZones.Columns(window.Editor.Project, window.Editor.Sheet) == 16, "Configuration par dialogue"); }
                        else { var grid = Descendants(content).OfType<DataGrid>().Single(); grid.SelectedIndex = 1; Descendants(content).OfType<Button>().Single(b => b.Content is string text && text == "Aller au symbole").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); assert(window.Editor.Sheet.Number == "CMD-02", "Navigation au folio cible"); }
                    };
                    window.TestDocumentDialog(kind);
                }
                var main = (FrameworkElement)window.Content; main.Measure(new(1480, 920)); main.Arrange(new(0, 0, 1480, 920)); main.UpdateLayout(); window.Canvas.Fit(); main.UpdateLayout(); SavePng(main, Path.Combine(dir, "ui-industrial-library.png"), 1480, 920);
            }
            finally { window.DocumentTestProbe = null; window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", oldSession); }
        });
        test("Fluidité des références sur 1 000 renvois", () =>
        {
            var p = new Project(); for (int i = 0; i < 1000; i++) p.Sheets[0].Components.Add(new() { Symbol = i % 2 == 0 ? "folio_next" : "folio_previous", Tag = "RF" + i, LinkName = "NET" + i / 2, X = 100 + i % 20 * 50, Y = 100 + i / 20 * 14 });
            var watch = Stopwatch.StartNew(); var links = new CrossReferences(p); foreach (var c in p.Sheets[0].Components) links.Label(p, c); watch.Stop(); assert(links.Targets(p.Sheets[0].Components[0].Id).Count == 1, "Résolution");
            File.WriteAllText(Path.Combine(dir, "references-performance.json"), JsonSerializer.Serialize(new { symbols = Symbols.All.Count, references = 1000, resolveAndLabelMs = watch.Elapsed.TotalMilliseconds }, ProjectStore.Json));
        });
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject p) { yield return p; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(p); i++) foreach (var c in Descendants(VisualTreeHelper.GetChild(p, i))) yield return c; }
    private static void Render(Project p, Sheet s, string path) { var v = new DrawingVisual(); using (var dc = v.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new(0, 0, 1400, 990)); Drawing.Render(dc, Drawing.Scene(p, s)); } SavePng(v, path, 1400, 990); }
    private static void SavePng(Visual visual, string path, int width, int height) { var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); var background = new DrawingVisual(); using (var dc = background.RenderOpen()) dc.DrawRectangle(Brushes.White, null, new(0, 0, width, height)); bitmap.Render(background); bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file); }
}
