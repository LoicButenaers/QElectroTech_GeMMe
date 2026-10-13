using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    private sealed record WorkspaceCommand(string Id, string Label, string Icon, RoutedUICommand Command, string Shortcut);
    private readonly Dictionary<string, WorkspaceCommand> commands = [];
    private readonly Border commandHost = new();
    private readonly TextBlock projectCaption = new();
    private TabControl? ribbon;
    internal string InterfaceLayout => preferences.InterfaceLayout;
    internal string InterfaceTheme => preferences.InterfaceTheme;

    private void RegisterCommands()
    {
        void Add(string id, string label, string icon, Action action, string shortcut = "", Func<bool>? enabled = null)
        {
            var command = new RoutedUICommand(label, id, typeof(MainWindow));
            commands.Add(id, new(id, label, icon, command, shortcut));
            CommandBindings.Add(new(command, (_, e) => { Run(action); e.Handled = true; }, (_, e) => { e.CanExecute = enabled?.Invoke() ?? true; e.Handled = true; }));
        }
        bool Selection() => Canvas.Selected.Count > 0 && !Editor.InteractionInProgress;
        Add("new", "Nouveau projet…", "new", NewProject, "Ctrl+N");
        Add("open", "Ouvrir…", "open", Open, "Ctrl+O");
        Add("save", "Enregistrer", "save", () => Save(false), "Ctrl+S");
        Add("saveAs", "Enregistrer sous…", "save", () => Save(true), "Ctrl+Maj+S");
        Add("projectInfo", "Informations du projet…", "edit", ProjectInfo);
        Add("demo", "Exemple de départ moteur", "library", Demo);
        Add("close", "Quitter", "delete", Close);
        Add("undo", "Annuler", "undo", Editor.Undo, "Ctrl+Z", () => Editor.CanUndo && !Editor.InteractionInProgress);
        Add("redo", "Rétablir", "redo", Editor.Redo, "Ctrl+Y", () => Editor.CanRedo && !Editor.InteractionInProgress);
        Add("duplicate", "Dupliquer", "new", Duplicate, "Ctrl+D", Selection);
        Add("rotate", "Rotation 90°", "rotate", Rotate, "R", Selection);
        Add("delete", "Supprimer", "delete", Delete, "Suppr", Selection);
        Add("pageAdd", "Ajouter un folio…", "new", AddPage);
        Add("sheetInfo", "Propriétés du folio…", "edit", SheetInfo);
        Add("titleblocks", "Cartouches…", "page", TitleBlockEditor);
        Add("zones", "Repérage lignes / colonnes…", "fit", ZonesDialog);
        Add("pageUp", "Monter dans le projet", "undo", () => MovePage(-1));
        Add("pageDown", "Descendre dans le projet", "redo", () => MovePage(1));
        Add("pageRename", "Renommer le folio…", "edit", RenamePage);
        Add("pageDelete", "Supprimer le folio", "delete", DeletePage);
        Add("select", "Sélection", "select", () => Canvas.SetMode("select"), "V");
        Add("wire", "Conducteur", "wire", () => Canvas.SetMode("wire"), "W");
        Add("text", "Texte", "text", () => Canvas.SetMode("text"), "T");
        Add("newSymbol", "Nouveau symbole…", "new", NewSymbol);
        Add("editSymbol", "Modifier un symbole en copie…", "edit", SymbolEditor);
        Add("importSymbol", "Importer un symbole DXF / GeMMeElec…", "open", ImportSymbol);
        Add("importQet", "Projet QElectroTech (.qet)…", "open", () => ImportDrawingProject(false));
        Add("importDxf", "Folios EPLAN / CAO exportés en DXF…", "open", () => ImportDrawingProject(true));
        Add("validate", "Vérifier les connexions et les repères", "check", Validate);
        Add("bom", "Nomenclature configurable…", "library", Bom);
        Add("references", "Navigateur des renvois…", "wire", ReferenceNavigator);
        Add("pdf", "Tous les folios en PDF…", "page", () => ExportFile("pdf"));
        Add("svg", "Folio courant en SVG…", "page", () => ExportFile("svg"));
        Add("csv", "Nomenclature CSV…", "library", () => ExportFile("csv"));
        Add("fit", "Ajuster le folio", "fit", Canvas.Fit, "F");
        Add("grid", "Grille du plan…", "fit", GridDialog);
        Add("palette", "Palette des conducteurs…", "edit", () => PaletteDialog());
        Add("library", "Bibliothèque", "library", () => workspace?.Show("library"));
        Add("properties", "Propriétés", "edit", () => workspace?.Show("properties"));
        Add("pages", "Folios", "page", () => workspace?.Show("pages"));
        Add("resetPanels", "Rétablir les panneaux", "fit", () => workspace?.Reset());
        Add("settings", "Paramètres de l’interface…", "settings", InterfaceSettingsDialog);
        Add("help", "Raccourcis et utilisation", "page", Help);
        Add("mcpHelp", "Connexion MCP", "wire", McpHelp);
        Add("license", "Licence du lecteur DXF", "page", () => { using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("netDxf-LICENSE.txt")!; using var reader = new System.IO.StreamReader(stream); MessageBox.Show(this, reader.ReadToEnd(), "netDxf 3.0.1 · licence MIT"); });
    }

    private void BuildShell(DockPanel root)
    {
        RegisterCommands();
        var header = UiTheme.Bind(new DockPanel { Height = 46 }, Panel.BackgroundProperty, "Ui.Surface");
        header.Children.Add(new Image { Source = Icon, Width = 28, Height = 28, Margin = new(14, 0, 9, 0) });
        var settings = CommandButton("settings", "", false); settings.Margin = new(8, 4, 12, 4); DockPanel.SetDock(settings, Dock.Right); header.Children.Add(settings);
        mcpStatus.Text = "MCP local disponible"; UiTheme.Bind(mcpStatus, TextBlock.ForegroundProperty, "Ui.Muted"); mcpStatus.FontSize = 11; mcpStatus.VerticalAlignment = VerticalAlignment.Center; mcpStatus.Margin = new(12, 0, 8, 0); DockPanel.SetDock(mcpStatus, Dock.Right); header.Children.Add(mcpStatus);
        var brandName = new TextBlock { Text = "GeMMeElec", FontSize = 18, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 20, 0) }; header.Children.Add(brandName);
        UiTheme.Bind(projectCaption, TextBlock.ForegroundProperty, "Ui.Muted"); projectCaption.VerticalAlignment = VerticalAlignment.Center; projectCaption.TextTrimming = TextTrimming.CharacterEllipsis; header.Children.Add(projectCaption);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var menu = new Menu { Padding = new(8, 1, 8, 1) };
        void Menu(string label, params string[] ids)
        {
            var group = new MenuItem { Header = label };
            foreach (var id in ids) { var c = commands[id]; group.Items.Add(new MenuItem { Header = c.Label, Command = c.Command, CommandTarget = this, InputGestureText = c.Shortcut, Icon = CommandIcon(c.Icon, 15) }); }
            menu.Items.Add(group);
        }
        Menu("_Fichier", "new", "open", "save", "saveAs", "projectInfo", "demo", "close");
        Menu("_Édition", "undo", "redo", "duplicate", "rotate", "delete");
        Menu("_Folio", "pageAdd", "sheetInfo", "titleblocks", "zones", "pageUp", "pageDown", "pageRename", "pageDelete");
        Menu("_Insérer", "wire", "text", "select", "newSymbol", "editSymbol", "importSymbol");
        Menu("I_mporter", "importQet", "importDxf");
        Menu("_Projet", "validate", "titleblocks", "bom", "references");
        Menu("E_xporter", "pdf", "svg", "csv");
        Menu("_Affichage", "fit", "grid", "palette", "library", "properties", "pages", "resetPanels", "settings");
        Menu("_Aide", "help", "mcpHelp", "license");
        DockPanel.SetDock(menu, Dock.Top); root.Children.Add(menu);
        commandHost.BorderThickness = new(0, 0, 0, 1); UiTheme.Bind(commandHost, Border.BorderBrushProperty, "Ui.Border"); UiTheme.Bind(commandHost, Border.BackgroundProperty, "Ui.Surface");
        DockPanel.SetDock(commandHost, Dock.Top); root.Children.Add(commandHost); RebuildCommandHost();
        var footer = UiTheme.Bind(new DockPanel { Height = 30 }, Panel.BackgroundProperty, "Ui.Surface"); status.Margin = new(12, 6, 6, 0); status.FontSize = 11; counts.FontSize = 11; counts.Margin = new(10, 6, 12, 0); UiTheme.Bind(counts, TextBlock.ForegroundProperty, "Ui.Muted"); DockPanel.SetDock(counts, Dock.Right); footer.Children.Add(counts); footer.Children.Add(status); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
    }

    private Button CommandButton(string id, string? label = null, bool vertical = false)
    {
        var c = commands[id]; var body = new StackPanel { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal };
        body.Children.Add(CommandIcon(c.Icon, vertical ? 21 : 17));
        if (label is null || label.Length > 0) body.Children.Add(new TextBlock { Text = label ?? c.Label.TrimEnd('…'), Margin = vertical ? new(0, 5, 0, 0) : new(7, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
        var b = new Button { Content = body, Command = c.Command, CommandTarget = this, ToolTip = c.Label + (c.Shortcut.Length > 0 ? " · " + c.Shortcut : ""), Padding = vertical ? new(10, 7, 10, 6) : new(8, 6, 8, 6), BorderThickness = new(0), Tag = "command:" + id };
        System.Windows.Automation.AutomationProperties.SetName(b, c.Label); return b;
    }

    private static Viewbox CommandIcon(string key, double size)
    {
        string path = key switch
        {
            "new" => "M3,1 L11,1 15,5 15,17 3,17 Z M11,1 L11,5 15,5 M6,10 L12,10 M9,7 L9,13",
            "open" => "M1,5 L1,16 16,16 18,7 7,7 5,4 1,4 M1,16 L4,8",
            "save" => "M2,2 L14,2 17,5 17,17 2,17 Z M5,2 L5,7 13,7 13,2 M5,17 L5,11 14,11 14,17",
            "undo" => "M7,3 L2,7 7,11 M2,7 L11,7 C17,7 17,15 11,15",
            "redo" => "M11,3 L16,7 11,11 M16,7 L7,7 C1,7 1,15 7,15",
            "select" => "M3,1 L3,16 7,12 10,17 13,15 10,10 16,10 Z",
            "wire" => "M2,3 L2,9 16,9 16,15 M0,1 L4,1 4,5 0,5 Z M14,13 L18,13 18,17 14,17 Z",
            "text" => "M2,4 L2,2 16,2 16,4 M9,2 L9,16 M5,16 L13,16",
            "rotate" => "M15,4 C11,-1 2,2 2,9 C2,16 11,20 16,13 M15,1 L15,6 10,6",
            "fit" => "M1,6 L1,1 6,1 M12,1 L17,1 17,6 M17,12 L17,17 12,17 M6,17 L1,17 1,12 M6,6 L12,6 12,12 6,12 Z",
            "check" => "M2,9 L7,14 16,3",
            "delete" => "M3,5 L4,17 14,17 15,5 M1,4 L17,4 M6,4 L6,1 12,1 12,4 M7,7 L7,14 M11,7 L11,14",
            "edit" => "M2,13 L2,17 6,17 16,7 12,3 Z M10,5 L14,9",
            "library" => "M2,2 L6,2 6,16 2,16 Z M8,2 L12,2 12,16 8,16 Z M14,3 L17,3 18,16 15,16 Z",
            "settings" => "M9,1 L11,1 12,4 15,4 17,7 15,9 17,11 15,14 12,14 11,17 8,17 7,14 4,14 2,11 4,9 2,7 4,4 7,4 8,1 Z M9,6 A3,3 0 1 0 9,12 A3,3 0 1 0 9,6",
            _ => "M3,1 L11,1 15,5 15,17 3,17 Z M11,1 L11,5 15,5 M6,9 L12,9 M6,12 L12,12"
        };
        var shape = new Path { Data = Geometry.Parse(path), StrokeThickness = 1.45, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        UiTheme.Bind(shape, System.Windows.Shapes.Shape.StrokeProperty, "Ui.Text"); return new Viewbox { Width = size, Height = size, Child = shape };
    }

    private void RebuildCommandHost()
    {
        string selected = (ribbon?.SelectedItem as TabItem)?.Header as string ?? "Accueil";
        ribbon = null;
        if (preferences.InterfaceLayout == "compact")
        {
            var bar = new WrapPanel { Margin = new(7, 2, 7, 2) };
            foreach (var id in new[] { "new", "open", "save", "undo", "redo", "select", "wire", "text", "rotate", "fit", "pdf" }) bar.Children.Add(CommandButton(id, id is "select" or "wire" or "text" ? null : ""));
            commandHost.Child = bar; return;
        }
        ribbon = new TabControl { BorderThickness = new(0), Padding = new(0), Margin = new(8, 1, 8, 0) };
        void Tab(string name, params (string Title, (string Id, string Label)[] Commands)[] groups)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var group in groups)
            {
                var body = new StackPanel(); var actions = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var (id, label) in group.Commands) actions.Children.Add(CommandButton(id, label, true));
                body.Children.Add(actions); body.Children.Add(UiTheme.Bind(new TextBlock { Text = group.Title, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 0, 0, 3) }, TextBlock.ForegroundProperty, "Ui.Muted"));
                row.Children.Add(UiTheme.Bind(new Border { Child = body, BorderThickness = new(0, 0, 1, 0), Margin = new(0, 0, 8, 0), Padding = new(0, 0, 8, 0) }, Border.BorderBrushProperty, "Ui.Border"));
            }
            var tab = new TabItem { Header = name, Content = new ScrollViewer { Content = row, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled } }; ribbon.Items.Add(tab); if (name == selected) ribbon.SelectedItem = tab;
        }
        Tab("Accueil", ("Projet", [("new", "Nouveau"), ("open", "Ouvrir"), ("save", "Enregistrer"), ("pdf", "PDF")]), ("Historique", [("undo", "Annuler"), ("redo", "Rétablir")]), ("Édition", [("select", "Sélection"), ("wire", "Conducteur"), ("text", "Texte"), ("rotate", "Rotation"), ("duplicate", "Dupliquer"), ("delete", "Supprimer")]));
        Tab("Dessiner", ("Symboles", [("newSymbol", "Créer"), ("editSymbol", "Copie"), ("importSymbol", "Importer")]), ("Dessin", [("select", "Sélection"), ("wire", "Conducteur"), ("text", "Texte")]), ("Folios", [("pageAdd", "Ajouter"), ("sheetInfo", "Propriétés"), ("titleblocks", "Cartouches"), ("zones", "Repérage")]));
        Tab("Projet", ("Données", [("projectInfo", "Informations"), ("bom", "Nomenclature"), ("references", "Renvois"), ("validate", "Vérifier")]), ("Importer", [("importQet", "QElectroTech"), ("importDxf", "DXF")]), ("Exporter", [("pdf", "PDF"), ("svg", "SVG"), ("csv", "CSV")]));
        Tab("Affichage", ("Vue", [("fit", "Ajuster"), ("grid", "Grille"), ("palette", "Palette")]), ("Panneaux", [("library", "Bibliothèque"), ("pages", "Folios"), ("properties", "Propriétés"), ("resetPanels", "Rétablir")]), ("Interface", [("settings", "Paramètres")]));
        commandHost.Child = ribbon;
    }

    internal void SetInterface(string layout, string theme, bool persist = true)
    {
        if (layout is not ("compact" or "ribbon") || theme is not ("light" or "dark")) throw new ArgumentException("Disposition ou thème inconnu.");
        bool changedLayout = preferences.InterfaceLayout != layout;
        var draft = DocumentModel.Copy(preferences); draft.InterfaceLayout = layout; draft.InterfaceTheme = theme;
        if (persist) draft.Save();
        preferences.InterfaceLayout = layout; preferences.InterfaceTheme = theme;
        Editor.EndPropertyEdit(); UiTheme.Apply(this, theme); if (changedLayout) RebuildCommandHost();
    }

    private void InterfaceSettingsDialog()
    {
        var dialog = DocumentWindow("Paramètres · interface", 530, 395); var panel = new StackPanel { Margin = new(22) }; dialog.Content = panel;
        panel.Children.Add(new TextBlock { Text = "Votre espace de travail", FontSize = 21, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(Hint("Les réglages sont mémorisés immédiatement. Les panneaux conservent leur organisation."));
        panel.Children.Add(new TextBlock { Text = "Disposition", Margin = new(0, 12, 0, 5) });
        var layout = new ComboBox { Name = "InterfaceLayout", ItemsSource = new[] { "Compacte · priorité au dessin", "Ruban · commandes par onglets" }, SelectedIndex = preferences.InterfaceLayout == "ribbon" ? 1 : 0 }; panel.Children.Add(layout);
        panel.Children.Add(new TextBlock { Text = "Thème", Margin = new(0, 15, 0, 5) });
        var theme = new ComboBox { Name = "InterfaceTheme", ItemsSource = new[] { "Clair", "Sombre" }, SelectedIndex = preferences.InterfaceTheme == "dark" ? 1 : 0 }; panel.Children.Add(theme);
        panel.Children.Add(Hint("Le folio reste blanc. Les couleurs des symboles, conducteurs, textes et exports restent celles du document."));
        void Update() { Run(() => { SetInterface(layout.SelectedIndex == 1 ? "ribbon" : "compact", theme.SelectedIndex == 1 ? "dark" : "light"); UiTheme.Apply(dialog, preferences.InterfaceTheme); }); }
        layout.SelectionChanged += (_, _) => Update(); theme.SelectionChanged += (_, _) => Update();
        panel.Children.Add(DocButton("Fermer", dialog, dialog.Close)); ShowDocumentDialog(dialog);
    }

    internal RoutedUICommand WorkspaceCommandForTest(string id) => commands[id].Command;
}
