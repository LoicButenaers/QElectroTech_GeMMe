using System.Windows;

namespace GeMMeElec;

public sealed partial class Editor
{
    // Project onto the selected segment before snapping along its axis: a junction
    // must stay on the wire even when the terminal is off the placement grid.
    public static (Point Point, int Segment, double Distance) WirePoint(IReadOnlyList<Point> route, Point point)
    {
        var best = (Point: route[0], Segment: 1, Distance: double.MaxValue);
        for (int i = 1; i < route.Count; i++)
        {
            var a = route[i - 1]; var b = route[i]; var v = b - a;
            var p = v.LengthSquared < .001 ? a : a + v * Math.Clamp(Vector.Multiply(point - a, v) / v.LengthSquared, 0, 1);
            double distance = (p - point).Length;
            if (distance >= best.Distance) continue;
            p = Math.Abs(a.Y - b.Y) < .001
                ? new(Math.Clamp(Snap(p.X), Math.Min(a.X, b.X), Math.Max(a.X, b.X)), a.Y)
                : new(a.X, Math.Clamp(Snap(p.Y), Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y)));
            best = (p, i, distance);
        }
        return best;
    }

    public Wire Branch(Endpoint from, string targetId, Point location, IEnumerable<Position>? points = null, string? reconnectId = null, bool changeFrom = false)
    {
        if (!double.IsFinite(location.X) || !double.IsFinite(location.Y)) throw new ArgumentException("Position invalide.");
        if (targetId == reconnectId) throw new ArgumentException("Choisir un autre conducteur pour la dérivation.");
        var target = Sheet.Wires.Single(w => w.Id == targetId);
        var route = target.Route(Sheet); var hit = WirePoint(route, location);
        if (hit.Distance > 100) throw new ArgumentException("Position trop éloignée du conducteur.");
        Wire? result = null;
        var newNumber = target.NumberIndependent ? NumberForConnection(from, target.From) : target.Number; var tag = NextTag("J");
        Change(() =>
        {
            Endpoint destination;
            List<Position>? branchPoints = null;
            if ((hit.Point - route[0]).Length < .001) destination = target.From;
            else if ((hit.Point - route[^1]).Length < .001) destination = target.To;
            else
            {
                var existing = reconnectId is null ? null : Sheet.Wires.Single(w => w.Id == reconnectId);
                var originEnd = existing is null ? from : changeFrom ? existing.To : existing.From;
                var origin = Sheet.Components.Single(c => c.Id == originEnd.ComponentId).PortPoint(originEnd.PortId);
                var stops = existing is null ? points ?? [] : changeFrom ? existing.Waypoints.AsEnumerable().Reverse() : existing.Waypoints;
                branchPoints = Interior(BranchPath(origin, BranchStops(Sheet, originEnd, stops, hit.Point), route, hit.Point, hit.Segment));
                if (changeFrom) branchPoints.Reverse();
                var junction = new Component { Symbol = "junction", AutoJunction = true, Tag = tag, X = hit.Point.X, Y = hit.Point.Y,
                    TagPlacement = new() { Visible = false }, DetailPlacement = new() { Visible = false }, DescriptionPlacement = new() { Visible = false } };
                destination = new(junction.Id, "1"); Sheet.Components.Add(junction);
                var right = new Wire { From = destination, To = target.To, Number = target.Number, Color = target.Color, NumberIndependent = target.NumberIndependent, Thickness = target.Thickness, LineStyle = target.LineStyle, NumberPlacement = DocumentModel.Copy(target.NumberPlacement),
                    Waypoints = Interior(new[] { hit.Point }.Concat(route.Skip(hit.Segment))) };
                target.To = destination; target.Waypoints = Interior(route.Take(hit.Segment).Append(hit.Point));
                Sheet.Wires.Add(right);
            }
            if (reconnectId is not null)
            {
                result = Sheet.Wires.Single(w => w.Id == reconnectId);
                if (changeFrom) result.From = destination; else result.To = destination;
                if (branchPoints is not null) result.Waypoints = branchPoints;
            }
            else { result = new Wire { From = from, To = destination, Waypoints = branchPoints ?? points?.ToList() ?? [], Number = newNumber, Color = CurrentWireColor }; Sheet.Wires.Add(result); }
            SynchronizeWireNumbers(result, result.NumberIndependent ? result.Number : newNumber);
        });
        return result!;
    }
    internal static List<Point> BranchPath(Point origin, IEnumerable<Position> stops, IReadOnlyList<Point> target, Point destination, int segment)
    {
        List<Point> path = [origin];
        void Append(Point point) { var last = path[^1]; if (last.X != point.X && last.Y != point.Y) path.Add(new(point.X, last.Y)); path.Add(point); }
        foreach (var stop in stops) Append(stop.Point);
        var end = path[^1]; bool vertical = Math.Abs(target[segment].X - target[segment - 1].X) < .001;
        double coordinate = vertical ? end.X : end.Y, axis = vertical ? destination.X : destination.Y;
        int sign = coordinate >= axis ? 1 : -1;
        bool Blocked(int direction) => target.Where(p => (p - destination).Length > .001).Any(p => vertical
            ? Math.Abs(p.Y - destination.Y) < .001 && Math.Sign(p.X - destination.X) == direction && (p == target[segment - 1] || p == target[segment] || segment + 1 < target.Count && p == target[segment + 1])
            : Math.Abs(p.X - destination.X) < .001 && Math.Sign(p.Y - destination.Y) == direction && (p == target[segment - 1] || p == target[segment] || segment + 1 < target.Count && p == target[segment + 1]));
        if (Blocked(sign)) { sign = -sign; coordinate = axis + sign * 40; }
        if (Math.Abs(coordinate - axis) < 20) coordinate = axis + sign * 40;
        if (vertical) { Append(new(coordinate, end.Y)); Append(new(coordinate, destination.Y)); }
        else { Append(new(end.X, coordinate)); Append(new(destination.X, coordinate)); }
        Append(destination); return WireRouting.Simplify(path);
    }
    internal static List<Position> BranchStops(Sheet sheet, Endpoint endpoint, IEnumerable<Position> stops, Point destination)
    {
        var result = stops.ToList(); if (result.Count > 0) return result;
        var source = sheet.Components.Single(c => c.Id == endpoint.ComponentId);
        if (source.Symbol == "junction") return result;
        var origin = source.PortPoint(endpoint.PortId); var normal = origin - new Point(source.X, source.Y); var bounds = source.Bounds();
        // Leave the source terminal outwards before turning towards the branch;
        // otherwise a top terminal could route through its own device.
        if (Math.Abs(normal.Y) >= Math.Abs(normal.X))
        {
            double y = origin.Y + (normal.Y <= 0 ? -20 : 20);
            result.Add(new(origin.X, y)); result.Add(new(destination.X < origin.X ? bounds.Left - 20 : bounds.Right + 20, y));
        }
        else
        {
            double x = origin.X + (normal.X <= 0 ? -20 : 20);
            result.Add(new(x, origin.Y)); result.Add(new(x, destination.Y < origin.Y ? bounds.Top - 20 : bounds.Bottom + 20));
        }
        return result;
    }
    private static List<Position> Interior(IEnumerable<Point> points)
    {
        var route = WireRouting.Simplify(points);
        return route.Skip(1).Take(Math.Max(0, route.Count - 2)).Select(p => new Position(p.X, p.Y)).ToList();
    }
    public List<Sheet> GenerateBom(BomOptions options)
    {
        List<Sheet> pages = [];
        Change(() =>
        {
            Project.Sheets.RemoveAll(s => s.BomPage is not null);
            if (Project.Sheets.Count == 0) throw new ArgumentException("Ajouter un folio de schéma avant la nomenclature.");
            Project.BomOptions = DocumentModel.Copy(options);
            pages = BomFolios.Create(Project, options);
            Project.Sheets.AddRange(pages);
        });
        SelectSheet(pages[0].Id); return pages;
    }
}
