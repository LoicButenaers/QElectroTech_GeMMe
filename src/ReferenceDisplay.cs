using System.Windows;

namespace GeMMeElec;

public static class ReferenceDisplay
{
    public static List<Ink> Scene(Project p, Component c, CrossReferences refs)
    {
        bool departure = c.Symbol == "folio_next";
        var origin = c.Transform(departure ? -8 : 8, -6); var port = c.PortPoint("1"); var tail = c.Transform(departure ? -8 : 8, 0);
        List<Ink> ink = [new("line", port.X, port.Y, tail.X, tail.Y, Color: "#E52828"), new("triangle", origin.X, origin.Y, departure ? 16 : -16, 12, Color: "#E52828", Rotation: c.Rotation)];
        string name = c.LinkName.Length > 0 ? c.LinkName : c.Tag; string destination = refs.Label(p, c);
        var lines = BomFolios.Wrap(destination, 360); double nameWidth = Drawing.Measure(name + " / ", 14).Width;
        double width = nameWidth + lines.Max(t => Drawing.Measure(t, 12).Width); var pos = c.TagPlacement.Origin(c, width, Math.Max(20, lines.Count * 16));
        if (departure && c.TagPlacement.Side == "right") pos.X -= c.Bounds().Right - c.X - 8;
        if (c.TagPlacement.Visible) ink.Add(new("text", pos.X, pos.Y, Text: name, Size: 14, Color: "#124CE8"));
        if (c.DetailPlacement.Visible) {
            ink.Add(new("text", pos.X + Drawing.Measure(name, 14).Width, pos.Y, Text: " / ", Size: 14, Color: "#55738C"));
            for (int i = 0; i < lines.Count; i++) ink.Add(new("text", pos.X + nameWidth, pos.Y + 2 + 16 * i, Text: lines[i], Size: 12, Color: "#6B9D20"));
        }
        return ink;
    }
    public static Rect HitBounds(Project p, Component c, CrossReferences refs)
    {
        var bounds = c.Bounds(); foreach (var ink in Scene(p, c, refs).Where(i => i.Kind == "text")) { var size = Drawing.Measure(ink.Text, ink.Size); bounds.Union(new Rect(new Point(ink.X, ink.Y), size)); } bounds.Inflate(5, 5); return bounds;
    }
}
