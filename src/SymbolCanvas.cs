using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GeMMeElec;

// The draft is local to the dialog. Neither previews nor its history mutate the project.
public sealed class SymbolDraft(SymbolDefinition source)
{
    public SymbolDefinition Definition { get; private set; } = source with { Shapes = [.. source.Shapes], Ports = [.. source.Ports], InternalConnections = [.. source.InternalConnections] };
    private readonly List<SymbolDefinition> undo = [], redo = [];
    public event Action? Changed;
    public void Preview(SymbolDefinition value) { Definition = value; }
    public void Commit(SymbolDefinition before)
    {
        try { CustomSymbols.Validate(Definition); }
        catch { Definition = before; Changed?.Invoke(); throw; }
        if (!Equivalent(before, Definition)) { undo.Add(before); if (undo.Count > 40) undo.RemoveAt(0); redo.Clear(); }
        Changed?.Invoke();
    }
    private static bool Equivalent(SymbolDefinition a, SymbolDefinition b) => a.Name == b.Name && a.Prefix == b.Prefix && a.Width == b.Width && a.Height == b.Height && a.Shapes.SequenceEqual(b.Shapes) && a.Ports.SequenceEqual(b.Ports) && a.InternalConnections.SequenceEqual(b.InternalConnections);
    public void Change(Func<SymbolDefinition, SymbolDefinition> action) { var before = Definition; Preview(action(before)); Commit(before); }
    public void Restore(SymbolDefinition value) { Definition = value; Changed?.Invoke(); }
    public void Undo() { if (undo.Count == 0) return; redo.Add(Definition); Definition = undo[^1]; undo.RemoveAt(undo.Count - 1); Changed?.Invoke(); }
    public void Redo() { if (redo.Count == 0) return; undo.Add(Definition); Definition = redo[^1]; redo.RemoveAt(redo.Count - 1); Changed?.Invoke(); }
}

