using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GeMMeElec;

public static class InteractionTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Reports : paire explicite, continuité et annulation", () =>
        {
            var e = new Editor(); var a = e.AddComponent("folio_next", 200, 200); e.AddSheet("Retour"); var b = e.AddComponent("folio_previous", 200, 200);
            var saved = ProjectStore.Serialize(e.Project); e.LinkReferencePair(a.Id, b.Id, "  DI1.4  ");
            assert(new CrossReferences(e.Project).Targets(a.Id).Single().Component.Id == b.Id, "Paire réciproque");
            assert(CrossReferences.Trace(e.Project, new(a.Id, "1")).Contains(new Endpoint(b.Id, "1")), "Continuité électrique");
            e.Undo(); assert(ProjectStore.Serialize(e.Project) == saved, "Annulation atomique"); e.Redo();
            var c = e.AddComponent("folio_next", 400, 200); reject(() => e.LinkReferencePair(c.Id, b.Id, "DI1.4")); reject(() => e.LinkReferencePair(a.Id, c.Id, "autre"));
            var reloaded = ProjectStore.Deserialize(ProjectStore.Serialize(e.Project)); assert(new CrossReferences(reloaded).Targets(b.Id).Single().Component.Id == a.Id, "Sauvegarde des liens");
            var next = Symbols.All["folio_next"]; var previous = Symbols.All["folio_previous"];
            assert(next.Ports[0].X == -40 && previous.Ports[0].X == 40 && next.Shapes.Skip(1).SequenceEqual(previous.Shapes.Skip(1)) && next.Shapes[0] != previous.Shapes[0], "Triangles orientés à droite, raccordements opposés et IDs stables");
        });
        test("Sélection rectangle : composants, texte, fils et ajout Ctrl", () =>
        {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var b = e.AddComponent("coil", 200, 400); var c = e.AddComponent("coil", 600, 200); var wire = e.Connect(new(a.Id, "A2"), new(b.Id, "A1")); e.Change(() => e.Sheet.Notes.Add(new() { X = 250, Y = 250, Text = "MOTEUR" }));
            var canvas = new SheetCanvas(e); var saved = ProjectStore.Serialize(e.Project); var wasDirty = e.Dirty; canvas.SelectRectangle(new(100, 100, 300, 400), false);
            assert(canvas.Selected.SetEquals(new[] { a.Id, b.Id, wire.Id, e.Sheet.Notes[0].Id }), "Objets entièrement encadrés");
            canvas.SelectRectangle(new(540, 100, 120, 200), true); assert(canvas.Selected.Count == 5, "Ajout à la sélection");
            canvas.ContextAt(new(200, 200)); assert(canvas.Selected.Count == 5, "Clic droit préserve un groupe sélectionné");
            canvas.SelectRectangle(new(0, 0, 10, 10), false); assert(canvas.Selected.Count == 0 && ProjectStore.Serialize(e.Project) == saved && e.Dirty == wasDirty, "Sélection sans mutation");
        });
        test("Atelier : gestes, poignées, bornes, annulation et sauvegarde", () =>
        {
            var original = new SymbolDefinition("draft", "Module", "X", 100, 100, [], []); var draft = new SymbolDraft(original); var canvas = new SymbolCanvas(draft);
            canvas.SetTool("rect"); canvas.Begin(new(-22, -19)); canvas.MoveTo(new(22, 21)); canvas.End(); var rect = draft.Definition.Shapes.Single();
            assert(rect == new Shape("rect", -20, -20, 40, 40), "Rectangle et grille");
            canvas.SetTool("select"); canvas.Begin(new(20, 20)); canvas.MoveTo(new(30, 40)); canvas.End(); assert(draft.Definition.Shapes[0].A == 50 && draft.Definition.Shapes[0].B == 60, "Poignée redimensionne");
            canvas.Begin(new(0, 0)); canvas.MoveTo(new(10, 5)); canvas.End(); assert(draft.Definition.Shapes[0].X == -10, "Déplacement du corps");
            canvas.SetTool("port"); canvas.Begin(new(0, -50)); canvas.End(); canvas.Begin(new(0, 50)); canvas.End(); assert(draft.Definition.Ports.Select(p => p.Id).SequenceEqual(new[] { "1", "2" }), "Bornes uniques");
            canvas.SetTool("line"); canvas.Begin(new(0, -50)); canvas.MoveTo(new(0, -15)); canvas.End(); var count = draft.Definition.Shapes.Count;
            canvas.Begin(new(80, 80)); canvas.MoveTo(new(100, 100)); canvas.Cancel(); assert(draft.Definition.Shapes.Count == count, "Échap annule le geste");
            canvas.Begin(new(80, 80)); canvas.End(); assert(draft.Definition.Shapes.Count == count, "Pas de ligne invisible");
            draft.Undo(); assert(draft.Definition.Shapes.Count == count - 1, "Annulation du tracé"); draft.Redo();
            reject(() => draft.Change(d => d with { Ports = [new("1", 0, 0), new("1", 1, 1)] })); assert(draft.Definition.Ports.Count == 2 && draft.Definition.Ports[1].Id == "2", "Rollback borne dupliquée");
            var e = new Editor(); var copy = e.SaveSymbolCopy(draft.Definition); e.AddComponent(copy.Id, 200, 200); var path = Path.Combine(dir, "interactive-symbol.gemelec"); e.Save(path); var loaded = ProjectStore.Load(path); assert(loaded.CustomSymbols[copy.Id].Shapes.SequenceEqual(draft.Definition.Shapes) && original.Shapes.Count == 0, "Sauvegarde et original intact");
        });
        test("Déplacement groupé : conservation des coudes et annulation", () =>
        {
            var e = new Editor(); var a = e.AddComponent("coil", 200, 200); var b = e.AddComponent("coil", 400, 500); var w = e.Connect(new(a.Id, "A2"), new(b.Id, "A1"), [new(200, 300), new(400, 300)]);
            var canvas = new SheetCanvas(e); canvas.SelectRectangle(new(100, 100, 500, 500), false); var saved = ProjectStore.Serialize(e.Project); var route = w.Route(e.Sheet);
            canvas.BeginSelectionMove(new(200, 200)); canvas.MoveSelection(new(250, 230));
            assert(w.Route(e.Sheet).SequenceEqual(route.Select(p => p + new Vector(50, 30))), "Les coudes suivent le groupe");
            canvas.CancelDrag(); assert(ProjectStore.Serialize(e.Project) == saved && !e.InteractionInProgress, "Échap restaure tout le groupe");
        });
        test("Interface : liaison guidée, navigation et menus sans fenêtre visible", () =>
        {
            string? old = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1"); var app = Application.Current ?? new Application(); var window = new MainWindow();
            try
            {
                var e = window.Editor; var first = e.Sheet; var a = e.AddComponent("folio_next", 200, 200); e.AddSheet("Commande"); var b = e.AddComponent("folio_previous", 400, 300); e.SelectSheet(first.Id);
                window.DocumentTestProbe = dialog => { var content = (FrameworkElement)dialog.Content; content.Measure(new(700, 500)); content.Arrange(new(0, 0, 700, 500)); content.UpdateLayout(); var list = Descendants(content).OfType<ListBox>().Single(); list.SelectedIndex = 0; Descendants(content).OfType<Button>().Single(b => (string)b.Content == "Lier les reports").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
                window.LinkReferenceDialog(a.Id); window.DocumentTestProbe = null;
                foreach (var mode in new[] { "place", "wire", "select", "text" }) { e.SelectSheet(first.Id); window.Canvas.SetMode(mode); assert(window.Canvas.ActivateReference(new(a.X, a.Y)) && e.Sheet.Components.Any(c => c.Id == b.Id) && window.Canvas.Selected.Contains(b.Id), "Navigation dans le mode " + mode); }
                assert(window.SchematicMenu().Items.OfType<MenuItem>().Any(i => (string)i.Header == "Aller au report lié"), "Menu report");
                var c = e.AddComponent("coil", 600, 300); window.Canvas.Select(c.Id); var menu = window.SchematicMenu(); assert(menu.Items.OfType<MenuItem>().Any(i => (string)i.Header == "Modifier le symbole en copie…"), "Menu symbole");
                menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Rotation 90°    R").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); assert(c.Rotation == 90, "Commande du menu active"); e.Undo(); assert(e.Sheet.Components.Single(x => x.Id == c.Id).Rotation == 0, "Commande annulable");
                var other = e.AddComponent("coil", 800, 500); var wire = e.Connect(new(c.Id, "A2"), new(other.Id, "A1"), [new(600, 400), new(800, 400)]); e.Change(() => e.Sheet.Notes.Add(new() { X = 600, Y = 600, Text = "Groupe" }));
                var snapshot = ProjectStore.Serialize(e.Project); window.Canvas.Selected.Clear(); foreach (var id in new[] { c.Id, other.Id, wire.Id, e.Sheet.Notes[0].Id }) window.Canvas.Selected.Add(id);
                window.SchematicMenu().Items.OfType<MenuItem>().Single(i => (string)i.Header == "Dupliquer    Ctrl+D").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                assert(e.Sheet.Notes.Count == 2 && e.Sheet.Wires.Count == 2 && e.Sheet.Wires[1].From.ComponentId != c.Id && e.Sheet.Wires[1].To.ComponentId != other.Id && e.Sheet.Wires[1].Waypoints[0] == new Position(640, 440), "Copie du groupe avec texte et liaisons internes"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == snapshot, "Annulation de la copie groupée");
            }
            finally { window.DocumentTestProbe = null; window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", old); }
        });
        test("Fluidité : sélection de 1 000 composants et survol de l’atelier", () =>
        {
            var e = new Editor(); e.Change(() => { for (int i = 0; i < 1000; i++) e.Sheet.Components.Add(new() { Symbol = "coil", X = 100 + i % 25 * 45, Y = 100 + i / 25 * 18 }); });
            var canvas = new SheetCanvas(e); var watch = Stopwatch.StartNew(); canvas.SelectRectangle(new(0, 0, 1400, 990), false); watch.Stop(); assert(canvas.Selected.Count == 1000, "Sélection intégrale");
            var selectionMs = watch.Elapsed.TotalMilliseconds; var draft = new SymbolDraft(new("draft", "Dense", "X", 1000, 1000, [], Enumerable.Range(0, 2000).Select(i => new Shape("line", i % 40 * 20, i / 40 * 10, i % 40 * 20 + 10, i / 40 * 10)).ToList())); var editor = new SymbolCanvas(draft); var state = draft.Definition;
            watch.Restart(); for (int i = 0; i < 10000; i++) editor.MoveTo(new(i % 500, 0)); watch.Stop(); assert(ReferenceEquals(state, draft.Definition), "Survol sans reconstruction du modèle"); File.WriteAllText(Path.Combine(dir, "interaction-performance.json"), JsonSerializer.Serialize(new { components = 1000, selectionMs, shapes = 2000, hoverSamples = 10000, hoverMs = watch.Elapsed.TotalMilliseconds }, ProjectStore.Json));
        });
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject p) { yield return p; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(p); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(p, i))) yield return child; }
}
