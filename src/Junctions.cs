using System.Windows;

namespace GeMMeElec;

public static class Junctions
{
    public static bool Automatic(Component c) => c.Symbol == "junction" && (c.AutoJunction || !c.TagPlacement.Visible && !c.DetailPlacement.Visible && !c.DescriptionPlacement.Visible);
    public static bool Cleanup(Project p)
    {
        bool changed = false;
        foreach (var s in p.Sheets)
        {
            var nodes = s.Components.Where(Automatic).ToList();
            // A two-edge node can be dissolved without losing segment metadata only
            // when both remaining conductors have the same number and colour.
            foreach (var c in nodes)
            {
                var edges = s.Wires.Where(w => w.From.ComponentId == c.Id || w.To.ComponentId == c.Id).ToList();
                if (edges.Count == 0) { s.Components.Remove(c); changed = true; continue; }
                if (edges.Count != 2 || edges[0].CableId!=edges[1].CableId||edges[0].CableCore!=edges[1].CableCore||edges[0].SectionMm2!=edges[1].SectionMm2||edges[0].RoutingMode!=edges[1].RoutingMode||edges[0].Number != edges[1].Number || edges[0].Color != edges[1].Color || edges[0].NumberIndependent != edges[1].NumberIndependent || edges[0].Thickness != edges[1].Thickness || edges[0].LineStyle != edges[1].LineStyle || System.Text.Json.JsonSerializer.Serialize(edges[0].NumberPlacement) != System.Text.Json.JsonSerializer.Serialize(edges[1].NumberPlacement)) continue;
                var a = edges[0]; var b = edges[1]; var start = a.From.ComponentId == c.Id ? a.To : a.From; var end = b.From.ComponentId == c.Id ? b.To : b.From;
                if (start == end || s.Wires.Except(edges).Any(w => w.From == start && w.To == end || w.From == end && w.To == start)) continue;
                var ra = a.Route(s); if (a.From.ComponentId == c.Id) ra.Reverse(); var rb = b.Route(s); if (b.To.ComponentId == c.Id) rb.Reverse();
                var route = WireRouting.Simplify(ra.Concat(rb)); if (route.Count > 102) continue;
                a.From = start; a.To = end; a.Waypoints = route.Skip(1).Take(route.Count - 2).Select(x => new Position(x.X, x.Y)).ToList(); s.Wires.Remove(b); s.Components.Remove(c); changed = true;
            }
        }
        return changed;
    }
    public static List<(Point Point, string Color)> Dots(Sheet s, Dictionary<string, List<Point>> routes)
    {
        var index = s.Components.ToDictionary(c => c.Id); var result = new Dictionary<Point, string>();
        var ends = s.Wires.SelectMany(w => new[] { (End: w.From, Wire: w, Reverse: false), (End: w.To, Wire: w, Reverse: true) });
        foreach (var group in ends.GroupBy(x => x.End))
        {
            var entries = group.ToArray(); if (entries.Length < 2) continue;
            var c = index[group.Key.ComponentId];
            if (Automatic(c)) { if (entries.Length >= 3) result[c.PortPoint(group.Key.PortId)] = entries[0].Wire.Color; continue; }
            // Shared prefixes terminate at the actual divergence, even after routing
            // or moving a device. Mere geometric crossings never create connections.
            var paths = entries.Select(e => e.Reverse ? routes[e.Wire.Id].AsEnumerable().Reverse().ToList() : routes[e.Wire.Id]).ToArray();
            foreach (var point in Divergences(paths)) result[point] = entries[0].Wire.Color;
        }
        return result.Select(x => (x.Key, x.Value)).ToList();
    }
    private sealed class PrefixNode(Point point)
    {
        public Point Point = point; public int Ends;
        public Dictionary<(int,int), PrefixNode> Children = [];
    }
    // A compressed route-prefix tree avoids comparing every pair of wires on a
    // busy terminal. Each orthogonal segment is inserted once, splitting edges.
    private static IEnumerable<Point> Divergences(List<Point>[] paths)
    {
        var root = new PrefixNode(paths[0][0]);
        foreach (var path in paths)
        {
            var node = root; int i = 1;
            while (i < path.Count)
            {
                var vector = path[i] - node.Point; if (vector.Length < .001) { i++; continue; }
                var direction = (Math.Sign(vector.X), Math.Sign(vector.Y));
                if (!node.Children.TryGetValue(direction, out var child)) { child = new(path[i]); node.Children[direction] = child; node = child; i++; continue; }
                double distance = (child.Point - node.Point).Length;
                if (distance > vector.Length + .001) { var split = new PrefixNode(path[i]); split.Children[direction] = child; node.Children[direction] = split; node = split; i++; }
                else { node = child; if (Math.Abs(distance - vector.Length) < .001) i++; }
            }
            node.Ends++;
        }
        var queue = new Queue<PrefixNode>(); queue.Enqueue(root);
        while (queue.TryDequeue(out var node)) { if (node.Children.Count + (node.Ends > 0 ? 1 : 0) > 1 || node.Ends > 1) yield return node.Point; foreach (var child in node.Children.Values) queue.Enqueue(child); }
    }
    public static void ShiftSegment(Sheet sheet, string wireId, IReadOnlyList<Point> original, int segment, Point pointer)
    {
        var wire = sheet.Wires.Single(w => w.Id == wireId); var a = original[segment - 1]; var b = original[segment]; bool horizontal = Math.Abs(a.Y - b.Y) < .001;
        double coordinate = Editor.Snap(horizontal ? pointer.Y : pointer.X);
        var moved = original.ToList(); moved[segment - 1] = horizontal ? new(a.X, coordinate) : new(coordinate, a.Y); moved[segment] = horizontal ? new(b.X, coordinate) : new(coordinate, b.Y);
        bool Move(Endpoint end, Point target)
        {
            var c = sheet.Components.Single(c => c.Id == end.ComponentId); if (!Automatic(c)) return false;
            c.X = target.X; c.Y = target.Y; return true;
        }
        if (segment == 1 && !Move(wire.From, moved[0])) moved.Insert(0, original[0]);
        if (segment == original.Count - 1 && !Move(wire.To, moved[^1])) moved.Add(original[^1]);
        var route = WireRouting.Simplify(moved); wire.Waypoints = route.Skip(1).Take(route.Count - 2).Select(x => new Position(x.X, x.Y)).ToList();
    }
}
