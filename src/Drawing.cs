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
        List<Ink> result = [new("rect", 30, 30, sheet.Width - 60, sheet.Height - 60, Color: "#53677B")];
        var y = sheet.Height - 90;
        result.Add(new("line", 30, y, sheet.Width - 30, y));
        result.Add(new("text", 46, y + 12, Text: project.Title, Size: 20));
        result.Add(new("text", 46, y + 40, Text: sheet.Title, Size: 12));
        result.Add(new("text", sheet.Width - 360, y + 12, Text: $"GeMMeElec  |  Révision {project.Revision}", Size: 13));
        result.Add(new("text", sheet.Width - 360, y + 38, Text: $"{project.Author}  •  Folio {project.Sheets.IndexOf(sheet) + 1}/{project.Sheets.Count}", Size: 12));
        for (int col = 1; col < 10; col++) result.Add(new("text", sheet.Width * col / 10, 10, Text: col.ToString(), Color: "#8293A5", Size: 10));
        foreach (var wire in sheet.Wires)
        {
            var path = wire.Route(sheet);
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
            foreach (var port in c.Definition.Ports)
            {
                var p = c.PortPoint(port.Id);
                result.Add(new("circle", p.X, p.Y, 2));
                result.Add(new("text", p.X + 4, p.Y - 13, Text: c.PortLabels.GetValueOrDefault(port.Id, port.Id), Color: "#64748B", Size: 9));
            }
            var bounds = c.Bounds();
            result.Add(new("text", bounds.Right + 12, c.Y - 18, Text: c.Tag, Size: 16));
            var detail = c.Rating.Length > 0 ? c.Rating : c.Reference.Length > 0 ? c.Reference : c.Range;
            if (detail.Length > 0) result.Add(new("text", bounds.Right + 12, c.Y + 5, Text: detail, Size: 10, Color: "#64748B"));
        }
        foreach (var n in sheet.Notes) result.Add(new("text", n.X, n.Y, Text: n.Text, Size: 16));
        return result;
    }
    public static Brush Brush(string color)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); } catch { return Brushes.DarkSlateGray; }
    }
    public static void Render(DrawingContext dc, IEnumerable<Ink> scene)
    {
        foreach (var s in scene)
        {
            var brush = Brush(s.Color); var pen = new Pen(brush, 1.6);
            dc.PushTransform(new RotateTransform(s.Rotation, s.X, s.Y));
            switch (s.Kind)
            {
                case "line": dc.DrawLine(pen, new(s.X, s.Y), new(s.A, s.B)); break;
                case "rect": dc.DrawRectangle(null, pen, new(s.X, s.Y, s.A, s.B)); break;
                case "circle": dc.DrawEllipse(Brushes.White, pen, new(s.X, s.Y), s.A, s.A); break;
                case "dot": dc.DrawEllipse(brush, null, new(s.X, s.Y), s.A, s.A); break;
                case "text": dc.DrawText(new FormattedText(s.Text, CultureInfo.GetCultureInfo("fr-BE"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), Math.Max(8, s.Size), brush, 1.0), new(s.X, s.Y)); break;
            }
            dc.Pop();
        }
    }
}
