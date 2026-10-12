using System.Windows;

namespace GeMMeElec;

public static class ReferenceDisplay
{
    public static List<Ink> Scene(Project p, Component c, CrossReferences refs)
    {
        bool departure = c.Symbol == "folio_next";
        List<Ink> ink = c.Definition.Shapes.Select(s => { var a = c.Transform(s.X, s.Y); var b = c.Transform(s.A, s.B); return new Ink("line", a.X, a.Y, b.X, b.Y, Color: "#000000"); }).ToList();
        string name = c.LinkName.Length > 0 ? c.LinkName : c.Tag; string destination = refs.Label(p, c);
        var lines = BomFolios.Wrap(destination, 360); double nameWidth = Drawing.Measure(name + " / ", 14).Width;
        double width = nameWidth + lines.Max(t => Drawing.Measure(t, 12).Width); var pos = c.TagPlacement.Origin(c, width, Math.Max(20, lines.Count * 16));
        if (departure && c.Rotation == 0 && c.TagPlacement.Side == "right") pos.X -= c.Bounds().Right - c.X - 18;
        if (c.TagPlacement.Visible) ink.Add(new("text", pos.X, pos.Y, Text: name, Size: 14, Color: "#000000"));
        if (c.DetailPlacement.Visible) {
            ink.Add(new("text", pos.X + Drawing.Measure(name, 14).Width, pos.Y, Text: " / ", Size: 14, Color: "#000000"));
            for (int i = 0; i < lines.Count; i++) ink.Add(new("text", pos.X + nameWidth, pos.Y + 2 + 16 * i, Text: lines[i], Size: 12, Color: "#000000"));
        }
        return ink;
    }
    public static Rect HitBounds(Project p, Component c, CrossReferences refs)
    {
        var bounds = c.Bounds(); foreach (var ink in Scene(p, c, refs).Where(i => i.Kind == "text")) { var size = Drawing.Measure(ink.Text, ink.Size); bounds.Union(new Rect(new Point(ink.X, ink.Y), size)); } bounds.Inflate(5, 5); return bounds;
    }
}
