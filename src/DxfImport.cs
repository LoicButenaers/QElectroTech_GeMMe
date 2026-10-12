using System.IO;
using System.Windows;
using netDxf;
using netDxf.Entities;
using DxfText = netDxf.Entities.Text;

namespace GeMMeElec;

public sealed record DxfImportResult(SymbolDefinition Symbol, List<string> Warnings, string Units);
public static class DxfImport
{
    public static DxfImportResult Read(string path)
    {
        if (new FileInfo(path).Length > 20_000_000) throw new InvalidDataException("DXF limité à 20 Mo.");
        var version = DxfDocument.CheckDxfFileVersion(path);
        if (version < netDxf.Header.DxfVersion.AutoCad2000) throw new InvalidDataException("Enregistrer le DXF en version AutoCAD 2000 ou ultérieure (ASCII ou binaire).");
        var document = DxfDocument.Load(path) ?? throw new InvalidDataException("DXF illisible.");
        List<Shape> shapes = []; Dictionary<string, int> omitted = []; int visited = 0;
        void Warn(string message) { omitted[message] = omitted.GetValueOrDefault(message) + 1; }
        void Line(double x, double y, double a, double b) { shapes.Add(new("line", x, -y, a, -b)); }
        void Poly(IEnumerable<Vector2> vertices, bool close)
        {
            var points = vertices.ToList(); for (int i = 1; i < points.Count; i++) Line(points[i-1].X, points[i-1].Y, points[i].X, points[i].Y);
            if (close && points.Count > 1) Line(points[^1].X, points[^1].Y, points[0].X, points[0].Y);
        }
        void Text(string value, Vector3 pos, double height, double rotation)
        {
            var lines = value.Replace("\\P", "\n").Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++) shapes.Add(new("text", pos.X, -pos.Y - height + i * height * 1.3, Math.Max(.5, height), Text: lines[i]) { Rotation = -rotation });
        }
        void Visit(EntityObject entity, int depth)
        {
            if (++visited > 50000 || shapes.Count > 20000 || depth > 12) throw new InvalidDataException("DXF trop complexe : limiter les entités ou décomposer les blocs.");
            if (!entity.IsVisible || !entity.Layer.IsVisible) return;
            if (Math.Abs(entity.Normal.X) > .001 || Math.Abs(entity.Normal.Y) > .001) { Warn("Entités hors du plan XY omises"); return; }
            switch (entity)
            {
                case Line l: Line(l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y); break;
                case Circle c: shapes.Add(new("circle", c.Center.X, -c.Center.Y, c.Radius)); break;
                case Arc a:
                    double sweep = (a.EndAngle - a.StartAngle + 360) % 360; if (sweep < .001) sweep = 360;
                    Poly(Enumerable.Range(0, 49).Select(i => { double angle = (a.StartAngle + sweep * i / 48) * Math.PI / 180; return new Vector2(a.Center.X + a.Radius * Math.Cos(angle), a.Center.Y + a.Radius * Math.Sin(angle)); }), false); break;
                case Polyline2D poly: foreach (var part in poly.Explode()) Visit(part, depth + 1); break;
                case Polyline3D poly: Poly(poly.Vertexes.Select(v => new Vector2(v.X, v.Y)), poly.IsClosed); Warn("Polylignes 3D projetées dans XY"); break;
                case Ellipse ellipse: Visit(ellipse.ToPolyline2D(96), depth + 1); break;
                case Spline spline: var vertices = spline.PolygonalVertexes(96); Poly(vertices.Select(v => new Vector2(v.X, v.Y)), spline.IsClosed); break;
                case DxfText t: Text(t.Value, t.Position, t.Height, t.Rotation); if (t.Alignment != netDxf.Entities.TextAlignment.BaselineLeft) Warn("Alignements de texte ramenés à gauche"); break;
                case MText t: Text(t.PlainText(), t.Position, t.Height, t.Rotation); Warn("Texte multiligne : mise en forme simplifiée"); break;
                case Insert insert:
                    foreach (var part in insert.Explode()) Visit(part, depth + 1);
                    foreach (var attr in insert.Attributes.Where(a => a.IsVisible)) Text(attr.Value, attr.Position, attr.Height, attr.Rotation);
                    break;
                case Solid solid: Poly([solid.FirstVertex, solid.SecondVertex, solid.ThirdVertex, solid.FourthVertex], true); Warn("Aplats convertis en contours"); break;
                case netDxf.Entities.Point: break;
                default: Warn("Entité omise : " + entity.Type); break;
            }
        }
        foreach (var entity in document.Entities.All) Visit(entity, 0);
        if (shapes.Count == 0) throw new InvalidDataException("Aucune géométrie 2D compatible dans l’espace objet du DXF.");
        var bounds = Bounds(shapes); double cx = (bounds.Left + bounds.Right) / 2, cy = (bounds.Top + bounds.Bottom) / 2;
        var definition = new SymbolDefinition("custom-" + Guid.NewGuid().ToString("N"), Path.GetFileNameWithoutExtension(path), "U", Math.Max(10, bounds.Width), Math.Max(10, bounds.Height), [], shapes.Select(s => Offset(s, -cx, -cy)).ToList()) { Category = "Mes symboles" };
        var warnings = omitted.Select(x => $"{x.Key} : {x.Value}").ToList(); warnings.Add("Couleurs, épaisseurs et styles de traits uniformisés ; arcs et courbes approchés par segments. Aucun réseau électrique ni borne n’est déduit du dessin.");
        return new(definition, warnings, document.DrawingVariables.InsUnits.ToString());
    }
    public static Shape Offset(Shape s, double dx, double dy) => s with { X = s.X + dx, Y = s.Y + dy, A = s.Kind == "line" ? s.A + dx : s.A, B = s.Kind == "line" ? s.B + dy : s.B };
    public static SymbolDefinition Scale(SymbolDefinition d, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0 || factor > 10000) throw new ArgumentException("Échelle positive requise.");
        return d with { Width = d.Width * factor, Height = d.Height * factor, Ports = d.Ports.Select(p => p with { X = p.X * factor, Y = p.Y * factor }).ToList(), Shapes = d.Shapes.Select(s => s with { X = s.X * factor, Y = s.Y * factor, A = s.A * factor, B = s.B * factor }).ToList() };
    }
    public static Rect Bounds(IEnumerable<Shape> shapes)
    {
        var bounds = Rect.Empty;
        foreach (var s in shapes)
        {
            var points = s.Kind switch { "line" => new[] { new System.Windows.Point(s.X, s.Y), new(s.A, s.B) }, "circle" or "dot" => [new(s.X-s.A, s.Y-s.A), new(s.X+s.A, s.Y+s.A)], "text" => [new(s.X,s.Y), new(s.X + s.Text.Length * s.A * .65, s.Y + s.A * 1.4)], _ => [new(s.X,s.Y), new(s.X+s.A,s.Y+s.B)] };
            foreach (var point in points) { var v = point - new System.Windows.Point(s.X, s.Y); double a = s.Rotation * Math.PI / 180; bounds.Union(new System.Windows.Point(s.X + v.X*Math.Cos(a)-v.Y*Math.Sin(a), s.Y+v.X*Math.Sin(a)+v.Y*Math.Cos(a))); }
        }
        return bounds;
    }
}
