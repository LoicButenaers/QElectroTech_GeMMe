using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GeMMeElec;

// A single scene description drives screen, SVG and PDF rendering.
public sealed record Ink(string Kind, double X, double Y, double A = 0, double B = 0, string Text = "", string Color = "#203348", double Size = 12, double Rotation = 0, double Thickness = 1.6, string LineStyle = "solid");
public sealed class SymbolPreview(string symbol) : FrameworkElement
{
    public SymbolDefinition? Definition { get; set; }
    public SymbolPreview() : this("breaker1") { }
    protected override Size MeasureOverride(Size availableSize) => new(38, 42);
    protected override void OnRender(DrawingContext dc)
    {
        var d = Definition ?? Symbols.All[symbol]; double scale = Math.Min(34 / d.Width, 38 / d.Height);
        dc.PushTransform(new TranslateTransform(19, 21)); dc.PushTransform(new ScaleTransform(scale, scale));
        Drawing.Render(dc, d.Shapes.Select(s => new Ink(s.Kind, s.X, s.Y, s.A, s.B, s.Text, Size: s.A, Rotation: s.Rotation, Color: symbol is "folio_next" or "folio_previous" ? "#000000" : "#203348")));
        dc.Pop(); dc.Pop();
    }
}
public static class Drawing
{
    public static List<Ink> Scene(Project project, Sheet sheet)
    {
        if (sheet.BomPage is not null) return BomFolios.Scene(project, sheet);
        List<Ink> result = [new("rect", 10, 10, sheet.Width - 40, sheet.Height - 40, Color: "#53677B")];
        result.AddRange(DocumentModel.Scene(project,sheet));
        result.AddRange(SheetZones.Frame(project, sheet));
        var references = new CrossReferences(project);
        var components = sheet.Components.ToDictionary(c => c.Id);
        var routes = sheet.Wires.ToDictionary(w => w.Id, w => w.Route(sheet, components));
        // Source images may have an opaque paper background. Paint them below
        // conductors so a terminal inside the crop remains visibly connected.
        foreach(var c in sheet.Components) foreach(var shape in c.Definition.Shapes.Where(s=>s.Kind=="image"))
        {
            var p=c.Transform(shape.X,shape.Y);
            result.Add(new("image",p.X,p.Y,shape.A,shape.B,shape.Text,Rotation:c.Rotation+shape.Rotation));
        }
        foreach (var wire in sheet.Wires)
        {
            var path = routes[wire.Id];
            for (int i = 1; i < path.Count; i++) result.Add(new("line", path[i - 1].X, path[i - 1].Y, path[i].X, path[i].Y, Color: wire.Color, Thickness: wire.Thickness, LineStyle: wire.LineStyle));
            var label = WireLabel(wire, path); if (label is not null) result.Add(label);
        }
        foreach (var c in sheet.Components) result.AddRange(ComponentScene(project, c, references, false));
        foreach (var dot in Junctions.Dots(sheet, routes)) result.Add(new("dot", dot.Point.X, dot.Point.Y, 4, Color: dot.Color));
        foreach (var n in sheet.Notes) result.Add(new("text", n.X, n.Y, Text: n.Text, Size: 16));
        return result;
    }
    public static Ink? WireLabel(Wire wire, IReadOnlyList<Point> path)
    {
        if (string.IsNullOrWhiteSpace(wire.Number) || !wire.NumberPlacement.Visible) return null;
        int segment = 0; double length = 0;
        for (int i = 1; i < path.Count; i++) { double candidate = (path[i] - path[i - 1]).Length; if (candidate > length) { segment = i; length = candidate; } }
        if (length < .001) return null;
        var tangent = (path[segment] - path[segment - 1]) / length;
        if (tangent.X < -.00001 || (Math.Abs(tangent.X) < .00001 && tangent.Y > 0)) tangent = -tangent;
        var midpoint = path[segment - 1] + (path[segment] - path[segment - 1]) / 2;
        double font = wire.NumberPlacement.FontSize == 0 ? 10 : wire.NumberPlacement.FontSize;
        var size = Measure(wire.Number, font); var normal = new Vector(tangent.Y, -tangent.X);
        var origin = midpoint - tangent * (size.Width / 2) + normal * (size.Height + 4);
        return new("text", origin.X + wire.NumberPlacement.OffsetX, origin.Y + wire.NumberPlacement.OffsetY, Text: wire.Number, Color: wire.NumberPlacement.Color.Length > 0 ? wire.NumberPlacement.Color : wire.Color, Size: font, Rotation: wire.NumberPlacement.Rotation ?? Math.Atan2(tangent.Y, tangent.X) * 180 / Math.PI);
    }
    public static List<Ink> ComponentScene(Project project, Component c, CrossReferences references, bool images = true)
    {
        if (c.Symbol == "junction") return [];
        if (CrossReferences.IsPoint(c)) return ReferenceDisplay.Scene(project, c, references).Concat(ComponentTexts.Scene(project, c, references)).ToList();
        List<Ink> result = [];
        foreach (var shape in c.Definition.Shapes)
        {
            if (shape.Kind == "text" || (!images && shape.Kind == "image")) continue;
            var p = c.Transform(shape.X, shape.Y);
            if (shape.Kind == "line") { double angle = shape.Rotation * Math.PI / 180, dx = shape.A - shape.X, dy = shape.B - shape.Y; var b = c.Transform(shape.X + dx * Math.Cos(angle) - dy * Math.Sin(angle), shape.Y + dx * Math.Sin(angle) + dy * Math.Cos(angle)); result.Add(new("line", p.X, p.Y, b.X, b.Y)); }
            else result.Add(new(shape.Kind, p.X, p.Y, shape.A, shape.B, shape.Text, Size: shape.A, Rotation: c.Rotation + shape.Rotation));
        }
        foreach (var port in c.Definition.Ports)
        {
            var p = c.PortPoint(port.Id);
            if (!c.Definition.PrintedTerminals) result.Add(new("circle", p.X, p.Y, 2));

        }
        result.AddRange(ComponentTexts.Scene(project, c, references));
        return result;
    }
    public static string Detail(Component c) => c.Rating.Length > 0 ? c.Rating : c.Reference.Length > 0 ? c.Reference : c.Range;
    public static Ink? Label(Component component, LabelPlacement placement, string text, double size, string color)
    {
        if (!placement.Visible || text.Length == 0) return null;
        size = placement.EffectiveSize(size);
        var local = new RotateTransform(placement.Rotation).TransformBounds(new Rect(new Point(), Measure(text, size)));
        var origin = placement.Origin(component, local.Width, local.Height);
        return TextAt(placement, text, size, color, origin);
    }
    public static Ink TextAt(LabelPlacement placement, string text, double size, string color, Point topLeft)
    {
        size = placement.EffectiveSize(size); color = placement.EffectiveColor(color);
        var local = new RotateTransform(placement.Rotation).TransformBounds(new Rect(new Point(), Measure(text, size)));
        return new("text", topLeft.X - local.X, topLeft.Y - local.Y, Text: text, Size: size, Color: color, Rotation: placement.Rotation);
    }
    public static IEnumerable<Ink> TextLines(Ink ink)
    {
        if (ink.Kind != "text" || !ink.Text.Contains('\n')) { yield return ink; yield break; }
        var lines = ink.Text.Replace("\r", "").Split('\n'); double step = Measure("Mg", ink.Size).Height;
        double angle = ink.Rotation * Math.PI / 180;
        for (int i = 0; i < lines.Length; i++) yield return ink with { Text = lines[i], X = ink.X - Math.Sin(angle) * i * step, Y = ink.Y + Math.Cos(angle) * i * step };
    }
    public static Rect TextBounds(Ink ink) => new RotateTransform(ink.Rotation, ink.X, ink.Y).TransformBounds(new Rect(new Point(ink.X, ink.Y), Measure(ink.Text, ink.Size)));
    public static double[] DashPattern(string style) => style switch { "dashed" => [4, 3], "dotted" => [1, 2], "dashdot" => [4, 2, 1, 2], _ => [] };
    private static void AddLabel(List<Ink> scene, Component c, LabelPlacement p, string text, double size, string color) { var label = Label(c, p, text, size, color); if (label is not null) scene.Add(label); }
    [ThreadStatic] private static Dictionary<string,Brush>? brushesCache;
    [ThreadStatic] private static Dictionary<(string,double,string),FormattedText>? textCache;
    [ThreadStatic] private static Typeface? textFace;
    private static Dictionary<string,Brush> BrushesCache => brushesCache ??= [];
    private static Dictionary<(string,double,string),FormattedText> TextCache => textCache ??= [];
    private static Typeface TextFace => textFace ??= new("Segoe UI");
    private static readonly CultureInfo TextCulture = CultureInfo.GetCultureInfo("fr-BE");
    public static Brush Brush(string color) { if(BrushesCache.TryGetValue(color,out var cached))return cached;try{var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));brush.Freeze();if(BrushesCache.Count>=256)BrushesCache.Clear();return BrushesCache[color]=brush;}catch{return Brushes.DarkSlateGray;} }
    private static FormattedText Text(string text,double size,string color) { var key=(text,Math.Max(.5,size),color);if(TextCache.TryGetValue(key,out var cached))return cached;if(TextCache.Count>=4096)TextCache.Clear();return TextCache[key]=new FormattedText(text,TextCulture,FlowDirection.LeftToRight,TextFace,key.Item2,Brush(color),1); }
    public static Size Measure(string text,double size) {var value=Text(text,size,"#203348");return new(value.WidthIncludingTrailingWhitespace,value.Height);}
    public static void Render(DrawingContext dc, IEnumerable<Ink> scene)
    {
        foreach (var s in scene)
        {
            var brush = Brush(s.Color); var pen = new Pen(brush, s.Thickness); if (s.LineStyle != "solid") pen.DashStyle = new DashStyle(DashPattern(s.LineStyle), 0);
            if(s.Rotation!=0)dc.PushTransform(new RotateTransform(s.Rotation, s.X, s.Y));
            switch (s.Kind)
            {
                case "line": dc.DrawLine(pen, new(s.X, s.Y), new(s.A, s.B)); break;
                case "rect": dc.DrawRectangle(null, pen, new(s.X, s.Y, s.A, s.B)); break;
                case "circle": dc.DrawEllipse(Brushes.White, pen, new(s.X, s.Y), s.A, s.A); break;
                case "dot": dc.DrawEllipse(brush, null, new(s.X, s.Y), s.A, s.A); break;
                case "triangle":
                    var geometry = new StreamGeometry(); using (var g = geometry.Open()) { g.BeginFigure(new(s.X, s.Y), true, true); g.LineTo(new(s.X, s.Y + s.B), true, false); g.LineTo(new(s.X + s.A, s.Y + s.B / 2), true, false); } geometry.Freeze(); dc.DrawGeometry(brush, null, geometry); break;
                case "text": dc.DrawText(Text(s.Text,s.Size,s.Color), new(s.X, s.Y)); break;
                case "image": dc.DrawImage(ImageAssets.Decode(s.Text),new(s.X,s.Y,s.A,s.B)); break;
            }
            if(s.Rotation!=0)dc.Pop();
        }
    }
}
