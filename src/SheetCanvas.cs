using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GeMMeElec;

public sealed partial class SheetCanvas : FrameworkElement
{
    public static readonly DependencyProperty WorkspaceBackgroundProperty = DependencyProperty.Register(nameof(WorkspaceBackground), typeof(Brush), typeof(SheetCanvas), new FrameworkPropertyMetadata(Drawing.Brush("#E7EDF3"), FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush WorkspaceBackground { get => (Brush)GetValue(WorkspaceBackgroundProperty); set => SetValue(WorkspaceBackgroundProperty, value); }
    public readonly Editor Editor;
    public bool GridVisible { get; set; } = true;
    public double GridIntensity { get; set; } = 25;
    private (string Id, Point Point)? hoveredWire;
    private Dictionary<string, List<Point>>? routeCache;
    private DrawingBrush? gridCache;
    private Rect gridBounds;
    private (bool, double, double, double, double) gridKey;
    public HashSet<string> Selected { get; } = [];
    public string Mode { get; private set; } = "select";
    public string Symbol { get; set; } = "breaker1";
    public string? CatalogId { get; set; }
    public double Zoom { get; private set; } = .6;
    private Vector offset = new(40, 40);
    private Point pointer, dragStart, panStart;
    private Vector panOffset;
    private bool panning;
    private Point? marqueeStart;
    private bool marqueeAdd;
    private string? before;
    private readonly Dictionary<string, Point> original = [];
    private readonly Dictionary<string, List<Position>> originalWaypoints = [];
    private Rect moveBounds;
    private Endpoint? connection;
    private Endpoint? hoveredPort;
    private string? editingWire;
    private List<Point>? originalRoute;
    private int segmentIndex;
    private bool reconnectFrom, connectionPressed, connectionMoved;
    private Point pressPosition;
    private readonly List<Position> waypoints = [];
    private string previousSheet = "";
    private CanvasScene? retainedScene;
    private (Component Component,Port Port,Endpoint Endpoint)[]? portHandles;
    private (string ComponentId, string Key, LabelPlacement Placement, bool DetachInline)? textDrag;
    private bool textMoved;
    private (string Id, WireNumberPlacement Placement)? wireTextDrag;
    private string? highlightedCircuitRoot;
    private double? guideX, guideY;
    public HashSet<string> HighlightedWires { get; } = [];
    public void HighlightCircuit(string wireId) { highlightedCircuitRoot = wireId; RefreshCircuitHighlight(); InvalidateVisual(); }
    public void ClearCircuitHighlight() { highlightedCircuitRoot = null; HighlightedWires.Clear(); InvalidateVisual(); }
    private void RefreshCircuitHighlight()
    {
        HighlightedWires.Clear(); if (highlightedCircuitRoot is null) return;
        if (!Editor.Project.Sheets.Any(s => s.Wires.Any(w => w.Id == highlightedCircuitRoot))) { highlightedCircuitRoot = null; return; }
        foreach (var row in ProjectNavigation.Circuit(Editor.Project, highlightedCircuitRoot)) HighlightedWires.Add(row.ObjectId);
    }
    public event Action? SelectionChanged;
    public event Action<string>? Status;
    public event Action<Point>? TextRequested;
    public event Action<string>? ReferenceRequested;
    public event Action? ContextRequested;
    internal bool ActivateReference(Point point)
    {
        var refs = new CrossReferences(Editor.Project);
        Rect ReferenceBounds(Component component){var bounds=component.Bounds();foreach(var ink in Reading.ComponentTexts(Editor.Project,component,refs))bounds.Union(Drawing.TextBounds(ink));bounds.Inflate(5,5);return bounds;}
        var c = Editor.Sheet.Components.LastOrDefault(c => CrossReferences.IsPoint(c) && ReferenceBounds(c).Contains(point));
        c ??= Editor.Sheet.Components.LastOrDefault(c => (CoilContacts.IsCoil(c) || CoilContacts.IsContact(c)) && Reading.Text("coilReferences",ComponentTexts.Ink(Editor.Project, c, refs, "coilReferences")) is Ink ink && Drawing.TextBounds(ink).Contains(point));
        if (c is null) return false;
        SetMode("select"); Select(c.Id); ReferenceRequested?.Invoke(c.Id); return true;
    }
    internal void SelectRectangle(Rect area, bool add)
    {
        if (!add) Selected.Clear();
        // Whole objects are enclosed: crossing an unrelated wire does not select it.
        foreach (var c in Editor.Sheet.Components.Where(c => !Junctions.Automatic(c) && area.Contains(c.Bounds()))) Selected.Add(c.Id);
        foreach (var n in Editor.Sheet.Notes) if (area.Contains(FreeGraphics.NoteBounds(Editor.Project,Editor.Sheet,n))) Selected.Add(n.Id);
        foreach(var g in Editor.Sheet.Graphics)if(area.Contains(g.Bounds()))Selected.Add(g.Id);
        var index = Editor.Sheet.Components.ToDictionary(c => c.Id);
        foreach (var w in Editor.Sheet.Wires) if (w.Route(Editor.Sheet, index).All(area.Contains)) Selected.Add(w.Id);
        SelectionChanged?.Invoke(); InvalidateVisual(); Status?.Invoke($"{Selected.Count} éléments sélectionnés • Ctrl + glisser pour ajouter à la sélection");
    }
    internal void ContextAt(Point point)
    {
        SetMode("select"); var hit = Hit(point);
        if (hit is not null && !Selected.Contains(hit)) Select(hit);
        if (hit is null) { Selected.Clear(); SelectionChanged?.Invoke(); }
        ContextRequested?.Invoke(); InvalidateVisual();
    }
    public void CenterOn(double x, double y) { offset = new(ActualWidth / 2 - x * Zoom, ActualHeight / 2 - y * Zoom); InvalidateVisual(); }
    public SheetCanvas(Editor editor)
    {
        Editor = editor; previousSheet = editor.Sheet.Id; Focusable = true; ClipToBounds = true;
        ToolTip = "Maj + clic gauche puis glisser sur un texte : déplacer ce texte · Échap : annuler";
        Editor.Changed += () => { retainedScene=null; portHandles=null; routeCache=null; RefreshCircuitHighlight(); if (previousSheet != Editor.Sheet.Id) { previousSheet = Editor.Sheet.Id; Selected.Clear(); SetMode("select"); Fit(); } InvalidateVisual(); };
        bool firstLoad=true; Loaded += (_, _) => { if(firstLoad){firstLoad=false;Fit();} };
    }
    public void SetMode(string mode)
    {
        CancelDrag(); CancelBand();Mode = mode; connection = null; waypoints.Clear();
        Cursor = mode == "select" ? Cursors.Arrow : Cursors.Cross;
        InvalidateVisual(); Status?.Invoke(mode switch { "place" => "Cliquer pour placer • Tirer depuis une borne pour câbler • Échap : terminer", "wire" => "Tirer d’une borne à l’autre, ou cliquer les bornes et les points de passage", "text" => "Cliquer sur le folio pour ajouter un texte", _ => "Tirer depuis une borne : câbler • Sélectionner un fil : déplacer ses poignées" });
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
        dc.DrawRectangle(WorkspaceBackground, null, new(0, 0, ActualWidth, ActualHeight));
        dc.PushTransform(new TranslateTransform(offset.X, offset.Y)); dc.PushTransform(new ScaleTransform(Zoom, Zoom));
        var sheet = Editor.Sheet;
        dc.DrawRectangle(Drawing.Brush("#CBD5E1"), null, new(6, 7, sheet.Width, sheet.Height));
        dc.DrawRectangle(Brushes.White, null, new(0, 0, sheet.Width, sheet.Height));
        var key = (GridVisible && sheet.BomPage is null && sheet.TerminalPlanId.Length==0 && sheet.CabinetPlanId.Length==0, GridIntensity, Zoom, sheet.Width, DocumentModel.DrawingBottom(Editor.Project, sheet));
        if (gridCache is null || gridKey != key)
        {
            gridKey=key;gridCache=null;
            if(key.Item1&&GridIntensity>0&&key.Item5>40&&sheet.Width>70)
            {
                double step=Zoom>.7?10:50;while(sheet.Width/step*sheet.Height/step>30000)step*=5;
                double radius=.65/Zoom;var tile=new DrawingGroup();using(var draw=tile.Open())draw.DrawEllipse(Drawing.Brush("#60758A"),null,new(step/2,step/2),radius,radius);tile.Freeze();
                gridCache=new DrawingBrush(tile){Viewbox=new Rect(0,0,step,step),ViewboxUnits=BrushMappingMode.Absolute,Viewport=new Rect(40-step/2,40-step/2,step,step),ViewportUnits=BrushMappingMode.Absolute,TileMode=TileMode.Tile,Stretch=Stretch.Fill,Opacity=Math.Clamp(GridIntensity/100,0,1)};gridCache.Freeze();
                double lastX=40+(Math.Ceiling((sheet.Width-70)/step)-1)*step,lastY=40+(Math.Ceiling((key.Item5-40)/step)-1)*step;
                gridBounds=new Rect(40-radius,40-radius,lastX-40+2*radius,lastY-40+2*radius);
            }
        }
        if(gridCache is not null)dc.DrawRectangle(gridCache,null,gridBounds);
        foreach (var wire in sheet.Wires.Where(w => HighlightedWires.Contains(w.Id))) { var path = wire.Route(sheet); var highlight = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 170, 0)), wire.Thickness + 7 / Zoom); for (int i = 1; i < path.Count; i++) dc.DrawLine(highlight, path[i-1], path[i]); }
        RenderBand(dc); RenderBlock(dc);
        retainedScene ??= new(Editor.Project,sheet,Reading);retainedScene.Update();
        dc.DrawDrawing(retainedScene.Drawing);RenderGraphicsOverlay(dc);
        var accent = Drawing.Brush("#007F86"); var pen = new Pen(accent, 2 / Zoom);
        foreach (var c in sheet.Components)
        {
            if (Selected.Contains(c.Id)) { var r = c.Bounds(); r.Inflate(7, 7); dc.DrawRoundedRectangle(null, pen, r, 4, 4); }
            if (Mode == "wire" || connection is not null || Selected.Contains(c.Id) || hoveredPort?.ComponentId == c.Id) foreach (var p in c.Definition.Ports) dc.DrawEllipse(Brushes.White, pen, c.PortPoint(p.Id), 4 / Zoom, 4 / Zoom);
        }
        foreach (var w in sheet.Wires.Where(w => Selected.Contains(w.Id)))
        {
            var points = w.Route(sheet);
            var highlight = new Pen(new SolidColorBrush(Color.FromArgb(55, 0, 127, 134)), w.Thickness + 4 / Zoom);
            var wirePen = new Pen(Drawing.Brush(w.Color), w.Thickness) { DashStyle = new DashStyle(Drawing.DashPattern(w.LineStyle), 0) };
            for (int i = 1; i < points.Count; i++) { dc.DrawLine(highlight, points[i - 1], points[i]); dc.DrawLine(wirePen, points[i - 1], points[i]); var mid = Mid(points[i - 1], points[i]); dc.DrawRectangle(Brushes.White, pen, new(mid.X - 4 / Zoom, mid.Y - 4 / Zoom, 8 / Zoom, 8 / Zoom)); }
            dc.DrawEllipse(accent, new Pen(Brushes.White, 1 / Zoom), points[0], 5 / Zoom, 5 / Zoom);
            dc.DrawEllipse(accent, new Pen(Brushes.White, 1 / Zoom), points[^1], 5 / Zoom, 5 / Zoom);
        }
        foreach (var n in sheet.Notes.Where(n => Selected.Contains(n.Id))) {var r=FreeGraphics.NoteBounds(Editor.Project,sheet,n,Reading.TextScale);r.Inflate(3,3);dc.DrawRectangle(null,pen,r);}
        if (connection is not null)
        {
            var c = sheet.Components.FirstOrDefault(x => x.Id == connection.ComponentId);
            if (c is not null)
            {
                var last = c.PortPoint(connection.PortId);
                var end = hoveredPort is null ? hoveredWire?.Point ?? pointer : PortPoint(hoveredPort);
                var preview = new Pen(accent, 2 / Zoom) { DashStyle = DashStyles.Dash };
                if (hoveredPort is null && hoveredWire is { } branchTarget && routeCache is not null)
                {
                    var targetRoute = routeCache[branchTarget.Id]; var hit = Editor.WirePoint(targetRoute, branchTarget.Point);
                    var stops = editingWire is null ? waypoints.AsEnumerable() : reconnectFrom ? sheet.Wires.Single(w => w.Id == editingWire).Waypoints.AsEnumerable().Reverse() : sheet.Wires.Single(w => w.Id == editingWire).Waypoints;
                    var path = Editor.BranchPath(last, Editor.BranchStops(sheet, connection, stops, hit.Point), targetRoute, hit.Point, hit.Segment);
                    for (int i = 1; i < path.Count; i++) dc.DrawLine(preview, path[i - 1], path[i]);
                }
                else
                foreach (var target in waypoints.Select(p => p.Point).Append(end)) { if (waypoints.Count == 0) { var y = Editor.Snap((last.Y + target.Y) / 2); var a = new Point(last.X, y); var b = new Point(target.X, y); dc.DrawLine(preview, last, a); dc.DrawLine(preview, a, b); dc.DrawLine(preview, b, target); } else { var elbow = new Point(target.X, last.Y); dc.DrawLine(preview, last, elbow); dc.DrawLine(preview, elbow, target); } last = target; }
            }
        }
        if (hoveredPort is not null && sheet.Components.Any(c => c.Id == hoveredPort.ComponentId)) dc.DrawEllipse(accent, new Pen(Brushes.White, 1 / Zoom), PortPoint(hoveredPort), 6 / Zoom, 6 / Zoom);
        if (hoveredWire is { } branch && connection is not null && hoveredPort is null) dc.DrawEllipse(accent, new Pen(Brushes.White, 1 / Zoom), branch.Point, 6 / Zoom, 6 / Zoom);
        if (sheet.BomPage is null && sheet.TerminalPlanId.Length==0 && sheet.CabinetPlanId.Length==0 && Mode == "place" && IsMouseOver && hoveredPort is null)
        {
            var ghost = new Component { Symbol = Symbol, ResolvedDefinition = CustomSymbols.Resolve(Editor.Project, Symbol), X = Editor.Snap(pointer.X), Y = Editor.Snap(pointer.Y), Tag = "" };
            var r = ghost.Bounds(); dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30, 0, 127, 134)), pen, r);
        }
        if (marqueeStart is Point start) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(25, 0, 127, 134)), new Pen(accent, 1 / Zoom) { DashStyle = DashStyles.Dash }, new Rect(start, pointer));
        var guidePen = new Pen(Drawing.Brush("#AD3E88"), 1 / Zoom) { DashStyle = DashStyles.Dash };
        if (guideX is double gx) dc.DrawLine(guidePen, new(gx, 30), new(gx, DocumentModel.DrawingBottom(Editor.Project, sheet)));
        if (guideY is double gy) dc.DrawLine(guidePen, new(30, gy), new(sheet.Width-30, gy));
        dc.Pop(); dc.Pop();
    }
    private Endpoint? HitPort(Point p)
    {
        portHandles ??= Editor.Sheet.Components.SelectMany(c=>c.Definition.Ports.Select(port=>(c,port,new Endpoint(c.Id,port.Id)))).ToArray();
        double nearest=64/(Zoom*Zoom);Endpoint? result=null;foreach(var handle in portHandles){double distance=(handle.Component.Transform(handle.Port.X,handle.Port.Y)-p).LengthSquared;if(distance<nearest){nearest=distance;result=handle.Endpoint;}}return result;
    }
    private Point PortPoint(Endpoint e) => Editor.Sheet.Components.Single(c => c.Id == e.ComponentId).PortPoint(e.PortId);
    private (string Id, Point Point)? HitWire(Point p)
    {
        if (routeCache is null) { var index = Editor.Sheet.Components.ToDictionary(c => c.Id); routeCache = Editor.Sheet.Wires.ToDictionary(w => w.Id, w => w.Route(Editor.Sheet, index)); }
        (string Id, Point Point)? result = null; double nearest = 8 / Zoom;
        foreach (var entry in routeCache) { if (entry.Key == editingWire) continue; var hit = Editor.WirePoint(entry.Value, p); if (hit.Distance < nearest) { nearest = hit.Distance; result = (entry.Key, hit.Point); } }
        return result;
    }
    private void CompleteBranch((string Id, Point Point) target)
    {
        var from = connection!; var id = editingWire; var end = reconnectFrom; var points = waypoints.ToList(); ClearConnection();
        var wire = Editor.Branch(from, target.Id, target.Point, points, id, end); Select(wire.Id);
        Status?.Invoke("Dérivation raccordée : jonction automatique suivant le câblage.");
    }
    private static Point Mid(Point a, Point b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    private void StartConnection(Endpoint from, MouseButtonEventArgs e, string? wireId = null, bool changeFrom = false)
    {
        connection = from; editingWire = wireId; reconnectFrom = changeFrom; connectionPressed = true; connectionMoved = false; pressPosition = e.GetPosition(this); Editor.InteractionInProgress = true; CaptureMouse(); InvalidateVisual();
    }
    private void ClearConnection()
    {
        connection = null; hoveredWire = null; editingWire = null; connectionPressed = false; connectionMoved = false; waypoints.Clear(); Editor.InteractionInProgress = false; ReleaseMouseCapture(); InvalidateVisual();
    }
    private void CompleteConnection(Endpoint destination)
    {
        var from = connection!; var id = editingWire; var end = reconnectFrom; var points = waypoints.ToList(); ClearConnection();
        if (id is not null) { Editor.Reconnect(id, end, destination); Select(id); }
        else { var w = Editor.Connect(from, destination, points); Select(w.Id); }
        Status?.Invoke("Liaison raccordée • Poignées carrées : tracé • Poignées rondes : bornes");
    }
    private void StartSegment(Wire wire, int index)
    {
        before = ProjectStore.Serialize(Editor.Project); wire.RoutingMode="manual";wire.RoutingWarning="";editingWire = wire.Id; segmentIndex = index; originalRoute = wire.Route(Editor.Sheet); dragStart = pointer; Editor.InteractionInProgress = true; CaptureMouse();
        Status?.Invoke("Déplacer le segment • Bornes conservées • Échap : annuler"); InvalidateVisual();
    }
    private string? Hit(Point p)
    {
        CrossReferences? references = null;
        foreach (var c in Editor.Sheet.Components.AsEnumerable().Reverse().Where(c => !Junctions.Automatic(c))) { var r = CrossReferences.IsPoint(c) ? ReferenceDisplay.HitBounds(Editor.Project, c, references ??= new CrossReferences(Editor.Project)) : c.Bounds(); r.Inflate(8, 8); if (r.Contains(p)) return c.Id; }
        foreach (var n in Editor.Sheet.Notes.AsEnumerable().Reverse().Where(_=>Reading.Notes)) if (FreeGraphics.NoteBounds(Editor.Project,Editor.Sheet,n,Reading.TextScale).Contains(p)) return n.Id;
        foreach (var w in Editor.Sheet.Wires.AsEnumerable().Reverse()) { var r = w.Route(Editor.Sheet); for (int i = 1; i < r.Count; i++) if (Distance(p, r[i - 1], r[i]) < 7 / Zoom) return w.Id; }
        return HitGraphic(p);
    }
    private static double Distance(Point p, Point a, Point b) { var v = b - a; if (v.LengthSquared < .001) return (p - a).Length; double t = Math.Clamp(Vector.Multiply(p - a, v) / v.LengthSquared, 0, 1); return (p - (a + v * t)).Length; }
    internal void BeginSelectionMove(Point point)
    {
        original.Clear(); originalWaypoints.Clear();originalGraphics.Clear(); moveBounds = Rect.Empty;
        foreach (var c in Editor.Sheet.Components.Where(c => Selected.Contains(c.Id) || Junctions.Automatic(c) && Editor.Sheet.Wires.Where(w => w.From.ComponentId == c.Id || w.To.ComponentId == c.Id).All(w => Selected.Contains(w.Id)))) original[c.Id] = new(c.X, c.Y);
        foreach (var n in Editor.Sheet.Notes.Where(n => Selected.Contains(n.Id))) original[n.Id] = new(n.X, n.Y);
        foreach (var w in Editor.Sheet.Wires.Where(w => original.ContainsKey(w.From.ComponentId) && original.ContainsKey(w.To.ComponentId))) originalWaypoints[w.Id] = w.Waypoints.ToList();
        foreach(var g in Editor.Sheet.Graphics.Where(g=>Selected.Contains(g.Id))){originalGraphics[g.Id]=DocumentModel.Copy(g);moveBounds.Union(g.Bounds());}
        foreach (var p in original.Values.Concat(originalWaypoints.Values.SelectMany(v => v).Select(p => p.Point))) moveBounds.Union(p);
        if (original.Count > 0||originalGraphics.Count>0) { before = ProjectStore.Serialize(Editor.Project); dragStart = point; Editor.InteractionInProgress = true; }
    }
    private (Component Component, ComponentTextTarget Text, Rect Bounds)? HitText(Point point)
    {
        CrossReferences? refs = null;
        foreach (var c in Editor.Sheet.Components.AsEnumerable().Reverse().OrderByDescending(c => Selected.Contains(c.Id)).Where(c => !Junctions.Automatic(c)))
        foreach (var text in ComponentTexts.List(c).AsEnumerable().Reverse())
        {
            if (!text.Placement.Visible) continue;
            var ink = Reading.TextInk(Editor.Project, c, refs ??= new(Editor.Project), text.Key);
            if (ink is not null && Drawing.TextBounds(ink) is var bounds && bounds.Contains(point)) return (c, text, bounds);
        }
        return null;
    }
    internal bool BeginTextMove(Point point)
    {
        if (before is not null || connection is not null || (Editor.Sheet.BomPage is not null || Editor.Sheet.TerminalPlanId.Length>0 || Editor.Sheet.CabinetPlanId.Length>0)) return false;
        var wire = Editor.Sheet.Wires.AsEnumerable().Reverse().OrderByDescending(w => Selected.Contains(w.Id)).FirstOrDefault(w => Reading.WireNumbers && Drawing.WireLabel(w, w.Route(Editor.Sheet)) is Ink ink && Drawing.TextBounds(Reading.Scale(ink)).Contains(point));
        if (wire is not null) { before = Editor.ExportSnapshot; dragStart = point; wireTextDrag = (wire.Id, DocumentModel.Copy(wire.NumberPlacement)); textMoved = false; Editor.InteractionInProgress = true; Select(wire.Id); Status?.Invoke("Déplacer le numéro du conducteur • Échap : annuler"); return true; }
        if (HitText(point) is not { } hit) return false;
        before = Editor.ExportSnapshot; dragStart = point;
        var placement = DocumentModel.Copy(hit.Text.Placement);
        bool detach = CrossReferences.IsPoint(hit.Component) && hit.Text.Key == "detail" && hit.Component.ReferenceDestinationInline;
        if (detach) { placement.Side = "custom"; placement.Horizontal = "left"; placement.Vertical = "top"; placement.AnchorPort = ""; placement.OffsetX = hit.Bounds.X - hit.Component.X; placement.OffsetY = hit.Bounds.Y - hit.Component.Y; }
        textDrag = (hit.Component.Id, hit.Text.Key, placement, detach); textMoved = false;
        Editor.InteractionInProgress = true; Select(hit.Component.Id);
        Status?.Invoke("Déplacer le texte • Relâcher pour valider • Échap : annuler"); return true;
    }
    internal void MoveText(Point point)
    {
        if (wireTextDrag is { } wireDrag && before is not null) { var wireDelta = point - dragStart; if (!textMoved && wireDelta.Length * Zoom < 3) return; textMoved = true; var wirePlacement = DocumentModel.Copy(wireDrag.Placement); wirePlacement.OffsetX = Math.Clamp(wirePlacement.OffsetX + wireDelta.X, -10000, 10000); wirePlacement.OffsetY = Math.Clamp(wirePlacement.OffsetY + wireDelta.Y, -10000, 10000); Editor.Sheet.Wires.Single(w => w.Id == wireDrag.Id).NumberPlacement = wirePlacement; retainedScene?.TextChanged(wireDrag.Id,true); InvalidateVisual(); return; }
        if (textDrag is not { } drag || before is null) return;
        var delta = point - dragStart; if (!textMoved && delta.Length * Zoom < 3) return; textMoved = true;
        var c = Editor.Sheet.Components.Single(c => c.Id == drag.ComponentId); var placement = DocumentModel.Copy(drag.Placement);
        if (drag.DetachInline) c.ReferenceDestinationInline = false;
        // Rebuild from the original placement on every frame to avoid drift.
        placement.OffsetX = Math.Clamp(placement.OffsetX + delta.X, -10000, 10000); placement.OffsetY = Math.Clamp(placement.OffsetY + delta.Y, -10000, 10000);
        ComponentTexts.SetPlacement(c, drag.Key, placement); retainedScene?.TextChanged(c.Id,false); InvalidateVisual();
    }
    internal void EndPreviewMove()
    {
        if (before is not string snapshot) return;
        before = null; textDrag = null; wireTextDrag = null;graphicResize=null; guideX = guideY = null; editingWire = null; originalRoute = null; Editor.InteractionInProgress = false;
        try { Editor.Commit(snapshot); } catch { Editor.CancelPreview(snapshot); throw; } SelectionChanged?.Invoke();
    }
    internal void MoveSelection(Point point)
    {
        if (before is null || original.Count+originalGraphics.Count == 0) return;
        routeCache = null;
        double dx = Math.Clamp(Editor.Snap(point.X - dragStart.X), -moveBounds.Left, Editor.Sheet.Width - moveBounds.Right);
        double dy = Math.Clamp(Editor.Snap(point.Y - dragStart.Y), -moveBounds.Top, Editor.Sheet.Height - moveBounds.Bottom);
        guideX = guideY = null;
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            var reference = original.Count>0?original.Values.First():new Point(originalGraphics.Values.First().X,originalGraphics.Values.First().Y); var candidates = Editor.Sheet.Components.Where(c => !original.ContainsKey(c.Id) && !Junctions.Automatic(c)).ToList();
            Component? nearX=null,nearY=null;double closestX=double.MaxValue,closestY=double.MaxValue;foreach(var candidate in candidates){double x=Math.Abs(candidate.X-reference.X-dx),y=Math.Abs(candidate.Y-reference.Y-dy);if(x<closestX){closestX=x;nearX=candidate;}if(y<closestY){closestY=y;nearY=candidate;}}
            if (nearX is not null && Math.Abs(nearX.X-reference.X-dx)*Zoom < 5) { dx = Math.Clamp(nearX.X-reference.X, -moveBounds.Left, Editor.Sheet.Width-moveBounds.Right); guideX = nearX.X; }
            if (nearY is not null && Math.Abs(nearY.Y-reference.Y-dy)*Zoom < 5) { dy = Math.Clamp(nearY.Y-reference.Y, -moveBounds.Top, Editor.Sheet.Height-moveBounds.Bottom); guideY = nearY.Y; }
        }
        foreach (var c in Editor.Sheet.Components.Where(c => original.ContainsKey(c.Id))) { c.X = original[c.Id].X + dx; c.Y = original[c.Id].Y + dy; }
        foreach (var n in Editor.Sheet.Notes.Where(n => original.ContainsKey(n.Id))) { n.X = original[n.Id].X + dx; n.Y = original[n.Id].Y + dy; }
        foreach (var w in Editor.Sheet.Wires.Where(w => originalWaypoints.ContainsKey(w.Id))) w.Waypoints = originalWaypoints[w.Id].Select(p => new Position(p.X + dx, p.Y + dy)).ToList();
        foreach(var g in Editor.Sheet.Graphics.Where(g=>originalGraphics.ContainsKey(g.Id))){g.X=originalGraphics[g.Id].X+dx;g.Y=originalGraphics[g.Id].Y+dy;}retainedScene?.GraphicsChanged(originalGraphics.Keys);
        retainedScene?.GeometryChanged(original.Keys.Where(id=>Editor.Sheet.Components.Any(c=>c.Id==id)),original.Keys.Where(id=>Editor.Sheet.Notes.Any(n=>n.Id==id)));
    }
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus(); pointer = World(e.GetPosition(this));
        if (e.ChangedButton == MouseButton.Middle) { panning = true; panStart = e.GetPosition(this); panOffset = offset; CaptureMouse(); return; }
        if (e.ChangedButton == MouseButton.Right) { ContextAt(pointer); e.Handled = true; return; }
        if (e.ChangedButton != MouseButton.Left) return;
        if ((Editor.Sheet.BomPage is not null || Editor.Sheet.TerminalPlanId.Length>0 || Editor.Sheet.CabinetPlanId.Length>0)) { Status?.Invoke("Folio de nomenclature : Projet → Nomenclature pour actualiser son contenu."); return; }
        if (pointer.X < 0 || pointer.Y < 0 || pointer.X > Editor.Sheet.Width || pointer.Y > Editor.Sheet.Height) return;
        try
        {
            if(GraphicClick(pointer)||BeginGraphicResize(pointer)||HandleBlockClick(pointer) || HandleBandClick(pointer)){e.Handled=true;return;}
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && BeginTextMove(pointer)) { CaptureMouse(); e.Handled = true; return; }
            if (e.ClickCount == 2 && ActivateReference(pointer)) { e.Handled = true; return; }
            if (Mode == "place" && Hit(pointer) is string existing && Editor.Sheet.Components.Any(c => c.Id == existing && CrossReferences.IsPoint(c))) { SetMode("select"); Select(existing); return; }
            var port = HitPort(pointer);
            if (connection is not null) { if (port is not null) CompleteConnection(port); else if (HitWire(pointer) is { } target) CompleteBranch(target); else { waypoints.Add(new(Editor.Snap(pointer.X), Editor.Snap(pointer.Y))); InvalidateVisual(); } return; }
            if (Mode == "select" && Selected.Count == 1) foreach (var w in Editor.Sheet.Wires.Where(w => Selected.Contains(w.Id)))
            {
                var route = w.Route(Editor.Sheet);
                if ((route[0] - pointer).Length < 8 / Zoom) { StartConnection(w.To, e, w.Id, true); return; }
                if ((route[^1] - pointer).Length < 8 / Zoom) { StartConnection(w.From, e, w.Id, false); return; }
                for (int i = 1; i < route.Count; i++) if ((Mid(route[i - 1], route[i]) - pointer).Length < 8 / Zoom) { StartSegment(w, i); return; }
            }
            if (Mode != "text" && port is not null) { if (Mode == "place") Mode = "select"; StartConnection(port, e); return; }
            if (Mode == "place") { var c = Editor.AddComponent(Symbol, pointer.X, pointer.Y, CatalogId); Select(c.Id); return; }
            if (Mode == "text") { TextRequested?.Invoke(pointer); return; }
            if (Mode == "wire") return;
            var hit = Hit(pointer); var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            if (hit is null) { marqueeStart = pointer; marqueeAdd = ctrl; Editor.InteractionInProgress = true; CaptureMouse(); InvalidateVisual(); return; }
            if (ctrl && Selected.Contains(hit)) { Selected.Remove(hit); SelectionChanged?.Invoke(); InvalidateVisual(); return; }
            if (!ctrl && !Selected.Contains(hit)) Selected.Clear(); Selected.Add(hit); SelectionChanged?.Invoke();
            var selectedWire = Editor.Sheet.Wires.FirstOrDefault(w => w.Id == hit);
            if (selectedWire is not null && !ctrl && Selected.Count == 1) { var route = selectedWire.Route(Editor.Sheet); var index = Enumerable.Range(1, route.Count - 1).OrderBy(i => Distance(pointer, route[i - 1], route[i])).First(); StartSegment(selectedWire, index); return; }
            BeginSelectionMove(pointer); if (before is not null) CaptureMouse();
            InvalidateVisual();
        }
        catch (Exception ex) { CancelDrag(); Status?.Invoke(ex.Message); }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        pointer = World(e.GetPosition(this));
        if(graphicResize is not null){ResizeGraphic(pointer);Cursor=Cursors.SizeNWSE;return;}
        if (textDrag is not null || wireTextDrag is not null) { MoveText(pointer); Cursor = Cursors.SizeAll; return; }
        if (marqueeStart is not null) { InvalidateVisual(); return; }
        var oldPort=hoveredPort; hoveredPort = HitPort(pointer); hoveredWire = connection is not null && hoveredPort is null ? HitWire(pointer) : null;
        if (panning) { offset = panOffset + (e.GetPosition(this) - panStart); InvalidateVisual(); return; }
        if (connectionPressed && (e.GetPosition(this) - pressPosition).Length > 4) connectionMoved = true;
        if (before is not null && editingWire is not null && originalRoute is not null)
        {
            routeCache=null;
            if ((pointer - dragStart).Length * Zoom > 3) Junctions.ShiftSegment(Editor.Sheet, editingWire, originalRoute, segmentIndex, new(Math.Clamp(pointer.X, 0, Editor.Sheet.Width), Math.Clamp(pointer.Y, 0, Editor.Sheet.Height)));
            var moved=Editor.Sheet.Wires.Single(w=>w.Id==editingWire);retainedScene?.GeometryChanged(Editor.Sheet.Components.Where(c=>Junctions.Automatic(c)&&(c.Id==moved.From.ComponentId||c.Id==moved.To.ComponentId)).Select(c=>c.Id),[],[editingWire]);
        }
        else if (before is not null)
        {
            MoveSelection(pointer);
        }
        Cursor = hoveredPort is not null || connection is not null ? Cursors.Cross : Mode == "select" ? Cursors.Arrow : Cursors.Cross;
        if(oldPort!=hoveredPort||before is not null||connection is not null||bandStart is not null||graphicStart is not null||Mode is "place" or "block" or "graphicImage")InvalidateVisual();
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle) { panning = false; if (before is null && !connectionPressed) ReleaseMouseCapture(); return; }
        if (e.ChangedButton != MouseButton.Left) return;
        try{if(GraphicRelease(World(e.GetPosition(this))))return;}catch(Exception ex){CancelDrag();Status?.Invoke(ex.Message);return;}
        if (marqueeStart is Point start) { marqueeStart = null; Editor.InteractionInProgress = false; SelectRectangle(new Rect(start, World(e.GetPosition(this))), marqueeAdd); ReleaseMouseCapture(); return; }
        try
        {
            if (connectionPressed)
            {
                var moved = connectionMoved; var reconnect = editingWire is not null; connectionPressed = false;
                if (moved) { var port = HitPort(World(e.GetPosition(this))); if (port is not null && port != connection) CompleteConnection(port); else if (port is null && HitWire(World(e.GetPosition(this))) is { } target) CompleteBranch(target); else { ClearConnection(); Status?.Invoke("Liaison annulée : relâcher sur une borne ou sur un conducteur."); } }
                else if (reconnect) ClearConnection(); else ReleaseMouseCapture();
            }
            if (before is not null)
            {
                if (textDrag is not null || wireTextDrag is not null) MoveText(World(e.GetPosition(this)));
                EndPreviewMove();
            }
        }
        catch (Exception ex) { CancelDrag(); Status?.Invoke(ex.Message); }
        if (before is null && !panning && !connectionPressed) ReleaseMouseCapture();
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); if (before is not null || connectionPressed || marqueeStart is not null||graphicPressed) CancelDrag(); panning = false; }
    public void CancelDrag()
    {
        CancelBand();CancelGraphicGesture();
        var snapshot = before; before = null; textDrag = null; wireTextDrag = null;graphicResize=null; guideX = guideY = null; marqueeStart = null; originalRoute = null; editingWire = null; connection = null; hoveredWire = null; connectionPressed = false; connectionMoved = false; waypoints.Clear(); panning = false; Editor.InteractionInProgress = false;
        ReleaseMouseCapture(); if (snapshot is not null) Editor.CancelPreview(snapshot); InvalidateVisual();
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (before is not null || connectionPressed || marqueeStart is not null) return;
        var screen = e.GetPosition(this); var world = World(screen); Zoom = Math.Clamp(Zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15), .08, 4);
        offset = new(screen.X - world.X * Zoom, screen.Y - world.Y * Zoom); InvalidateVisual(); e.Handled = true;
    }
}