public sealed class SymbolCanvas : FrameworkElement
{
    public SymbolDraft Draft { get; }
    public string Tool { get; private set; } = "select";
    public bool SnapToGrid { get; set; } = true;
    public int ShapeIndex { get; private set; } = -1;
    public int PortIndex { get; private set; } = -1;
    public event Action? SelectionChanged;
    public event Action? ToolChanged;
    public event Action<string>? Status;
    private double zoom = 2;
    private Vector offset;
    private bool fitted, panning;
    private Point start, panStart;
    private Vector panOffset;
    private SymbolDefinition? before;
    private int handle = -1;
    private DrawingGroup? drawing;
    public SymbolCanvas(SymbolDraft draft)
    {
        Draft = draft; Focusable = true; ClipToBounds = true;
        Draft.Changed += () => { drawing = null; ShapeIndex = Math.Min(ShapeIndex, Draft.Definition.Shapes.Count - 1); PortIndex = Math.Min(PortIndex, Draft.Definition.Ports.Count - 1); InvalidateVisual(); };
        Loaded += (_, _) => Fit();
    }
    public void Fit()
    {
        if (ActualWidth < 10 || ActualHeight < 10) return;
        var d = Draft.Definition; var bounds = new Rect(-d.Width / 2, -d.Height / 2, d.Width, d.Height);
        foreach (var s in d.Shapes) bounds.Union(Bounds(s)); foreach (var p in d.Ports) bounds.Union(new Point(p.X, p.Y));
        zoom = Math.Clamp(Math.Min((ActualWidth - 100) / Math.Max(40, bounds.Width), (ActualHeight - 100) / Math.Max(40, bounds.Height)), .02, 5);
        offset = new(ActualWidth / 2 - (bounds.Left + bounds.Width / 2) * zoom, ActualHeight / 2 - (bounds.Top + bounds.Height / 2) * zoom); fitted = true; InvalidateVisual();
    }
    public void SetTool(string tool) { Cancel(); Tool = tool; Cursor = tool == "select" ? Cursors.Arrow : Cursors.Cross; ToolChanged?.Invoke(); Status?.Invoke(tool == "select" ? "Glisser une partie ou une poignée • Suppr : supprimer • Molette : zoom" : "Cliquer-glisser pour dessiner • Texte / borne : cliquer pour placer • Échap : sélection"); }
    public void SelectPart(int shape, int port) { ShapeIndex = shape; PortIndex = port; SelectionChanged?.Invoke(); InvalidateVisual(); }
    public void DeleteSelection()
    {
        Cancel(); if (ShapeIndex < 0 && PortIndex < 0) return;
        Draft.Change(d => d with { Shapes = d.Shapes.Where((_, i) => i != ShapeIndex).ToList(), Ports = d.Ports.Where((_, i) => i != PortIndex).ToList(), InternalConnections = d.InternalConnections.Where(c => PortIndex < 0 || c.From != d.Ports[PortIndex].Id && c.To != d.Ports[PortIndex].Id).ToList() }); SelectPart(-1, -1);
    }
    public void DuplicateSelection()
    {
        Cancel(); var d = Draft.Definition;
        if (ShapeIndex >= 0) { var s = Move(d.Shapes[ShapeIndex], new(5, 5)); Draft.Change(d => d with { Shapes = [.. d.Shapes, s] }); SelectPart(Draft.Definition.Shapes.Count - 1, -1); }
        else if (PortIndex >= 0) { var p = d.Ports[PortIndex]; Draft.Change(d => d with { Ports = [.. d.Ports, new(NextPort(d), p.X + 5, p.Y + 5)] }); SelectPart(-1, Draft.Definition.Ports.Count - 1); }
    }
    private static string NextPort(SymbolDefinition d) { int n = 1; while (d.Ports.Any(p => p.Id == n.ToString())) n++; return n.ToString(); }
    private Point World(Point screen) => new((screen.X - offset.X) / zoom, (screen.Y - offset.Y) / zoom);
    private Point Snap(Point p) => SnapToGrid ? new(Math.Round(p.X / 5) * 5, Math.Round(p.Y / 5) * 5) : p;
    internal static Rect Bounds(Shape s)
    {
        var r = s.Kind switch { "line" => new Rect(new Point(s.X, s.Y), new Point(s.A, s.B)), "rect" or "image" => new Rect(s.X, s.Y, s.A, s.B), "circle" or "dot" => new Rect(s.X - s.A, s.Y - s.A, s.A * 2, s.A * 2), "text" => new Rect(new Point(s.X, s.Y), Drawing.Measure(s.Text, s.A)), _ => Rect.Empty };
        return s.Rotation == 0 ? r : new RotateTransform(s.Rotation, s.X, s.Y).TransformBounds(r);
    }
    private static Shape Move(Shape s, Vector v) => s with { X = s.X + v.X, Y = s.Y + v.Y, A = s.Kind == "line" ? s.A + v.X : s.A, B = s.Kind == "line" ? s.B + v.Y : s.B };
    private List<Point> Handles(Shape s)
    {
        if (s.Rotation != 0) return [];
        return s.Kind switch { "line" => [new(s.X, s.Y), new(s.A, s.B)], "rect" => [new(s.X, s.Y), new(s.X + s.A, s.Y + s.B)], "circle" or "dot" => [new(s.X + s.A, s.Y)], _ => [] };
    }
    internal void Begin(Point point)
    {
        start = Snap(point); handle = -1; var d = Draft.Definition;
        if (Tool == "select")
        {
            if (ShapeIndex >= 0) { var handles = Handles(d.Shapes[ShapeIndex]); handle = handles.FindIndex(p => (point - p).Length < 7 / zoom); }
            if (handle < 0)
            {
                int port = d.Ports.FindLastIndex(p => (point - new Point(p.X, p.Y)).Length < 8 / zoom);
                int shape = port >= 0 ? -1 : d.Shapes.FindLastIndex(s => Hit(s, point, 5 / zoom)); SelectPart(shape, port);
            }
            if (ShapeIndex < 0 && PortIndex < 0) return;
        }
        before = d;
        if (Tool == "port") { Draft.Preview(d with { Ports = [.. d.Ports, new(NextPort(d), start.X, start.Y)] }); SelectPart(-1, d.Ports.Count); }
        else if (Tool != "select")
        {
            var s = new Shape(Tool, start.X, start.Y, Tool == "text" ? 12 : Tool == "line" ? start.X : 0, Tool == "line" ? start.Y : 0, Tool == "text" ? "Texte" : "");
            Draft.Preview(d with { Shapes = [.. d.Shapes, s] }); SelectPart(d.Shapes.Count, -1);
        }
        drawing = null; InvalidateVisual();
    }
    private static bool Hit(Shape s, Point point, double tolerance)
    {
        if (s.Rotation != 0) point = new RotateTransform(-s.Rotation, s.X, s.Y).Transform(point);
        if (s.Kind == "line") { var a = new Point(s.X, s.Y); var v = new Point(s.A, s.B) - a; double t = v.LengthSquared < .0001 ? 0 : Math.Clamp(Vector.Multiply(point - a, v) / v.LengthSquared, 0, 1); return (point - (a + t * v)).Length <= tolerance; }
        var bounds = Bounds(s with { Rotation = 0 }); bounds.Inflate(tolerance, tolerance); return bounds.Contains(point);
    }
    internal void MoveTo(Point point)
    {
        if (before is null) return; point = Snap(point); var v = point - start; var d = Draft.Definition;
        if (Tool == "select")
        {
            if (PortIndex >= 0) { var ports = before.Ports.ToList(); var p = ports[PortIndex]; ports[PortIndex] = p with { X = p.X + v.X, Y = p.Y + v.Y }; Draft.Preview(before with { Ports = ports }); }
            else if (ShapeIndex >= 0)
            {
                var shapes = before.Shapes.ToList(); var s = shapes[ShapeIndex];
                if (handle < 0) s = Move(s, v);
                else if (s.Kind == "line") s = handle == 0 ? s with { X = point.X, Y = point.Y } : s with { A = point.X, B = point.Y };
                else if (s.Kind == "rect") { var anchor = handle == 0 ? new Point(s.X + s.A, s.Y + s.B) : new Point(s.X, s.Y); var r = new Rect(anchor, point); s = s with { X = r.X, Y = r.Y, A = r.Width, B = r.Height }; }
                else s = s with { A = (point - new Point(s.X, s.Y)).Length };
                shapes[ShapeIndex] = s; Draft.Preview(before with { Shapes = shapes });
            }
        }
        else if (Tool == "port") { var ports = d.Ports.ToList(); ports[PortIndex] = ports[PortIndex] with { X = point.X, Y = point.Y }; Draft.Preview(d with { Ports = ports }); }
        else
        {
            var shapes = d.Shapes.ToList(); var s = shapes[ShapeIndex]; var r = new Rect(start, point);
            shapes[ShapeIndex] = Tool switch { "line" => s with { A = point.X, B = point.Y }, "rect" => s with { X = r.X, Y = r.Y, A = r.Width, B = r.Height }, "circle" => s with { A = v.Length }, _ => s };
            Draft.Preview(d with { Shapes = shapes });
        }
        drawing = null; InvalidateVisual();
    }
    internal void End()
    {
        if (before is null) return; var saved = before; before = null;
        // A click without a drag must not leave an invisible zero-sized primitive.
        var s = Tool is "line" or "rect" or "circle" && ShapeIndex >= 0 ? Draft.Definition.Shapes[ShapeIndex] : null;
        if (s is not null && (s.Kind == "line" ? Math.Abs(s.X - s.A) + Math.Abs(s.Y - s.B) < .01 : s.A < .01 || s.Kind == "rect" && s.B < .01)) { Draft.Restore(saved); SelectPart(-1, -1); }
        else { Draft.Commit(saved); SelectionChanged?.Invoke(); }
    }
    public void Cancel() { if (before is null) return; var saved = before; before = null; Draft.Restore(saved); SelectPart(-1, -1); ReleaseMouseCapture(); }
    protected override void OnRender(DrawingContext dc)
    {
        if (!fitted) Fit();
        dc.DrawRectangle(Brushes.White, null, new(0, 0, ActualWidth, ActualHeight));
        dc.PushTransform(new TranslateTransform(offset.X, offset.Y)); dc.PushTransform(new ScaleTransform(zoom, zoom));
        var min = World(new(0, 0)); var max = World(new(ActualWidth, ActualHeight)); double step = 5; while (step * zoom < 12) step *= 2;
        for (double x = Math.Ceiling(min.X / step) * step; x < max.X; x += step) for (double y = Math.Ceiling(min.Y / step) * step; y < max.Y; y += step) dc.DrawEllipse(Brushes.LightSlateGray, null, new(x, y), .65 / zoom, .65 / zoom);
        dc.DrawLine(new Pen(Brushes.LightBlue, 1 / zoom), new(min.X, 0), new(max.X, 0)); dc.DrawLine(new Pen(Brushes.LightBlue, 1 / zoom), new(0, min.Y), new(0, max.Y));
        var d = Draft.Definition; dc.DrawRectangle(null, new Pen(Brushes.LightGray, 1 / zoom) { DashStyle = DashStyles.Dash }, new(-d.Width / 2, -d.Height / 2, d.Width, d.Height));
        if (drawing is null) { drawing = new(); using (var context = drawing.Open()) Drawing.Render(context, d.Shapes.Select(s => new Ink(s.Kind, s.X, s.Y, s.A, s.B, s.Text, Size: s.A, Rotation: s.Rotation, Color: "#000000"))); drawing.Freeze(); }
        dc.DrawDrawing(drawing);
        foreach (var p in d.Ports) { dc.DrawEllipse(Brushes.White, new Pen(Brushes.Teal, 1.5 / zoom), new(p.X, p.Y), 4 / zoom, 4 / zoom); Drawing.Render(dc, [new("text", p.X + 6 / zoom, p.Y - 17 / zoom, Text: p.Id, Size: 12 / zoom, Color: "#007F86")]); }
        var accent = new Pen(Brushes.Teal, 1.5 / zoom);
        if (ShapeIndex >= 0) { var s = d.Shapes[ShapeIndex]; var r = Bounds(s); r.Inflate(3 / zoom, 3 / zoom); dc.DrawRectangle(null, accent, r); foreach (var h in Handles(s)) dc.DrawRectangle(Brushes.White, accent, new(h.X - 4 / zoom, h.Y - 4 / zoom, 8 / zoom, 8 / zoom)); }
        if (PortIndex >= 0) { var p = d.Ports[PortIndex]; dc.DrawEllipse(null, accent, new(p.X, p.Y), 8 / zoom, 8 / zoom); }
        dc.Pop(); dc.Pop();
    }
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus();
        if (e.ChangedButton == MouseButton.Middle) { panning = true; panStart = e.GetPosition(this); panOffset = offset; CaptureMouse(); }
        else if (e.ChangedButton == MouseButton.Left) { Begin(World(e.GetPosition(this))); if (before is not null) CaptureMouse(); }
        else if (e.ChangedButton == MouseButton.Right)
        {
            SetTool("select"); var point = World(e.GetPosition(this)); var d = Draft.Definition;
            int port = d.Ports.FindLastIndex(p => (point - new Point(p.X, p.Y)).Length < 8 / zoom);
            SelectPart(port >= 0 ? -1 : d.Shapes.FindLastIndex(s => Hit(s, point, 5 / zoom)), port);
            return;
        }
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (panning) { offset = panOffset + (e.GetPosition(this) - panStart); InvalidateVisual(); return; }
        var p = World(e.GetPosition(this)); MoveTo(p); var q = Snap(p); Status?.Invoke($"X : {q.X:0.##}   Y : {q.Y:0.##}   •   Zoom {zoom:P0}   •   Grille : 5 unités");
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right) return;
        try { if (e.ChangedButton == MouseButton.Middle) panning = false; else if (e.ChangedButton == MouseButton.Left) { MoveTo(World(e.GetPosition(this))); End(); } }
        catch (Exception ex) { Cancel(); Status?.Invoke(ex.Message); }
        finally { ReleaseMouseCapture(); e.Handled = true; }
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); panning = false; Cancel(); }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (before is not null) return; var screen = e.GetPosition(this); var world = World(screen); zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15), .02, 20); offset = new(screen.X - world.X * zoom, screen.Y - world.Y * zoom); InvalidateVisual(); e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.Key == Key.Escape) SetTool("select");
        else if (e.Key == Key.Delete) DeleteSelection();
        else if (ctrl && e.Key == Key.Z) { Cancel(); Draft.Undo(); SelectPart(-1, -1); }
        else if (ctrl && e.Key == Key.Y) { Cancel(); Draft.Redo(); SelectPart(-1, -1); }
        else if (ctrl && e.Key == Key.D) DuplicateSelection();
        else if (e.Key == Key.F) Fit(); else return;
        e.Handled = true;
    }
}
