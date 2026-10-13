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
        var displayName = Field("referenceName", "Nom affiché du report (libre)", ReferenceDisplay.Name(c));
        var independentName = new CheckBox { Content = "Nom indépendant", IsChecked = c.ReferenceIndependentName, Margin = new(0, 6, 0, 8), ToolTip = "Coché : seul ce report est renommé. Décoché : les reports liés partagent le même nom." }; properties.Children.Add(independentName);
        properties.Children.Add(Hint("Emplacement : " + SheetZones.Address(Editor.Project, Editor.Sheet, c)));
        properties.Children.Add(Button("Lier à un autre report…", () => LinkReferenceDialog(c.Id)));
        properties.Children.Add(Hint("Le nom commun relie les flèches, même sur des folios éloignés. Exemple : +24V ou COMMANDE_M1. R fait tourner le symbole."));
        properties.Children.Add(new TextBlock { Text = "Nom de liaison", Margin = new(0, 10, 0, 4) });
        var names = new ComboBox { IsEditable = true, ItemsSource = CrossReferences.Points(Editor.Project).Select(x => x.Component.LinkName).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(), Text = c.LinkName, Padding = new(5) }; properties.Children.Add(names);
        var mode = new ComboBox { ItemsSource = new[] { "Chaîne · paires 1–2, 3–4…", "Étoile · une source, plusieurs arrivées" }, SelectedIndex = c.LinkMode == "star" ? 1 : 0, Margin = new(0, 10, 0, 10) }; properties.Children.Add(mode);
        var source = new CheckBox { Content = "Point source de l’étoile", IsChecked = c.LinkSource, IsEnabled = mode.SelectedIndex == 1, Margin = new(0, 4, 0, 8) }; mode.SelectionChanged += (_, _) => source.IsEnabled = mode.SelectedIndex == 1; properties.Children.Add(source);
        var order = Field("linkOrder", "Ordre (0 = tri par folio et position)", c.LinkOrder.ToString());
        Component Current() => Editor.Sheet.Components.Single(x => x.Id == c.Id);
        void SyncFields()
        {
            updatingPropertyControls = true;
            try { var current = Current(); displayName.Text = ReferenceDisplay.Name(current); independentName.IsChecked = current.ReferenceIndependentName; }
            finally { updatingPropertyControls = false; }
        }
        void Configure(Control control)
        {
            LiveProperty(control, () => { if (!int.TryParse(order.Text, out var n)) throw new ArgumentException("Ordre entier attendu."); Editor.ConfigureReference(c.Id, names.Text, mode.SelectedIndex == 1 ? "star" : "pair", n, source.IsChecked == true, null, independentName.IsChecked == true, c.Id + ":link"); SyncFields(); });
        }
        LiveText(displayName, value => Editor.RenameReference(c.Id, value, c.Id + ":name"));
        order.TextChanged += (_, _) => Configure(order); order.LostKeyboardFocus += (_, _) => Editor.EndPropertyEdit();
        independentName.Click += (_, _) => Configure(independentName);
        source.Click += (_, _) => Configure(source);
        mode.SelectionChanged += (_, _) => Configure(mode);
        names.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => Configure(names)));
        names.LostKeyboardFocus += (_, _) => Editor.EndPropertyEdit();

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
        if (targets.Count == 1) GoToReference(targets[0]); else if (targets.Count > 1) ReferenceNavigator(); else LinkReferenceDialog(id);
    }
    internal void LinkReferenceDialog(string id)
    {
        Canvas.SetMode("select");
        var c = CrossReferences.Points(Editor.Project).Single(p => p.Component.Id == id).Component;
        var dialog = DocumentWindow("Lier les reports de folio", 720, 540); var root = new DockPanel { Margin = new(18) }; dialog.Content = root;
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(Hint("Choisir la contrepartie, puis nommer la liaison. Le double-clic sur une flèche ou son nom ouvre ensuite le report lié. Une liaison existante des deux reports choisis sera remplacée."));
        var name = DocField(top, "Nom commun aux deux reports", c.LinkName.Length > 0 ? c.LinkName : c.Tag);
        var bottom = new WrapPanel(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var list = new ListBox(); root.Children.Add(list);
        foreach (var p in CrossReferences.Points(Editor.Project).Where(p => p.Component.Symbol != c.Symbol))
            list.Items.Add(new ListBoxItem { Tag = p, Content = $"{SheetZones.Address(Editor.Project, p.Sheet, p.Component)}  ·  {p.Sheet.Title}  ·  {ReferenceDisplay.Name(p.Component)} · liaison {p.Component.LinkName}", Padding = new(8) });
        if (list.Items.Count == 0) top.Children.Add(Hint("Placer d’abord un report de sens complémentaire depuis la bibliothèque, sur le folio souhaité."));
        bottom.Children.Add(DocButton("Lier les reports", dialog, () => { if (list.SelectedItem is not ListBoxItem { Tag: ReferencePoint target }) throw new ArgumentException("Sélectionner un report dans la liste."); Editor.LinkReferencePair(id, target.Component.Id, name.Text); dialog.Close(); }));
        bottom.Children.Add(DocButton("Annuler", dialog, dialog.Close)); ShowDocumentDialog(dialog);
    }
    private sealed record ReferenceRow(ReferencePoint Point, string Name, string DisplayName, string Type, string Address, string Mode, int Order, string Targets);
    private void ReferenceNavigator()
    {
        Canvas.CancelDrag(); var dialog = DocumentWindow("Renvois interfolios", 1120, 670); var root = new DockPanel { Margin = new(18) }; dialog.Content = root;
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(Hint("Même nom de liaison = connexion commune. Le nom affiché est indépendant. Double-cliquer une ligne pour aller au symbole."));
        var filter = new TextBox { Margin = new(0, 8, 0, 12), Padding = new(7), ToolTip = "Filtrer par nom de liaison ou emplacement" }; top.Children.Add(filter);
        var actions = new WrapPanel(); DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, EnableRowVirtualization = true, SelectionMode = DataGridSelectionMode.Single }; root.Children.Add(grid);
        foreach (var (label, field, width) in new[] { ("Nom affiché", "DisplayName", 170), ("Liaison", "Name", 150), ("Flèche", "Type", 100), ("Emplacement", "Address", 110), ("Mode", "Mode", 95), ("Ordre", "Order", 65), ("Destination(s)", "Targets", 390) }) grid.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(field), Width = width });
        var refs = new CrossReferences(Editor.Project); var rows = CrossReferences.Points(Editor.Project).Select(x => new ReferenceRow(x, x.Component.LinkName, ReferenceDisplay.Name(x.Component), x.Component.Symbol == "folio_next" ? "Départ" : "Arrivée", SheetZones.Address(Editor.Project, x.Sheet, x.Component), x.Component.LinkMode == "star" ? "Étoile" : "Chaîne", x.Component.LinkOrder, refs.Label(Editor.Project, x.Component))).ToList();
        void RefreshRows() => grid.ItemsSource = rows.Where(r => Symbols.SearchKey(r.Name + " " + r.DisplayName + " " + r.Address).Contains(Symbols.SearchKey(filter.Text))).ToList(); filter.TextChanged += (_, _) => RefreshRows(); RefreshRows();
        void Go() { if (grid.SelectedItem is ReferenceRow row) { dialog.Close(); GoToReference(row.Point); } }
        actions.Children.Add(DocButton("Aller au symbole", dialog, Go)); actions.Children.Add(DocButton("Fermer", dialog, dialog.Close)); grid.MouseDoubleClick += (_, _) => Go(); ShowDocumentDialog(dialog);
    }
}
