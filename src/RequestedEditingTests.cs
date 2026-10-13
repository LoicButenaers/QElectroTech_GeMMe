using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

public static class RequestedEditingTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Cadre et cartouche : bord commun sous les lettres", () =>
        {
            var e = Example(); var s = e.Sheet; var bottom = DocumentModel.DrawingBottom(e.Project, s);
            var cells = DocumentModel.Scene(e.Project, s).Where(i => i.Kind == "rect").ToList();
            assert(cells.Min(i => i.X) == 10 && cells.Min(i => i.Y) == bottom && cells.Max(i => i.X + i.A) == s.Width - 30, "Bords du cartouche décalés");
            var frame = SheetZones.Frame(e.Project, s).ToList();
            assert(frame.Any(i => i.Kind == "rect" && i.X == 10 && i.Y + i.B == bottom), "Bande des lettres interrompue");
            assert(Drawing.Scene(e.Project, s)[0] is { X: 10, Y: 10 }, "Ancien bord traversant le cartouche");
            var preview = new TitleBlockPreview(); preview.Set(e.Project, s, DocumentModel.Template(e.Project, s)!); assert(preview.ContentBounds.Left >= -1 && preview.ContentBounds.Right <= s.Width - 39, "Aperçu de l’éditeur de cartouche décalé");
            Render(e.Project, s, Path.Combine(dir, "apercu-cadre-cartouche.png"), new(0, 0, s.Width, s.Height), 1);
            Render(e.Project, s, Path.Combine(dir, "apercu-detail-cartouche.png"), new(0, bottom - 105, 450, 285), 2);
            Export.Pdf(e.Project, Path.Combine(dir, "self-test-frame-continuity.pdf"));
            e.Save(Path.Combine(dir, "apercu-cadre-cartouche.gemelec"));
        });
        test("Repérage commun : exceptions, nouveaux folios, annulation et sauvegarde", () =>
        {
            var e = new Editor(); var first = e.Sheet; var exception = e.AddSheet("Exception");
            e.Change(() => { exception.UseProjectZones = false; exception.ZoneColumns = 8; exception.ZoneRows = 6; e.Project.ZoneColumns = 16; e.Project.ZoneRows = 8; });
            var next = e.AddSheet("Hérite"); assert(SheetZones.Columns(e.Project, first) == 16 && SheetZones.Rows(e.Project, next) == 8 && SheetZones.Columns(e.Project, exception) == 8, "Héritage ou exception perdu");
            string before = ProjectStore.Serialize(e.Project); e.Change(() => { e.Project.ZoneColumns = 24; e.Project.ZoneRows = 12; });
            assert(SheetZones.Rows(e.Project, first) == 12 && SheetZones.Rows(e.Project, exception) == 6, "Exception écrasée");
            e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Undo projet"); e.Redo();
            var path = Path.Combine(dir, "self-test-project-zones.gemelec"); e.Save(path); var restored = ProjectStore.Load(path);
            assert(restored.ZoneColumns == 24 && !restored.Sheets[1].UseProjectZones && SheetZones.Rows(restored, restored.Sheets[1]) == 6, "Persistance de l’exception");
            reject(() => e.Change(() => e.Project.ZoneRows = 27)); assert(e.Project.ZoneRows == 12, "Réglage invalide retenu");
        });
        test("Migration v1/v2 : repérages locaux conservés", () =>
        {
            foreach (int version in new[] { 1, 2 })
            {
                var p = new Project { Sheets = [new() { ZoneColumns = 16, ZoneRows = 8 }, new() { ZoneColumns = 12, ZoneRows = 6 }] };
                var old = JsonNode.Parse(ProjectStore.Serialize(p))!; old["version"] = version; old.AsObject().Remove("zoneColumns"); old.AsObject().Remove("zoneRows"); foreach (var s in old["sheets"]!.AsArray()) s!.AsObject().Remove("useProjectZones");
                var migrated = ProjectStore.Deserialize(old.ToJsonString());
                assert(migrated.Version == Project.CurrentVersion && migrated.ZoneColumns == 16 && migrated.Sheets[0].UseProjectZones && !migrated.Sheets[1].UseProjectZones && SheetZones.Rows(migrated, migrated.Sheets[1]) == 6, "Migration modifiant les zones");
            }
        });
        test("Dialogue de repérage : case d’exception et retour à l’héritage", () =>
        {
            string? oldSession = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1");
            var window = new MainWindow(); window.SetInterface("compact", "light", false);
            try
            {
                window.DocumentTestProbe = dialog => {
                    var content = (FrameworkElement)dialog.Content; content.Measure(new(544, 600)); content.Arrange(new(0, 0, 544, 600)); content.UpdateLayout();
                    var controls = Descendants((DependencyObject)dialog.Content).ToList(); var boxes = controls.OfType<TextBox>().ToArray(); var except = controls.OfType<CheckBox>().Single(c => c.Name == "ZoneException");
                    assert(!boxes[2].IsEnabled && except.IsChecked == false, "Champs locaux actifs sans exception"); except.IsChecked = true; assert(boxes[2].IsEnabled && boxes[3].IsEnabled, "Case sans effet");
                    boxes[0].Text = "16"; boxes[1].Text = "8"; boxes[2].Text = "12"; boxes[3].Text = "6";
                    var root = (FrameworkElement)dialog.Content; root.Measure(new(544, 600)); root.Arrange(new(0, 0, 544, 600)); root.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(544, 600, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(dir, "ui-zones-exception.png"))) png.Save(file);
                    controls.OfType<Button>().Single(b => b.Content is string text && text == "Enregistrer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }; window.TestDocumentDialog("zones");
                assert(!window.Editor.Sheet.UseProjectZones && SheetZones.Columns(window.Editor.Project, window.Editor.Sheet) == 12 && window.Editor.Project.ZoneColumns == 16, "Exception non enregistrée");
                window.DocumentTestProbe = dialog => { var content = (FrameworkElement)dialog.Content; content.Measure(new(544, 600)); content.Arrange(new(0, 0, 544, 600)); content.UpdateLayout(); var controls = Descendants((DependencyObject)dialog.Content).ToList(); controls.OfType<CheckBox>().Single(c => c.Name == "ZoneException").IsChecked = false; controls.OfType<Button>().Single(b => b.Content is string text && text == "Enregistrer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }; window.TestDocumentDialog("zones");
                assert(window.Editor.Sheet.UseProjectZones && SheetZones.Columns(window.Editor.Project, window.Editor.Sheet) == 16, "Retour à l’héritage");
            }
            finally { window.DocumentTestProbe = null; window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", oldSession); }
        });
        test("Numéros : même borne, passage, PE et bornes distinctes", () =>
        {
            var e = new Editor(); var source = e.AddComponent("coil", 200, 150); var a = e.AddComponent("terminal", 200, 300); var b = e.AddComponent("terminal_pe", 400, 300); var load = e.AddComponent("coil", 200, 500);
            var w = e.Connect(new(source.Id, "A1"), new(a.Id, "1"), number: "26");
            var continuation = e.Connect(new(a.Id, "2"), new(load.Id, "A1")); var branch = e.Connect(new(source.Id, "A1"), new(b.Id, "1"));
            var independent = e.Connect(new(source.Id, "A2"), new(b.Id, "2")); // Physical connection joins this output to the existing PE network.
            assert(new[] { w, continuation, branch, independent }.All(w => w.Number == "26"), "Passage ou dérivation renuméroté");
            var other = e.AddComponent("coil", 600, 500); var distinct = e.Connect(new(load.Id, "A2"), new(other.Id, "A1")); assert(distinct.Number != "26", "Bornes d’un appareil confondues");
            assert(CrossReferences.Trace(e.Project, new(a.Id, "1")).Contains(new Endpoint(a.Id, "2")) && !CrossReferences.Trace(e.Project, new(load.Id, "A1")).Contains(new Endpoint(load.Id, "A2")), "Continuité incorrecte");
            string before = ProjectStore.Serialize(e.Project); e.SetWireNumber(continuation.Id, "24V-01");
            assert(e.Sheet.Wires.Where(w => w.Id != distinct.Id).All(w => w.Number == "24V-01"), "Renommage incomplet"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Renommage non atomique"); e.Redo();
            var path = Path.Combine(dir, "self-test-wire-continuity.gemelec"); e.Save(path); assert(ProjectStore.Load(path).Sheets[0].Wires.Count(w => w.Number == "24V-01") == 4, "Numérotation relue");
        });
        test("Dérivation, reconnexion et copie de borne : numéro continu", () =>
        {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var b = e.AddComponent("coil", 200, 500); var c = e.AddComponent("terminal", 400, 300);
            var wire = e.Connect(new(a.Id, "A2"), new(b.Id, "A1"), number: "P1"); var branch = e.Branch(new(c.Id, "1"), wire.Id, new(200, 350));
            assert(e.Sheet.Wires.All(w => w.Number == "P1"), "Dérivation renumérotée");
            e.SaveSymbolCopy(c.Definition, c.Id); var next = e.AddComponent("coil", 500, 500); var w = e.Connect(new(c.Id, "2"), new(next.Id, "A1")); assert(w.Number == "P1", "Copie perdant sa continuité");
            var isolated = e.AddComponent("coil", 600, 500); e.Reconnect(w.Id, false, new(isolated.Id, "A1")); assert(e.Sheet.Wires.Single(x => x.Id == w.Id).Number == "P1", "Reconnexion renumérotée");
            var invalid = c.Definition with { InternalConnections = [new("1", "absente")] }; reject(() => CustomSymbols.Validate(invalid));
        });
        test("Maj + glisser : texte hors symbole, aperçu fluide et undo unique", () =>
        {
            var e = new Editor(); var c = e.AddComponent("coil", 300, 300); var d = e.AddComponent("terminal", 500, 500); e.Connect(new(c.Id, "A2"), new(d.Id, "1"));
            var canvas = new SheetCanvas(e); var refs = new CrossReferences(e.Project); var bounds = Drawing.TextBounds(ComponentTexts.Ink(e.Project, c, refs, "tag")!); var at = bounds.TopLeft + new Vector(2, 2);
            string before = ProjectStore.Serialize(e.Project), cached = e.ExportSnapshot; var routes = e.Sheet.Wires[0].Route(e.Sheet).ToList(); var origin = c.TagPlacement.OffsetX;
            assert(canvas.BeginTextMove(at), "Texte hors du symbole inaccessible"); var watch = Stopwatch.StartNew(); for (int i = 0; i < 120; i++) canvas.MoveText(at + new Vector(60 + i / 120d, -25)); watch.Stop();
            assert(e.ExportSnapshot == cached && c.X == 300 && c.Y == 300 && routes.SequenceEqual(e.Sheet.Wires[0].Route(e.Sheet)), "Aperçu sérialisé ou câblage déplacé");
            canvas.MoveText(at + new Vector(65.5, -23.25)); canvas.EndPreviewMove(); assert(Math.Abs(c.TagPlacement.OffsetX - origin - 65.5) < .001, "Déplacement imprécis");
            File.WriteAllText(Path.Combine(dir, "text-drag-timing.txt"), $"120 déplacements d’aperçu : {watch.Elapsed.TotalMilliseconds:F2} ms");
            e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Plusieurs actions d’annulation"); e.Redo();
            c = e.Sheet.Components.Single(x => x.Id == c.Id); bounds = Drawing.TextBounds(ComponentTexts.Ink(e.Project, c, new(e.Project), "tag")!); at = bounds.TopLeft + new Vector(2, 2); before = ProjectStore.Serialize(e.Project);
            assert(canvas.BeginTextMove(at), "Deuxième déplacement"); canvas.MoveText(at + new Vector(40, 20)); canvas.CancelDrag(); assert(ProjectStore.Serialize(e.Project) == before, "Échap ne restaure pas");
            assert(canvas.BeginTextMove(at), "Maj + clic"); canvas.EndPreviewMove(); assert(ProjectStore.Serialize(e.Project) == before, "Simple clic modifiant le modèle");
        });
        test("Maj + glisser : destination calculée et borne tournée", () =>
        {
            var e = new Editor(); var report = e.AddComponent("folio_next", 300, 300); var canvas = new SheetCanvas(e); var bounds = Drawing.TextBounds(ComponentTexts.Ink(e.Project, report, new(e.Project), "detail")!); var at = bounds.TopLeft + new Vector(2, 2); string before = ProjectStore.Serialize(e.Project);
            assert(canvas.BeginTextMove(at), "Destination inaccessible"); canvas.MoveText(at + new Vector(50, 30)); canvas.MoveText(at + new Vector(60, 40)); canvas.EndPreviewMove();
            var moved = Drawing.TextBounds(ComponentTexts.Ink(e.Project, report, new(e.Project), "detail")!); assert(!report.ReferenceDestinationInline && Math.Abs(moved.X - bounds.X - 60) < .001 && Math.Abs(moved.Y - bounds.Y - 40) < .001, "Destination décalée au détachement"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Détachement non annulable");
            var terminal = e.AddComponent("terminal", 600, 500); e.Change(() => terminal.Rotation = 90); bounds = Drawing.TextBounds(ComponentTexts.Ink(e.Project, terminal, new(e.Project), "port:1")!); at = bounds.TopLeft + new Vector(2, 2); var original = DocumentModel.Copy(terminal.Definition);
            assert(canvas.BeginTextMove(at), "Borne tournée inaccessible"); canvas.MoveText(at + new Vector(40, 20)); canvas.EndPreviewMove(); assert(terminal.TextOverrides.ContainsKey("port:1") && terminal.Definition == Symbols.All["terminal"] && original.Ports.SequenceEqual(terminal.Definition.Ports), "Bibliothèque modifiée");
        });
    }
    private static Editor Example()
    {
        var e = new Editor(); e.Change(() => { e.Project.Title = "Nouveau projet"; e.Sheet.Title = "Conducteurs"; e.Sheet.TitleBlockId = "technical"; });
        var source = e.AddComponent("folio_previous", 240, 320); var a = e.AddComponent("terminal", 600, 520); var b = e.AddComponent("terminal", 780, 420);
        e.Change(() => { source.ReferenceName = "RF2"; a.DescriptionPlacement.Visible = b.DescriptionPlacement.Visible = false; a.DetailPlacement.Visible = b.DetailPlacement.Visible = false; });
        e.Connect(new(source.Id, "1"), new(a.Id, "1"), [new(240, 240), new(600, 240)], "26");
        e.Connect(new(a.Id, "1"), new(b.Id, "1"), [new(600, 320), new(780, 320)]);
        e.Connect(new(a.Id, "2"), new(b.Id, "2"), [new(600, 620), new(780, 620)]); return e;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent) { yield return parent; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child; }
    private static void Render(Project p, Sheet s, string path, Rect area, double scale)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new(0, 0, area.Width * scale, area.Height * scale)); dc.PushTransform(new ScaleTransform(scale, scale)); dc.PushTransform(new TranslateTransform(-area.X, -area.Y)); Drawing.Render(dc, Drawing.Scene(p, s)); dc.Pop(); dc.Pop(); }
        var bitmap = new RenderTargetBitmap((int)(area.Width * scale), (int)(area.Height * scale), 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
