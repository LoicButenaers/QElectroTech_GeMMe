using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    private void ZonesDialog()
    {
        Canvas.CancelDrag(); if (Editor.Sheet.BomPage is not null) throw new ArgumentException("Le repérage s’applique aux folios de schéma.");
        var dialog = DocumentWindow("Repérage du folio · lignes et colonnes", 520, 430); var panel = new StackPanel { Margin = new(22) }; dialog.Content = panel;
        panel.Children.Add(Hint("Les chiffres en haut et les lettres à gauche permettent de retrouver les composants. Ce cadre est conservé dans le PDF ; il est indépendant de la grille de placement."));
        var columns = DocField(panel, "Colonnes (1 à 40)", Editor.Sheet.ZoneColumns.ToString()); var rows = DocField(panel, "Lignes (1 à 26, A à Z)", Editor.Sheet.ZoneRows.ToString());
        var all = new CheckBox { Content = "Appliquer à tous les folios de schéma", Margin = new(0, 12, 0, 12) }; panel.Children.Add(all);
        panel.Children.Add(DocButton("Enregistrer", dialog, () => { if (!int.TryParse(columns.Text, out var x) || !int.TryParse(rows.Text, out var y)) throw new ArgumentException("Saisir des nombres entiers."); Editor.Change(() => { foreach (var s in Editor.Project.Sheets.Where(s => s.BomPage is null && (all.IsChecked == true || s.Id == Editor.Sheet.Id))) { s.ZoneColumns = x; s.ZoneRows = y; } }); dialog.Close(); }));
        panel.Children.Add(DocButton("Annuler", dialog, dialog.Close)); ShowDocumentDialog(dialog);
    }
    private void ReferenceInspector(Component c)
    {
        properties.Children.Add(Hint("Emplacement : " + SheetZones.Address(Editor.Project, Editor.Sheet, c)));
        properties.Children.Add(Hint("Le nom commun relie les flèches, même sur des folios éloignés. Exemple : +24V ou COMMANDE_M1. R fait tourner le symbole."));
        properties.Children.Add(new TextBlock { Text = "Nom de liaison", Margin = new(0, 10, 0, 4) });
        var names = new ComboBox { IsEditable = true, ItemsSource = CrossReferences.Points(Editor.Project).Select(x => x.Component.LinkName).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(), Text = c.LinkName, Padding = new(5) }; properties.Children.Add(names);
        var mode = new ComboBox { ItemsSource = new[] { "Chaîne · paires 1–2, 3–4…", "Étoile · une source, plusieurs arrivées" }, SelectedIndex = c.LinkMode == "star" ? 1 : 0, Margin = new(0, 10, 0, 10) }; properties.Children.Add(mode);
        var source = new CheckBox { Content = "Point source de l’étoile", IsChecked = c.LinkSource, IsEnabled = mode.SelectedIndex == 1, Margin = new(0, 4, 0, 8) }; mode.SelectionChanged += (_, _) => source.IsEnabled = mode.SelectedIndex == 1; properties.Children.Add(source);
        var order = Field("linkOrder", "Ordre (0 = tri par folio et position)", c.LinkOrder.ToString());
        properties.Children.Add(Button("Appliquer le renvoi", () => { if (!int.TryParse(order.Text, out var n)) throw new ArgumentException("Ordre entier attendu."); Editor.ConfigureReference(c.Id, names.Text, mode.SelectedIndex == 1 ? "star" : "pair", n, source.IsChecked == true); }));
        properties.Children.Add(Hint("En chaîne, le tri utilise l’ordre, puis le folio et la position. En étoile, choisir une source ; à défaut le premier point est utilisé. Le mode est commun aux points de même nom."));
        var targets = new CrossReferences(Editor.Project).Targets(c.Id);
        if (targets.Count == 0) properties.Children.Add(Hint("Non lié : placer un autre renvoi puis lui attribuer le même nom."));
        foreach (var t in targets) properties.Children.Add(Button("Aller à " + SheetZones.Address(Editor.Project, t.Sheet, t.Component), () => GoToReference(t)));
        properties.Children.Add(Button("Navigateur des renvois…", ReferenceNavigator));
        TextPositionEditor(c);
    }
    private void GoToReference(ReferencePoint point) { Canvas.CancelDrag(); Editor.SelectSheet(point.Sheet.Id); Canvas.Select(point.Component.Id); Canvas.CenterOn(point.Component.X, point.Component.Y); }
    private void FollowReference(string id)
    {
        var targets = new CrossReferences(Editor.Project).Targets(id);
        if (targets.Count == 1) GoToReference(targets[0]); else if (targets.Count > 1) ReferenceNavigator(); else status.Text = "Renvoi non lié : renseigner le même nom sur sa contrepartie.";
    }
    private sealed record ReferenceRow(ReferencePoint Point, string Name, string Type, string Address, string Mode, int Order, string Targets);
    private void ReferenceNavigator()
    {
        Canvas.CancelDrag(); var dialog = DocumentWindow("Renvois interfolios", 1120, 670); var root = new DockPanel { Margin = new(18) }; dialog.Content = root;
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(Hint("Même nom = liaison commune. Double-cliquer une ligne pour aller au symbole ; les propriétés permettent de modifier le nom, le mode et l’ordre."));
        var filter = new TextBox { Margin = new(0, 8, 0, 12), Padding = new(7), ToolTip = "Filtrer par nom de liaison ou emplacement" }; top.Children.Add(filter);
        var actions = new WrapPanel(); DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, EnableRowVirtualization = true, SelectionMode = DataGridSelectionMode.Single }; root.Children.Add(grid);
        foreach (var (label, field, width) in new[] { ("Liaison", "Name", 170), ("Flèche", "Type", 100), ("Emplacement", "Address", 110), ("Mode", "Mode", 95), ("Ordre", "Order", 65), ("Destination(s)", "Targets", 390) }) grid.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(field), Width = width });
        var refs = new CrossReferences(Editor.Project); var rows = CrossReferences.Points(Editor.Project).Select(x => new ReferenceRow(x, x.Component.LinkName, x.Component.Symbol == "folio_next" ? "Départ" : "Arrivée", SheetZones.Address(Editor.Project, x.Sheet, x.Component), x.Component.LinkMode == "star" ? "Étoile" : "Chaîne", x.Component.LinkOrder, refs.Label(Editor.Project, x.Component))).ToList();
        void RefreshRows() => grid.ItemsSource = rows.Where(r => Symbols.SearchKey(r.Name + " " + r.Address).Contains(Symbols.SearchKey(filter.Text))).ToList(); filter.TextChanged += (_, _) => RefreshRows(); RefreshRows();
        void Go() { if (grid.SelectedItem is ReferenceRow row) { dialog.Close(); GoToReference(row.Point); } }
        actions.Children.Add(DocButton("Aller au symbole", dialog, Go)); actions.Children.Add(DocButton("Fermer", dialog, dialog.Close)); grid.MouseDoubleClick += (_, _) => Go(); ShowDocumentDialog(dialog);
    }
}
