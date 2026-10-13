using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

internal static class CoilContactTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert, Action<Action> reject, string dir)
    {
        test("Bobines/contacts : association interfolios, repère partagé, références et connectivité", () => {
            var e = new Editor(); var coilSheet = e.Sheet; coilSheet.Title = "Commande"; var coil = e.AddComponent("coil", 400, 300); e.AddSheet("Contacts"); var no = e.AddComponent("contact_no", 350, 300); var nc = e.AddComponent("contact_nc", 650, 500); var w = e.Connect(new(no.Id, "14"), new(nc.Id, "21"));
            e.AssociateContact(no.Id, coil.Id); e.AssociateContact(nc.Id, coil.Id); assert(no.Tag == coil.Tag && nc.Tag == coil.Tag, "Repère non partagé");
            var refs = new CoilContacts(e.Project); assert(refs.Contacts(coil.Id).Count == 2 && refs.Label(coil).Contains("NO · 13–14") && refs.Label(coil).Contains("NF · 21–22") && refs.Label(no).Contains("1 /"), "Références réciproques");
            assert(!CrossReferences.Trace(e.Project, new(no.Id, "14")).Any(x => x.ComponentId == coil.Id), "Une association a créé une liaison électrique");
            assert(!e.Validate().Any(f => f.Message.StartsWith("Repère utilisé plusieurs fois")), "Faux doublon de repère");
            e.Change(() => e.SetApparatusTag(no, "KM-Été")); assert(coil.Tag == "KM-Été" && nc.Tag == "KM-Été", "Renommage depuis le contact"); e.Undo(); e.Redo();
            string path = Path.Combine(dir, "self-test-coil-contacts.gemelec"); e.Save(path); var loaded = ProjectStore.Load(path); assert(loaded.Version == 2 && new CoilContacts(loaded).Contacts(coil.Id).Count == 2, "Persistance");
            Export.Pdf(loaded, Path.Combine(dir, "self-test-coil-contacts.pdf")); Export.Svg(loaded, loaded.Sheets[1], Path.Combine(dir, "self-test-coil-contacts.svg"));
            var text = Drawing.Scene(loaded, loaded.Sheets[0]).Where(i => i.Kind == "text"); assert(text.Any(i => i.Text.Contains("NO · 13–14")), "Références absentes du rendu commun");
            assert(BomReport.Rows(loaded, new()).Sum(r => int.Parse(r.Values["quantity"])) == 1, "Fonctions associées comptées comme appareils supplémentaires"); assert(BomReport.Rows(loaded, new() { IncludeAuxiliary = true }).Sum(r => int.Parse(r.Values["quantity"])) == 3, "Fonctions auxiliaires non accessibles");
            var filtered = BomReport.Rows(loaded, new() { SheetId = loaded.Sheets[1].Id }); assert(filtered.Count == 1 && filtered[0].Values["pages"].Contains("2 /") && !filtered[0].Values["pages"].Contains("1 /"), "Filtre par folio perdant l’article de la bobine située ailleurs");
            loaded.Sheets.Reverse(); loaded.Sheets[0].Number = "CONTACTS"; var moved = new CoilContacts(loaded); assert(moved.Label(loaded.Sheets[1].Components[0]).Contains("CONTACTS /"), "Renvoi obsolète après réorganisation");
        });
        test("Bobines/contacts : association invalide, détachement, suppression et annulation", () => {
            var e = new Editor(); var coil = e.AddComponent("coil", 200, 200); var wrong = e.AddComponent("terminal", 500, 200); e.AddSheet("Contacts"); var no = e.AddComponent("contact_no", 300, 300); var nc = e.AddComponent("contact_nc", 600, 300); var w = e.Connect(new(no.Id, "14"), new(nc.Id, "21")); e.AssociateContact(no.Id, coil.Id);
            string before = ProjectStore.Serialize(e.Project); reject(() => e.AssociateContact(no.Id, wrong.Id)); reject(() => e.AssociateContact(coil.Id, coil.Id)); assert(ProjectStore.Serialize(e.Project) == before, "Association invalide partiellement appliquée");
            e.AssociateContact(no.Id, ""); assert(e.Sheet.Wires.Count == 1 && e.Sheet.Components[0].CoilId == "", "Détachement perdant un fil"); e.Undo(); assert(e.Sheet.Components[0].CoilId == coil.Id, "Annulation du détachement");
            e.SelectSheet(e.Project.Sheets[0].Id); before = ProjectStore.Serialize(e.Project); e.Delete([coil.Id]); assert(e.Project.Sheets[1].Wires.Single().Id == w.Id && e.Project.Sheets[1].Components[0].CoilId == "", "Suppression de bobine supprimant le contact ou ses fils"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Annulation de la suppression");
            e.DeleteSheet(e.Project.Sheets[0].Id); assert(e.Sheet.Components[0].CoilId == "" && e.Sheet.Wires.Count == 1, "Suppression du folio non cohérente"); e.Undo(); assert(new CoilContacts(e.Project).Contacts(coil.Id).Count == 1, "Annulation du folio");
        });
        test("Format v2 : migration v1 sans association inventée, sauvegarde et limite 200", () => {
            var p = new Project(); p.Sheets[0].Components.Add(new() { Symbol = "coil", Tag = "K1", X = 300, Y = 300 }); var old = JsonNode.Parse(ProjectStore.Serialize(p))!; old["version"] = 1; var c = old["sheets"]![0]!["components"]![0]!.AsObject(); c.Remove("coilId"); c.Remove("relayFunction"); c.Remove("coilReferencePlacement");
            var migrated = ProjectStore.Deserialize(old.ToJsonString()); assert(migrated.Version == 2 && migrated.Sheets[0].Components[0].CoilId == "" && migrated.Sheets[0].Components[0].Tag == "K1", "Migration altérant les données");
            assert(ProjectStore.Serialize(ProjectStore.Deserialize(ProjectStore.Serialize(migrated))) == ProjectStore.Serialize(migrated), "Round trip v2"); old["version"] = 99; reject(() => ProjectStore.Deserialize(old.ToJsonString()));
            for (int i = 1; i < 200; i++) migrated.Sheets.Add(new()); ProjectStore.ValidateStructure(migrated); migrated.Sheets.Add(new()); reject(() => ProjectStore.ValidateStructure(migrated));
        });
        test("Échec d’enregistrement : modèle et état non enregistré conservés", () => {
            var e = new Editor(); e.AddComponent("coil", 300, 300); string before = ProjectStore.Serialize(e.Project); string path = Path.Combine(dir, "self-test-unwritable.gemelec"); Directory.CreateDirectory(path);
            reject(() => e.Save(path)); assert(e.FilePath is null && e.Dirty && ProjectStore.Serialize(e.Project) == before && !Directory.GetFiles(dir, "self-test-unwritable.gemelec.tmp-*").Any(), "Échec de sauvegarde perdant le modèle ou laissant un fichier temporaire");
        });
        test("Historique compressé : huit opérations sur 5 000 composants, retour exact et connexions", () => {
            var e = new Editor(); e.ImportProject(EditorBenchmark.Fixture(100)); string initial = ProjectStore.Serialize(e.Project);
            for (int i = 0; i < 8; i++) e.Change(() => e.Sheet.Components[0].Description = "Commande · été " + i);
            string final = ProjectStore.Serialize(e.Project); for (int i = 0; i < 8; i++) e.Undo(); assert(ProjectStore.Serialize(e.Project) == initial && !e.CanUndo, "Budget mémoire ayant perdu des opérations utiles");
            for (int i = 0; i < 8; i++) e.Redo(); assert(ProjectStore.Serialize(e.Project) == final && new CoilContacts(e.Project).Contacts(e.Sheet.Components[0].Id).Count == 2 && e.Sheet.Wires.Count == 49, "Restauration perdant les données ou les associations");
        });
        test("PDF en arrière-plan : édition et rendu concurrents, instantané et images identiques", () => WithWindow(window => {
            var e = window.Editor; Examples.Motor(e); e.AddComponent(Catalog.Entries.First(x => x.CanPlace).Symbol, 650, 400); e.AddSheet("Suite"); e.SelectSheet(e.Project.Sheets[0].Id);
            string foreground = Path.Combine(dir, "self-test-pdf-foreground.pdf"), background = Path.Combine(dir, "self-test-pdf-background.pdf"); Export.Pdf(e.Project, foreground);
            var task = window.Dispatcher.Invoke(() => window.ExportPdfAsync(background));
            for (int i = 0; i < 20; i++) { e.Change(() => e.Sheet.Notes[0].Text = "Édition pendant le PDF " + i); Drawing.Scene(e.Project, e.Sheet); }
            var limit = DateTime.UtcNow.AddSeconds(15); while (!task.IsCompleted && DateTime.UtcNow < limit) window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            assert(task.IsCompleted, "L’export en arrière-plan ne s’est pas terminé"); task.GetAwaiter().GetResult(); assert(window.IsEnabled && e.Sheet.Notes[0].Text.EndsWith("19") && File.ReadAllBytes(foreground).SequenceEqual(File.ReadAllBytes(background)), "Export affecté par l’édition ou rendu différent entre fils");
        }));
        test("Bobines/contacts : inspector, navigation, duplication et copie du symbole", () => WithWindow(window => {
            var e = window.Editor; var coil = e.AddComponent("coil", 300, 300); var first = e.Sheet; e.AddSheet("Contacts"); var no = e.AddComponent("contact_no", 400, 300); window.Canvas.Select(no.Id);
            var panel = (FrameworkElement)window.Content; Arrange(panel); var choice = Descendants(window.PropertyPanel).OfType<ComboBox>().Single(c => c.Name == "CoilAssociation"); choice.SelectedValue = coil.Id;
            assert(e.Sheet.Components.Single(c => c.Id == no.Id).CoilId == coil.Id && window.PropertyField("tag").Text == coil.Tag, "Association non immédiate par le contrôle");
            window.PropertyField("tag").Text = "KM7"; assert(e.Project.Sheets[0].Components.Single(c => c.Id == coil.Id).Tag == "KM7", "Repère non propagé depuis l’inspecteur");
            var refInk = ComponentTexts.Ink(e.Project, no, new(e.Project), "coilReferences")!; var bounds = Drawing.TextBounds(refInk); assert(window.Canvas.ActivateReference(new(bounds.X + 2, bounds.Y + 2)) && e.Sheet.Id == first.Id && window.Canvas.Selected.Contains(coil.Id), "Navigation contact vers bobine");
            window.Canvas.Selected.Add(e.AddComponent("contact_nc", 600, 300).Id); var local = e.Sheet.Components.Last(); e.AssociateContact(local.Id, coil.Id); window.Canvas.Select(coil.Id); window.Canvas.Selected.Add(local.Id); window.WorkspaceCommandForTest("duplicate").Execute(null, window);
            var copiedCoil = e.Sheet.Components.Last(c => CoilContacts.IsCoil(c)); var copiedContact = e.Sheet.Components.Last(c => CoilContacts.IsContact(c)); assert(copiedContact.CoilId == copiedCoil.Id && copiedContact.Tag == copiedCoil.Tag && copiedCoil.Tag != coil.Tag, "Duplication de groupe avec ancienne association");
            e.SaveSymbolCopy(copiedCoil.Definition, copiedCoil.Id); assert(CoilContacts.IsCoil(copiedCoil) && new CoilContacts(e.Project).Contacts(copiedCoil.Id).Count == 1, "Copie du symbole perdant la fonction");
            e.Change(() => { copiedCoil.X = 900; copiedCoil.Y = 550; copiedContact.X = 1150; copiedContact.Y = 550; });
            window.Canvas.Select(coil.Id); window.SetInterface("compact", "light", false); Arrange(panel); window.Canvas.Fit(); panel.UpdateLayout(); Save(panel, Path.Combine(dir, "ui-coil-contacts.png"));
            string pdf = Path.Combine(dir, "self-test-linked-theme.pdf"); Export.Pdf(e.Project, pdf); var originalPdf = File.ReadAllBytes(pdf); window.SetInterface("ribbon", "dark", false); Export.Pdf(e.Project, pdf); assert(originalPdf.SequenceEqual(File.ReadAllBytes(pdf)), "Thème ou disposition modifiant les références bobine/contacts dans le PDF");
            window.DocumentTestProbe = dialog => { var content = (FrameworkElement)dialog.Content; content.Measure(new(1040, 590)); content.Arrange(new(0, 0, 1040, 590)); content.UpdateLayout(); var grid = Descendants(content).OfType<DataGrid>().Single(x => x.Name == "CoilFunctions"); assert(grid.Items.Count == 5, "Fonctions manquantes dans le navigateur"); };
            window.WorkspaceCommandForTest("coilContacts").Execute(null, window);
        }));
        test("Bobines/contacts : doublons de bornes signalés et référence calculée non éditable", () => {
            var e = new Editor(); var coil = e.AddComponent("coil", 200, 200); var a = e.AddComponent("contact_no", 500, 200); var b = e.AddComponent("contact_no", 800, 200); e.AssociateContact(a.Id, coil.Id); e.AssociateContact(b.Id, coil.Id);
            assert(e.Validate().Count(f => f.Message.Contains("représentées plusieurs fois")) == 2, "Doublons de fonctions non signalés"); e.Change(() => { b.PortLabels["13"] = "23"; b.PortLabels["14"] = "24"; }); assert(!e.Validate().Any(f => f.Message.Contains("représentées plusieurs fois")), "Correction de bornes non prise en compte");
            reject(() => e.Change(() => ComponentTexts.SetText(a, "coilReferences", "Inventé"))); var current = e.Sheet.Components.Single(c => c.Id == a.Id); e.Change(() => ComponentTexts.SetPlacement(current, "coilReferences", new() { Side = "bottom", FontSize = 14, Color = "#112233" })); var ink = ComponentTexts.Ink(e.Project, current, new(e.Project), "coilReferences")!; assert(ink.Size == 14 && ink.Color == "#112233", "Style indépendant non appliqué");
        });
        test("Symboles personnels : fonction déclarée, association et changement annulable", () => WithWindow(window => {
            var e = window.Editor; var definition = e.SaveSymbolCopy(Symbols.All["coil"]); var coil = e.AddComponent(definition.Id, 300, 300); var contact = e.AddComponent("contact_no", 650, 300); reject(() => e.AssociateContact(contact.Id, coil.Id));
            window.Canvas.Select(coil.Id); var panel = (FrameworkElement)window.Content; Arrange(panel); var group = Descendants(window.PropertyPanel).OfType<Expander>().Single(x => x.Header is TextBlock t && t.Text == "Définition du symbole"); group.IsExpanded = true; Arrange(panel);
            Descendants(window.PropertyPanel).OfType<ComboBox>().Single(x => x.Name == "RelayFunctionDeclaration").SelectedValue = "coil"; assert(CoilContacts.IsCoil(e.Sheet.Components.Single(x => x.Id == coil.Id)), "Déclaration non appliquée par le contrôle");
            e.AssociateContact(contact.Id, coil.Id); var before = ProjectStore.Serialize(e.Project); e.Change(() => e.SetRelayFunction(e.Sheet.Components.Single(x => x.Id == coil.Id), "")); assert(e.Sheet.Components.Single(x => x.Id == contact.Id).CoilId == "", "Changement de rôle laissant une association invalide"); e.Undo(); assert(ProjectStore.Serialize(e.Project) == before, "Changement de rôle non annulable");
        }));
    }
    private static void WithWindow(Action<MainWindow> action) { string? old = Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION"); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1"); var app = Application.Current ?? new Application(); var window = new MainWindow(); try { action(window); } finally { window.DisposeTestBridge(); Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", old); } }
    private static void Arrange(FrameworkElement panel) { System.Windows.Input.CommandManager.InvalidateRequerySuggested(); panel.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); panel.InvalidateMeasure(); panel.InvalidateArrange(); panel.Measure(new(1440, 900)); panel.Arrange(new(0, 0, 1440, 900)); panel.UpdateLayout(); }
    private static void Save(Visual visual, string path) { var image = new RenderTargetBitmap(1440, 900, 96, 96, PixelFormats.Pbgra32); image.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(path); encoder.Save(file); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent) { yield return parent; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var c in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return c; }
}
