using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GeMMeElec;

// A single scene description drives screen, SVG and PDF rendering.
public sealed record Ink(string Kind, double X, double Y, double A = 0, double B = 0, string Text = "", string Color = "#203348", double Size = 12, double Rotation = 0);
public sealed class SymbolPreview(string symbol) : FrameworkElement
{
    public SymbolPreview() : this("breaker1") { }
    protected override Size MeasureOverride(Size availableSize) => new(38, 42);
    protected override void OnRender(DrawingContext dc)
    {
        var d = Symbols.All[symbol]; double scale = Math.Min(34 / d.Width, 38 / d.Height);
        dc.PushTransform(new TranslateTransform(19, 21)); dc.PushTransform(new ScaleTransform(scale, scale));
        Drawing.Render(dc, d.Shapes.Select(s => new Ink(s.Kind, s.X, s.Y, s.A, s.B, s.Text, Size: s.A)));
        dc.Pop(); dc.Pop();
    }
}
public static class Drawing
{
    public static List<Ink> Scene(Project project, Sheet sheet)
    {
        if (sheet.BomPage is not null) return BomFolios.Scene(project, sheet);
        List<Ink> result = [new("rect", 30, 30, sheet.Width - 60, sheet.Height - 60, Color: "#53677B")];
        result.AddRange(DocumentModel.Scene(project,sheet));
        result.AddRange(SheetZones.Frame(project, sheet));
        var references = new CrossReferences(project);
        var components = sheet.Components.ToDictionary(c => c.Id);
        foreach (var wire in sheet.Wires)
        {
            var path = wire.Route(sheet, components);
            for (int i = 1; i < path.Count; i++) result.Add(new("line", path[i - 1].X, path[i - 1].Y, path[i].X, path[i].Y, Color: wire.Color));
            var segment = Enumerable.Range(1, path.Count - 1).OrderByDescending(i => (path[i] - path[i - 1]).Length).First();
            var mid = new Point((path[segment - 1].X + path[segment].X) / 2, (path[segment - 1].Y + path[segment].Y) / 2);
            result.Add(new("text", mid.X + 5, mid.Y - 15, Text: wire.Number, Color: wire.Color, Size: 10));
        }
        foreach (var c in sheet.Components)
        {
            foreach (var shape in c.Definition.Shapes)
            {
                var p = c.Transform(shape.X, shape.Y);
                if (shape.Kind == "line") { var b = c.Transform(shape.A, shape.B); result.Add(new("line", p.X, p.Y, b.X, b.Y)); }
                else result.Add(new(shape.Kind, p.X, p.Y, shape.A, shape.B, shape.Text, Size: shape.A, Rotation: c.Rotation));
            }
            foreach (var port in c.Definition.Ports.Where(_ => c.Symbol != "junction"))
            {
                var p = c.PortPoint(port.Id);
                result.Add(new("circle", p.X, p.Y, 2));
                result.Add(new("text", p.X + 4, p.Y - 13, Text: c.PortLabels.GetValueOrDefault(port.Id, port.Id), Color: "#64748B", Size: 9));
            }
            var detail = c.Rating.Length > 0 ? c.Rating : c.Reference.Length > 0 ? c.Reference : c.Range;
            AddLabel(result, c, c.TagPlacement, CrossReferences.IsPoint(c) && c.LinkName.Length > 0 ? c.LinkName : c.Tag, 16, "#203348");
            if (CrossReferences.IsPoint(c) && c.DetailPlacement.Visible)
            {
                var lines = BomFolios.Wrap(references.Label(project, c), 320); double width = lines.Max(t => Measure(t, 11).Width);
                var origin = c.DetailPlacement.Origin(c, width, lines.Count * 16);
                for (int i = 0; i < lines.Count; i++) result.Add(new("text", origin.X, origin.Y + i * 16, Text: lines[i], Size: 11, Color: "#007F86"));
            }
            else AddLabel(result, c, c.DetailPlacement, detail, 10, "#64748B");
            AddLabel(result, c, c.DescriptionPlacement, c.Description, 12, "#64748B");
        }
        foreach (var n in sheet.Notes) result.Add(new("text", n.X, n.Y, Text: n.Text, Size: 16));
        return result;
    }
    public static Ink? Label(Component component, LabelPlacement placement, string text, double size, string color)
    {
        if (!placement.Visible || text.Length == 0) return null;
        var metrics = Measure(text,size);
        var origin = placement.Origin(component, metrics.Width, metrics.Height);
        return new("text", origin.X, origin.Y, Text: text, Size: size, Color: color);
    }
    private static void AddLabel(List<Ink> scene, Component c, LabelPlacement p, string text, double size, string color) { var label = Label(c, p, text, size, color); if (label is not null) scene.Add(label); }
    private static readonly Dictionary<string,Brush> BrushesCache = [];
    private static readonly Dictionary<(string,double,string),FormattedText> TextCache = [];
    private static readonly Typeface TextFace = new("Segoe UI");
    private static readonly CultureInfo TextCulture = CultureInfo.GetCultureInfo("fr-BE");
    public static Brush Brush(string color) { if(BrushesCache.TryGetValue(color,out var cached))return cached;try{var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));brush.Freeze();if(BrushesCache.Count>=256)BrushesCache.Clear();return BrushesCache[color]=brush;}catch{return Brushes.DarkSlateGray;} }
    private static FormattedText Text(string text,double size,string color) { var key=(text,Math.Max(.5,size),color);if(TextCache.TryGetValue(key,out var cached))return cached;if(TextCache.Count>=4096)TextCache.Clear();return TextCache[key]=new FormattedText(text,TextCulture,FlowDirection.LeftToRight,TextFace,key.Item2,Brush(color),1); }
    public static Size Measure(string text,double size) {var value=Text(text,size,"#203348");return new(value.WidthIncludingTrailingWhitespace,value.Height);}
    public static void Render(DrawingContext dc, IEnumerable<Ink> scene)
    {
        foreach (var s in scene)
        {
            var brush = Brush(s.Color); var pen = new Pen(brush, 1.6);
            if(s.Rotation!=0)dc.PushTransform(new RotateTransform(s.Rotation, s.X, s.Y));
            switch (s.Kind)
            {
                case "line": dc.DrawLine(pen, new(s.X, s.Y), new(s.A, s.B)); break;
                case "rect": dc.DrawRectangle(null, pen, new(s.X, s.Y, s.A, s.B)); break;
                case "circle": dc.DrawEllipse(Brushes.White, pen, new(s.X, s.Y), s.A, s.A); break;
                case "dot": dc.DrawEllipse(brush, null, new(s.X, s.Y), s.A, s.A); break;
                case "text": dc.DrawText(Text(s.Text,s.Size,s.Color), new(s.X, s.Y)); break;
                case "image": dc.DrawImage(ImageAssets.Decode(s.Text),new(s.X,s.Y,s.A,s.B)); break;
            }
            if(s.Rotation!=0)dc.Pop();
        }
    }
}
