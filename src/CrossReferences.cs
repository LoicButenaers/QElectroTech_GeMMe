using System.Windows;

namespace GeMMeElec;

public static class SheetZones
{
    public static Rect Area(Project p, Sheet s) => new(30, 30, s.Width - 60, DocumentModel.DrawingBottom(p, s) - 30);
    public static string Zone(Project p, Sheet s, double x, double y)
    {
        var area = Area(p, s);
        if (s.BomPage is not null || !area.Contains(new Point(x, y))) return "hors cadre";
        int column = Math.Min(s.ZoneColumns - 1, (int)((x - area.Left) / area.Width * s.ZoneColumns));
        int row = Math.Min(s.ZoneRows - 1, (int)((y - area.Top) / area.Height * s.ZoneRows));
        return $"{(char)('A' + row)}{column + 1}";
    }
    public static string Address(Project p, Sheet s, Component c) => $"{DocumentModel.PageNumber(p, s)} / {Zone(p, s, c.X, c.Y)}";
    public static IEnumerable<Ink> Frame(Project p, Sheet s)
    {
        var r = Area(p, s); const string color = "#53677B";
        yield return new("rect", 10, 10, s.Width - 40, r.Bottom - 10, Color: color);
        yield return new("line", 10, 30, s.Width - 30, 30, Color: color);
        yield return new("line", 30, 10, 30, r.Bottom, Color: color);
        for (int i = 0; i < s.ZoneColumns; i++)
        {
            double x = r.Left + i * r.Width / s.ZoneColumns;
            if (i > 0) yield return new("line", x, 10, x, 30, Color: color);
            string text = (i + 1).ToString(); yield return new("text", x + r.Width / s.ZoneColumns / 2 - Drawing.Measure(text, 10).Width / 2, 12, Text: text, Size: 10);
        }
        for (int i = 0; i < s.ZoneRows; i++)
        {
            double y = r.Top + i * r.Height / s.ZoneRows;
            if (i > 0) yield return new("line", 10, y, 30, y, Color: color);
            string text = ((char)('A' + i)).ToString(); yield return new("text", 20 - Drawing.Measure(text, 10).Width / 2, y + r.Height / s.ZoneRows / 2 - 7, Text: text, Size: 10);
        }
    }
}

public sealed record ReferencePoint(Sheet Sheet, Component Component);
public sealed class CrossReferences
{
    private readonly Dictionary<string, List<ReferencePoint>> targets = [];
    public static bool IsPoint(Component c) => c.Symbol is "folio_next" or "folio_previous";
    public static IEnumerable<ReferencePoint> Points(Project p) => p.Sheets.SelectMany(s => s.Components.Where(IsPoint).Select(c => new ReferencePoint(s, c)));
    public CrossReferences(Project p)
    {
        var order = p.Sheets.Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        foreach (var group in Points(p).Where(x => x.Component.LinkName.Trim().Length > 0).GroupBy(x => x.Component.LinkName.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var points = group.OrderBy(x => x.Component.LinkOrder).ThenBy(x => order[x.Sheet.Id]).ThenBy(x => x.Component.Y).ThenBy(x => x.Component.X).ThenBy(x => x.Component.Id, StringComparer.Ordinal).ToList();
            if (points.Select(x => x.Component.LinkMode).Distinct().Count() != 1) continue;
            if (points[0].Component.LinkMode == "star")
            {
                if (points.Count(x => x.Component.LinkSource) > 1) continue;
                var source = points.FirstOrDefault(x => x.Component.LinkSource) ?? points[0];
                targets[source.Component.Id] = points.Where(x => x != source).ToList();
                foreach (var point in points.Where(x => x != source)) targets[point.Component.Id] = [source];
            }
            else for (int i = 0; i + 1 < points.Count; i += 2) { targets[points[i].Component.Id] = [points[i + 1]]; targets[points[i + 1].Component.Id] = [points[i]]; }
        }
    }
    public IReadOnlyList<ReferencePoint> Targets(string id) => targets.GetValueOrDefault(id) ?? [];
    public string Label(Project p, Component c) => Targets(c.Id).Count == 0 ? "Non lié" : string.Join(" ; ", Targets(c.Id).Select(t => SheetZones.Address(p, t.Sheet, t.Component)));
    public static IEnumerable<Finding> Findings(Project p)
    {
        var links = new CrossReferences(p);
        foreach (var point in Points(p))
        {
            var c = point.Component;
            if (links.Targets(c.Id).Count == 0) yield return new("Renvoi", point.Sheet.Id, c.Id, $"{c.Tag} : renvoi non lié, incomplet ou paramètres incompatibles ({c.LinkName})");
        }
    }
    // Graph edges include wires and resolved interruption-point pairs. This is
    // connectivity, not mere page navigation; electrical device internals are excluded.
    public static List<Endpoint> Trace(Project p, Endpoint start)
    {
        var components = p.Sheets.SelectMany(s => s.Components).ToDictionary(c => c.Id);
        if (!components.TryGetValue(start.ComponentId, out var c) || !c.Definition.Ports.Any(port => port.Id == start.PortId)) throw new ArgumentException("Borne introuvable.");
        Dictionary<Endpoint, List<Endpoint>> edges = [];
        void Edge(Endpoint a, Endpoint b) { if (!edges.TryGetValue(a, out var list)) edges[a] = list = []; list.Add(b); }
        foreach (var w in p.Sheets.SelectMany(s => s.Wires)) { Edge(w.From, w.To); Edge(w.To, w.From); }
        var refs = new CrossReferences(p);
        foreach (var point in Points(p)) foreach (var target in refs.Targets(point.Component.Id)) Edge(new(point.Component.Id, "1"), new(target.Component.Id, "1"));
        HashSet<Endpoint> visited = [start]; Queue<Endpoint> queue = new(); queue.Enqueue(start);
        while (queue.TryDequeue(out var current)) foreach (var next in edges.GetValueOrDefault(current) ?? []) if (visited.Add(next)) queue.Enqueue(next);
        return visited.OrderBy(x => x.ComponentId, StringComparer.Ordinal).ThenBy(x => x.PortId, StringComparer.Ordinal).ToList();
    }
}

public sealed partial class Editor
{
    public void ConfigureReference(string id, string name, string mode, int order, bool source)
    {
        var c = Sheet.Components.Single(x => x.Id == id && CrossReferences.IsPoint(x));
        Change(() => { c.LinkName = name.Trim(); c.LinkOrder = order; c.LinkMode = mode; c.LinkSource = source && mode == "star";
            if (c.LinkName.Length > 0) foreach (var other in CrossReferences.Points(Project).Select(x => x.Component).Where(x => x.Id != c.Id && x.LinkName.Trim().Equals(c.LinkName, StringComparison.OrdinalIgnoreCase))) { other.LinkMode = mode; if (source || mode == "pair") other.LinkSource = false; }
        });
    }
}
