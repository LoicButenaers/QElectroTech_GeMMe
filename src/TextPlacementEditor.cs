using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace GeMMeElec;

// Edits only a component draft. The project receives one undoable change on validation.
public sealed class TextPlacementPreview : FrameworkElement
{
    public Component Draft { get; }
    private readonly Project project;
    private readonly CrossReferences references;
    public int Target { get; set; }
    public event Action? Edited;
    private double zoom = 1.8;
    private Point? drag;
    private LabelPlacement? dragPlacement;
    private bool dragInline;
    public List<ComponentTextTarget> Items => ComponentTexts.List(Draft);
    public LabelPlacement Placement => Items[Target].Placement;
    public string TextKey => Items[Target].Key;
    public TextPlacementPreview(Project project, Component component)
    {
        this.project = project; references = new(project);
        Draft = DocumentModel.Copy(component); Draft.ResolvedDefinition = component.Definition; ComponentTexts.Materialize(Draft);
        Focusable = true; ClipToBounds = true; Cursor = Cursors.Hand;
        ToolTip = "Glisser le texte sélectionné · flèches du clavier : 1 unité (Maj : 10) · molette : zoom · Échap : annuler le glissement";
    }
    public SmartTextResult SmartAdjust(string? side = null)
    {
        EndDrag(); var sheet = project.Sheets.Single(s => s.Components.Any(c => c.Id == Draft.Id));
        var result = SmartTextPlacement.Adjust(project, sheet, Draft, Target, side);
        Edited?.Invoke(); Fit(); return result;
    }
    public List<Ink> Scene() => Drawing.ComponentScene(project, Draft, references);
    public Rect TextBounds()
    {
        var ink = ComponentTexts.Ink(project, Draft, references, TextKey); return ink is null ? Rect.Empty : Drawing.TextBounds(ink);
    }
    public string ContentText => Items[Target].ReadOnly ? references.Label(project, Draft) : Items[Target].Text;
    public void SetContent(string text) { ComponentTexts.SetText(Draft, TextKey, text); InvalidateVisual(); }
    public void AddText() { EndDrag(); string key = ComponentTexts.Add(Draft, "Nouveau texte"); Target = Items.FindIndex(t => t.Key == key); Edited?.Invoke(); Fit(); }
    public void RemoveText() { if (!TextKey.StartsWith("extra:")) return; string key = TextKey; Draft.AdditionalTexts.RemoveAll(t => "extra:" + t.Id == key); Target = 0; Edited?.Invoke(); Fit(); }
    public SmartTextResult AlignAll(string? side = null)
    {
        EndDrag(); var sheet = project.Sheets.Single(s => s.Components.Any(c => c.Id == Draft.Id));
        var result = SmartTextPlacement.AdjustAll(project, sheet, Draft, side); Edited?.Invoke(); Fit(); return result;
    }
    private Point Model(Point p) => new(Draft.X + (p.X - ActualWidth / 2) / zoom, Draft.Y + (p.Y - ActualHeight / 2) / zoom);
    public void Fit()
    {
        var bounds = Draft.Bounds();
        foreach (var i in Scene().Where(i => i.Kind == "text")) bounds.Union(Drawing.TextBounds(i));
        double width = Math.Max(Math.Abs(bounds.Left - Draft.X), Math.Abs(bounds.Right - Draft.X)) * 2 + 60;
        double height = Math.Max(Math.Abs(bounds.Top - Draft.Y), Math.Abs(bounds.Bottom - Draft.Y)) * 2 + 60;
        zoom = Math.Clamp(Math.Min(Math.Max(100, ActualWidth) / width, Math.Max(100, ActualHeight) / height), .03, 5); InvalidateVisual();
    }
    public void DetachDestination()
    {
        if (!CrossReferences.IsPoint(Draft) || Target != 1 || !Draft.ReferenceDestinationInline) return;
        var bounds = TextBounds(); Draft.ReferenceDestinationInline = false;
        if (!bounds.IsEmpty) { var p = Placement; p.AnchorPort = ""; p.Side = "custom"; p.Horizontal = "left"; p.Vertical = "top"; p.OffsetX = bounds.X - Draft.X; p.OffsetY = bounds.Y - Draft.Y; }
    }
    public bool BeginDrag(Point modelPoint)
    {
        var bounds = TextBounds();
        if (bounds.IsEmpty || !bounds.Contains(modelPoint))
        {
            int found = -1; var items = Items;
            for (int i = items.Count - 1; i >= 0; i--) { var ink = ComponentTexts.Ink(project, Draft, references, items[i].Key); if (ink is not null && Drawing.TextBounds(ink).Contains(modelPoint)) { found = i; break; } }
            if (found < 0) return false; Target = found; Edited?.Invoke(); bounds = TextBounds();
        }
        dragPlacement = DocumentModel.Copy(Placement); dragInline = Draft.ReferenceDestinationInline;
        DetachDestination(); drag = modelPoint; return true;
    }
    public void MoveDrag(Point modelPoint)
    {
        if (drag is not Point previous) return;
        var delta = modelPoint - previous;
        Placement.OffsetX = Math.Clamp(Placement.OffsetX + delta.X, -10000, 10000);
        Placement.OffsetY = Math.Clamp(Placement.OffsetY + delta.Y, -10000, 10000);
        drag = modelPoint; InvalidateVisual(); Edited?.Invoke();
    }
    public void EndDrag(bool cancel = false)
    {
        if (cancel && dragPlacement is not null) { ComponentTexts.SetPlacement(Draft, TextKey, dragPlacement); Draft.ReferenceDestinationInline = dragInline; }
        drag = null; dragPlacement = null; if (IsMouseCaptured) ReleaseMouseCapture(); InvalidateVisual(); Edited?.Invoke();
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { base.OnMouseLeftButtonDown(e); Focus(); if (BeginDrag(Model(e.GetPosition(this)))) { CaptureMouse(); e.Handled = true; } }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (drag is not null) MoveDrag(Model(e.GetPosition(this))); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); if (drag is not null) { EndDrag(); e.Handled = true; } }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); if (drag is not null) EndDrag(); }
    protected override void OnMouseWheel(MouseWheelEventArgs e) { zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15), .03, 10); InvalidateVisual(); e.Handled = true; }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && drag is not null) { EndDrag(true); e.Handled = true; return; }
        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        Vector delta = e.Key switch { Key.Left => new(-step, 0), Key.Right => new(step, 0), Key.Up => new(0, -step), Key.Down => new(0, step), _ => new() };
        if (delta.Length > 0) { DetachDestination(); Placement.OffsetX = Math.Clamp(Placement.OffsetX + delta.X, -10000, 10000); Placement.OffsetY = Math.Clamp(Placement.OffsetY + delta.Y, -10000, 10000); InvalidateVisual(); Edited?.Invoke(); e.Handled = true; }
        base.OnKeyDown(e);
    }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Drawing.Brush("#F7FAFD"), null, new(0, 0, ActualWidth, ActualHeight));
        dc.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight / 2)); dc.PushTransform(new ScaleTransform(zoom, zoom)); dc.PushTransform(new TranslateTransform(-Draft.X, -Draft.Y));
        double grid = 10; while (grid * zoom < 12) grid *= 2;
        double rx = ActualWidth / zoom / 2, ry = ActualHeight / zoom / 2;
        for (double x = Math.Floor((Draft.X - rx) / grid) * grid; x <= Draft.X + rx; x += grid)
            for (double y = Math.Floor((Draft.Y - ry) / grid) * grid; y <= Draft.Y + ry; y += grid) dc.DrawEllipse(Drawing.Brush("#B6C6D8"), null, new(x, y), .7 / zoom, .7 / zoom);
        Drawing.Render(dc, Scene()); var bounds = TextBounds();
        if (!bounds.IsEmpty) { bounds.Inflate(3, 3); dc.DrawRectangle(null, new Pen(Drawing.Brush("#007F86"), 1.5 / zoom) { DashStyle = DashStyles.Dash }, bounds); }
        dc.Pop(); dc.Pop(); dc.Pop();
    }
}

