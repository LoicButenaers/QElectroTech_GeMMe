using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeMMeElec;

public sealed record ProjectLocation(string SheetId, string ObjectId, string Folio, string Type, string Name, string Detail, double X, double Y);
public static class ProjectNavigation
{
    public static List<ProjectLocation> Search(Project p, string query)
    {
        string key = Symbols.SearchKey(query.Trim()); var result = new List<ProjectLocation>();
        bool Matches(params string[] values) => key.Length == 0 || Symbols.SearchKey(string.Join(' ', values)).Contains(key);
        foreach (var s in p.Sheets)
        {
            string folio = DocumentModel.PageNumber(p, s) + " · " + s.Title;
            if (Matches(s.Title, s.Number)) result.Add(new(s.Id, "", folio, "Folio", s.Title, s.Number, s.Width / 2, s.Height / 2));
            foreach (var c in s.Components.Where(c => !Junctions.Automatic(c))) if (Matches(c.Tag, c.Description, c.Reference, c.Brand, c.Rating, c.LinkName, ReferenceDisplay.Name(c), string.Join(' ', c.AdditionalTexts.Select(t => t.Text).Concat(c.TextOverrides.Values.Select(t => t.Text)).Concat(c.Definition.Shapes.Where(s => s.Kind == "text").Select(s => s.Text))))) result.Add(new(s.Id, c.Id, folio, "Composant", c.Tag, string.Join(" · ", new[] { c.Description, c.Brand, c.Reference }.Where(v => v.Length > 0)), c.X, c.Y));
            foreach (var w in s.Wires) if (Matches(w.Number)) { var point = w.Route(s)[0]; result.Add(new(s.Id, w.Id, folio, "Conducteur", w.Number, w.NumberIndependent ? "Numéro indépendant" : "Numéro commun", point.X, point.Y)); }
            foreach (var n in s.Notes) if (Matches(n.Text,n.ResolveFields?DocumentModel.Resolve(n.Text,DocumentModel.Values(p,s)):"")) result.Add(new(s.Id, n.Id, folio, "Texte", n.Text, "", n.X, n.Y));
        }
        return result;
    }
    public static List<ProjectLocation> Circuit(Project p, string wireId)
    {
        var origin = p.Sheets.SelectMany(s => s.Wires).Single(w => w.Id == wireId); var ids = new ConductorNetworks(p).Connected(origin.From).Select(w => w.Id).ToHashSet();
        return p.Sheets.SelectMany(s => s.Wires.Where(w => ids.Contains(w.Id)).Select(w => {
            var from = s.Components.Single(c => c.Id == w.From.ComponentId); var to = s.Components.Single(c => c.Id == w.To.ComponentId); var point = w.Route(s)[0];
            return new ProjectLocation(s.Id, w.Id, DocumentModel.PageNumber(p, s) + " · " + s.Title, "Conducteur", w.Number, $"{from.Tag}:{from.PortLabels.GetValueOrDefault(w.From.PortId,w.From.PortId)} → {to.Tag}:{to.PortLabels.GetValueOrDefault(w.To.PortId,w.To.PortId)}", point.X, point.Y);
        })).ToList();
    }
}

