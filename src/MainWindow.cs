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
    public Editor Editor { get; } = new();
    public SheetCanvas Canvas { get; }
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
        Background = Drawing.Brush("#F5F7FA"); FontFamily = new("Segoe UI"); FontSize = 13; Foreground = Drawing.Brush("#203348");
        Icon = new BitmapImage(new Uri("pack://application:,,,/assets/logo.png"));
        Resources.Add(typeof(Button), ButtonStyle()); Resources.Add(typeof(TextBox), TextBoxStyle());
        Canvas = new(Editor) { GridVisible = preferences.GridVisible, GridIntensity = preferences.GridIntensity }; Editor.CurrentWireColor = preferences.WireColor;
        var root = new DockPanel(); Content = root;
        var header = new DockPanel { Background = Drawing.Brush("#152B40"), LastChildFill = true, Height = 64 };
        var logo = new Image { Source = Icon, Width = 38, Height = 38, Margin = new(20, 0, 14, 0) }; header.Children.Add(logo);
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "GeMMeElec", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = "ATELIER DE SCHÉMAS ÉLECTRIQUES", Foreground = Drawing.Brush("#AFC5D9"), FontSize = 10 });
        mcpStatus.Text = "● MCP local disponible"; mcpStatus.Foreground = Drawing.Brush("#82D5CB"); mcpStatus.VerticalAlignment = VerticalAlignment.Center; mcpStatus.Margin = new(16); DockPanel.SetDock(mcpStatus, Dock.Right); header.Children.Add(mcpStatus); header.Children.Add(title);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var menu = new Menu { Background = Brushes.White, Padding = new(12, 3, 12, 3) };
        AddMenu(menu, "_Fichier", ("Nouveau projet…", NewProject), ("Ouvrir…", Open), ("Enregistrer", () => Save(false)), ("Enregistrer sous…", () => Save(true)), ("Informations du projet…", ProjectInfo), ("Exemple de départ moteur", Demo), ("Quitter", Close));
        AddMenu(menu, "_Édition", ("Annuler    Ctrl+Z", Editor.Undo), ("Rétablir    Ctrl+Y", Editor.Redo), ("Dupliquer    Ctrl+D", Duplicate), ("Rotation 90°    R", Rotate), ("Supprimer    Suppr", Delete));
        AddMenu(menu, "_Folio", ("Ajouter…", AddPage), ("Propriétés du folio…", SheetInfo), ("Cartouches…", TitleBlockEditor), ("Repérage lignes / colonnes…", ZonesDialog), ("Monter dans le projet", () => MovePage(-1)), ("Descendre dans le projet", () => MovePage(1)), ("Renommer…", RenamePage), ("Supprimer le folio", DeletePage));
        AddMenu(menu, "_Insérer", ("Conducteur    W", () => Canvas.SetMode("wire")), ("Texte    T", () => Canvas.SetMode("text")), ("Sélection    V", () => Canvas.SetMode("select")), ("Nouveau symbole…", NewSymbol), ("Modifier un symbole en copie…", SymbolEditor), ("Importer un symbole DXF / GeMMeElec…", ImportSymbol));
        AddMenu(menu, "_Importer", ("Projet QElectroTech (.qet)…", () => ImportDrawingProject(false)), ("Folios EPLAN / CAO exportés en DXF…", () => ImportDrawingProject(true)));
        AddMenu(menu, "_Projet", ("Vérifier les connexions et les repères", Validate), ("Cartouches…", TitleBlockEditor), ("Nomenclature configurable…", Bom), ("Navigateur des renvois…", ReferenceNavigator));
        AddMenu(menu, "E_xporter", ("Tous les folios en PDF…", () => ExportFile("pdf")), ("Folio courant en SVG…", () => ExportFile("svg")), ("Nomenclature CSV…", () => ExportFile("csv")));
        AddMenu(menu, "_Affichage", ("Ajuster le folio    F", Canvas.Fit), ("Grille du plan…", GridDialog), ("Palette des conducteurs…", () => PaletteDialog()), ("Bibliothèque", () => workspace?.Show("library")), ("Propriétés", () => workspace?.Show("properties")), ("Folios", () => workspace?.Show("pages")), ("Rétablir les panneaux", () => workspace?.Reset()));
        AddMenu(menu, "_Aide", ("Raccourcis et utilisation", Help), ("Connexion MCP", McpHelp), ("Licence du lecteur DXF", () => { using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("netDxf-LICENSE.txt")!; using var reader = new System.IO.StreamReader(stream); MessageBox.Show(this, reader.ReadToEnd(), "netDxf 3.0.1 · licence MIT"); }));
        DockPanel.SetDock(menu, Dock.Top); root.Children.Add(menu);
        var toolbar = new WrapPanel { Background = Brushes.White, Margin = new(0, 1, 0, 1) };
        foreach (var (label, action) in new (string, Action)[] { ("Nouveau", NewProject), ("Ouvrir", Open), ("Enregistrer", () => Save(false)), ("↶", Editor.Undo), ("↷", Editor.Redo), ("Sélection  V", () => Canvas.SetMode("select")), ("Conducteur  W", () => Canvas.SetMode("wire")), ("Texte  T", () => Canvas.SetMode("text")), ("Rotation  R", Rotate), ("Ajuster  F", Canvas.Fit), ("Vérifier", Validate), ("PDF", () => ExportFile("pdf")) }) toolbar.Children.Add(Button(label, action));
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        var footer = new DockPanel { Height = 34, Background = Drawing.Brush("#F8FAFC") }; status.Margin = new(14, 7, 6, 0); counts.Margin = new(10, 7, 16, 0); counts.Foreground = Drawing.Brush("#64748B"); DockPanel.SetDock(counts, Dock.Right); footer.Children.Add(counts); footer.Children.Add(status); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var left = new DockPanel { Background = Brushes.White };
        var leftTop = new StackPanel { Margin = new(14, 4, 14, 8) };
        search.ToolTip = "Rechercher un équipement, une marque ou une gamme"; search.Margin = new(0, 10, 0, 8); search.TextChanged += (_, _) => Filter(); leftTop.Children.Add(search);
        libraryMode.ItemsSource = new[] { "Symboles industriels", "Gammes fabricants", "Mes symboles" }; libraryMode.SelectedIndex = 0; libraryMode.Margin = new(0, 0, 0, 8); leftTop.Children.Add(libraryMode);
        family.Margin = new(0, 0, 0, 8); family.SelectionChanged += (_, _) => Filter(); leftTop.Children.Add(family);
        brand.Items.Add("Tous les fabricants"); foreach (var b in Catalog.Entries.Select(e => e.Brand).Distinct().Order()) brand.Items.Add(b); brand.SelectedIndex = 0; brand.Margin = new(0, 0, 0, 8); brand.SelectionChanged += (_, _) => Filter(); leftTop.Children.Add(brand);
        libraryMode.SelectionChanged += (_, _) => LibraryCategories(); LibraryCategories();
        libraryCount.Foreground = Drawing.Brush("#64748B"); libraryCount.FontSize = 11; leftTop.Children.Add(libraryCount); DockPanel.SetDock(leftTop, Dock.Top); left.Children.Add(leftTop);
        var pagePanel = new DockPanel { Margin = new(10, 4, 10, 8) }; var addPage = Button("+ Ajouter un folio", AddPage); DockPanel.SetDock(addPage, Dock.Bottom); pagePanel.Children.Add(addPage);
        pages.DisplayMemberPath = "Display"; pages.SelectionChanged += (_, _) => { if (!refreshing && pages.SelectedItem is Sheet s) { Canvas.CancelDrag(); Editor.SelectSheet(s.Id); Inspect(); } }; pagePanel.Children.Add(pages);
        library.BorderThickness = new(0); library.Margin = new(8, 0, 8, 0); ScrollViewer.SetHorizontalScrollBarVisibility(library, ScrollBarVisibility.Disabled); library.SelectionChanged += (_, _) => { if (library.SelectedItem is ListBoxItem item && item.Tag is string id) { var e = Catalog.Entries.FirstOrDefault(c => c.Id == id); Canvas.Symbol = e?.Symbol ?? id; Canvas.CatalogId = e?.Id; Canvas.SetMode("place"); } }; left.Children.Add(library);
        var right = new ScrollViewer { Content = properties, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.White }; properties.Margin = new(14);
        workspace = new WorkspacePanels(Canvas, left, pagePanel, right); root.Children.Add(workspace);
        Canvas.ContextRequested += ShowSchematicMenu;
        Canvas.ReferenceRequested += FollowReference; Canvas.Status += text => status.Text = text; Canvas.SelectionChanged += Inspect; Canvas.TextRequested += p => { var text = Prompt("Texte du schéma", "Texte", "Texte"); if (text is not null) Editor.Change(() => Editor.Sheet.Notes.Add(new() { X = Editor.Snap(p.X), Y = Editor.Snap(p.Y), Text = text })); };
        Editor.Changed += Refresh; PreviewKeyDown += Keys;
        Closing += (_, e) => { Canvas.CancelDrag(); if (!CanReplace()) e.Cancel = true; }; Closed += (_, _) => bridge?.Dispose();
        bridge = new LocalBridge(this); Filter(); Refresh(); Canvas.SetMode("select");
        if (file is not null) Run(() => Editor.Open(file));
    }
    private static Style ButtonStyle() { var s = new Style(typeof(Button)); s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7))); s.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(3))); s.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White)); s.Setters.Add(new Setter(Control.BorderBrushProperty, Drawing.Brush("#D5DFE9"))); s.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand)); return s; }
    private static Style TextBoxStyle() { var s = new Style(typeof(TextBox)); s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(7))); s.Setters.Add(new Setter(Control.BorderBrushProperty, Drawing.Brush("#D5DFE9"))); return s; }
    private static TextBlock Heading(string text) => new() { Text = text, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Drawing.Brush("#61758A"), VerticalAlignment = VerticalAlignment.Center };
    private Button Button(string text, Action action) { var b = new Button { Content = text }; b.Click += (_, _) => Run(action); return b; }
    public void Run(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "GeMMeElec", MessageBoxButton.OK, MessageBoxImage.Information); } }
    private void AddMenu(Menu menu, string label, params (string, Action)[] actions) { var m = new MenuItem { Header = label }; foreach (var (text, action) in actions) { var item = new MenuItem { Header = text }; item.Click += (_, _) => Run(action); m.Items.Add(item); } menu.Items.Add(m); }
    private void LibraryCategories()
    {
        family.Visibility = libraryMode.SelectedIndex == 2 ? Visibility.Collapsed : Visibility.Visible;
        family.Items.Clear(); family.Items.Add("Toutes les familles");
        foreach (var category in libraryMode.SelectedIndex == 1 ? Catalog.Entries.Select(e => e.Family).Distinct().Order() : Symbols.All.Values.Select(s => s.Category).Distinct().Order()) family.Items.Add(category);
        brand.Visibility = libraryMode.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; family.SelectedIndex = 0; Filter();
    }
    private void Filter()
    {
        if (family.SelectedItem is null || brand.SelectedItem is null) return;
        library.Items.Clear(); var f = family.SelectedIndex > 0 ? (string)family.SelectedItem : "";
        if (libraryMode.SelectedIndex == 1) foreach (var e in Catalog.Search(search.Text, f, brand.SelectedIndex > 0 ? (string)brand.SelectedItem : "")) LibraryItem(e.Id, e.Range, e.Brand + " · " + e.Family, e.Note);
        else if (libraryMode.SelectedIndex == 2) foreach (var s in Editor.Project.CustomSymbols.Values.Where(s => Symbols.SearchKey(s.Name).Contains(Symbols.SearchKey(search.Text)))) LibraryItem(s.Id, s.Name, s.Category, "Copie enregistrée dans ce projet · modifier ou exporter depuis Insérer");
        else foreach (var s in Symbols.Search(search.Text, f)) LibraryItem(s.Id, s.Name, s.Category, "Symbole fonctionnel · cliquer puis placer · R pour tourner");
        libraryCount.Text = $"{library.Items.Count} résultats · cliquer puis placer";
        if (libraryMode.SelectedIndex == 2) { displayedCustomSymbols = Editor.Project.CustomSymbols; displayedCustomCount = displayedCustomSymbols.Count; }
    }
    private void LibraryItem(string id, string name, string detail, string tip)
    {
        var row = new DockPanel { Margin = new(3, 6, 3, 6) }; var symbol = Catalog.Entries.FirstOrDefault(e => e.Id == id)?.Symbol ?? id; row.Children.Add(new SymbolPreview(symbol) { Definition = CustomSymbols.Resolve(Editor.Project, symbol) });
        var stack = new StackPanel { Margin = new(7, 0, 0, 0) }; stack.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap }); stack.Children.Add(new TextBlock { Text = detail, Foreground = Drawing.Brush("#687D90"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new(0, 3, 0, 0) }); row.Children.Add(stack);
        var item = new ListBoxItem { Tag = id, Content = row, ToolTip = tip, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var context = new ContextMenu();
        context.Items.Add(QuickAction("Placer sur le folio", () => { var entry = Catalog.Entries.FirstOrDefault(e => e.Id == id); Canvas.Symbol = symbol; Canvas.CatalogId = entry?.Id; Canvas.SetMode("place"); }));
        if (symbol is not ("junction" or "folio_next" or "folio_previous")) context.Items.Add(QuickAction("Modifier en copie…", () => EditSymbol(CustomSymbols.Resolve(Editor.Project, symbol))));
        item.ContextMenu = context;
        // A second click resumes placement after wiring or Escape, even if selection did not change.
        item.PreviewMouseLeftButtonDown += (_, _) => { if (item.IsSelected) { var entry = Catalog.Entries.FirstOrDefault(c => c.Id == id); Canvas.Symbol = entry?.Symbol ?? id; Canvas.CatalogId = entry?.Id; Canvas.SetMode("place"); } };
        library.Items.Add(item);
    }
    private void Refresh()
    {
        if (!Symbols.All.ContainsKey(Canvas.Symbol) && !Editor.Project.CustomSymbols.ContainsKey(Canvas.Symbol)) { Canvas.Symbol = "breaker1"; Canvas.CatalogId = null; }
        if (libraryMode.SelectedIndex == 2 && (!ReferenceEquals(displayedCustomSymbols, Editor.Project.CustomSymbols) || displayedCustomCount != Editor.Project.CustomSymbols.Count)) Filter();
        refreshing = true; pages.ItemsSource = null; pages.ItemsSource = Editor.Project.Sheets; pages.SelectedItem = Editor.Sheet; refreshing = false;
        Title = $"{(Editor.Dirty ? "● " : "")}{Editor.Project.Title} — GeMMeElec";
        counts.Text = $"{Editor.Sheet.Components.Count} symboles   {Editor.Sheet.Wires.Count} conducteurs   |   Folio {Editor.Project.Sheets.IndexOf(Editor.Sheet) + 1}";
        Inspect(); Canvas.InvalidateVisual();
    }
    private TextBox Field(string key, string label, string value)
    {
        properties.Children.Add(new TextBlock { Text = label, Margin = new(0, 12, 0, 4), FontSize = 12, Foreground = Drawing.Brush("#61758A") }); var box = new TextBox { Text = value }; fields[key] = box; properties.Children.Add(box); return box;
    }
    private void Inspect()
    {
        properties.Children.Clear(); fields.Clear();
        var c = Editor.Sheet.Components.FirstOrDefault(c => Canvas.Selected.Contains(c.Id));
        var wire = Editor.Sheet.Wires.FirstOrDefault(w => Canvas.Selected.Contains(w.Id));
        var note = Editor.Sheet.Notes.FirstOrDefault(n => Canvas.Selected.Contains(n.Id));
        if (c is not null)
        {
            properties.Children.Add(new TextBlock { Text = c.Definition.Name, FontSize = 18, TextWrapping = TextWrapping.Wrap, Margin = new(0, 14, 0, 4) });
            if (CrossReferences.IsPoint(c)) { ReferenceInspector(c); return; }
            if (c.Symbol != "junction") properties.Children.Add(Button("Modifier le symbole en copie…", SymbolEditor));
            properties.Children.Add(Hint("Emplacement : " + SheetZones.Address(Editor.Project, Editor.Sheet, c)));
            Field("tag", "Repère", c.Tag); Field("description", "Description", c.Description); Field("brand", "Fabricant", c.Brand); Field("range", "Gamme", c.Range); Field("reference", "Référence exacte", c.Reference); Field("rating", "Calibre / tension / puissance", c.Rating);
            properties.Children.Add(Button("Appliquer les propriétés", () => { var values = fields.ToDictionary(x => x.Key, x => x.Value.Text); Editor.Change(() => { c.Tag = values["tag"]; c.Description = values["description"]; c.Brand = values["brand"]; c.Range = values["range"]; c.Reference = values["reference"]; c.Rating = values["rating"]; foreach (var p in c.Definition.Ports) c.PortLabels[p.Id] = values["port:" + p.Id]; }); }));
            if (Uri.TryCreate(c.Source, UriKind.Absolute, out var uri) && uri.Scheme == "https") properties.Children.Add(Button("Documentation fabricant ↗", () => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })));
            TextPositionEditor(c);
            properties.Children.Add(Button(c.ImageId.Length > 0 ? "Remplacer l’image BOM…" : "Ajouter une image BOM…", () => ComponentImage(c.Id)));
            if (c.ImageId.Length > 0) { properties.Children.Add(new Image { Source = ImageAssets.Decode(Editor.Project.Images[c.ImageId]), Height = 90, Margin = new(8), Stretch = Stretch.Uniform }); properties.Children.Add(Button("Retirer l’image BOM", () => Editor.Change(() => c.ImageId = ""))); }
            properties.Children.Add(new TextBlock { Text = "Bornes fonctionnelles : adapter les libellés à la référence choisie.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Drawing.Brush("#7C6B4B"), Margin = new(0, 14, 0, 0) });
            foreach (var p in c.Definition.Ports) Field("port:" + p.Id, "Borne " + p.Id, c.PortLabels.GetValueOrDefault(p.Id, p.Id));
        }
        else if (wire is not null)
        {
            Field("number", "Numéro du conducteur", wire.Number); Field("color", "Couleur (#RRGGBB)", wire.Color);
            properties.Children.Add(Button("Palette de couleurs…", () => PaletteDialog(fields["color"].Text, color => fields["color"].Text = color)));
            properties.Children.Add(Button("Appliquer", () => { string n = fields["number"].Text, color = fields["color"].Text; Editor.Change(() => { wire.Number = n; wire.Color = color; }); }));
            properties.Children.Add(Button("Rétablir le parcours automatique", () => Editor.Change(() => wire.Waypoints.Clear())));
            properties.Children.Add(new TextBlock { Text = "Tirez une poignée carrée pour déplacer un segment. Tirez une poignée ronde vers une borne pour changer le raccordement.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 10), Foreground = Drawing.Brush("#61758A") });
            var points = Field("waypoints", "Points de passage · X ; Y par ligne", string.Join(Environment.NewLine, wire.Waypoints.Select(p => $"{Number(p.X)} ; {Number(p.Y)}"))); points.AcceptsReturn = true; points.Height = 110; points.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            properties.Children.Add(Button("Appliquer le tracé", () => { var values = ParsePoints(points.Text); Editor.Change(() => wire.Waypoints = values); }));
            properties.Children.Add(new TextBlock { Text = "Coordonnées du folio, en unités de dessin. Un champ vide rétablit le tracé automatique.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new(0, 6, 0, 0) });
        }
        else if (note is not null) { Field("text", "Annotation", note.Text); properties.Children.Add(Button("Appliquer", () => { var t = fields["text"].Text; Editor.Change(() => note.Text = t); })); }
        else if (Editor.Sheet.BomPage is not null)
        {
            properties.Children.Add(new TextBlock { Text = "Folio de nomenclature", FontSize = 20, TextWrapping = TextWrapping.Wrap, Margin = new(0, 18, 0, 12) });
            properties.Children.Add(Hint("Ce tableau est enregistré dans le projet et inclus dans l’export PDF, sans cartouche. Après une modification du schéma, ouvrir la nomenclature et cliquer Ajouter / actualiser les folios BOM."));
            properties.Children.Add(Button("Configurer / actualiser la nomenclature", Bom));
            properties.Children.Add(Button("Propriétés du folio", SheetInfo));
        }
        else
        {
            properties.Children.Add(new TextBlock { Text = Editor.Project.Title, FontSize = 22, TextWrapping = TextWrapping.Wrap, Margin = new(0, 18, 0, 12) });
            properties.Children.Add(new TextBlock { Text = "Dessinez votre premier circuit", FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            properties.Children.Add(new TextBlock { Text = "1   Choisissez un équipement dans la bibliothèque.\n\n2   Cliquez sur le folio pour le placer.\n\n3   Activez Conducteur et reliez les bornes.\n\n4   Sélectionnez un appareil pour renseigner sa référence.", TextWrapping = TextWrapping.Wrap, Foreground = Drawing.Brush("#61758A"), Margin = new(0, 14, 0, 20) });
            properties.Children.Add(Button("Informations du projet", ProjectInfo)); properties.Children.Add(Button("Propriétés du folio", SheetInfo)); properties.Children.Add(Button("Cartouches", TitleBlockEditor)); properties.Children.Add(Button("Nomenclature", Bom)); properties.Children.Add(Button("Charger un exemple", Demo));
        }
    }
    private static string Number(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    private static double ReadNumber(string value)
    {
        if (!double.TryParse(value.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) throw new ArgumentException("Saisir une coordonnée numérique valide (exemple : 12,5).");
        return number;
    }
    private static List<Position> ParsePoints(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => { var xy = line.Split(';'); if (xy.Length != 2) throw new ArgumentException("Chaque point doit avoir la forme X ; Y."); return new Position(ReadNumber(xy[0]), ReadNumber(xy[1])); }).ToList();
    private void TextPositionEditor(Component component)
    {
        properties.Children.Insert(1, Button("Position des textes…", () => TextPositionDialog(component.Id)));
    }
    private void TextPositionDialog(string id)
    {
        var dialog = new Window { Owner = this, Title = "Position des textes", Width = 460, Height = Math.Min(720, SystemParameters.WorkArea.Height * .9), WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brushes.White };
        var panel = new StackPanel { Margin = new(20) }; dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        ComboBox Choice(string label, (string Id, string Name)[] values)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new(0, 10, 0, 4) }); var combo = new ComboBox { ItemsSource = values.Select(v => new KeyValuePair<string, string>(v.Id, v.Name)), DisplayMemberPath = "Value", SelectedValuePath = "Key", Padding = new(5) }; panel.Children.Add(combo); return combo;
        }
        TextBox Numeric(string label) { panel.Children.Add(new TextBlock { Text = label, Margin = new(0, 10, 0, 4) }); var box = new TextBox { Padding = new(7) }; panel.Children.Add(box); return box; }
        var target = Choice("Texte à positionner", [("tag", "Repère du composant"), ("detail", "Caractéristiques / référence / gamme"), ("description", "Description")]);
        var visible = new CheckBox { Content = "Afficher ce texte", Margin = new(0, 12, 0, 0) }; panel.Children.Add(visible);
        var side = Choice("Position par rapport au composant", [("top", "Haut"), ("bottom", "Bas"), ("left", "Gauche"), ("right", "Droite"), ("custom", "Coordonnées libres depuis le centre")]);
        var horizontal = Choice("Alignement horizontal du texte", [("left", "Gauche"), ("center", "Centré"), ("right", "Droite")]);
        var vertical = Choice("Alignement vertical du texte", [("top", "Haut"), ("center", "Milieu"), ("bottom", "Bas")]);
        var gap = Numeric("Distance au bord du symbole"); var x = Numeric("Décalage X précis · positif vers la droite"); var y = Numeric("Décalage Y précis · positif vers le bas");
        panel.Children.Add(new TextBlock { Text = "Les décalages sont en unités de dessin, sans arrondi à la grille. En position libre, X et Y partent du centre du composant. Le texte suit ses déplacements et reste lisible après rotation.", TextWrapping = TextWrapping.Wrap, Foreground = Drawing.Brush("#61758A"), FontSize = 11, Margin = new(0, 12, 0, 8) });
        bool loading = false;
        void LoadPlacement()
        {
            loading = true; var c = Editor.Sheet.Components.Single(c => c.Id == id); labelTarget = target.SelectedIndex;
            var p = labelTarget == 0 ? c.TagPlacement : labelTarget == 1 ? c.DetailPlacement : c.DescriptionPlacement;
            visible.IsChecked = p.Visible; side.SelectedValue = p.Side; horizontal.SelectedValue = p.Horizontal; vertical.SelectedValue = p.Vertical; gap.Text = Number(p.Gap); x.Text = Number(p.OffsetX); y.Text = Number(p.OffsetY); loading = false;
        }
        side.SelectionChanged += (_, _) => { if (loading) return; string s = (string)side.SelectedValue; horizontal.SelectedValue = s == "left" ? "right" : s == "right" ? "left" : "center"; vertical.SelectedValue = s == "top" ? "bottom" : s == "bottom" ? "top" : "center"; x.Text = "0"; y.Text = "0"; };
        target.SelectionChanged += (_, _) => LoadPlacement(); target.SelectedIndex = labelTarget;
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 8), Foreground = Drawing.Brush("#007F86") }; panel.Children.Add(feedback);
        var apply = new Button { Content = "Appliquer au schéma", Padding = new(10), IsDefault = true, Margin = new(0, 8, 0, 0) };
        apply.Click += (_, _) =>
        {
            try
            {
                var placement = new LabelPlacement { Side = (string)side.SelectedValue, Horizontal = (string)horizontal.SelectedValue, Vertical = (string)vertical.SelectedValue, Gap = ReadNumber(gap.Text), OffsetX = ReadNumber(x.Text), OffsetY = ReadNumber(y.Text), Visible = visible.IsChecked == true }; placement.Validate();
                var c = Editor.Sheet.Components.Single(c => c.Id == id); Editor.Change(() => { if (labelTarget == 0) c.TagPlacement = placement; else if (labelTarget == 1) c.DetailPlacement = placement; else c.DescriptionPlacement = placement; }); feedback.Text = "Position appliquée. Ctrl+Z permet de l’annuler dans l’éditeur.";
            }
            catch (Exception ex) { feedback.Text = ex.Message; }
        };
        panel.Children.Add(apply); var close = new Button { Content = "Fermer", IsCancel = true, Padding = new(10), Margin = new(0, 6, 0, 0) }; panel.Children.Add(close); dialog.ShowDialog();
    }
    private string? Prompt(string title, string label, string value)
    {
        var dialog = new Window { Owner = this, Title = title, Width = 440, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Brushes.White };
        var panel = new StackPanel { Margin = new(22) }; panel.Children.Add(new TextBlock { Text = label }); var input = new TextBox { Text = value, Margin = new(0, 12, 0, 12), Padding = new(8) }; panel.Children.Add(input); var ok = new Button { Content = "Valider", IsDefault = true, Padding = new(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Right }; ok.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = true; }; panel.Children.Add(ok); dialog.Content = panel; dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); }; return dialog.ShowDialog() == true ? input.Text : null;
    }
    private bool CanReplace()
    {
        if (!Editor.Dirty) return true;
        var result = MessageBox.Show(this, "Enregistrer les modifications du projet ?", "GeMMeElec", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (result == MessageBoxResult.Cancel) return false; if (result == MessageBoxResult.Yes) return Save(false); return true;
    }
    private void NewProject() { Canvas.CancelDrag(); if (!CanReplace()) return; var title = Prompt("Nouveau projet", "Nom du projet", "Nouveau projet"); if (title is not null) Editor.New(title); }
    private void Open() { Canvas.CancelDrag(); if (!CanReplace()) return; var dialog = new OpenFileDialog { Filter = "Projet GeMMeElec|*.gemelec" }; if (dialog.ShowDialog(this) == true) Editor.Open(dialog.FileName); }
    private bool Save(bool saveAs)
    {
        Canvas.CancelDrag(); var path = Editor.FilePath;
        if (saveAs || path is null) { var dialog = new SaveFileDialog { Filter = "Projet GeMMeElec|*.gemelec", DefaultExt = ".gemelec", FileName = path is null ? "Projet.gemelec" : Path.GetFileName(path) }; if (dialog.ShowDialog(this) != true) return false; path = dialog.FileName; }
        Editor.Save(path); status.Text = "Projet enregistré · " + path; return true;
    }
    private void ProjectInfo() => MetadataDialog(false);
    private void AddPage() { var title = Prompt("Ajouter un folio", "Titre", $"Folio {Editor.Project.Sheets.Count + 1}"); if (title is not null) Editor.AddSheet(title); }
    private void RenamePage() { var name = Prompt("Renommer le folio", "Titre", Editor.Sheet.Title); if (name is not null) Editor.Change(() => Editor.Sheet.Title = name); }
    private void DeletePage() { if (Editor.Project.Sheets.Count == 1) throw new InvalidOperationException("Conserver au moins un folio."); if (MessageBox.Show(this, "Supprimer ce folio et son contenu ? Cette opération peut être annulée.", "Folio", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { var id = Editor.Sheet.Id; Editor.Change(() => Editor.Project.Sheets.RemoveAll(s => s.Id == id)); Editor.SelectSheet(Editor.Project.Sheets[0].Id); } }
    private void Delete() { Editor.Delete(Canvas.Selected); Canvas.Selected.Clear(); Inspect(); }
    private void Rotate() => Editor.Change(() => { foreach (var c in Editor.Sheet.Components.Where(c => Canvas.Selected.Contains(c.Id))) c.Rotation = (c.Rotation + 90) % 360; });
    private void Duplicate()
    {
        Canvas.CancelDrag();
        var originals = Editor.Sheet.Components.Where(c => Canvas.Selected.Contains(c.Id)).ToList();
        var notes = Editor.Sheet.Notes.Where(n => Canvas.Selected.Contains(n.Id)).ToList();
        if (originals.Count == 0 && notes.Count == 0) return;
        var map = originals.ToDictionary(c => c.Id, _ => Guid.NewGuid().ToString("N"));
        var wires = Editor.Sheet.Wires.Where(w => map.ContainsKey(w.From.ComponentId) && map.ContainsKey(w.To.ComponentId)).ToList();
        var positions = originals.Select(c => new Point(c.X, c.Y)).Concat(notes.Select(n => new Point(n.X, n.Y))).Concat(wires.SelectMany(w => w.Waypoints).Select(p => p.Point)).ToList();
        double dx = Math.Min(40, Editor.Sheet.Width - positions.Max(p => p.X)), dy = Math.Min(40, Editor.Sheet.Height - positions.Max(p => p.Y));
        T Copy<T>(T value) => System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(value, ProjectStore.Json), ProjectStore.Json)!;
        List<string> ids = [];
        Editor.Change(() =>
        {
            foreach (var c in originals) { var copy = Copy(c); copy.Id = map[c.Id]; copy.X += dx; copy.Y += dy; copy.ResolvedDefinition = CustomSymbols.Resolve(Editor.Project, copy.Symbol); copy.Tag = Editor.NextTag(copy.Definition.Prefix); if (CrossReferences.IsPoint(copy)) { copy.LinkName = ""; copy.LinkSource = false; } Editor.Sheet.Components.Add(copy); ids.Add(copy.Id); }
            foreach (var n in notes) { var copy = Copy(n); copy.Id = Guid.NewGuid().ToString("N"); copy.X += dx; copy.Y += dy; Editor.Sheet.Notes.Add(copy); ids.Add(copy.Id); }
            foreach (var w in wires) { var copy = Copy(w); copy.Id = Guid.NewGuid().ToString("N"); copy.From = new(map[w.From.ComponentId], w.From.PortId); copy.To = new(map[w.To.ComponentId], w.To.PortId); copy.Waypoints = w.Waypoints.Select(p => new Position(p.X + dx, p.Y + dy)).ToList(); Editor.Sheet.Wires.Add(copy); ids.Add(copy.Id); }
        });
        Canvas.Selected.Clear(); foreach (var id in ids) Canvas.Selected.Add(id); Inspect(); Canvas.InvalidateVisual();
    }
    private void Keys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Canvas.SetMode("select"); Canvas.Focus(); return; }
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.OriginalSource is TextBox && !(ctrl && e.Key is Key.S or Key.O)) return;
        Action? action = ctrl ? e.Key switch { Key.N => NewProject, Key.O => Open, Key.S => () => Save(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)), Key.Z => Editor.Undo, Key.Y => Editor.Redo, Key.D => Duplicate, _ => null } : e.Key switch { Key.Delete => Delete, Key.R => Rotate, Key.F => Canvas.Fit, Key.V => () => Canvas.SetMode("select"), Key.W => () => Canvas.SetMode("wire"), Key.T => () => Canvas.SetMode("text"), _ => null };
        if (action is not null) { e.Handled = true; Run(action); }
    }
    private void Validate()
    {
        var findings = Editor.Validate(); var list = new ListBox { Margin = new(16) }; foreach (var f in findings) list.Items.Add(new ListBoxItem { Content = $"{f.Level}  ·  {f.Message}", Tag = f }); if (findings.Count == 0) list.Items.Add("Aucune anomalie de structure ou de repérage détectée.");
        var dialog = new Window { Owner = this, Title = "Vérification du schéma · double-clic pour localiser", Width = 760, Height = 460, Content = list, WindowStartupLocation = WindowStartupLocation.CenterOwner }; list.MouseDoubleClick += (_, _) => { if (list.SelectedItem is ListBoxItem i && i.Tag is Finding f) { Editor.SelectSheet(f.SheetId); Canvas.Select(f.ComponentId); dialog.Close(); } }; dialog.ShowDialog();
    }
    private void Bom() => BomDialog();
    private void ExportFile(string format)
    {
        var dialog = new SaveFileDialog { Filter = $"{format.ToUpperInvariant()}|*.{format}", FileName = "Schema." + format }; if (dialog.ShowDialog(this) != true) return;
        if (format == "pdf") Export.Pdf(Editor.Project, dialog.FileName); else if (format == "svg") Export.Svg(Editor.Project, Editor.Sheet, dialog.FileName); else Export.Csv(Editor.Project, dialog.FileName); status.Text = "Export créé · " + dialog.FileName;
    }
    private void Help() => MessageBox.Show(this, "Bibliothèque : cliquez un équipement, puis cliquez sur le folio. Échap termine la pose.\n\nV : sélectionner · W : conducteur · T : texte · R : rotation · F : ajuster\nGlisser dans le vide : sélection par rectangle · Ctrl : ajouter\nClic droit : commandes rapides · Suppr : supprimer · Ctrl+D : dupliquer\nMolette : zoom · Bouton central : déplacer la vue\nCtrl+S : enregistrer · Ctrl+Z / Ctrl+Y : annuler / rétablir\n\nCâbler : tirez depuis une borne vers une autre, même en mode Sélection. W permet aussi de cliquer les bornes et les points de passage.\nFil sélectionné : glissez un segment ou sa poignée carrée pour ajuster le tracé ; glissez une poignée ronde pour changer de borne.\nComposant sélectionné : Propriétés → Position des textes pour choisir côté, alignement ou coordonnées précises.\nPanneaux : ● / ○ pour épingler ou masquer automatiquement, × pour fermer ; menu Affichage pour les retrouver.\n\nLes connexions sont attachées aux bornes et suivent les déplacements. Pour une dérivation, tirez une borne sur le trajet d’un fil : un point de jonction est créé. Les simples croisements restent sans connexion.\n\nLes gammes fabricants sont des aides à la sélection. Les symboles sont fonctionnels ; vérifiez le bornage de la référence retenue. La vérification ne calcule pas les protections ni la conformité électrique.", "Utiliser GeMMeElec");
    private void McpHelp() => MessageBox.Show(this, "Le serveur MCP fait partie du même exécutable.\n\nCommande :\n" + Environment.ProcessPath + " --mcp\n\nTransport stdio. Gardez l’application ouverte. Le serveur rejoint cette fenêtre par un canal Windows limité à votre compte. Les modifications sont annulables avec Ctrl+Z.\n\nVoir docs/MCP.md dans le dépôt pour la configuration du client.", "MCP GeMMeElec");
    public void McpActivity(string tool) { mcpStatus.Text = "● MCP · " + tool; status.Text = "Commande MCP exécutée : " + tool; }
    internal void DisposeTestBridge() => bridge.Dispose();
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
        var q = e.AddComponent("motorbreaker", 400, 230, "f06-1"); var k = e.AddComponent("contactor", 400, 430, "f07-1"); var m = e.AddComponent("motor", 400, 690, "f22-2");
        var source = new List<Component>(); for (int i = 0; i < 3; i++) { var c = e.AddComponent("source", 360 + i * 40, 90); c.Tag = "L" + (i + 1); source.Add(c); e.Connect(new(c.Id, "1"), new(q.Id, (2 * i + 1).ToString())); e.Connect(new(q.Id, (2 * i + 2).ToString()), new(k.Id, (2 * i + 1).ToString())); e.Connect(new(k.Id, (2 * i + 2).ToString()), new(m.Id, new[] { "U1", "V1", "W1" }[i])); }
        var pe = e.AddComponent("earth", 600, 780); e.Connect(new(m.Id, "PE"), new(pe.Id, "PE"));
        var coil = e.AddComponent("coil", 940, 620); e.Change(() => { coil.Tag = "KM1-A"; e.Sheet.Notes.Add(new() { X = 790, Y = 130, Text = "COMMANDE À COMPLÉTER" }); e.Sheet.Notes.Add(new() { X = 790, Y = 170, Text = "Ajouter arrêt, marche et auto-maintien." }); });
    }
}
