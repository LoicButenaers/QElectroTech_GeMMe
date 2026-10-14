using System.Windows;

namespace GeMMeElec;

public static class SheetZones
{
    public static int Columns(Project p, Sheet s) => s.UseProjectZones ? p.ZoneColumns : s.ZoneColumns;
    public static int Rows(Project p, Sheet s) => s.UseProjectZones ? p.ZoneRows : s.ZoneRows;
    public static Rect Area(Project p, Sheet s) => new(30, 30, s.Width - 60, DocumentModel.DrawingBottom(p, s) - 30);
    public static string Zone(Project p, Sheet s, double x, double y)
    {
        var area = Area(p, s);
        if(s.Kind=="cover")return "page de garde";
        if (s.BomPage is not null || !area.Contains(new Point(x, y))) return "hors cadre";
        int columns = Columns(p, s), rows = Rows(p, s);
        int column = Math.Min(columns - 1, (int)((x - area.Left) / area.Width * columns));
        int row = Math.Min(rows - 1, (int)((y - area.Top) / area.Height * rows));
        return $"{(char)('A' + row)}{column + 1}";
    }
    public static string Address(Project p, Sheet s, Component c) => $"{DocumentModel.PageNumber(p, s)} / {Zone(p, s, c.X, c.Y)}";
    public static IEnumerable<Ink> Frame(Project p, Sheet s)
    {
        if(s.Kind=="cover")yield break;
        var r = Area(p, s); const string color = "#53677B";
        yield return new("rect", 10, 10, s.Width - 40, r.Bottom - 10, Color: color);
        yield return new("line", 10, 30, s.Width - 30, 30, Color: color);
        yield return new("line", 30, 10, 30, r.Bottom, Color: color);
        int columns = Columns(p, s), rows = Rows(p, s);
        for (int i = 0; i < columns; i++)
        {
            double x = r.Left + i * r.Width / columns;
            if (i > 0) yield return new("line", x, 10, x, 30, Color: color);
            string text = (i + 1).ToString(); yield return new("text", x + r.Width / columns / 2 - Drawing.Measure(text, 10).Width / 2, 12, Text: text, Size: 10);
        }
        for (int i = 0; i < rows; i++)
        {
            double y = r.Top + i * r.Height / rows;
            if (i > 0) yield return new("line", 10, y, 30, y, Color: color);
            string text = ((char)('A' + i)).ToString(); yield return new("text", 20 - Drawing.Measure(text, 10).Width / 2, y + r.Height / rows / 2 - 7, Text: text, Size: 10);
        }
    }
}

public sealed record ReferencePoint(Sheet Sheet, Component Component);
public sealed class CrossReferences
{
    public CoilContacts Coils { get; }
    private readonly Dictionary<string, List<ReferencePoint>> targets = [];
    public static bool IsPoint(Component c) => c.Symbol is "folio_next" or "folio_previous";
    public static IEnumerable<ReferencePoint> Points(Project p) => p.Sheets.SelectMany(s => s.Components.Where(IsPoint).Select(c => new ReferencePoint(s, c)));
    public CrossReferences(Project p)
    {
        Coils = new(p);
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
    // Includes wires, resolved reports and explicit permanent terminal continuity.
    public static List<Endpoint> Trace(Project p, Endpoint start) => new ConductorNetworks(p).Trace(start).OrderBy(x => x.ComponentId, StringComparer.Ordinal).ThenBy(x => x.PortId, StringComparer.Ordinal).ToList();

}

public sealed partial class Editor
{
    public void LinkReferencePair(string firstId, string secondId, string name)
    {
        var points = CrossReferences.Points(Project).Select(p => p.Component).ToList();
        var first = points.Single(c => c.Id == firstId); var second = points.Single(c => c.Id == secondId);
        name = name.Trim();
        if (first.Id == second.Id || first.Symbol == second.Symbol) throw new ArgumentException("Choisir un départ et une arrivée distincts.");
        if (name.Length == 0) throw new ArgumentException("Saisir un nom de liaison.");
        if (points.Any(c => c.Id != firstId && c.Id != secondId && c.LinkName.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Ce nom appartient déjà à d’autres reports. Choisir un nom unique pour cette paire, ou utiliser les propriétés pour une liaison en étoile.");
        Change(() => { foreach (var c in new[] { first, second }) { c.LinkName = name; if (!c.ReferenceIndependentName) c.ReferenceName = name; c.LinkMode = "pair"; c.LinkSource = false; c.LinkOrder = 0; } });
    }
    public void ConfigureReference(string id, string name, string mode, int order, bool source, string? displayName = null, bool? independentName = null, string? historyKey = null)
    {
        var c = Sheet.Components.Single(x => x.Id == id && CrossReferences.IsPoint(x));
        Change(() => {
            bool rejoin = c.ReferenceIndependentName && independentName == false;
            string oldDisplay = ReferenceDisplay.Name(c), oldLink = c.LinkName;
            c.ReferenceIndependentName = independentName ?? c.ReferenceIndependentName;
            c.LinkName = name.Trim(); c.LinkOrder = order; c.LinkMode = mode; c.LinkSource = source && mode == "star";
            if (c.LinkName.Length > 0) foreach (var other in CrossReferences.Points(Project).Select(x => x.Component).Where(x => x.Id != c.Id && x.LinkName.Trim().Equals(c.LinkName, StringComparison.OrdinalIgnoreCase))) { other.LinkMode = mode; if (source || mode == "pair") other.LinkSource = false; }
            string value = displayName ?? (oldLink != c.LinkName && !c.ReferenceIndependentName ? c.LinkName : ReferenceDisplay.Name(c));
            if (rejoin && (displayName is null || displayName == oldDisplay)) value = LinkedNames(c).FirstOrDefault(x => x.Id != c.Id && !x.ReferenceIndependentName) is Component shared ? ReferenceDisplay.Name(shared) : value;
            if (displayName is not null || oldLink != c.LinkName || rejoin) SetReferenceName(c, value);
        }, historyKey);
    }
    private List<Component> LinkedNames(Component c)
    {
        var refs = new CrossReferences(Project); var result = new List<Component>(); var seen = new HashSet<string>(); var queue = new Queue<Component>(); queue.Enqueue(c);
        while (queue.TryDequeue(out var current)) { if (!seen.Add(current.Id)) continue; result.Add(current); foreach (var target in refs.Targets(current.Id)) queue.Enqueue(target.Component); }
        return result;
    }
    internal void SetReferenceName(Component c, string name)
    {
        c.ReferenceName = name;
        if (!c.ReferenceIndependentName) foreach (var linked in LinkedNames(c).Where(x => !x.ReferenceIndependentName)) linked.ReferenceName = name;
    }
    public void RenameReference(string id, string name, string? historyKey = null)
    {
        var c = CrossReferences.Points(Project).Single(x => x.Component.Id == id).Component;
        Change(() => SetReferenceName(c, name), historyKey);
    }

}