public sealed partial class MainWindow
{
    internal void TextPositionDialog(string id)
    {
        Canvas.CancelDrag(); var component = Editor.Sheet.Components.Single(c => c.Id == id);
        var dialog = DocumentWindow("Position des textes · aperçu interactif", 1050, 760);
        var root = new DockPanel { Margin = new(16), Background = Brushes.White }; dialog.Content = root;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var controls = new StackPanel { Margin = new(0, 0, 18, 0) }; var scroll = new ScrollViewer { Content = controls, Width = 305, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; DockPanel.SetDock(scroll, Dock.Left); root.Children.Add(scroll);
        var area = new DockPanel(); root.Children.Add(area);
        var instruction = Hint("Glisse le texte encadré pour le placer. Les réglages sont visibles immédiatement dans cet aperçu. Molette : zoom. Flèches du clavier : déplacement précis."); DockPanel.SetDock(instruction, Dock.Top); area.Children.Add(instruction);
        var preview = new TextPlacementPreview(Editor.Project, component); var fit = DocButton("Ajuster l’aperçu", dialog, preview.Fit); DockPanel.SetDock(fit, Dock.Bottom); area.Children.Add(fit); area.Children.Add(preview);
        bool report = CrossReferences.IsPoint(component), loading = true;
        controls.Children.Add(Hint("Texte à modifier · ou cliquer sur le texte dans l’aperçu"));
        var target = new ComboBox { Name = "TextTarget", DisplayMemberPath = "Name", Padding = new(6) }; controls.Children.Add(target); preview.Target = Math.Min(labelTarget, preview.Items.Count - 1);
        var contentText = DocField(controls, "Contenu", ""); contentText.Name = "ComponentTextContent"; contentText.AcceptsReturn = true; contentText.MaxLength = 2000;
        var textActions = new WrapPanel(); controls.Children.Add(textActions);
        textActions.Children.Add(DocButton("Ajouter du texte", dialog, preview.AddText));
        var removeText = DocButton("Supprimer ce texte", dialog, preview.RemoveText); textActions.Children.Add(removeText);
        var allTexts = new CheckBox { Name = "AllComponentTexts", Content = "Aligner tous les textes visibles", Margin = new(0, 8, 0, 8), ToolTip = "Les boutons d’ajustement alignent tous les textes en une colonne, sans les superposer." }; controls.Children.Add(allTexts);

        var visible = new CheckBox { Content = "Afficher ce texte", Margin = new(0, 12, 0, 8) }; controls.Children.Add(visible);
        var inline = new CheckBox { Content = "Destination sous le nom", Visibility = report ? Visibility.Visible : Visibility.Collapsed, Margin = new(0, 0, 0, 8) }; controls.Children.Add(inline);
        var stylePanel = new StackPanel(); controls.Children.Add(new Expander { Header = "Taille, couleur et rotation", IsExpanded = true, Content = stylePanel, Margin = new(0, 4, 0, 8) });
        var fontSize = DocField(stylePanel, "Taille du texte (0 = automatique)", "0"); fontSize.Name = "TextFontSize";
        var textColor = DocField(stylePanel, "Couleur #RRGGBB (vide = automatique)", ""); textColor.Name = "TextColor";
        var swatches = new WrapPanel { Margin = new(0, 4, 0, 0) }; stylePanel.Children.Add(swatches);
        foreach (var color in preferences.Colors)
        {
            var swatch = new Button { Width = 23, Height = 23, Margin = new(2), Background = Drawing.Brush(color.Color), ToolTip = color.Name + " " + color.Color };
            AutomationProperties.SetName(swatch, color.Name); swatch.Click += (_, _) => textColor.Text = color.Color; swatches.Children.Add(swatch);
        }
        var textRotation = DocField(stylePanel, "Rotation du texte (degrés)", "0"); textRotation.Name = "TextRotation";
        TextBox gap = null!, x = null!, y = null!; TextBlock feedback = null!;
        var smartStatus = Hint(""); smartStatus.Visibility = Visibility.Collapsed;
        void AdjustText(string? side) { var result = allTexts.IsChecked == true ? preview.AlignAll(side) : preview.SmartAdjust(side); smartStatus.Text = result.Message; smartStatus.Visibility = Visibility.Visible; }
        controls.Children.Add(Hint("Ajustement intelligent · choisir un côté"));
        var smart = DocButton("Automatique · tous les côtés", dialog, () => AdjustText(null));
        smart.Name = "SmartTextAdjust"; smart.ToolTip = "Aligner le texte sélectionné sur le symbole en tenant compte des bornes, fils, textes voisins et limites du folio.";
        controls.Children.Add(smart);
        var smartDirections = new UniformGrid { Columns = 2, Rows = 2 };
        foreach (var (side, caption) in new[] { ("top", "↑ Haut"), ("bottom", "↓ Bas"), ("left", "← Gauche"), ("right", "→ Droite") })
        {
            var button = DocButton(caption, dialog, () => AdjustText(side)); button.Name = "SmartText_" + side;
            button.ToolTip = "Aligner sur le symbole en restant du côté choisi, même si l’espace y est limité.";
            AutomationProperties.SetName(button, "Ajustement intelligent : " + caption[2..]); smartDirections.Children.Add(button);
        }
        controls.Children.Add(smartDirections); controls.Children.Add(smartStatus);
        controls.Children.Add(Hint("Position manuelle"));
        var directions = new Grid { Height = 126, Margin = new(0, 8, 0, 8) }; for (int i = 0; i < 3; i++) { directions.RowDefinitions.Add(new()); directions.ColumnDefinitions.Add(new()); } controls.Children.Add(directions);
        var horizontal = new Dictionary<string, ToggleButton>(); var vertical = new Dictionary<string, ToggleButton>(); var sides = new Dictionary<string, ToggleButton>();
        foreach (var (side, glyph, row, col, caption) in new[] { ("top", "↑", 0, 1, "Au-dessus"), ("left", "←", 1, 0, "À gauche"), ("custom", "Libre", 1, 1, "Coordonnées libres"), ("right", "→", 1, 2, "À droite"), ("bottom", "↓", 2, 1, "En dessous") })
        {
            var b = new ToggleButton { Content = glyph, FontSize = side == "custom" ? 12 : 24, Margin = new(2), ToolTip = caption, Tag = "side:" + side }; AutomationProperties.SetName(b, caption); sides[side] = b; Grid.SetRow(b, row); Grid.SetColumn(b, col); directions.Children.Add(b);
            b.Click += (_, _) => { if (loading) return; preview.DetachDestination(); var p = preview.Placement; if (side == "custom") { var bounds = preview.TextBounds(); p.AnchorPort = ""; p.Horizontal = "left"; p.Vertical = "top"; if (!bounds.IsEmpty) { p.OffsetX = bounds.X - preview.Draft.X; p.OffsetY = bounds.Y - preview.Draft.Y; } } else { p.Horizontal = side == "left" ? "right" : side == "right" ? "left" : "center"; p.Vertical = side == "top" ? "bottom" : side == "bottom" ? "top" : "center"; p.OffsetX = p.OffsetY = 0; } p.Side = side; Load(); };
        }
        void Alignments(string caption, bool isVertical, Dictionary<string, ToggleButton> buttons)
        {
            controls.Children.Add(Hint(caption)); var row = new StackPanel { Orientation = Orientation.Horizontal }; controls.Children.Add(row);
            foreach (string value in isVertical ? new[] { "top", "center", "bottom" } : new[] { "left", "center", "right" })
            {
                string label = isVertical ? value == "top" ? "Aligner en haut" : value == "bottom" ? "Aligner en bas" : "Centrer verticalement" : value == "left" ? "Aligner à gauche" : value == "right" ? "Aligner à droite" : "Centrer horizontalement";
                var b = new ToggleButton { Content = AlignmentIcon(value, isVertical), Width = 62, Height = 38, Margin = new(2), ToolTip = label, Tag = (isVertical ? "vertical:" : "horizontal:") + value }; AutomationProperties.SetName(b, label); row.Children.Add(b); buttons[value] = b;
                b.Click += (_, _) => { if (loading) return; preview.DetachDestination(); if (isVertical) preview.Placement.Vertical = value; else preview.Placement.Horizontal = value; Load(); };
            }
        }
        var alignmentIndex = controls.Children.Count;
        gap = DocField(controls, "Distance au symbole", ""); gap.Name = "TextGap";
        x = DocField(controls, "Décalage X (droite +)", ""); x.Name = "TextOffsetX";
        y = DocField(controls, "Décalage Y (bas +)", ""); y.Name = "TextOffsetY";
        feedback = Hint(""); controls.Children.Add(feedback);
        Alignments("Alignement horizontal", false, horizontal); Alignments("Alignement vertical", true, vertical);
        var alignmentControls = controls.Children.Cast<UIElement>().Skip(controls.Children.Count - 4).ToArray();
        foreach (var control in alignmentControls) { controls.Children.Remove(control); controls.Children.Insert(alignmentIndex++, control); }
        var apply = DocButton("Valider", dialog, () => { if (!ReadCoordinates()) return; var c = Editor.Sheet.Components.Single(c => c.Id == id); Editor.Change(() => { c.TagPlacement = DocumentModel.Copy(preview.Draft.TagPlacement); c.DetailPlacement = DocumentModel.Copy(preview.Draft.DetailPlacement); c.DescriptionPlacement = DocumentModel.Copy(preview.Draft.DescriptionPlacement); c.ReferenceDestinationInline = preview.Draft.ReferenceDestinationInline;
                c.AdditionalTexts = DocumentModel.Copy(preview.Draft.AdditionalTexts); c.TextOverrides = DocumentModel.Copy(preview.Draft.TextOverrides); c.PortLabels = DocumentModel.Copy(preview.Draft.PortLabels); c.ShowPortLabels = preview.Draft.ShowPortLabels;
                c.Tag = preview.Draft.Tag; c.Rating = preview.Draft.Rating; c.Reference = preview.Draft.Reference; c.Range = preview.Draft.Range; c.Description = preview.Draft.Description;
                if (report && c.ReferenceName != preview.Draft.ReferenceName) Editor.SetReferenceName(c, preview.Draft.ReferenceName);
            }); dialog.Close(); }); footer.Children.Add(apply);
        footer.Children.Add(DocButton("Annuler", dialog, dialog.Close));
        bool ReadCoordinates()
        {
            try { var p = DocumentModel.Copy(preview.Placement); p.Gap = ReadNumber(gap.Text); p.OffsetX = ReadNumber(x.Text); p.OffsetY = ReadNumber(y.Text); p.FontSize = ReadNumber(fontSize.Text); p.Color = textColor.Text.Trim(); p.Rotation = ReadNumber(textRotation.Text); p.Validate(); preview.Placement.Gap = p.Gap; preview.Placement.OffsetX = p.OffsetX; preview.Placement.OffsetY = p.OffsetY; preview.Placement.FontSize = p.FontSize; preview.Placement.Color = p.Color; preview.Placement.Rotation = p.Rotation; feedback.Text = ""; return true; } catch (Exception ex) { feedback.Text = ex.Message; return false; }
        }
        void Load()
        {
            smartStatus.Visibility = Visibility.Collapsed; loading = true;
            target.ItemsSource = preview.Items; target.SelectedIndex = preview.Target; contentText.IsReadOnly = preview.Items[preview.Target].ReadOnly; contentText.Text = preview.ContentText; removeText.IsEnabled = preview.TextKey.StartsWith("extra:");
            var p = preview.Placement; visible.IsChecked = p.Visible; inline.IsChecked = preview.Draft.ReferenceDestinationInline;
            gap.Text = Number(p.Gap); x.Text = Number(p.OffsetX); y.Text = Number(p.OffsetY); fontSize.Text = Number(p.FontSize); textColor.Text = p.Color; textRotation.Text = Number(p.Rotation);
            foreach (var b in sides) b.Value.IsChecked = b.Key == p.Side; foreach (var b in horizontal) b.Value.IsChecked = b.Key == p.Horizontal; foreach (var b in vertical) b.Value.IsChecked = b.Key == p.Vertical;
            bool independent = !(report && preview.Target == 1 && preview.Draft.ReferenceDestinationInline); gap.IsEnabled = x.IsEnabled = y.IsEnabled = independent;
            feedback.Text = independent ? "Coordonnées en unités du schéma, sans arrondi à la grille." : "Glisse la destination ou choisis une flèche pour la placer séparément.";
            loading = false; preview.InvalidateVisual();
        }
        foreach (var field in new[] { gap, x, y, fontSize, textColor, textRotation }) field.TextChanged += (_, _) => { if (!loading) { smartStatus.Visibility = Visibility.Collapsed; ReadCoordinates(); preview.InvalidateVisual(); } };
        target.SelectionChanged += (_, _) => { if (loading || target.SelectedIndex < 0) return; int selected = target.SelectedIndex; preview.EndDrag(); preview.Target = labelTarget = selected; Load(); };
        contentText.TextChanged += (_, _) => { if (loading || contentText.IsReadOnly) return; try { preview.SetContent(contentText.Text); } catch (Exception ex) { feedback.Text = ex.Message; } };
        visible.Click += (_, _) => { preview.Placement.Visible = visible.IsChecked == true; Load(); };
        inline.Click += (_, _) => { if (inline.IsChecked == true) preview.Draft.ReferenceDestinationInline = true; else { int old = preview.Target; preview.Target = 1; preview.DetachDestination(); preview.Target = old; } Load(); };
        preview.Edited += Load; preview.Loaded += (_, _) => preview.Fit(); Load(); ShowDocumentDialog(dialog);
    }
    private static Image AlignmentIcon(string alignment, bool vertical)
    {
        var drawing = new DrawingGroup(); using (var dc = drawing.Open())
        {
            var pen = new Pen(Drawing.Brush("#203348"), 1.8);
            for (int i = 0; i < 4; i++)
            {
                double length = i % 2 == 0 ? 22 : 13;
                if (!vertical) { double start = alignment == "left" ? 3 : alignment == "right" ? 25 - length : 14 - length / 2; dc.DrawLine(pen, new(start, 4 + i * 6), new(start + length, 4 + i * 6)); }
                else { double start = alignment == "top" ? 3 : alignment == "bottom" ? 25 - length : 14 - length / 2; dc.DrawLine(pen, new(4 + i * 6, start), new(4 + i * 6, start + length)); }
            }
        }
        drawing.Freeze(); return new Image { Source = new DrawingImage(drawing), Width = 25, Height = 25 };
    }
}
