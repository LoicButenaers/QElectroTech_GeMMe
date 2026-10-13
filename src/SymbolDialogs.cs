using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    private void NewSymbol() => EditSymbol(new("draft", "Nouveau symbole", "X", 100, 100, [], []));
    private void SymbolEditor()
    {
        Canvas.CancelDrag(); var c = Editor.Sheet.Components.FirstOrDefault(c => Canvas.Selected.Contains(c.Id));
        string id = c?.Symbol ?? Canvas.Symbol;
        if (id is "junction" or "folio_next" or "folio_previous") throw new ArgumentException("Les reports et jonctions se règlent dans leurs propriétés.");
        EditSymbol(CustomSymbols.Resolve(Editor.Project, id), c?.Id);
    }
    private async void ImportSymbol()
    {
        Canvas.CancelDrag(); var picker = new OpenFileDialog { Filter = "Symboles DXF / GeMMeElec|*.dxf;*.gemsymbol", Title = "Importer un symbole" }; if (picker.ShowDialog(this) != true) return;
        try
        {
            IsEnabled = false; status.Text = "Lecture du symbole…";
            var result = await Task.Run(() => picker.FileName.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ? DxfImport.Read(picker.FileName) : new DxfImportResult(CustomSymbols.Load(picker.FileName), [], "unités GeMMeElec"));
            IsEnabled = true; EditSymbol(result.Symbol, null, string.Join("\n", new[] { "Unité du fichier : " + result.Units }.Concat(result.Warnings)));
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Import du symbole"); }
        finally { IsEnabled = true; status.Text = "Prêt"; }
    }
    private sealed record SymbolPart(int Shape, int Port, string Label);
    internal void EditSymbol(SymbolDefinition source, string? replace = null, string warnings = "")
    {
        var dialog = DocumentWindow("Atelier de symboles · dessin et bornes", 1450, 900);
        var draft = new SymbolDraft(source with { Name = source.Id == "draft" ? source.Name : source.Name + " · copie" });
        var canvas = new SymbolCanvas(draft); var root = new DockPanel { Margin = new(12) }; dialog.Content = root;
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var fields = new WrapPanel(); top.Children.Add(fields);
        TextBox Field(string label, string value, int size)
        {
            fields.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new(5) });
            var box = new TextBox { Text = value, Width = size, Margin = new(3), Padding = new(5) }; fields.Children.Add(box); return box;
        }
        string Num(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
        double Number(string s) => double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);
        var name = Field("Nom", draft.Definition.Name, 230); var prefix = Field("Préfixe", source.Prefix, 40);
        var width = Field("Largeur", Num(source.Width), 60); var height = Field("Hauteur", Num(source.Height), 60); var factor = Field("Échelle ×", "1", 40);
        SymbolDefinition Read() => draft.Definition with { Name = name.Text, Prefix = prefix.Text, Width = Number(width.Text), Height = Number(height.Text), Category = "Mes symboles" };
        fields.Children.Add(DocButton("Appliquer", dialog, () => { canvas.Cancel(); draft.Change(_ => Read()); }));
        fields.Children.Add(DocButton("Redimensionner", dialog, () => { canvas.Cancel(); var scaled = DxfImport.Scale(Read(), Number(factor.Text)); draft.Change(_ => scaled); factor.Text = "1"; canvas.Fit(); }));
        var bar = new WrapPanel(); top.Children.Add(bar);
        bar.Children.Add(DocButton("↶ Annuler", dialog, () => { canvas.Cancel(); draft.Undo(); canvas.SelectPart(-1, -1); }));
        bar.Children.Add(DocButton("↷ Rétablir", dialog, () => { canvas.Cancel(); draft.Redo(); canvas.SelectPart(-1, -1); }));
        bar.Children.Add(DocButton("Dupliquer", dialog, canvas.DuplicateSelection)); bar.Children.Add(DocButton("Supprimer", dialog, canvas.DeleteSelection));
        bar.Children.Add(DocButton("Ajuster la vue", dialog, canvas.Fit));
        var snap = new CheckBox { Content = "Accrochage 5 unités", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new(10) }; snap.Click += (_, _) => canvas.SnapToGrid = snap.IsChecked == true; bar.Children.Add(snap);
        var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var tip = Hint("Clic gauche maintenu : dessiner / déplacer • Poignées : dimensions • Molette : zoom • Bouton central : vue • Échap : sélection"); bottom.Children.Add(tip); canvas.Status += text => tip.Text = text;
        if (warnings.Length > 0) bottom.Children.Add(new TextBox { Text = warnings, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 60, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.LemonChiffon });
        var actions = new WrapPanel(); bottom.Children.Add(actions);
        var replaceBox = new CheckBox { Content = "Remplacer le composant sélectionné", IsEnabled = replace is not null, IsChecked = replace is not null, VerticalAlignment = VerticalAlignment.Center, Margin = new(10) }; actions.Children.Add(replaceBox);
        actions.Children.Add(DocButton("Exporter .gemsymbol…", dialog, () => { canvas.Cancel(); var d = CustomSymbols.Copy(Read(), name.Text); CustomSymbols.Validate(d); var save = new SaveFileDialog { Filter = "Symbole GeMMeElec|*.gemsymbol", DefaultExt = ".gemsymbol", FileName = "symbole.gemsymbol" }; if (save.ShowDialog(dialog) == true) CustomSymbols.Export(d, save.FileName); }));
        actions.Children.Add(DocButton("Enregistrer la copie", dialog, () => { canvas.Cancel(); var d = Editor.SaveSymbolCopy(Read(), replaceBox.IsChecked == true ? replace : null); Canvas.Symbol = d.Id; Canvas.CatalogId = null; libraryMode.SelectedIndex = 2; Filter(); dialog.Close(); }));
        actions.Children.Add(DocButton("Fermer sans enregistrer", dialog, dialog.Close));
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new GridLength(180) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(225) }); root.Children.Add(body);
        var left = new DockPanel { Margin = new(0, 4, 8, 0) }; body.Children.Add(left);
        var toolPanel = new StackPanel(); DockPanel.SetDock(toolPanel, Dock.Top); left.Children.Add(toolPanel);
        var toolButtons = new Dictionary<string, Button>();
        foreach (var (kind, label) in new[] { ("select", "↖ Sélection"), ("line", "+ Ligne"), ("rect", "+ Rectangle"), ("circle", "+ Cercle"), ("text", "+ Texte"), ("port", "+ Borne") })
        {
            var button = DocButton(label, dialog, () => { canvas.SetTool(kind); canvas.Focus(); }); toolPanel.Children.Add(button); toolButtons[kind] = button;
        }
        canvas.ToolChanged += () => { foreach (var pair in toolButtons) pair.Value.Background = pair.Key == canvas.Tool ? Brushes.LightCyan : Brushes.White; }; canvas.SetTool("select");
        toolPanel.Children.Add(Heading("PARTIES DU SYMBOLE"));
        var parts = new ListBox { DisplayMemberPath = "Label", Margin = new(0, 8, 0, 0) }; left.Children.Add(parts);
        var border = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new(1), Child = canvas, Margin = new(0, 4, 0, 0) }; Grid.SetColumn(border, 1); body.Children.Add(border);
        var prop = new StackPanel { Margin = new(12, 4, 0, 0) }; var scroll = new ScrollViewer { Content = prop, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(scroll, 2); body.Children.Add(scroll);
        bool syncing = false;
        void Properties()
        {
            prop.Children.Clear(); prop.Children.Add(Heading("PROPRIÉTÉS DE LA PARTIE"));
            var d = draft.Definition; int si = canvas.ShapeIndex, pi = canvas.PortIndex;
            if (si < 0 && pi < 0) { prop.Children.Add(Hint("Choisir un outil puis dessiner sur la grille. Cliquer une partie ou son nom pour la déplacer et régler ses coordonnées.\n\nLes bornes vertes sont les points de raccordement des conducteurs. Leur identifiant doit rester unique.\n\nLe cadre pointillé indique l’encombrement du symbole.")); return; }
            var s = si >= 0 ? d.Shapes[si] : null; var p = pi >= 0 ? d.Ports[pi] : null;
            var x = DocField(prop, "X", Num(s?.X ?? p!.X)); var y = DocField(prop, "Y", Num(s?.Y ?? p!.Y));
            TextBox? a = null, b = null, text = null, rotation = null, portId = null;
            if (p is not null) portId = DocField(prop, "Identifiant de borne", p.Id);
            else if (s is not null)
            {
                a = DocField(prop, s.Kind switch { "line" => "X de l’extrémité", "rect" or "image" => "Largeur", "circle" or "dot" => "Rayon", _ => "Taille du texte" }, Num(s.A));
                if (s.Kind is "line" or "rect" or "image") b = DocField(prop, s.Kind == "line" ? "Y de l’extrémité" : "Hauteur", Num(s.B));
                if (s.Kind == "text") text = DocField(prop, "Texte", s.Text);
                if (s.Kind is "text" or "rect" or "image") rotation = DocField(prop, "Rotation (degrés)", Num(s.Rotation));
            }
            prop.Children.Add(DocButton("Appliquer à la partie", dialog, () =>
            {
                if (p is not null) draft.Change(d => { var ports = d.Ports.ToList(); ports[pi] = new(portId!.Text, Number(x.Text), Number(y.Text)); return d with { Ports = ports, InternalConnections = d.InternalConnections.Select(c => new PortConnection(c.From == p.Id ? portId.Text : c.From, c.To == p.Id ? portId.Text : c.To)).ToList() }; });
                else draft.Change(d => { var shapes = d.Shapes.ToList(); shapes[si] = s! with { X = Number(x.Text), Y = Number(y.Text), A = Number(a!.Text), B = b is null ? s!.B : Number(b.Text), Text = text?.Text ?? s!.Text, Rotation = rotation is null ? s!.Rotation : Number(rotation.Text) }; return d with { Shapes = shapes }; });
            }));
            prop.Children.Add(Hint("Les modifications à la souris sont immédiates. Pour les champs ci-dessus, cliquer Appliquer à la partie."));
        }
        // Geometry changes must not discard metadata currently being typed.
        var displayed = draft.Definition;
        void RefreshParts()
        {
            syncing = true; var d = draft.Definition;
            if (displayed.Name != d.Name) name.Text = d.Name; if (displayed.Prefix != d.Prefix) prefix.Text = d.Prefix;
            if (displayed.Width != d.Width) width.Text = Num(d.Width); if (displayed.Height != d.Height) height.Text = Num(d.Height); displayed = d;
            parts.ItemsSource = d.Shapes.Select((s, i) => new SymbolPart(i, -1, $"{i + 1} · " + (s.Kind switch { "line" => "Ligne", "rect" => "Rectangle", "circle" => "Cercle", "text" => s.Text, "image" => "Dessin importé (image)", _ => "Point" }))).Concat(d.Ports.Select((p, i) => new SymbolPart(-1, i, "Borne " + p.Id))).ToList();
            parts.SelectedItem = parts.Items.Cast<SymbolPart>().FirstOrDefault(p => p.Shape == canvas.ShapeIndex && p.Port == canvas.PortIndex); syncing = false; Properties();
        }
        parts.SelectionChanged += (_, _) => { if (!syncing && parts.SelectedItem is SymbolPart p) canvas.SelectPart(p.Shape, p.Port); };
        canvas.SelectionChanged += () => { syncing = true; parts.SelectedItem = parts.Items.Cast<SymbolPart>().FirstOrDefault(p => p.Shape == canvas.ShapeIndex && p.Port == canvas.PortIndex); syncing = false; Properties(); };
        draft.Changed += RefreshParts;
        var context = new ContextMenu();
        foreach (var (label, action) in new (string, Action)[] { ("Dupliquer", canvas.DuplicateSelection), ("Supprimer", canvas.DeleteSelection), ("Ajuster la vue", canvas.Fit) }) { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); context.Items.Add(item); }
        canvas.ContextMenu = context;
        dialog.Closed += (_, _) => canvas.Cancel(); RefreshParts(); ShowDocumentDialog(dialog);
    }
}
