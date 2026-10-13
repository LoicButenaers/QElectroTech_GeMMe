using System.Windows;

namespace GeMMeElec;

public static class ReferenceDisplay
{
    public static string Name(Component c) => c.ReferenceName.Length > 0 ? c.ReferenceName : c.LinkName.Length > 0 ? c.LinkName : c.Tag;
    public static List<Ink> Scene(Project p, Component c, CrossReferences refs)
    {
        List<Ink> ink = c.Definition.Shapes.Select(s => { var a = c.Transform(s.X, s.Y); var b = c.Transform(s.A, s.B); return new Ink("line", a.X, a.Y, b.X, b.Y, Color: "#000000"); }).ToList();
        var name = Drawing.Label(c, c.TagPlacement, Name(c), 14, "#000000");
        if (name is not null)
        {
            if (c.Symbol == "folio_next" && c.Rotation == 0 && c.TagPlacement.Side == "right") name = name with { X = name.X - (c.Bounds().Right - c.X - 18) };
            ink.Add(name);
        }
        if (c.DetailPlacement.Visible)
        {
            string destination = string.Join("\n", BomFolios.Wrap(refs.Label(p, c), 360));
            if (c.ReferenceDestinationInline)
            {
                var bounds = name is null ? new Rect(c.TagPlacement.Origin(c, 0, 0), new Size()) : Drawing.TextBounds(name);
                ink.Add(Drawing.TextAt(c.DetailPlacement, destination, 12, "#008000", new(bounds.Left, bounds.Bottom + (name is null ? 0 : 4))));
            }
            else if (Drawing.Label(c, c.DetailPlacement, destination, 12, "#008000") is Ink label) ink.Add(label);
        }
        return ink;
    }
    public static Rect HitBounds(Project p, Component c, CrossReferences refs)
    {
        var bounds = c.Bounds(); foreach (var ink in Scene(p, c, refs).Where(i => i.Kind == "text")) bounds.Union(Drawing.TextBounds(ink)); bounds.Inflate(5, 5); return bounds;
    }
}
