using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace GeMMeElec;

public sealed partial class MainWindow : Window
{
    public Editor Editor { get; private set; } = new();
    public SheetCanvas Canvas { get; private set; }
    private readonly ListBox library = new(), pages = new();
    private readonly TextBox search = new();
    private readonly ComboBox family = new(), brand = new(), libraryMode = new();
    private Dictionary<string, SymbolDefinition>? displayedCustomSymbols;
    private int displayedCustomCount;
    private readonly StackPanel properties = new();
    private readonly TextBlock status = new(), counts = new(), mcpStatus = new(), libraryCount = new();
    private readonly Dictionary<string, TextBox> fields = [];
    private bool refreshing;
    private readonly LocalBridge bridge;
    private WorkspacePanels? workspace;
    private int labelTarget;
    public MainWindow(string? file = null)
    {
        Title = "GeMMeElec · Atelier électrique"; Width = Math.Min(1500, SystemParameters.WorkArea.Width * .96); Height = Math.Min(950, SystemParameters.WorkArea.Height * .94); MinWidth = 1000; MinHeight = 600; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new("Segoe UI"); FontSize = 13; UiTheme.Configure(this, preferences.InterfaceTheme);
        Icon = new BitmapImage(new Uri("pack://application:,,,/assets/logo.png"));
        Canvas = new(Editor) { GridVisible = preferences.GridVisible, GridIntensity = preferences.GridIntensity }; Editor.CurrentWireColor = preferences.WireColor;
        UiTheme.Bind(Canvas, SheetCanvas.WorkspaceBackgroundProperty, "Ui.Background");
        var root = UiTheme.Bind(new DockPanel(), Panel.BackgroundProperty, "Ui.Background"); Content = root;
        BuildShell(root); InitializeSessions(root);
        var left = UiTheme.Bind(new DockPanel(), Panel.BackgroundProperty, "Ui.Surface");
        var leftTop = new StackPanel { Margin = new(14, 4, 14, 8) };
        search.ToolTip = "Rechercher un équipement, une marque ou une gamme"; search.Margin = new(0, 10, 0, 8); search.TextChanged += (_, _) => Filter(); leftTop.Children.Add(search);
        libraryMode.ItemsSource = new[] { "Symboles industriels", "GeMMe fabricant · dessins officiels", "Mes symboles", "EPLAN · aperçus officiels", "Favoris et circuits" }; libraryMode.SelectedIndex = 0; libraryMode.Margin = new(0, 0, 0, 8); leftTop.Children.Add(libraryMode);
        family.Margin = new(0, 0, 0, 8); family.SelectionChanged += (_, _) => Filter(); leftTop.Children.Add(family);
        brand.Items.Add("Tous les fabricants"); foreach (var b in Catalog.Entries.Where(e=>e.Library=="manufacturer").Select(e => e.Brand).Distinct().Order()) brand.Items.Add(b); brand.SelectedIndex = 0; brand.Margin = new(0, 0, 0, 8); brand.SelectionChanged += (_, _) => Filter(); leftTop.Children.Add(brand);
        libraryMode.SelectionChanged += (_, _) => LibraryCategories(); LibraryCategories();
        UiTheme.Bind(libraryCount, TextBlock.ForegroundProperty, "Ui.Muted"); libraryCount.FontSize = 11; leftTop.Children.Add(libraryCount); DockPanel.SetDock(leftTop, Dock.Top); left.Children.Add(leftTop);
        var pagePanel = new DockPanel { Margin = new(10, 4, 10, 8) }; var addPage = Button("+ Ajouter un folio", AddPage); DockPanel.SetDock(addPage, Dock.Bottom); pagePanel.Children.Add(addPage);
        pages.DisplayMemberPath = "Display"; pages.SelectionChanged += (_, _) => { if (!refreshing && pages.SelectedItem is Sheet s) { Canvas.CancelDrag(); Editor.SelectSheet(s.Id); Inspect(); } }; pagePanel.Children.Add(pages);
        library.BorderThickness = new(0); library.Margin = new(8, 0, 8, 0); ScrollViewer.SetHorizontalScrollBarVisibility(library, ScrollBarVisibility.Disabled); library.SelectionChanged += (_, _) => { if (library.SelectedItem is ListBoxItem item && item.Tag is string id) Run(() => { if(id.StartsWith("personal:"))SelectPersonalEntry(id[9..]);else SelectLibrary(id); }); }; left.Children.Add(library);
        var right = UiTheme.Bind(new ScrollViewer { Content = properties, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Control.BackgroundProperty, "Ui.Surface"); properties.Margin = new(14, 0, 14, 16);
        workspace = new WorkspacePanels(Canvas, left, pagePanel, right); root.Children.Add(workspace);
        PreviewKeyDown += Keys;
        Closing += (_, e) => { if (exportInProgress||recoveryWriting) { e.Cancel = true; status.Text = "Export ou copie en cours · fermer après sa fin."; return; } Canvas.CancelDrag(); if (!CanCloseAllProjects()) e.Cancel = true; }; Closed += (_, _) => { recoveryTimer.Stop();bridge?.Dispose(); };
        bridge = new LocalBridge(this); Run(ReloadPersonalLibrary); Filter(); Refresh(); Canvas.SetMode("select");
        if (file is not null) Run(() => Editor.Open(file));
        StartRecovery();
    }
    private static TextBlock Heading(string text) => UiTheme.Bind(new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }, TextBlock.ForegroundProperty, "Ui.Muted");
    private Button Button(string text, Action action) { var b = new Button { Content = text }; b.Click += (_, _) => Run(action); return b; }
    public void Run(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "GeMMeElec", MessageBoxButton.OK, MessageBoxImage.Information); } }
    private void LibraryCategories()
    {
        family.Visibility = libraryMode.SelectedIndex == 2 ? Visibility.Collapsed : Visibility.Visible;
        family.Items.Clear(); family.Items.Add("Toutes les familles");
        Canvas.SetMode("select"); Canvas.CatalogId = null;
        foreach (var category in libraryMode.SelectedIndex == 4 ? personalEntries.Select(e=>e.Category).Distinct().Order() : libraryMode.SelectedIndex is 1 or 3 ? Catalog.Entries.Where(e=>e.Library==(libraryMode.SelectedIndex==1?"manufacturer":"eplan")).Select(e => e.Family).Distinct().Order() : Symbols.All.Values.Where(s=>!s.Id.StartsWith("official-")).Select(s => s.Category).Distinct().Order()) family.Items.Add(category);
        brand.Visibility = libraryMode.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; family.SelectedIndex = 0; Filter();
    }
    private void Filter()
    {
        if (family.SelectedItem is null || brand.SelectedItem is null) return;
        library.Items.Clear(); var f = family.SelectedIndex > 0 ? (string)family.SelectedItem : "";
        if(libraryMode.SelectedIndex==4) foreach(var entry in personalEntries.Where(e=>(f.Length==0||e.Category==f)&&Symbols.SearchKey(e.Name+" "+e.Category+" "+e.Tags).Contains(Symbols.SearchKey(search.Text)))) PersonalLibraryItem(entry);
        else if (libraryMode.SelectedIndex is 1 or 3) foreach (var e in Catalog.Search(search.Text, f, libraryMode.SelectedIndex==1 && brand.SelectedIndex > 0 ? (string)brand.SelectedItem : "", libraryMode.SelectedIndex==1?"manufacturer":"eplan")) OfficialLibraryItem(e);
        else if (libraryMode.SelectedIndex == 2) foreach (var s in Editor.Project.CustomSymbols.Values.Where(s => Symbols.SearchKey(s.Name).Contains(Symbols.SearchKey(search.Text)))) LibraryItem(s.Id, s.Name, s.Category, "Copie enregistrée dans ce projet · modifier ou exporter depuis Insérer");
        else foreach (var s in Symbols.Search(search.Text, f).Where(s=>!s.Id.StartsWith("official-"))) LibraryItem(s.Id, s.Name, s.Category, "Symbole fonctionnel générique GeMMeElec · cliquer puis placer · R pour tourner");
        libraryCount.Text = $"{library.Items.Count} résultats · " + (libraryMode.SelectedIndex == 3 ? "double-clic : aperçu et source" : "cliquer puis placer");
        if (libraryMode.SelectedIndex == 2) { displayedCustomSymbols = Editor.Project.CustomSymbols; displayedCustomCount = displayedCustomSymbols.Count; }
    }
    private void LibraryItem(string id, string name, string detail, string tip)
    {
        var row = new DockPanel { Margin = new(3, 6, 3, 6) }; var symbol = Catalog.Entries.FirstOrDefault(e => e.Id == id)?.Symbol ?? id; row.Children.Add(new Border { Background = Brushes.White, CornerRadius = new(3), Child = new SymbolPreview(symbol) { Definition = CustomSymbols.Resolve(Editor.Project, symbol) } });
        var stack = new StackPanel { Margin = new(7, 0, 0, 0) }; stack.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap }); stack.Children.Add(UiTheme.Bind(new TextBlock { Text = detail, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new(0, 3, 0, 0) }, TextBlock.ForegroundProperty, "Ui.Muted")); row.Children.Add(stack);
        var item = new ListBoxItem { Tag = id, Content = row, ToolTip = tip, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var context = new ContextMenu();
        context.Items.Add(QuickAction("Ajouter aux favoris…", () => FavoriteSymbol(symbol)));
        context.Items.Add(QuickAction("Placer sur le folio", () => { var entry = Catalog.Entries.FirstOrDefault(e => e.Id == id); Canvas.Symbol = symbol; Canvas.CatalogId = entry?.Id; Canvas.SetMode("place"); }));
        if (symbol is not ("junction" or "folio_next" or "folio_previous")) context.Items.Add(QuickAction("Modifier en copie…", () => EditSymbol(CustomSymbols.Resolve(Editor.Project, symbol))));
        item.ContextMenu = context;
        // A second click resumes placement after wiring or Escape, even if selection did not change.
        item.PreviewMouseLeftButtonDown += (_, _) => { if (item.IsSelected) { var entry = Catalog.Entries.FirstOrDefault(c => c.Id == id); Canvas.Symbol = entry?.Symbol ?? id; Canvas.CatalogId = entry?.Id; Canvas.SetMode("place"); } };
        library.Items.Add(item);
    }
    private void Refresh()
    {
        if (editingProperty) { UpdateProjectCaption(); Title = $"{(Editor.Dirty ? "● " : "")}{Editor.Project.Title} — GeMMeElec"; Canvas.InvalidateVisual(); return; }
        if (!Symbols.All.ContainsKey(Canvas.Symbol) && !Editor.Project.CustomSymbols.ContainsKey(Canvas.Symbol)) { Canvas.Symbol = "breaker1"; Canvas.CatalogId = null; }
        if (libraryMode.SelectedIndex == 2 && (!ReferenceEquals(displayedCustomSymbols, Editor.Project.CustomSymbols) || displayedCustomCount != Editor.Project.CustomSymbols.Count)) Filter();
        refreshing = true; pages.ItemsSource = null; pages.ItemsSource = Editor.Project.Sheets; pages.SelectedItem = Editor.Sheet; refreshing = false;
        Title = $"{(Editor.Dirty ? "● " : "")}{Editor.Project.Title} — GeMMeElec";
        counts.Text = $"{Editor.Sheet.Components.Count} symboles   {Editor.Sheet.Wires.Count} conducteurs   |   Folio {Editor.Project.Sheets.IndexOf(Editor.Sheet) + 1}";
        UpdateProjectCaption(); CommandManager.InvalidateRequerySuggested(); Inspect(); Canvas.InvalidateVisual();
    }
    private void UpdateProjectCaption() => projectCaption.Text = Editor.Project.Title + (Editor.Dirty ? " · modifications non enregistrées" : "");
    private static string Number(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    private static double ReadNumber(string value)
    {
        if (!double.TryParse(value.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) throw new ArgumentException("Saisir une coordonnée numérique valide (exemple : 12,5).");
        return number;
    }
    private static List<Position> ParsePoints(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => { var xy = line.Split(';'); if (xy.Length != 2) throw new ArgumentException("Chaque point doit avoir la forme X ; Y."); return new Position(ReadNumber(xy[0]), ReadNumber(xy[1])); }).ToList();
    private void TextPositionEditor(Component component)
    {
        PropertyContents.Children.Add(Button("Position des textes…", () => TextPositionDialog(component.Id)));
    }
    private string? Prompt(string title, string label, string value)
    {
        var dialog = DocumentWindow(title, 440, 250); dialog.SizeToContent = SizeToContent.Height; dialog.ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new(22) }; panel.Children.Add(new TextBlock { Text = label }); var input = new TextBox { Text = value, Margin = new(0, 12, 0, 12), Padding = new(8) }; panel.Children.Add(input); var ok = new Button { Content = "Valider", IsDefault = true, Padding = new(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Right }; ok.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = true; }; panel.Children.Add(ok); dialog.Content = panel; dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); }; return dialog.ShowDialog() == true ? input.Text : null;
    }
    private bool CanReplace()
    {
        if (!Editor.Dirty) return true;
        var result = MessageBox.Show(this, "Enregistrer les modifications du projet ?", "GeMMeElec", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (result == MessageBoxResult.Cancel) return false;
        if (result == MessageBoxResult.Yes) { try { return Save(false); } catch (Exception ex) { MessageBox.Show(this, "Enregistrement échoué. Le projet reste ouvert et ses modifications sont conservées.\n\n" + ex.Message, "GeMMeElec", MessageBoxButton.OK, MessageBoxImage.Error); return false; } }
        return true;
    }
    private void NewProject() { Canvas.CancelDrag(); var title = Prompt("Nouveau projet", "Nom du projet", "Nouveau projet"); if (title is not null) AddProjectTab(title: title); }
    private void Open() { Canvas.CancelDrag(); var dialog = new OpenFileDialog { Filter = "Projet GeMMeElec|*.gemelec" }; if (dialog.ShowDialog(this) == true) AddProjectTab(path: dialog.FileName); }
    private bool Save(bool saveAs)
    {
        Canvas.CancelDrag(); var path = Editor.FilePath;
        if (saveAs || path is null) { var dialog = new SaveFileDialog { Filter = "Projet GeMMeElec|*.gemelec", DefaultExt = ".gemelec", FileName = path is null ? "Projet.gemelec" : Path.GetFileName(path) }; if (dialog.ShowDialog(this) != true) return false; path = dialog.FileName; }
        CheckProjectSavePath(path); Editor.Save(path); status.Text = "Projet enregistré · " + path; return true;
    }
    private void ProjectInfo() => MetadataDialog(false);
    private void AddPage() { var title = Prompt("Ajouter un folio", "Titre", $"Folio {Editor.Project.Sheets.Count + 1}"); if (title is not null) Editor.AddSheet(title); }
    private void RenamePage() { var name = Prompt("Renommer le folio", "Titre", Editor.Sheet.Title); if (name is not null) Editor.Change(() => Editor.Sheet.Title = name); }
    private void DeletePage() { if (Editor.Project.Sheets.Count == 1) throw new InvalidOperationException("Conserver au moins un folio."); if (MessageBox.Show(this, "Supprimer ce folio et son contenu ? Cette opération peut être annulée.", "Folio", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { Editor.DeleteSheet(Editor.Sheet.Id); } }
    private void Delete() { Editor.Delete(Canvas.Selected); Canvas.Selected.Clear(); Inspect(); }
    private void Rotate() => Editor.Change(() => { foreach (var c in Editor.Sheet.Components.Where(c => Canvas.Selected.Contains(c.Id))) c.Rotation = (c.Rotation + 90) % 360;foreach(var n in Editor.Sheet.Notes.Where(n=>Canvas.Selected.Contains(n.Id)))n.Rotation=(n.Rotation+90)%360; });
    private void Duplicate()
    {
        Canvas.CancelDrag();
        var originals = Editor.Sheet.Components.Where(c => Canvas.Selected.Contains(c.Id)).ToList();
        var notes = Editor.Sheet.Notes.Where(n => Canvas.Selected.Contains(n.Id)).ToList();var graphics=Editor.Sheet.Graphics.Where(g=>Canvas.Selected.Contains(g.Id)).ToList();
        if (originals.Count == 0 && notes.Count == 0&&graphics.Count==0) return;
        var map = originals.ToDictionary(c => c.Id, _ => Guid.NewGuid().ToString("N"));
        var wires = Editor.Sheet.Wires.Where(w => map.ContainsKey(w.From.ComponentId) && map.ContainsKey(w.To.ComponentId)).ToList();
        var graphicPoints=graphics.SelectMany(g=>new[]{g.Bounds().TopLeft,g.Bounds().BottomRight});
        var positions = originals.Select(c => new Point(c.X, c.Y)).Concat(notes.Select(n => new Point(n.X, n.Y))).Concat(wires.SelectMany(w => w.Waypoints).Select(p => p.Point)).Concat(graphicPoints).ToList();
        double dx = Math.Min(40, Editor.Sheet.Width - positions.Max(p => p.X)), dy = Math.Min(40, Editor.Sheet.Height - positions.Max(p => p.Y));
        T Copy<T>(T value) => System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(value, ProjectStore.Json), ProjectStore.Json)!;
        List<string> ids = [];
        Editor.Change(() =>
        {
            foreach (var c in originals) { var copy = Copy(c); copy.Id = map[c.Id]; copy.X += dx; copy.Y += dy; copy.ResolvedDefinition = CustomSymbols.Resolve(Editor.Project, copy.Symbol); copy.Tag = Editor.NextTag(copy.Definition.Prefix); if (CrossReferences.IsPoint(copy)) { copy.LinkName = ""; copy.LinkSource = false; } Editor.Sheet.Components.Add(copy); ids.Add(copy.Id); }
            foreach (var part in Editor.Sheet.Components.Where(c => ids.Contains(c.Id) && c.CoilId.Length > 0)) { part.CoilId = map.GetValueOrDefault(part.CoilId, part.CoilId); part.Tag = Editor.Project.Sheets.SelectMany(s => s.Components).Single(c => c.Id == part.CoilId).Tag; }
            foreach (var n in notes) { var copy = Copy(n); copy.Id = Guid.NewGuid().ToString("N"); copy.X += dx; copy.Y += dy; Editor.Sheet.Notes.Add(copy); ids.Add(copy.Id); }
            foreach(var g in graphics){var copy=Copy(g);copy.Id=Guid.NewGuid().ToString("N");copy.X+=dx;copy.Y+=dy;Editor.Sheet.Graphics.Add(copy);ids.Add(copy.Id);}
            foreach (var w in wires) { var copy = Copy(w); copy.Id = Guid.NewGuid().ToString("N"); copy.From = new(map[w.From.ComponentId], w.From.PortId); copy.To = new(map[w.To.ComponentId], w.To.PortId); copy.Waypoints = w.Waypoints.Select(p => new Position(p.X + dx, p.Y + dy)).ToList(); Editor.Sheet.Wires.Add(copy); ids.Add(copy.Id); }
        });
        Canvas.Selected.Clear(); foreach (var id in ids) Canvas.Selected.Add(id); Inspect(); Canvas.InvalidateVisual();
    }
    private void Keys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Canvas.SetMode("select"); Canvas.Focus(); return; }
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool propertyInput = e.OriginalSource is DependencyObject source && IsPropertyInput(source);
        if (e.OriginalSource is TextBox && !(ctrl && (e.Key is Key.S or Key.O || propertyInput && e.Key is Key.Z or Key.Y))) return;
        string? id = ctrl ? e.Key switch { Key.N => "new", Key.O => "open", Key.F => "projectSearch", Key.S => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? "saveAs" : "save", Key.Z => "undo", Key.Y => "redo", Key.D => "duplicate", _ => null } : e.Key switch { Key.Delete => "delete", Key.R => "rotate", Key.F => "fit", Key.V => "select", Key.W => "wire", Key.T => "text", _ => null };
        if (id is not null) { e.Handled = true; var command = commands[id].Command; if (command.CanExecute(null, this)) command.Execute(null, this); }
    }
    private void Validate() => FindingsDialog();
    private void Bom() => BomDialog();
    private async void ExportFile(string format)
    {
        try {
            var dialog = new SaveFileDialog { Filter = $"{format.ToUpperInvariant()}|*.{format}", FileName = "Schema." + format }; if (dialog.ShowDialog(this) != true) return;
            if (format == "pdf") await ExportPdfAsync(dialog.FileName);
            else { Canvas.CancelDrag(); if (format == "svg") Export.Svg(Editor.Project, Editor.Sheet, dialog.FileName); else Export.Csv(Editor.Project, dialog.FileName); status.Text = "Export créé · " + dialog.FileName; }
        } catch (Exception ex) { status.Text = "Export échoué · " + ex.Message; MessageBox.Show(this, ex.Message, "Export GeMMeElec", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
    private void Help() => MessageBox.Show(this, "Bibliothèque : cliquez un équipement, puis cliquez sur le folio. Échap termine la pose.\n\nV : sélectionner · W : conducteur · T : texte · R : rotation · F : ajuster\nGlisser dans le vide : sélection par rectangle · Ctrl : ajouter\nClic droit : commandes rapides · Suppr : supprimer · Ctrl+D : dupliquer\nMolette : zoom · Bouton central : déplacer la vue\nCtrl+S : enregistrer · Ctrl+Z / Ctrl+Y : annuler / rétablir\n\nCâbler : tirez depuis une borne vers une autre, même en mode Sélection. W permet aussi de cliquer les bornes et les points de passage.\nFil sélectionné : glissez un segment ou sa poignée carrée pour ajuster le tracé ; glissez une poignée ronde pour changer de borne.\nComposant sélectionné : Propriétés → Position des textes pour glisser les textes dans l’aperçu, choisir un côté ou une icône d’alignement et saisir des coordonnées précises.\nPanneaux : ● / ○ pour épingler ou masquer automatiquement, × pour fermer ; menu Affichage pour les retrouver.\n\nLes connexions sont attachées aux bornes et suivent les déplacements. Pour une dérivation, tirez une borne sur le trajet d’un fil : un point de jonction est créé. Les simples croisements restent sans connexion.\n\nLes gammes fabricants sont des aides à la sélection. Les symboles sont fonctionnels ; vérifiez le bornage de la référence retenue. La vérification ne calcule pas les protections ni la conformité électrique.", "Utiliser GeMMeElec");
    private void McpHelp() => MessageBox.Show(this, "Le serveur MCP fait partie du même exécutable.\n\nCommande :\n" + Environment.ProcessPath + " --mcp\n\nTransport stdio. Gardez l’application ouverte. Le serveur rejoint cette fenêtre par un canal Windows limité à votre compte. Les modifications sont annulables avec Ctrl+Z.\n\nVoir docs/MCP.md dans le dépôt pour la configuration du client.", "MCP GeMMeElec");
    public void McpActivity(string tool) { mcpStatus.Text = "● MCP · " + tool; status.Text = "Commande MCP exécutée : " + tool; }
    internal void DisposeTestBridge() { recoveryTimer.Stop();bridge.Dispose(); }
    private void Demo()
    {
        if (!CanReplace()) return; Editor.New("Exemple · départ moteur triphasé"); Examples.Motor(Editor); Canvas.Fit();
    }
}

public static class Examples
{
    public static void Motor(Editor e)
    {
        e.Change(() => { e.Project.Author = "GeMMeElec"; e.Sheet.Title = "Puissance · exemple à compléter"; });
        var q = e.AddComponent("motorbreaker", 400, 230); var k = e.AddComponent("contactor", 400, 430); var m = e.AddComponent("motor", 400, 690);
        var source = new List<Component>(); for (int i = 0; i < 3; i++) { var c = e.AddComponent("source", 360 + i * 40, 90); c.Tag = "L" + (i + 1); source.Add(c); e.Connect(new(c.Id, "1"), new(q.Id, (2 * i + 1).ToString())); e.Connect(new(q.Id, (2 * i + 2).ToString()), new(k.Id, (2 * i + 1).ToString())); e.Connect(new(k.Id, (2 * i + 2).ToString()), new(m.Id, new[] { "U1", "V1", "W1" }[i])); }
        var pe = e.AddComponent("earth", 600, 780); e.Connect(new(m.Id, "PE"), new(pe.Id, "PE"));
        var coil = e.AddComponent("coil", 940, 620); e.Change(() => { coil.Tag = "KM1-A"; e.Sheet.Notes.Add(new() { X = 790, Y = 130, Text = "COMMANDE À COMPLÉTER" }); e.Sheet.Notes.Add(new() { X = 790, Y = 170, Text = "Ajouter arrêt, marche et auto-maintien." }); });
    }
}
