using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

public static class LiveTextTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Tous les textes : sélection directe des bornes, contenu et ancrage", () =>
        {
            var e = new Editor(); var c = e.AddComponent("breaker1", 400, 400); c.Rating = "63 A"; var before = ProjectStore.Serialize(e.Project); var preview = new TextPlacementPreview(e.Project, c);
            var port = ComponentTexts.Ink(e.Project, preview.Draft, new(e.Project), "port:1")!; var point = Drawing.TextBounds(port).TopLeft + new Vector(2, 2);
            assert(preview.BeginDrag(point) && preview.TextKey == "port:1", "Cliquer la borne sélectionne son texte"); preview.MoveDrag(point + new Vector(10.5, 4.25)); preview.EndDrag(); preview.SetContent("L1"); assert(preview.Draft.PortLabels["1"] == "L1", "Libellé modifié individuellement");
            var old = Drawing.TextBounds(ComponentTexts.Ink(e.Project, preview.Draft, new(e.Project), "port:1")!).TopLeft; var oldPort = preview.Draft.PortPoint("1"); preview.Draft.Rotation = 90; var next = Drawing.TextBounds(ComponentTexts.Ink(e.Project, preview.Draft, new(e.Project), "port:1")!).TopLeft; var nextPort = preview.Draft.PortPoint("1"); assert((next - old - (nextPort - oldPort)).Length < .001, "Texte déplacé suit sa borne après rotation");
            assert(ProjectStore.Serialize(e.Project) == before, "Le brouillon ne modifie pas le projet");
        });
        test("Textes ajoutés et alignement global : styles, suppression et relecture", () =>
        {
            var e = new Editor(); var c = e.AddComponent("breaker1", 400, 400); c.Rating = "63 A"; var preview = new TextPlacementPreview(e.Project, c); preview.AddText(); preview.SetContent("Protection moteur"); preview.Placement.Color = "#123456"; preview.Placement.FontSize = 18; preview.Placement.Rotation = 15; var addedKey = preview.TextKey;
            var result = preview.AlignAll("right"); var texts = preview.Scene().Where(i => i.Kind == "text").ToList(); var boxes = texts.Select(Drawing.TextBounds).ToList();
            assert(result.Changed && result.Clear && texts.Any(i => i.Text == "Protection moteur" && i.Color == "#123456" && i.Rotation == 15), "Styles conservés dans l’alignement global");
            assert(boxes.All(b => b.Left > c.Bounds().Right) && boxes.All(b => Math.Abs(b.Left - boxes[0].Left) < .001), "Colonne alignée à droite du symbole");
            for (int i = 0; i < boxes.Count; i++) for (int j = i + 1; j < boxes.Count; j++) assert(!boxes[i].IntersectsWith(boxes[j]), "Textes non superposés");
            e.Change(() => { c.AdditionalTexts = DocumentModel.Copy(preview.Draft.AdditionalTexts); c.TextOverrides = DocumentModel.Copy(preview.Draft.TextOverrides); c.TagPlacement = preview.Draft.TagPlacement; c.DetailPlacement = preview.Draft.DetailPlacement; }); var saved = ProjectStore.Serialize(e.Project); var path = Path.Combine(dir, "self-test-all-texts.gemelec"); e.Save(path); assert(ProjectStore.Serialize(ProjectStore.Load(path)) == saved, "Textes et placements persistants");
            var beforeDelete = ProjectStore.Serialize(e.Project); e.Change(() => c.AdditionalTexts.Clear()); e.Undo(); assert(ProjectStore.Serialize(e.Project) == beforeDelete, "Suppression annulable");
            preview.Target = preview.Items.FindIndex(t => t.Key == addedKey); preview.RemoveText(); assert(preview.Draft.AdditionalTexts.Count == 0, "Suppression depuis l’aperçu");
            Export.Svg(e.Project, e.Sheet, Path.Combine(dir, "self-test-all-texts.svg")); assert(File.ReadAllText(Path.Combine(dir, "self-test-all-texts.svg")).Contains("Protection moteur"), "Texte ajouté exporté");
        });
        test("Propriétés immédiates : repères, reports liés, cases et annulation groupée", () => WithWindow(window =>
        {
            var e = window.Editor; var first = e.Sheet; var c = e.AddComponent("coil", 300, 300); window.Canvas.Select(c.Id); var initial = ProjectStore.Serialize(e.Project); var field = window.PropertyField("tag"); field.Text = "M"; field.Text = "MO"; field.Text = "MOTEUR";
            assert(c.Tag == "MOTEUR" && ReferenceEquals(field, window.PropertyField("tag")), "Modèle immédiat sans reconstruire le champ ni perdre le curseur"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == initial, "Une annulation pour une séquence de saisie"); e.Redo();
            var a = e.AddComponent("folio_next", 500, 300); e.AddSheet("Suite"); var b = e.AddComponent("folio_previous", 300, 300); e.LinkReferencePair(a.Id, b.Id, "24V"); window.Canvas.Select(b.Id);
            window.PropertyField("referenceName").Text = "Commande moteur"; assert(ReferenceDisplay.Name(a) == "Commande moteur" && ReferenceDisplay.Name(b) == "Commande moteur", "Nom propagé sans Appliquer");
            window.PropertyPanel.Measure(new(290, 1200)); window.PropertyPanel.Arrange(new(0, 0, 290, 1200)); window.PropertyPanel.UpdateLayout();
            var checkbox = Descendants(window.PropertyPanel).OfType<CheckBox>().Single(x => x.Content as string == "Nom indépendant"); checkbox.IsChecked = true; checkbox.RaiseEvent(new(ButtonBase.ClickEvent)); window.PropertyField("referenceName").Text = "Nom local"; assert(ReferenceDisplay.Name(a) == "Commande moteur" && ReferenceDisplay.Name(b) == "Nom local", "Case appliquée immédiatement");
            checkbox.IsChecked = false; checkbox.RaiseEvent(new(ButtonBase.ClickEvent)); assert(ReferenceDisplay.Name(b) == "Commande moteur" && window.PropertyField("referenceName").Text == "Commande moteur", "Décocher rétablit immédiatement le nom commun dans le champ");
            window.PropertyField("linkOrder").Text = "invalide"; assert(b.LinkOrder == 0, "Ordre incomplet refusé"); window.PropertyField("linkOrder").Text = "7"; assert(e.Sheet.Components.Single(x => x.Id == b.Id).LinkOrder == 7, "Correction acceptée");
            assert(!Descendants(window.PropertyPanel).OfType<Button>().Any(x => (x.Content as string)?.StartsWith("Appliquer") == true), "Aucun bouton Appliquer dans les propriétés");
        }));
        test("Propriétés immédiates : conducteur, coordonnées et annotation", () => WithWindow(window =>
        {
            var e = window.Editor; var a = e.AddComponent("coil", 200, 200); var b = e.AddComponent("coil", 400, 400); var w = e.Connect(new(a.Id, "A2"), new(b.Id, "A1")); window.Canvas.Select(w.Id);
            window.PropertyField("number").Text = "W99"; window.PropertyField("color").Text = "#112233"; window.PropertyField("waypoints").Text = "300 ; 300"; assert(w.Number == "W99" && w.Color == "#112233" && w.Waypoints.Count == 1, "Conducteur actualisé");
            window.PropertyField("color").Text = "#11"; assert(w.Color == "#112233", "Couleur partielle non appliquée"); window.PropertyField("color").Text = "#445566"; assert(w.Color == "#445566", "Correction couleur sans perdre le champ");
            var note = new Annotation { Text = "Avant", X = 300, Y = 500 }; e.Change(() => e.Sheet.Notes.Add(note)); window.Canvas.Select(note.Id); window.PropertyField("text").Text = "Après"; assert(note.Text == "Après", "Annotation actualisée");
        }));
        test("Fenêtre texte : ajout, édition et alignement groupé par les contrôles", () => WithWindow(window =>
        {
            var e = window.Editor; var c = e.AddComponent("breaker1", 400, 400); c.Rating = "63 A";
            window.DocumentTestProbe = dialog =>
            {
                var content = (FrameworkElement)dialog.Content; content.Measure(new(1010, 680)); content.Arrange(new(0, 0, 1010, 680)); content.UpdateLayout();
                Descendants(content).OfType<Button>().Single(b => b.Content as string == "Ajouter du texte").RaiseEvent(new(ButtonBase.ClickEvent));
                Descendants(content).OfType<TextBox>().Single(b => b.Name == "ComponentTextContent").Text = "Départ moteur";
                Descendants(content).OfType<CheckBox>().Single(b => b.Name == "AllComponentTexts").IsChecked = true;
                Descendants(content).OfType<Button>().Single(b => b.Name == "SmartText_right").RaiseEvent(new(ButtonBase.ClickEvent));
                var preview = Descendants(content).OfType<TextPlacementPreview>().Single(); assert(preview.Scene().Any(i => i.Text == "Départ moteur"), "Texte ajouté dans l’aperçu");
                content.UpdateLayout(); var image = new RenderTargetBitmap(1010, 680, 96, 96, PixelFormats.Pbgra32); image.Render(content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using (var file = File.Create(Path.Combine(dir, "self-test-all-texts-dialog.png"))) encoder.Save(file);
                Descendants(content).OfType<Button>().Single(b => b.Content as string == "Fermer").RaiseEvent(new(ButtonBase.ClickEvent));
            };
            window.TextPositionDialog(c.Id); assert(c.AdditionalTexts.Single().Text == "Départ moteur", "Ajout validé dans le composant"); e.Undo(); assert(e.Sheet.Components.Single(x => x.Id == c.Id).AdditionalTexts.Count == 0, "Ajout et alignement annulés ensemble");
        }));
        test("Fluidité des propriétés immédiates : saisie dans un projet de 1000 composants", () => WithWindow(window =>
        {
            var e = window.Editor; e.Change(() => { for (int i = 0; i < 1000; i++) e.Sheet.Components.Add(new() { Symbol = "coil", X = 100 + i % 20 * 50, Y = 100 + i / 20 * 14 }); }); var c = e.Sheet.Components[0]; window.Canvas.Select(c.Id); var field = window.PropertyField("tag"); var timer = Stopwatch.StartNew(); for (int i = 0; i < 10; i++) field.Text = "Texte " + i; timer.Stop();
            File.WriteAllText(Path.Combine(dir, "self-test-live-properties-performance.json"), JsonSerializer.Serialize(new { components = 1000, edits = 10, elapsedMs = timer.Elapsed.TotalMilliseconds })); assert(timer.Elapsed.TotalSeconds < 5 && ReferenceEquals(field, window.PropertyField("tag")), "Propriétés trop lentes ou champ reconstruit");
        }));
    }
    private static void WithWindow(Action<MainWindow> action)
    {
        string? old = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1"); var app = Application.Current ?? new Application(); var window = new MainWindow();
        try { action(window); } finally { window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", old); }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent) { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
}
