using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

public static class WorkflowTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Dérivation : division du tracé, continuité électrique, couleur et annulation", () =>
        {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 150); var b = e.AddComponent("coil", 600, 600); var c = e.AddComponent("coil", 800, 300);
            var w = e.Connect(new(a.Id, "A2"), new(b.Id, "A1"), [new(200, 250), new(450, 250), new(450, 450), new(600, 450)], "24V", "#2468AC");
            var original = w.Route(e.Sheet); var snapshot = ProjectStore.Serialize(e.Project); e.CurrentWireColor = "#A02080";
            var branch = e.Branch(new(c.Id, "A1"), w.Id, new(452, 350)); var j = e.Sheet.Components.Single(x => x.Symbol == "junction");
            assert(j.X == 450 && j.Y == 350 && !j.TagPlacement.Visible, "Projection sur le segment");
            assert(e.Sheet.Wires.Count == 3 && e.Sheet.Wires.Count(x => x.From.ComponentId == j.Id || x.To.ComponentId == j.Id) == 3, "Trois branches attachées à la même jonction");
            var parts = e.Sheet.Wires.Where(x => x.Id != branch.Id).ToArray(); assert(parts.Length == 2 && parts.All(x => x.Color == "#2468AC") && branch.Color == "#A02080" && e.Sheet.Wires.All(x => x.Number == "24V"), "Couleurs et numéro commun");
            var arrival = branch.Route(e.Sheet); assert(arrival[^2].Y == j.Y && arrival[^2].X != j.X, "Arrivée perpendiculaire sans longer le fil cible");
            assert(arrival[1].Y < arrival[0].Y, "Sortie vers l’extérieur de la borne haute");
            var joined = WireRouting.Simplify(parts[0].Route(e.Sheet).Concat(parts[1].Route(e.Sheet))); assert(joined.SequenceEqual(original), "Tracé d’origine conservé");
            var inks = Drawing.Scene(e.Project, e.Sheet); assert(inks.Any(x => x.Kind == "dot" && x.X == j.X && x.Y == j.Y) && !inks.Any(x => x.Kind == "circle" && x.X == j.X && x.Y == j.Y), "Point plein visible");
            e.Undo(); assert(ProjectStore.Serialize(e.Project) == snapshot, "Une annulation retire toute la dérivation"); e.Redo();
            e.Save(Path.Combine(dir, "branch-example.gemelec")); var loaded = ProjectStore.Load(e.FilePath!); assert(loaded.Sheets[0].Wires.Count == 3, "Sauvegarde de la topologie");
            Export.Pdf(e.Project, Path.Combine(dir, "branch-preview.pdf")); Render(e.Project, e.Sheet, Path.Combine(dir, "branch-preview.png"));
        });
        test("Dérivation : borne existante, reconnexion et rejet atomique", () =>
        {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 150); var b = e.AddComponent("coil", 200, 550); var c = e.AddComponent("coil", 500, 300);
            var target = e.Connect(new(a.Id, "A2"), new(b.Id, "A1")); var added = e.Branch(new(c.Id, "A1"), target.Id, a.PortPoint("A2"));
            assert(e.Sheet.Components.Count == 3 && added.To == target.From, "Réutilisation de la borne sans jonction superposée");
            e.Branch(added.From, target.Id, new(200, 350), reconnectId: added.Id); assert(e.Sheet.Wires.Count == 3 && e.Sheet.Components.Count == 4, "Reconnexion sur jonction");
            string saved = ProjectStore.Serialize(e.Project);
            reject(() => e.Branch(new("missing", "1"), target.Id, new(200, 280))); assert(ProjectStore.Serialize(e.Project) == saved, "Rollback complet");
            reject(() => e.Branch(added.From, added.Id, new(300, 350), reconnectId: added.Id));
            var projected = Editor.WirePoint([new(203.5, 100), new(203.5, 300)], new(207, 157)); assert(projected.Point == new Point(203.5, 160), "Ne pas décaler hors du fil sur une grille différente");
        });
        test("Folios BOM : pagination, images, en-têtes, remplacement, persistance et PDF", () =>
        {
            var e = new Editor(); var s = e.Sheet;
            e.Change(() => { for (int i = 0; i < 70; i++) s.Components.Add(new() { Symbol = "coil", Tag = "K" + i, Description = "Contacteur de commande " + i, Brand = "Marque exemple", Reference = "REF-" + i, X = 100 + i % 10 * 100, Y = 100 + i / 10 * 80 }); });
            var image = ImageAssets.Import(Path.Combine(dir, "document-image-source.png")); e.Change(() => { e.Project.Images[image.Id] = image.Data; s.Components[0].ImageId = image.Id; });
            var saved = ProjectStore.Serialize(e.Project); var pages = e.GenerateBom(new());
            assert(pages.Count > 1 && pages.Sum(x => x.BomPage!.Rows.Count) == 70, "Pagination complète sans lignes perdues");
            assert(pages.All(x => x.TitleBlockId == "" && x.Components.Count == 0 && DocumentModel.Template(e.Project, x) is null), "Sans cartouche");
            assert(pages.All(x => Drawing.Scene(e.Project, x).Any(i => i.Text == "Fabricant")), "En-têtes répétés");
            assert(Drawing.Scene(e.Project, pages[0]).Any(i => i.Kind == "image"), "Photo intégrée dans le folio");
            reject(() => e.AddComponent("coil", 100, 100)); assert(e.Sheet.Components.Count == 0, "Folio généré protégé");
            e.Undo(); assert(ProjectStore.Serialize(e.Project) == saved, "Undo génération"); e.Redo();
            int count = e.Project.Sheets.Count; e.GenerateBom(e.Project.BomOptions); assert(e.Project.Sheets.Count == count, "Actualisation sans doublons");
            e.Save(Path.Combine(dir, "bom-folios.gemelec")); assert(ProjectStore.Load(e.FilePath!).Sheets.Count == count, "Relecture des folios");
            Export.Pdf(e.Project, Path.Combine(dir, "bom-folios.pdf")); assert(System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(dir, "bom-folios.pdf"))).Contains("/Type /Pages /Count " + PdfNavigation.Pages(e.Project).Count), "Toutes les pages exportées");
            Render(e.Project, e.Sheet, Path.Combine(dir, "bom-folios.png"));
        });
        test("BOM : continuation des longues lignes et limites atomiques", () =>
        {
            var e = new Editor(); e.Change(() => { for (int i = 0; i < 150; i++) e.Sheet.Components.Add(new() { Symbol = "coil", Tag = "REPERE-TRES-LONG-" + i, Description = new string('X', 1800), X = 100, Y = 100 }); });
            var o = new BomOptions(); var pages = e.GenerateBom(o); var fragments = pages.SelectMany(s => s.BomPage!.Rows).ToArray();
            assert(fragments.Length > 1 && fragments.Skip(1).All(r => r.Continuation), "Une ligne longue continue sur plusieurs pages");
            int tagColumn = pages[0].BomPage!.Columns.FindIndex(c => c.Key == "tags"); var text = string.Concat(fragments.SelectMany(r => r.Cells[tagColumn]));
            assert(text == BomReport.Rows(e.Project, o)[0].Values["tags"], "Texte conservé intégralement");
            var before = ProjectStore.Serialize(e.Project); o.GroupArticles = false; reject(() => e.GenerateBom(o)); assert(ProjectStore.Serialize(e.Project) == before, "Limite 200 pages sans perte de l’existant");
        });
        test("Préférences : grille et palette persistantes, couleur des nouveaux fils", () =>
        {
            var file = Path.Combine(dir, "test-preferences-roundtrip.json"); var p = new EditorPreferences { GridVisible = false, GridIntensity = 65, WireColor = "#A123BC" }; p.Colors.Add(new("Teinte atelier", "#123456")); p.Save(file);
            var loaded = EditorPreferences.Load(file); assert(!loaded.GridVisible && loaded.GridIntensity == 65 && loaded.Colors.Last().Color == "#123456", "Préférences relues");
            loaded.WireColor = "invalid"; reject(() => loaded.Save(file)); assert(EditorPreferences.Load(file).WireColor == "#A123BC", "Préserver les préférences après rejet");
        });
        test("Grille écran seule, palette et BOM : commandes des dialogues hors écran", () =>
        {
            var old = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1");
            var app = Application.Current ?? new Application(); var window = new MainWindow();
            try
            {
                window.Editor.Open(Path.Combine(dir, "document-example.gemelec"));
                foreach (var kind in new[] { "grid", "palette", "bom" })
                {
                    window.DocumentTestProbe = dialog =>
                    {
                        var content = (FrameworkElement)dialog.Content; double w = dialog.Width - 16, h = dialog.Height - 40;
                        content.Measure(new(w, h)); content.Arrange(new(0, 0, w, h)); content.UpdateLayout(); SavePng(content, Path.Combine(dir, "ui-" + kind + ".png"), (int)w, (int)h);
                        void Click(string label) => Descendants(content).OfType<Button>().Single(b => b.Content is string text && text == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        if (kind == "grid") { Descendants(content).OfType<Slider>().Single().Value = 70; Click("Enregistrer"); assert(window.Canvas.GridIntensity == 70, "Intensité appliquée"); }
                        if (kind == "palette") { var inputs = Descendants(content).OfType<TextBox>().ToArray(); inputs[0].Text = "#914FC2"; inputs[1].Text = "Test violet"; Click("Enregistrer cette couleur"); Click("Utiliser pour les prochains fils"); assert(window.Editor.CurrentWireColor == "#914FC2" && EditorPreferences.Load().Colors.Any(c => c.Name == "Test violet"), "Palette enregistrée"); }
                        if (kind == "bom") { Click("Ajouter / actualiser les folios BOM"); assert(window.Editor.Sheet.BomPage is not null, "Folio ajouté depuis dialogue"); }
                    };
                    window.TestDocumentDialog(kind);
                }
                window.Editor.SelectSheet(window.Editor.Project.Sheets[0].Id); var canvas = new SheetCanvas(window.Editor) { GridVisible = true, GridIntensity = 70 }; canvas.Measure(new(1000, 750)); canvas.Arrange(new(0, 0, 1000, 750)); canvas.Fit(); canvas.UpdateLayout();
                Export.Pdf(window.Editor.Project, Path.Combine(dir, "grid-on.pdf")); var on = Pixels(canvas); canvas.GridVisible = false; canvas.InvalidateVisual(); canvas.UpdateLayout(); var off = Pixels(canvas);
                assert(!on.SequenceEqual(off), "Réglage modifie le rendu écran"); Export.Pdf(window.Editor.Project, Path.Combine(dir, "grid-off.pdf"));
                assert(File.ReadAllBytes(Path.Combine(dir, "grid-on.pdf")).SequenceEqual(File.ReadAllBytes(Path.Combine(dir, "grid-off.pdf"))), "PDF identique sans grille");
            }
            finally { window.DocumentTestProbe = null; window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", old); }
        });
        test("Fluidité : pagination 1 000 articles et rendu mis en cache", () =>
        {
            var p = new Project(); for (int i = 0; i < 1000; i++) p.Sheets[0].Components.Add(new() { Symbol = "coil", Tag = "K" + i, Description = "Contacteur", Reference = "R" + i, X = 80 + i % 25 * 48, Y = 70 + i / 25 * 18 });
            var options = new BomOptions { Columns = [new() { Key = "tags", Label = "Repères" }, new() { Key = "reference", Label = "Référence" }, new() { Key = "description", Label = "Description" }] };
            var watch = Stopwatch.StartNew(); var pages = BomFolios.Create(p, options); watch.Stop(); double bomMs = watch.Elapsed.TotalMilliseconds; p.Sheets.AddRange(pages);
            var editor = new Editor(); editor.Change(() => { editor.Sheet.Components = p.Sheets[0].Components; for (int i = 1; i < 1000; i++) editor.Sheet.Wires.Add(new() { From = new(editor.Sheet.Components[i - 1].Id, "A2"), To = new(editor.Sheet.Components[i].Id, "A1") }); });
            var canvas = new SheetCanvas(editor); canvas.Measure(new(1000, 750)); canvas.Arrange(new(0, 0, 1000, 750)); canvas.Fit(); canvas.UpdateLayout(); Pixels(canvas);
            watch.Restart(); for (int i = 0; i < 10; i++) { canvas.InvalidateVisual(); canvas.UpdateLayout(); Pixels(canvas); } watch.Stop();
            double offscreenRasterMs = watch.Elapsed.TotalMilliseconds / 10;
            var index = editor.Sheet.Components.ToDictionary(c => c.Id); var routes = editor.Sheet.Wires.Select(w => w.Route(editor.Sheet, index)).ToArray();
            watch.Restart(); for (int sample = 0; sample < 100; sample++) foreach (var route in routes) Editor.WirePoint(route, new(400 + sample, 400)); watch.Stop();
            assert(pages.Sum(s => s.BomPage!.Rows.Count) == 1000, "1 000 articles paginés"); File.WriteAllText(Path.Combine(dir, "workflow-performance.json"), JsonSerializer.Serialize(new { articles = 1000, wires = 999, pages = pages.Count, bomMs, offscreenRasterMs, branchHitMs = watch.Elapsed.TotalMilliseconds / 100 }, ProjectStore.Json));
        });
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent) { yield return parent; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static byte[] Pixels(FrameworkElement visual) { var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0); return bytes; }
    private static void Render(Project p, Sheet s, string path) { var v = new DrawingVisual(); using (var dc = v.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new(0, 0, 1400, 990)); Drawing.Render(dc, Drawing.Scene(p, s)); } SavePng(v, path, 1400, 990); }
    private static void SavePng(Visual visual, string path, int width, int height) { var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); var background = new DrawingVisual(); using (var dc = background.RenderOpen()) dc.DrawRectangle(Brushes.White, null, new(0, 0, width, height)); bitmap.Render(background); bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file); }
}
