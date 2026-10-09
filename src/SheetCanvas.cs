using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GeMMeElec;

public sealed class SheetCanvas : FrameworkElement
{
    public readonly Editor Editor;
    public HashSet<string> Selected { get; } = [];
    public string Mode { get; private set; } = "select";
    public string Symbol { get; set; } = "breaker1";
    public string? CatalogId { get; set; }
    public double Zoom { get; private set; } = .6;
    private Vector offset = new(40, 40);
    private Point pointer, dragStart, panStart;
    private Vector panOffset;
    private bool panning;
    private string? before;
    private readonly Dictionary<string, Point> original = [];
    private Endpoint? connection;
    private readonly List<Position> waypoints = [];
    private string previousSheet = "";
    public event Action? SelectionChanged;
    public event Action<string>? Status;
    public event Action<Point>? TextRequested;
    public SheetCanvas(Editor editor)
    {
        Editor = editor; previousSheet = editor.Sheet.Id; Focusable = true; ClipToBounds = true;
        Editor.Changed += () => { if (previousSheet != Editor.Sheet.Id) { previousSheet = Editor.Sheet.Id; Selected.Clear(); SetMode("select"); Fit(); } InvalidateVisual(); };
        Loaded += (_, _) => Fit();
    }
    public void SetMode(string mode)
    {
        CancelDrag(); Mode = mode; connection = null; waypoints.Clear();
        Cursor = mode == "select" ? Cursors.Arrow : Cursors.Cross;
        InvalidateVisual(); Status?.Invoke(mode switch { "place" => "Cliquer pour placer • Échap pour terminer", "wire" => "Cliquer une borne, puis une autre • Clics intermédiaires : coudes", "text" => "Cliquer sur le folio pour ajouter un texte", _ => "Sélection • Glisser : déplacer • Molette : zoom • Bouton central : déplacer la vue" });
    }
    public void Fit()
    {
        if (ActualWidth < 10 || ActualHeight < 10) return;
        Zoom = Math.Clamp(Math.Min((ActualWidth - 60) / Editor.Sheet.Width, (ActualHeight - 60) / Editor.Sheet.Height), .08, 3);
        offset = new((ActualWidth - Editor.Sheet.Width * Zoom) / 2, (ActualHeight - Editor.Sheet.Height * Zoom) / 2); InvalidateVisual();
    }
    public Point World(Point screen) => new((screen.X - offset.X) / Zoom, (screen.Y - offset.Y) / Zoom);
    public void Select(string id) { Selected.Clear(); Selected.Add(id); SelectionChanged?.Invoke(); InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Drawing.Brush("#E7EDF3"), null, new(0, 0, ActualWidth, ActualHeight));
        dc.PushTransform(new TranslateTransform(offset.X, offset.Y)); dc.PushTransform(new ScaleTransform(Zoom, Zoom));
        var sheet = Editor.Sheet;
        dc.DrawRectangle(Drawing.Brush("#CBD5E1"), null, new(6, 7, sheet.Width, sheet.Height));
        dc.DrawRectangle(Brushes.White, null, new(0, 0, sheet.Width, sheet.Height));
        double step = Zoom > .7 ? 10 : 50;
        for (double x = 40; x < sheet.Width - 30; x += step) for (double y = 40; y < sheet.Height - 90; y += step) dc.DrawEllipse(Drawing.Brush("#D8E2EC"), null, new(x, y), .65 / Zoom, .65 / Zoom);
        Drawing.Render(dc, Drawing.Scene(Editor.Project, sheet));
        var accent = Drawing.Brush("#007F86"); var pen = new Pen(accent, 2 / Zoom);
        foreach (var c in sheet.Components)
        {
            if (Selected.Contains(c.Id)) { var r = c.Bounds(); r.Inflate(7, 7); dc.DrawRoundedRectangle(null, pen, r, 4, 4); }
            if (Mode == "wire" || Selected.Contains(c.Id)) foreach (var p in c.Definition.Ports) dc.DrawEllipse(Brushes.White, pen, c.PortPoint(p.Id), 4 / Zoom, 4 / Zoom);
        }
        foreach (var w in sheet.Wires.Where(w => Selected.Contains(w.Id))) { var points = w.Route(sheet); for (int i = 1; i < points.Count; i++) dc.DrawLine(pen, points[i - 1], points[i]); }
        foreach (var n in sheet.Notes.Where(n => Selected.Contains(n.Id))) dc.DrawRectangle(null, pen, new(n.X - 3, n.Y - 3, Math.Max(60, n.Text.Length * 9), 26));
        if (connection is not null)
        {
            var c = sheet.Components.FirstOrDefault(x => x.Id == connection.ComponentId);
            if (c is not null)
            {
                var last = c.PortPoint(connection.PortId);
                foreach (var target in waypoints.Select(p => p.Point).Append(pointer)) { var elbow = new Point(target.X, last.Y); dc.DrawLine(pen, last, elbow); dc.DrawLine(pen, elbow, target); last = target; }
            }
        }
        if (Mode == "place" && IsMouseOver)
        {
            var ghost = new Component { Symbol = Symbol, X = Editor.Snap(pointer.X), Y = Editor.Snap(pointer.Y), Tag = "" };
            var r = ghost.Bounds(); dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30, 0, 127, 134)), pen, r);
        }
        dc.Pop(); dc.Pop();
    }
    private Endpoint? HitPort(Point p)
    {
        return Editor.Sheet.Components.SelectMany(c => c.Definition.Ports.Select(t => (End: new Endpoint(c.Id, t.Id), Distance: (c.PortPoint(t.Id) - p).Length))).Where(x => x.Distance < 10 / Zoom).OrderBy(x => x.Distance).Select(x => x.End).FirstOrDefault();
    }
    private string? Hit(Point p)
    {
        foreach (var c in Editor.Sheet.Components.AsEnumerable().Reverse()) { var r = c.Bounds(); r.Inflate(8, 8); if (r.Contains(p)) return c.Id; }
        foreach (var n in Editor.Sheet.Notes.AsEnumerable().Reverse()) if (new Rect(n.X, n.Y, Math.Max(60, n.Text.Length * 9), 24).Contains(p)) return n.Id;
        foreach (var w in Editor.Sheet.Wires.AsEnumerable().Reverse()) { var r = w.Route(Editor.Sheet); for (int i = 1; i < r.Count; i++) if (Distance(p, r[i - 1], r[i]) < 7 / Zoom) return w.Id; }
        return null;
    }
    private static double Distance(Point p, Point a, Point b) { var v = b - a; if (v.LengthSquared < .001) return (p - a).Length; double t = Math.Clamp(Vector.Multiply(p - a, v) / v.LengthSquared, 0, 1); return (p - (a + v * t)).Length; }
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus(); pointer = World(e.GetPosition(this));
        if (e.ChangedButton == MouseButton.Middle) { panning = true; panStart = e.GetPosition(this); panOffset = offset; CaptureMouse(); return; }
        if (e.ChangedButton == MouseButton.Right) { SetMode("select"); return; }
        if (e.ChangedButton != MouseButton.Left) return;
        if (pointer.X < 0 || pointer.Y < 0 || pointer.X > Editor.Sheet.Width || pointer.Y > Editor.Sheet.Height) return;
        try
        {
            if (Mode == "place") { var c = Editor.AddComponent(Symbol, pointer.X, pointer.Y, CatalogId); Select(c.Id); return; }
            if (Mode == "text") { TextRequested?.Invoke(pointer); return; }
            if (Mode == "wire")
            {
                var port = HitPort(pointer);
                if (port is not null) { if (connection is null) connection = port; else { Editor.Connect(connection, port, waypoints); connection = null; waypoints.Clear(); } }
                else if (connection is not null) waypoints.Add(new(Editor.Snap(pointer.X), Editor.Snap(pointer.Y)));
                InvalidateVisual(); return;
            }
            var hit = Hit(pointer); var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            if (hit is null) { Selected.Clear(); SelectionChanged?.Invoke(); InvalidateVisual(); return; }
            if (ctrl && Selected.Contains(hit)) { Selected.Remove(hit); SelectionChanged?.Invoke(); InvalidateVisual(); return; }
            if (!ctrl && !Selected.Contains(hit)) Selected.Clear(); Selected.Add(hit); SelectionChanged?.Invoke();
            original.Clear();
            foreach (var c in Editor.Sheet.Components.Where(c => Selected.Contains(c.Id))) original[c.Id] = new(c.X, c.Y);
            foreach (var n in Editor.Sheet.Notes.Where(c => Selected.Contains(c.Id))) original[n.Id] = new(n.X, n.Y);
            if (original.Count > 0) { before = ProjectStore.Serialize(Editor.Project); dragStart = pointer; Editor.InteractionInProgress = true; CaptureMouse(); }
            InvalidateVisual();
        }
        catch (Exception ex) { Status?.Invoke(ex.Message); }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        pointer = World(e.GetPosition(this));
        if (panning) { offset = panOffset + (e.GetPosition(this) - panStart); InvalidateVisual(); return; }
        if (before is not null)
        {
            double dx = Editor.Snap(pointer.X - dragStart.X), dy = Editor.Snap(pointer.Y - dragStart.Y);
            dx = Math.Clamp(dx, -original.Values.Min(p => p.X), Editor.Sheet.Width - original.Values.Max(p => p.X));
            dy = Math.Clamp(dy, -original.Values.Min(p => p.Y), Editor.Sheet.Height - original.Values.Max(p => p.Y));
            foreach (var c in Editor.Sheet.Components.Where(c => original.ContainsKey(c.Id))) { c.X = original[c.Id].X + dx; c.Y = original[c.Id].Y + dy; }
            foreach (var n in Editor.Sheet.Notes.Where(c => original.ContainsKey(c.Id))) { n.X = original[n.Id].X + dx; n.Y = original[n.Id].Y + dy; }
        }
        InvalidateVisual();
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle) panning = false;
        if (e.ChangedButton == MouseButton.Left && before is not null) { var snapshot = before; before = null; Editor.InteractionInProgress = false; Editor.Commit(snapshot); SelectionChanged?.Invoke(); }
        if (before is null && !panning) ReleaseMouseCapture();
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); if (before is not null) CancelDrag(); panning = false; }
    public void CancelDrag()
    {
        if (before is null) return;
        foreach (var c in Editor.Sheet.Components.Where(c => original.ContainsKey(c.Id))) { c.X = original[c.Id].X; c.Y = original[c.Id].Y; }
        foreach (var n in Editor.Sheet.Notes.Where(c => original.ContainsKey(c.Id))) { n.X = original[n.Id].X; n.Y = original[n.Id].Y; }
        before = null; Editor.InteractionInProgress = false; ReleaseMouseCapture(); InvalidateVisual();
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var screen = e.GetPosition(this); var world = World(screen); Zoom = Math.Clamp(Zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15), .08, 4);
        offset = new(screen.X - world.X * Zoom, screen.Y - world.Y * Zoom); InvalidateVisual(); e.Handled = true;
    }
}
