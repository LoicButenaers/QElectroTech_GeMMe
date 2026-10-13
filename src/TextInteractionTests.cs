using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace GeMMeElec;

public static class TextInteractionTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Nom libre du report : connexion, persistance et compatibilité", () =>
        {
            var e = new Editor(); var a = e.AddComponent("folio_next", 200, 200); e.AddSheet("Suite"); var b = e.AddComponent("folio_previous", 200, 200); e.LinkReferencePair(a.Id, b.Id, "24V");
            var before = ProjectStore.Serialize(e.Project); e.ConfigureReference(b.Id, b.LinkName, b.LinkMode, b.LinkOrder, b.LinkSource, "Alimentation automate / été");
            assert(ReferenceDisplay.Name(b) == "Alimentation automate / été" && b.LinkName == "24V", "Nom indépendant");
            assert(CrossReferences.Trace(e.Project, new(a.Id, "1")).Contains(new(b.Id, "1")), "Continuité préservée");
            assert(ReferenceDisplay.Scene(e.Project, b, new(e.Project)).Any(i => i.Text == b.ReferenceName), "Nom dans la scène");
            var path = Path.Combine(dir, "self-test-reference-name.gemelec"); e.Save(path); var restored = ProjectStore.Load(path); assert(ReferenceDisplay.Name(restored.Sheets[1].Components[0]) == b.ReferenceName, "Nom relu");
            e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Undo atomique"); e.Redo();
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(before)!;
            foreach (var sheet in legacy["sheets"]!.AsArray()) foreach (var c in sheet!["components"]!.AsArray()) { c!.AsObject().Remove("referenceName"); c.AsObject().Remove("referenceDestinationInline"); }
            var old = ProjectStore.Deserialize(legacy.ToJsonString()); assert(ReferenceDisplay.Name(old.Sheets[0].Components[0]) == "24V" && old.Sheets[0].Components[0].ReferenceDestinationInline, "Ancien affichage conservé");
        });
        test("Câbles : texte parallèle, centré et indépendant du sens de parcours", () =>
        {
            var w = new Wire { Number = "CABLE_VERTICAL" };
            foreach (var path in new List<Point>[] { [new(200, 100), new(200, 600)], [new(200, 600), new(200, 100)] })
            {
                var ink = Drawing.WireLabel(w, path)!; assert(ink.Rotation == -90 && ink.X < 200, "Texte vertical à gauche du conducteur");
                assert(Math.Abs(ink.Y - Drawing.Measure(w.Number, 10).Width / 2 - 350) < .001, "Centrage sur le segment");
            }
            var h = Drawing.WireLabel(w, [new(500, 100), new(100, 100)])!; assert(h.Rotation == 0 && h.Y < 100, "Texte horizontal au-dessus");
            assert(Drawing.WireLabel(w, [new(1, 1), new(1, 1)]) is null && Drawing.WireLabel(w, []) is null, "Routes dégénérées");
            assert(Drawing.WireLabel(w, [new(0, 0), new(50, 0), new(50, 200)])!.Rotation == -90, "Plus long segment");
            var e = new Editor(); var a = e.AddComponent("coil", 300, 200); var b = e.AddComponent("coil", 300, 600); e.Connect(new(a.Id, "A2"), new(b.Id, "A1"), number: w.Number);
            string svg = Path.Combine(dir, "self-test-wire-orientation.svg"); Export.Svg(e.Project, e.Sheet, svg); var label = XDocument.Load(svg).Descendants().Single(x => x.Name.LocalName == "text" && x.Value == w.Number); assert(label.Attribute("transform")!.Value.StartsWith("rotate(-90 "), "SVG orienté");
            Export.Pdf(e.Project, Path.Combine(dir, "self-test-wire-orientation.pdf"));
            var horizontalA = e.AddComponent("folio_next", 550, 300); var horizontalB = e.AddComponent("folio_previous", 850, 300); e.Connect(new(horizontalA.Id, "1"), new(horizontalB.Id, "1"), number: "CABLE_HORIZONTAL");
            SaveScene(Drawing.Scene(e.Project, e.Sheet), Path.Combine(dir, "self-test-wire-orientation.png"), 1100, 800);
        });
        test("Aperçu texte : glissement précis, annulation et destination indépendante", () =>
        {
            var e = new Editor(); var c = e.AddComponent("coil", 200, 200); var preview = new TextPlacementPreview(e.Project, c); var before = ProjectStore.Serialize(e.Project); var box = preview.TextBounds(); var point = new Point(box.X + 2, box.Y + 2);
            assert(preview.BeginDrag(point), "Début sur le texte"); preview.MoveDrag(point + new Vector(13.25, -7.75)); preview.EndDrag(); assert(preview.Draft.TagPlacement.OffsetX == c.TagPlacement.OffsetX + 13.25 && preview.Draft.TagPlacement.OffsetY == c.TagPlacement.OffsetY - 7.75, "Déplacement sans arrondi");
            box = preview.TextBounds(); point = new(box.X + 2, box.Y + 2); var offset = preview.Placement.OffsetX; preview.BeginDrag(point); preview.MoveDrag(point + new Vector(50, 50)); preview.EndDrag(true); assert(preview.Placement.OffsetX == offset, "Échap restaure"); assert(ProjectStore.Serialize(e.Project) == before, "Projet non modifié par l’aperçu");
            var r = e.AddComponent("folio_next", 400, 300); var refPreview = new TextPlacementPreview(e.Project, r) { Target = 1 }; box = refPreview.TextBounds(); point = new(box.X + 2, box.Y + 2); refPreview.BeginDrag(point); refPreview.MoveDrag(point + new Vector(-20, 60)); refPreview.EndDrag(); assert(!refPreview.Draft.ReferenceDestinationInline, "Destination détachée"); var after = refPreview.TextBounds(); assert(Math.Abs(after.X - box.X + 20) < .001 && Math.Abs(after.Y - box.Y - 60) < .001, "Destination suit le pointeur sans saut");
        });
        test("Dialogue texte : aperçu immédiat, icônes, validation unique et abandon", () =>
        {
            string? old = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1"); var app = Application.Current ?? new Application(); var window = new MainWindow();
            try
            {
                var e = window.Editor; var c = e.AddComponent("coil", 300, 300); c.Rating = "24 V DC"; c.Description = "Bobine de commande"; var before = ProjectStore.Serialize(e.Project);
                window.DocumentTestProbe = dialog =>
                {
                    var content = (FrameworkElement)dialog.Content; content.Measure(new(1010, 680)); content.Arrange(new(0, 0, 1010, 680)); content.UpdateLayout();
                    var preview = Descendants(content).OfType<TextPlacementPreview>().Single(); preview.Fit();
                    var buttons = Descendants(content).OfType<ToggleButton>().ToList(); buttons.Single(b => (string?)b.Tag == "side:top").RaiseEvent(new(ButtonBase.ClickEvent)); assert(preview.Draft.TagPlacement.Side == "top", "Flèche haut active dans l’aperçu");
                    var x = Descendants(content).OfType<TextBox>().Single(t => t.Name == "TextOffsetX"); x.Text = "12,75"; assert(preview.Placement.OffsetX == 12.75, "Champ précis mis à jour immédiatement");
                    var alignment = buttons.Single(b => (string?)b.Tag == "horizontal:right"); assert(alignment.Content is Image && !string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(alignment)), "Icône accessible"); alignment.RaiseEvent(new(ButtonBase.ClickEvent));
                    var target = Descendants(content).OfType<ComboBox>().Single(); target.SelectedIndex = 1; buttons.Single(b => (string?)b.Tag == "side:bottom").RaiseEvent(new(ButtonBase.ClickEvent)); assert(preview.Draft.TagPlacement.OffsetX == 12.75, "Changer de texte conserve le brouillon");
                    assert(ProjectStore.Serialize(e.Project) == before, "Pas de mutation avant validation");
                    content.UpdateLayout(); var image = new RenderTargetBitmap(1010, 680, 96, 96, PixelFormats.Pbgra32); image.Render(content); SaveImage(image, Path.Combine(dir, "self-test-text-dialog.png"));
                    Descendants(content).OfType<Button>().Single(b => b.Content as string == "Valider").RaiseEvent(new(ButtonBase.ClickEvent));
                };
                window.TextPositionDialog(c.Id); assert(e.Sheet.Components[0].TagPlacement.OffsetX == 12.75 && e.Sheet.Components[0].DetailPlacement.Side == "bottom", "Tous les textes validés"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Une seule annulation restaure les deux textes"); e.Redo(); var committed = ProjectStore.Serialize(e.Project);
                window.DocumentTestProbe = dialog => { var content = (FrameworkElement)dialog.Content; content.Measure(new(1010, 680)); content.Arrange(new(0, 0, 1010, 680)); content.UpdateLayout(); Descendants(content).OfType<ToggleButton>().Single(b => (string?)b.Tag == "side:left").RaiseEvent(new(ButtonBase.ClickEvent)); Descendants(content).OfType<Button>().Single(b => b.Content as string == "Annuler").RaiseEvent(new(ButtonBase.ClickEvent)); };
                window.TextPositionDialog(c.Id); assert(ProjectStore.Serialize(e.Project) == committed, "Annuler laisse le projet intact");
            }
            finally { window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", old); }
        });
        test("Ajustement intelligent : symbole, voisins, limites et stabilité", () =>
        {
            var e = new Editor(); var c = e.AddComponent("coil", 300, 300); c.Rating = c.Reference = c.Range = ""; c.ShowPortLabels = false;
            var obstacle = e.AddComponent("coil", 350, 300); obstacle.ShowPortLabels = false;
            var preview = new TextPlacementPreview(e.Project, c); var before = ProjectStore.Serialize(e.Project);
            var result = preview.SmartAdjust(); var box = preview.TextBounds();
            assert(result.Changed && result.Clear && preview.Placement.Side == "left", "Choisir le côté libre");
            assert(preview.Placement.Horizontal == "right" && preview.Placement.Vertical == "center" && Math.Abs(box.Y + box.Height / 2 - c.Y) < .001, "Alignement sur le centre du symbole");
            assert(!box.IntersectsWith(c.Bounds()) && !box.IntersectsWith(obstacle.Bounds()), "Éviter les symboles");
            var placement = JsonSerializer.Serialize(preview.Placement); preview.SmartAdjust(); assert(JsonSerializer.Serialize(preview.Placement) == placement, "Résultat déterministe sans dérive");
            assert(ProjectStore.Serialize(e.Project) == before, "Projet intact avant validation");
            c.X = e.Sheet.Width - 45; c.Rotation = 90; var border = new TextPlacementPreview(e.Project, c); result = border.SmartAdjust(); box = border.TextBounds(); assert(result.Clear && box.Right <= e.Sheet.Width - 30 && !box.IntersectsWith(c.Bounds()), "Rotation et bord du folio pris en compte");
            border.Draft.DescriptionPlacement.Visible = false; border.Target = 2; assert(!border.SmartAdjust().Changed && !border.Draft.DescriptionPlacement.Visible, "Ne révèle pas les textes masqués");
        });
        test("Ajustement intelligent : fils, reports et espace insuffisant", () =>
        {
            var e = new Editor(); var c = e.AddComponent("coil", 300, 300); c.Rating = c.Reference = c.Range = ""; c.ShowPortLabels = false;
            var a = e.AddComponent("coil", 350, 100); var b = e.AddComponent("coil", 350, 600); e.Connect(new(a.Id, "A2"), new(b.Id, "A1"));
            var preview = new TextPlacementPreview(e.Project, c); assert(preview.SmartAdjust().Clear && preview.Placement.Side == "left", "Évite le fil vertical à droite");
            var r = e.AddComponent("folio_next", 700, 300); e.AddSheet("Suite"); var t = e.AddComponent("folio_previous", 400, 300); e.LinkReferencePair(r.Id, t.Id, "Alimentation");
            var report = new TextPlacementPreview(e.Project, r); var result = report.SmartAdjust(); assert(result.Clear && report.Draft.ReferenceDestinationInline, "Nom et destination alignés ensemble");
            report.Target = 1; result = report.SmartAdjust(); assert(result.Clear && !report.Draft.ReferenceDestinationInline, "Destination ajustable séparément");
            assert(CrossReferences.Trace(e.Project, new(r.Id, "1")).Contains(new(t.Id, "1")), "Liaison électrique inchangée");
            c.Tag = new string('W', 500); var crowded = new TextPlacementPreview(e.Project, c); assert(!crowded.SmartAdjust().Clear, "Signale le manque de place pour un texte très long");
        });
        test("Ajustement intelligent directionnel : quatre côtés et rotation", () =>
        {
            var e = new Editor(); var c = e.AddComponent("coil", 600, 400); c.Rating = c.Reference = c.Range = ""; c.ShowPortLabels = false;
            foreach (int rotation in new[] { 0, 90, 180, 270 })
            foreach (string side in new[] { "top", "bottom", "left", "right" })
            {
                c.Rotation = rotation; var preview = new TextPlacementPreview(e.Project, c); var result = preview.SmartAdjust(side); var text = preview.TextBounds(); var symbol = c.Bounds();
                assert(result.Clear && preview.Placement.Side == side, "Respect du côté imposé après rotation");
                assert(side switch { "top" => text.Bottom <= symbol.Top, "bottom" => text.Top >= symbol.Bottom, "left" => text.Right <= symbol.Left, _ => text.Left >= symbol.Right }, "Texte sur le bon côté du symbole");
                assert(side is "top" or "bottom" ? Math.Abs(text.X + text.Width / 2 - c.X) < .001 : Math.Abs(text.Y + text.Height / 2 - c.Y) < .001, "Centrage sur le symbole sans obstacle");
            }
        });
        test("Ajustement directionnel : côté encombré conservé et reports", () =>
        {
            var e = new Editor(); var c = e.AddComponent("coil", e.Sheet.Width - 50, 300); c.Tag = "REPERE_TROP_LARGE";
            var preview = new TextPlacementPreview(e.Project, c); var before = ProjectStore.Serialize(e.Project); var result = preview.SmartAdjust("right");
            assert(result.Changed && !result.Clear && preview.Placement.Side == "right", "Ne bascule pas vers un côté libre sans demande");
            var placed = JsonSerializer.Serialize(preview.Placement); preview.SmartAdjust("right"); assert(JsonSerializer.Serialize(preview.Placement) == placed, "Clic répété stable");
            reject(() => preview.SmartAdjust("custom")); assert(ProjectStore.Serialize(e.Project) == before, "Projet intact et côté inconnu rejeté");
            var r = e.AddComponent("folio_next", 700, 400); e.AddSheet("Arrivée"); var t = e.AddComponent("folio_previous", 300, 300); e.LinkReferencePair(r.Id, t.Id, "24V");
            foreach (string side in new[] { "top", "bottom", "left", "right" })
            {
                var report = new TextPlacementPreview(e.Project, r); report.SmartAdjust(side); assert(report.Placement.Side == side && report.Draft.ReferenceDestinationInline, "Nom et destination respectent le côté choisi");
                report.Target = 1; report.SmartAdjust(side); assert(report.Placement.Side == side && !report.Draft.ReferenceDestinationInline, "Destination indépendante du côté choisi");
            }
            assert(CrossReferences.Trace(e.Project, new(r.Id, "1")).Contains(new(t.Id, "1")), "Liaison conservée");
        });
        test("Bouton intelligent : validation, persistance et annulation", () =>
        {
            string? old = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1"); var app = Application.Current ?? new Application(); var window = new MainWindow();
            try
            {
                var e = window.Editor; var c = e.AddComponent("coil", 300, 300); c.TagPlacement = new() { Side = "custom", OffsetX = 0, OffsetY = 0 }; var before = ProjectStore.Serialize(e.Project);
                window.DocumentTestProbe = dialog =>
                {
                    var content = (FrameworkElement)dialog.Content; content.Measure(new(1010, 680)); content.Arrange(new(0, 0, 1010, 680)); content.UpdateLayout();
                    Descendants(content).OfType<Button>().Single(b => b.Name == "SmartTextAdjust").RaiseEvent(new(ButtonBase.ClickEvent));
                    var preview = Descendants(content).OfType<TextPlacementPreview>().Single(); assert(preview.Placement.Side != "custom" && !preview.TextBounds().IntersectsWith(c.Bounds()), "Le bouton corrige le chevauchement avec le symbole");
                    foreach (string side in new[] { "top", "left", "right", "bottom" })
                    {
                        Descendants(content).OfType<Button>().Single(b => b.Name == "SmartText_" + side).RaiseEvent(new(ButtonBase.ClickEvent));
                        assert(preview.Placement.Side == side, "Bouton directionnel " + side);
                    }
                    assert(ProjectStore.Serialize(e.Project) == before, "Aperçu uniquement"); content.UpdateLayout(); var image = new RenderTargetBitmap(1010, 680, 96, 96, PixelFormats.Pbgra32); image.Render(content); SaveImage(image, Path.Combine(dir, "self-test-smart-text.png"));
                    Descendants(content).OfType<Button>().Single(b => b.Content as string == "Valider").RaiseEvent(new(ButtonBase.ClickEvent));
                };
                window.TextPositionDialog(c.Id); var after = ProjectStore.Serialize(e.Project); assert(after != before, "Validation effective");
                e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Une annulation restaure la position"); e.Redo(); assert(ProjectStore.Serialize(e.Project) == after, "Rétablissement");
                var path = Path.Combine(dir, "self-test-smart-text.gemelec"); e.Save(path); assert(ProjectStore.Serialize(ProjectStore.Load(path)) == after, "Placement sauvegardé");
            }
            finally { window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", old); }
        });
        test("Fluidité : ajustement intelligent parmi 1000 composants", () =>
        {
            var p = new Project(); for (int i = 0; i < 1000; i++) p.Sheets[0].Components.Add(new() { Symbol = "coil", X = 80 + i % 20 * 60, Y = 80 + i / 20 * 16, Tag = "K" + i });
            var preview = new TextPlacementPreview(p, p.Sheets[0].Components[500]); var timer = Stopwatch.StartNew(); var result = preview.SmartAdjust(); timer.Stop();
            assert(result.Changed && timer.Elapsed.TotalSeconds < 2, "Recherche bornée trop lente"); File.WriteAllText(Path.Combine(dir, "self-test-smart-text-performance.json"), JsonSerializer.Serialize(new { components = 1000, elapsedMs = timer.Elapsed.TotalMilliseconds, result.Clear }));
        });
        test("Reports : nom partagé, exception indépendante et retour au groupe", () =>
        {
            var e = new Editor(); var a = e.AddComponent("folio_next", 300, 300); e.AddSheet("Arrivée"); var b = e.AddComponent("folio_previous", 300, 300); e.LinkReferencePair(a.Id, b.Id, "24V");
            e.RenameReference(b.Id, "Alimentation automate"); assert(ReferenceDisplay.Name(a) == "Alimentation automate" && ReferenceDisplay.Name(b) == ReferenceDisplay.Name(a), "Nom propagé au report lié");
            e.ConfigureReference(b.Id, b.LinkName, b.LinkMode, 0, false, "Entrée locale", true); e.RenameReference(a.Id, "Commande"); assert(ReferenceDisplay.Name(b) == "Entrée locale" && b.ReferenceIndependentName, "Nom indépendant préservé");
            var before = ProjectStore.Serialize(e.Project); e.ConfigureReference(b.Id, b.LinkName, b.LinkMode, 0, false, "Entrée locale", false); assert(ReferenceDisplay.Name(b) == "Commande" && !b.ReferenceIndependentName, "Décocher reprend le nom partagé");
            assert(CrossReferences.Trace(e.Project, new(a.Id, "1")).Contains(new(b.Id, "1")), "Connexion inchangée"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Annulation du retour au groupe"); e.Redo();
            var path = Path.Combine(dir, "self-test-shared-report.gemelec"); e.Save(path); assert(ProjectStore.Serialize(ProjectStore.Load(path)) == ProjectStore.Serialize(e.Project), "Persistance du nom et de son mode");
            var star = new Editor(); var source = star.AddComponent("folio_next", 200, 200); star.ConfigureReference(source.Id, "NET", "star", 0, true); var branch = star.AddComponent("folio_previous", 400, 200); star.ConfigureReference(branch.Id, "NET", "star", 1, false); var independent = star.AddComponent("folio_previous", 600, 200); star.ConfigureReference(independent.Id, "NET", "star", 2, false, "Local", true);
            star.RenameReference(branch.Id, "Commun"); assert(ReferenceDisplay.Name(source) == "Commun" && ReferenceDisplay.Name(branch) == "Commun" && ReferenceDisplay.Name(independent) == "Local", "Étoile et exception indépendante");
        });
        test("Report : destination verte dessous et repérage dynamique", () =>
        {
            var e = new Editor(); var a = e.AddComponent("folio_next", 300, 300); e.AddSheet("Destination"); var b = e.AddComponent("folio_previous", 400, 300); e.LinkReferencePair(a.Id, b.Id, "Commande");
            a.TagPlacement.FontSize = 22; a.TagPlacement.Color = "#123456"; a.TagPlacement.Rotation = 30; a.DetailPlacement.FontSize = 11;
            var scene = ReferenceDisplay.Scene(e.Project, a, new(e.Project)).Where(i => i.Kind == "text").ToList(); var name = scene[0]; var destination = scene[1];
            assert(name.Size == 22 && name.Color == "#123456" && name.Rotation == 30, "Style principal appliqué");
            assert(destination.Color == "#008000" && destination.Size == 11 && Drawing.TextBounds(destination).Top > Drawing.TextBounds(name).Bottom, "Destination verte sous le nom tourné");
            e.Sheet.Number = "CMD-22"; b.X += 100; var updated = ReferenceDisplay.Scene(e.Project, a, new(e.Project)).Last(); assert(updated.Text.Contains("CMD-22") && updated.Text != destination.Text, "Adresse actualisée");
            assert(ReferenceDisplay.HitBounds(e.Project, a, new(e.Project)).Contains(Drawing.TextBounds(updated)), "Lien entièrement cliquable");
            SaveScene(ReferenceDisplay.Scene(e.Project, a, new(e.Project)), Path.Combine(dir, "self-test-report-style.png"), 800, 600);
        });
        test("Styles des composants : rotation, placement intelligent, exports et persistance", () =>
        {
            var e = new Editor(); var c = e.AddComponent("coil", 400, 400); c.Rating = "24 V"; c.Description = "Commande";
            var before = ProjectStore.Serialize(e.Project); e.Change(() => { c.TagPlacement.FontSize = 24; c.TagPlacement.Color = "#AA1122"; c.TagPlacement.Rotation = 90; c.DetailPlacement.FontSize = 18; c.DetailPlacement.Color = "#123456"; c.DetailPlacement.Rotation = -35; c.DescriptionPlacement.Visible = true; c.DescriptionPlacement.FontSize = 13; c.DescriptionPlacement.Color = "#008000"; c.DescriptionPlacement.Rotation = 180; });
            var texts = Drawing.ComponentScene(e.Project, c, new(e.Project)); assert(texts.Single(i => i.Text == c.Tag).Rotation == 90 && texts.Single(i => i.Text == "24 V").Size == 18 && texts.Single(i => i.Text == "Commande").Color == "#008000", "Styles séparés");
            var preview = new TextPlacementPreview(e.Project, c); preview.SmartAdjust("left"); assert(preview.Placement.FontSize == 24 && preview.Placement.Color == "#AA1122" && preview.Placement.Rotation == 90 && preview.TextBounds().Right < c.Bounds().Left, "Ajustement conserve le style et utilise l’encombrement tourné");
            var svg = Path.Combine(dir, "self-test-text-style.svg"); Export.Svg(e.Project, e.Sheet, svg); var text = XDocument.Load(svg).Descendants().Single(x => x.Name.LocalName == "text" && x.Value == c.Tag); assert(text.Attribute("font-size")!.Value == "24" && text.Attribute("fill")!.Value == "#AA1122" && text.Attribute("transform")!.Value.StartsWith("rotate(90 "), "SVG stylé");
            var pdfPath = Path.Combine(dir, "self-test-text-style.pdf"); Export.Pdf(e.Project, pdfPath); var pdf = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(pdfPath)); assert(pdf.Contains("/F1 24 Tf") && pdf.Contains("0 1 -1 0"), "Taille et rotation PDF");
            var snapshot = ProjectStore.Serialize(e.Project); assert(ProjectStore.Serialize(ProjectStore.Deserialize(snapshot)) == snapshot, "Styles persistants"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Undo styles"); e.Redo();
            foreach (var invalid in new[] { new LabelPlacement { FontSize = -1 }, new LabelPlacement { FontSize = 201 }, new LabelPlacement { Color = "rouge" }, new LabelPlacement { Rotation = double.NaN } }) reject(invalid.Validate);
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(before)!; var placement = legacy["sheets"]![0]!["components"]![0]!["tagPlacement"]!.AsObject(); placement.Remove("fontSize"); placement.Remove("color"); placement.Remove("rotation"); var old = ProjectStore.Deserialize(legacy.ToJsonString()).Sheets[0].Components[0].TagPlacement; assert(old.FontSize == 0 && old.Color == "" && old.Rotation == 0, "Compatibilité anciens styles");
            var multiline = new Ink("text", 100, 200, Text: "Première\nDeuxième", Size: 20, Rotation: 90); var lines = Drawing.TextLines(multiline).ToList(); assert(lines.Count == 2 && lines[1].X < lines[0].X && Math.Abs(lines[1].Y - lines[0].Y) < .001, "Lignes exportées dans le sens de la rotation");
        });
        test("Interface styles : aperçu direct, entrée invalide et validation", () =>
        {
            string? old = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1"); var app = Application.Current ?? new Application(); var window = new MainWindow();
            try
            {
                var e = window.Editor; var c = e.AddComponent("coil", 400, 400); var before = ProjectStore.Serialize(e.Project);
                window.DocumentTestProbe = dialog =>
                {
                    var content = (FrameworkElement)dialog.Content; content.Measure(new(1010, 680)); content.Arrange(new(0, 0, 1010, 680)); content.UpdateLayout(); var preview = Descendants(content).OfType<TextPlacementPreview>().Single();
                    TextBox Field(string name) => Descendants(content).OfType<TextBox>().Single(t => t.Name == name);
                    Field("TextFontSize").Text = "26"; Field("TextColor").Text = "#C03050"; Field("TextRotation").Text = "45";
                    var text = preview.Scene().Single(i => i.Text == c.Tag); assert(text.Size == 26 && text.Color == "#C03050" && text.Rotation == 45, "Style immédiatement visible");
                    Field("TextFontSize").Text = "-2"; Descendants(content).OfType<Button>().Single(b => b.Content as string == "Valider").RaiseEvent(new(ButtonBase.ClickEvent)); assert(ProjectStore.Serialize(e.Project) == before, "Style invalide non validé");
                    Field("TextFontSize").Text = "26"; preview.Fit(); content.UpdateLayout(); var image = new RenderTargetBitmap(1010, 680, 96, 96, PixelFormats.Pbgra32); image.Render(content); SaveImage(image, Path.Combine(dir, "self-test-text-style-dialog.png"));
                    Descendants(content).OfType<Button>().Single(b => b.Content as string == "Valider").RaiseEvent(new(ButtonBase.ClickEvent));
                };
                window.TextPositionDialog(c.Id); assert(e.Sheet.Components[0].TagPlacement.FontSize == 26 && e.Sheet.Components[0].TagPlacement.Rotation == 45, "Style validé"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Style annulable");
            }
            finally { window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", old); }
        });
        test("Fluidité : aperçu isolé dans un projet de 1000 composants", () =>
        {
            var p = new Project(); for (int i = 0; i < 1000; i++) p.Sheets[0].Components.Add(new() { Symbol = "coil", X = 200, Y = 200, Tag = "K" + i }); var preview = new TextPlacementPreview(p, p.Sheets[0].Components[0]);
            var bounds = preview.TextBounds(); var point = new Point(bounds.X + 2, bounds.Y + 2); preview.BeginDrag(point); var timer = Stopwatch.StartNew();
            for (int i = 0; i < 5000; i++) { preview.MoveDrag(point + new Vector(i % 100, i % 70)); assert(preview.Scene().Count < 30, "Un seul composant rendu"); }
            timer.Stop(); preview.EndDrag(); File.WriteAllText(Path.Combine(dir, "self-test-text-performance.json"), JsonSerializer.Serialize(new { projectComponents = 1000, moves = 5000, elapsedMs = timer.Elapsed.TotalMilliseconds })); assert(timer.Elapsed.TotalSeconds < 10, "Aperçu trop lent");
        });
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent) { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static void SaveImage(BitmapSource image, string path) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(path); encoder.Save(file); }
    private static void SaveScene(List<Ink> scene, string path, int width, int height) { var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new(0, 0, width, height)); Drawing.Render(dc, scene); } var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(visual); SaveImage(image, path); }
}