public sealed partial class MainWindow
{
    internal void NavigateTo(ProjectLocation location)
    {
        Canvas.CancelDrag(); Editor.SelectSheet(location.SheetId); if (location.ObjectId.Length > 0) Canvas.Select(location.ObjectId); Canvas.CenterOn(location.X, location.Y);
    }
    private void ProjectSearchDialog() => LocationDialog("Rechercher dans le projet", () => ProjectNavigation.Search(Editor.Project, ""), true);
    private void CircuitDialog()
    {
        var wire = Editor.Sheet.Wires.FirstOrDefault(w => Canvas.Selected.Contains(w.Id)) ?? throw new ArgumentException("Sélectionner un conducteur.");
        Canvas.HighlightCircuit(wire.Id); LocationDialog("Continuité du circuit · fils et bornes", () => ProjectNavigation.Circuit(Editor.Project, wire.Id), false);
    }
    private void LocationDialog(string title, Func<List<ProjectLocation>> source, bool searchMode)
    {
        Canvas.CancelDrag(); var dialog = DocumentWindow(title, 1080, 700); var root = new DockPanel { Margin = new(18) }; dialog.Content = root;
        var input = new TextBox { Name = "ProjectSearchQuery", Margin = new(0, 0, 0, 10), Padding = new(8), ToolTip = "Repère, numéro de fil, référence, texte ou titre de folio" }; DockPanel.SetDock(input, Dock.Top); root.Children.Add(input); input.Visibility = searchMode ? Visibility.Visible : Visibility.Collapsed;
        var info = Hint(""); DockPanel.SetDock(info, Dock.Top); root.Children.Add(info);
        var bottom = new WrapPanel(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var grid = new DataGrid { Name = "ProjectLocations", IsReadOnly = true, AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Single, CanUserAddRows = false }; root.Children.Add(grid);
        foreach (var (header, path, width) in new[] { ("Folio", "Folio", 220d), ("Type", "Type", 105d), ("Repère / texte", "Name", 200d), ("Détail", "Detail", 420d) }) grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(path), Width = width });
        void RefreshRows() { var rows = searchMode ? ProjectNavigation.Search(Editor.Project, input.Text) : source(); grid.ItemsSource = rows; info.Text = rows.Count + " résultat(s) · double-clic pour rejoindre le folio"; }
        void Follow() { if (grid.SelectedItem is ProjectLocation row) { NavigateTo(row); dialog.Close(); } }
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) }; timer.Tick += (_, _) => { timer.Stop(); RefreshRows(); };
        input.TextChanged += (_, _) => { timer.Stop(); timer.Start(); }; grid.MouseDoubleClick += (_, _) => Follow(); grid.KeyDown += (_, e) => { if (e.Key == Key.Enter) Follow(); };
        bottom.Children.Add(DocButton("Rejoindre la sélection", dialog, Follow)); bottom.Children.Add(DocButton("Actualiser", dialog, RefreshRows));
        if (!searchMode) bottom.Children.Add(DocButton("Effacer le surlignage", dialog, () => Canvas.ClearCircuitHighlight()));
        bottom.Children.Add(DocButton("Fermer", dialog, dialog.Close)); dialog.Closed += (_, _) => timer.Stop(); RefreshRows(); ShowDocumentDialog(dialog);
    }
    private void FindingsDialog()
    {
        Canvas.CancelDrag(); var dialog = DocumentWindow("Anomalies du projet · actualiser et localiser", 1100, 700); var root = new DockPanel { Margin = new(18) }; dialog.Content = root;
        var query = new TextBox { Name = "FindingFilter", Padding = new(7), Margin = new(0, 0, 0, 8), ToolTip = "Filtrer par famille, repère, folio ou message" }; DockPanel.SetDock(query, Dock.Top); root.Children.Add(query);
        var info = Hint(""); DockPanel.SetDock(info, Dock.Top); root.Children.Add(info); var footer = new WrapPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var grid = new DataGrid { Name = "ProjectFindings", IsReadOnly = true, AutoGenerateColumns = true, CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single }; root.Children.Add(grid);
        List<Finding> findings = []; void Display() { string key = Symbols.SearchKey(query.Text); var rows = findings.Where(f => Symbols.SearchKey(f.Level + " " + f.Message + " " + DocumentModel.PageNumber(Editor.Project, Editor.Project.Sheets.Single(s => s.Id == f.SheetId)) + " " + Editor.Project.Sheets.Single(s => s.Id == f.SheetId).Title).Contains(key)).Select(f => new FindingRow(f.Level, DocumentModel.PageNumber(Editor.Project, Editor.Project.Sheets.Single(s => s.Id == f.SheetId)), f.Message, f)).ToList(); grid.ItemsSource = rows; info.Text = rows.Count == 0 ? "Aucune anomalie correspondant au filtre. Les contrôles portent sur la cohérence du projet." : rows.Count + " anomalie(s) · sélectionner puis Localiser"; }
        void RefreshRows() { findings = Editor.Validate(); Display(); }
        void Follow() { if (grid.SelectedItem is FindingRow row) { var s = Editor.Project.Sheets.Single(s => s.Id == row.Finding.SheetId); var c = s.Components.FirstOrDefault(c => c.Id == row.Finding.ComponentId); NavigateTo(new(s.Id, row.Finding.ComponentId, "", "", "", "", c?.X ?? s.Wires.FirstOrDefault(w=>w.Id==row.Finding.ComponentId)?.Route(s)[0].X ?? s.Width / 2, c?.Y ?? s.Wires.FirstOrDefault(w=>w.Id==row.Finding.ComponentId)?.Route(s)[0].Y ?? s.Height / 2)); dialog.Close(); } }
        grid.AutoGeneratingColumn += (_, e) => { if (e.PropertyName == "Finding") e.Cancel = true; }; query.TextChanged += (_, _) => Display(); grid.MouseDoubleClick += (_, _) => Follow();
        footer.Children.Add(DocButton("Actualiser", dialog, RefreshRows)); footer.Children.Add(DocButton("Localiser", dialog, Follow)); footer.Children.Add(DocButton("Fermer", dialog, dialog.Close)); RefreshRows(); ShowDocumentDialog(dialog);
    }
    private sealed record FindingRow(string Famille, string Folio, string Message, Finding Finding);
}
