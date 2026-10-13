using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

internal static class WorkspaceUiTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Interface : migration des préférences, persistance et validation", () => {
            string path = Path.Combine(dir, "self-test-interface-preferences.json");
            File.WriteAllText(path, "{\"gridIntensity\":35,\"wireColor\":\"#123456\"}");
            var p = EditorPreferences.Load(path); assert(p.InterfaceTheme == "light" && p.InterfaceLayout == "compact" && p.GridIntensity == 35 && p.WireColor == "#123456", "Anciens réglages conservés, clair par défaut");
            p.InterfaceLayout = "ribbon"; p.InterfaceTheme = "dark"; p.Save(path); var loaded = EditorPreferences.Load(path); assert(loaded.InterfaceLayout == "ribbon" && loaded.InterfaceTheme == "dark", "Réglages non persistants");
            reject(() => { p.InterfaceLayout = "unknown"; p.Save(path); }); assert(EditorPreferences.Load(path).InterfaceLayout == "ribbon", "Préférence invalide a écrasé le fichier");
        });
        test("Interface : quatre combinaisons, commandes communes, panneaux et exports inchangés", () => WithWindow(window => {
            var e = window.Editor; Examples.Motor(e); window.Canvas.Select(e.Sheet.Components.Last(c => c.Symbol == "coil").Id);
            var initial = ProjectStore.Serialize(e.Project); string pdf = Path.Combine(dir, "self-test-interface.pdf"); Export.Pdf(e.Project, pdf); var bytes = File.ReadAllBytes(pdf);
            var compactWire = window.WorkspaceCommandForTest("wire"); var compactUndo = window.WorkspaceCommandForTest("undo");
            var heights = new Dictionary<string, double>();
            foreach (string layout in new[] { "compact", "ribbon" }) foreach (string theme in new[] { "light", "dark" })
            {
                window.SetInterface(layout, theme, false); var content = (FrameworkElement)window.Content; Arrange(content, 1440, 900); window.Canvas.Fit(); content.UpdateLayout();
                assert(window.Canvas.ActualWidth >= 650 && window.Canvas.ActualHeight >= 600, "Zone de dessin trop réduite");
                heights[layout] = window.Canvas.ActualHeight;
                assert(ReferenceEquals(compactWire, window.WorkspaceCommandForTest("wire")) && ReferenceEquals(compactUndo, window.WorkspaceCommandForTest("undo")), "Commandes dupliquées entre dispositions");
                var buttons = Descendants(content).OfType<Button>().Where(b => Equals(b.Tag, "command:wire")).ToList(); assert(buttons.Count > 0 && buttons.All(b => ReferenceEquals(b.Command, compactWire)), "Boutons non reliés à la commande commune");
                assert(buttons.All(b => b.IsEnabled), "Commande Conducteur désactivée");
                Save(content, window.Background, Path.Combine(dir, $"ui-{layout}-{theme}.png"), 1440, 900);
                Arrange(content, 1000, 600); assert(window.Canvas.ActualWidth >= 250 && window.Canvas.ActualHeight >= 300, "Disposition inutilisable à la taille minimale");
                Export.Pdf(e.Project, pdf); assert(bytes.SequenceEqual(File.ReadAllBytes(pdf)) && ProjectStore.Serialize(e.Project) == initial, "Le thème ou la disposition modifie le document/PDF");
                compactWire.Execute(null, window); assert(window.Canvas.Mode == "wire", "Commande non exécutée"); window.WorkspaceCommandForTest("select").Execute(null, window);
            }
            assert(heights["compact"] >= heights["ribbon"] + 50, "La disposition compacte conserve l’espace réservé au ruban");
            var panels = Descendants((FrameworkElement)window.Content).OfType<WorkspacePanels>().Single(); panels.SetState("pages", "auto"); panels.SetState("properties", "closed");
            string states = File.ReadAllText(WorkspacePanels.SettingsPath); window.SetInterface("compact", "light", false); window.SetInterface("ribbon", "dark", false); assert(states == File.ReadAllText(WorkspacePanels.SettingsPath), "Changer de disposition réinitialise les panneaux"); panels.Reset();
            reject(() => window.SetInterface("invalid", "light", false));
        }));
        test("Inspecteur : coordonnées, erreurs en ligne, curseur, connexions et historique", () => WithWindow(window => {
            var e = window.Editor; var a = e.AddComponent("coil", 300, 300); var b = e.AddComponent("coil", 500, 500); e.Connect(new(a.Id, "A2"), new(b.Id, "A1")); window.Canvas.Select(a.Id);
            var x = window.PropertyField("X"); x.Text = "325,5"; x.CaretIndex = 2; var beforeInvalid = ProjectStore.Serialize(e.Project);
            x.Text = "-"; assert(ProjectStore.Serialize(e.Project) == beforeInvalid && window.PropertyError("X").Length > 0 && ReferenceEquals(x, window.PropertyField("X")), "Saisie invalide appliquée ou champ reconstruit");
            x.Text = "340,25"; x.CaretIndex = 3; assert(window.PropertyError("X") == "" && x.ToolTip is string && e.Sheet.Components[0].X == 340.25 && x.CaretIndex == 3, "Correction, aide ou curseur incorrect");
            window.PropertyField("rotation").Text = "90"; assert(e.Sheet.Wires[0].Route(e.Sheet)[0] == e.Sheet.Components[0].PortPoint("A2"), "Le fil ne suit pas la rotation");
            var snapshot = ProjectStore.Serialize(e.Project); window.PropertyField("rotation").Text = "45"; assert(ProjectStore.Serialize(e.Project) == snapshot && window.PropertyError("rotation").Length > 0, "Rotation invalide acceptée");
            window.PropertyField("rotation").Text = "180"; e.Undo(); assert(e.Sheet.Components[0].Rotation == 0, "Rotation non regroupée"); e.Redo(); assert(e.Sheet.Components[0].Rotation == 180, "Rétablissement");
            window.Canvas.Selected.Add(b.Id); window.Canvas.SelectRectangle(new Rect(200, 200, 450, 450), false); assert(Descendants(window.PropertyPanel).OfType<TextBox>().Count() == 0, "Une sélection multiple modifie silencieusement le premier élément");
        }));
        test("Compatibilité : lecture et enregistrement d’une copie du projet utilisateur", () => {
            string source = @"C:\Users\pravi\Documents\LaserSieve_GH.gemelec";
            if (!File.Exists(source)) return;
            var bytes = File.ReadAllBytes(source); string copy = Path.Combine(dir, "self-test-user-copy.gemelec"); File.WriteAllBytes(copy, bytes);
            var p = ProjectStore.Load(copy); string before = ProjectStore.Serialize(p); ProjectStore.Save(p, copy); assert(ProjectStore.Serialize(ProjectStore.Load(copy)) == before, "Relecture de la copie différente");
            assert(bytes.SequenceEqual(File.ReadAllBytes(source)), "Document utilisateur modifié");
        });
        test("Propriétés documentaires : édition immédiate, validation et annulation sans enregistrer", () => WithWindow(window => {
            var e = window.Editor; string original = e.Project.Title;
            window.DocumentTestProbe = dialog => {
                var panel = (FrameworkElement)dialog.Content; Arrange(panel, 650, 800);
                var inputs = Descendants(panel).OfType<TextBox>().ToList(); var title = inputs.Single(t => t.Text == original); title.Text = "Projet test"; title.CaretIndex = 3;
                assert(e.Project.Title == "Projet test" && e.Dirty && e.FilePath is null && title.CaretIndex == 3, "Modification non immédiate ou enregistrement implicite");
                e.Undo(); assert(e.Project.Title == original && title.Text == original && !e.Dirty, "Historique ou synchronisation du dialogue"); e.Redo();
                var custom = inputs.Last(); custom.Text = "armoire=AR1"; string beforeInvalid = ProjectStore.Serialize(e.Project); custom.Text = "incomplet"; assert(ProjectStore.Serialize(e.Project) == beforeInvalid, "Champ personnalisé invalide appliqué"); custom.Text = "armoire=AR2"; assert(e.Project.CustomFields["armoire"] == "AR2", "Correction non appliquée");
                assert(!Descendants(panel).OfType<Button>().Any(b => b.Content as string is "Enregistrer" or "Appliquer"), "Bouton de validation résiduel");
                Save(panel, dialog.Background, Path.Combine(dir, "ui-live-project.png"), 650, 800);
            };
            window.TestDocumentDialog("project");
        }));
        test("Paramètres : choix par les contrôles et erreurs immédiates sous les champs", () => WithWindow(window => {
            window.SetInterface("compact", "light", false); var e = window.Editor; var a = e.AddComponent("coil", 300, 300); var b = e.AddComponent("coil", 600, 500); var wire = e.Connect(new(a.Id, "A2"), new(b.Id, "A1")); window.Canvas.Select(wire.Id);
            window.PropertyField("color").Text = "#12"; assert(window.PropertyError("color").Contains("six"), "Erreur de couleur absente");
            var content = (FrameworkElement)window.Content; Arrange(content, 1440, 900); window.Canvas.Fit(); content.UpdateLayout(); Save(content, window.Background, Path.Combine(dir, "ui-property-error.png"), 1440, 900);
            window.DocumentTestProbe = dialog => { var panel = (FrameworkElement)dialog.Content; Arrange(panel, 500, 350); Descendants(panel).OfType<ComboBox>().Single(c => c.Name == "InterfaceLayout").SelectedIndex = 1; Descendants(panel).OfType<ComboBox>().Single(c => c.Name == "InterfaceTheme").SelectedIndex = 1; assert(window.InterfaceLayout == "ribbon" && window.InterfaceTheme == "dark" && EditorPreferences.Load().InterfaceTheme == "dark", "Paramètres non appliqués/mémorisés"); Save(panel, dialog.Background, Path.Combine(dir, "ui-interface-settings.png"), 500, 350); };
            window.WorkspaceCommandForTest("settings").Execute(null, window);
        }));
        test("Charge : propriétés sur 100 folios et 5 000 composants avec conducteurs et textes", () => WithWindow(window => {
            var e = window.Editor; var p = new Project { Title = "Charge 100 / 5000" }; p.Sheets.Clear();
            for (int page = 0; page < 100; page++) {
                var s = new Sheet { Title = "Folio " + (page + 1) }; p.Sheets.Add(s);
                for (int i = 0; i < 50; i++) { var c = new Component { Symbol = "coil", Tag = $"K{page * 50 + i + 1}", X = 100 + i % 10 * 120, Y = 100 + i / 10 * 160 }; s.Components.Add(c); if (i > 0) s.Wires.Add(new Wire { From = new(s.Components[i - 1].Id, "A2"), To = new(c.Id, "A1"), Number = $"W{page * 50 + i}" }); }
                s.Notes.Add(new() { X = 100, Y = 50, Text = "Commande 24 V DC · exemple synthétique" });
            }
            var timer = Stopwatch.StartNew(); string path = Path.Combine(dir, "self-test-load-100.gemelec"); ProjectStore.Save(p, path); timer.Stop(); double saveMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); e.Open(path); timer.Stop(); double openMs = timer.Elapsed.TotalMilliseconds; window.Canvas.Select(e.Sheet.Components[0].Id);
            var field = window.PropertyField("description"); var edits = new List<double>(); for (int i = 0; i < 5; i++) { timer.Restart(); field.Text = "Commande " + i; timer.Stop(); edits.Add(timer.Elapsed.TotalMilliseconds); }
            timer.Restart(); var scene = Drawing.Scene(e.Project, e.Sheet); timer.Stop(); double sceneMs = timer.Elapsed.TotalMilliseconds;
            File.WriteAllText(Path.Combine(dir, "interface-performance.json"), JsonSerializer.Serialize(new { folios = 100, components = 5000, conductors = 4900, annotations = 100, saveMs, openMs, propertyEditMs = edits, sceneMs, privateMemoryBytes = Process.GetCurrentProcess().PrivateMemorySize64 }, ProjectStore.Json));
            assert(scene.Count > 100 && ReferenceEquals(field, window.PropertyField("description")), "Scène ou champ perdu pendant le test de charge");
        }));
    }

    private static void WithWindow(Action<MainWindow> action)
    {
        string? old = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1");
        var app = Application.Current ?? new Application(); var window = new MainWindow();
        try { action(window); } finally { window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", old); }
    }
    private static void Arrange(FrameworkElement content, int w, int h) { System.Windows.Input.CommandManager.InvalidateRequerySuggested(); content.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); content.InvalidateMeasure(); content.InvalidateArrange(); content.Measure(new(w, h)); content.Arrange(new(0, 0, w, h)); content.UpdateLayout(); }
    private static void Save(Visual visual, Brush background, string path, int w, int h)
    {
        if (visual is FrameworkElement content) content.UpdateLayout();
        var image = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); var fill = new DrawingVisual(); using (var dc = fill.RenderOpen()) dc.DrawRectangle(background, null, new(0, 0, w, h)); image.Render(fill); image.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(path); encoder.Save(file);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent) { yield return parent; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var c in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return c; }
}
