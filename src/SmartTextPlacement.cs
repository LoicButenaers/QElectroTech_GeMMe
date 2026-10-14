using System.Windows;
using System.Windows.Media;

namespace GeMMeElec;

public sealed record SmartTextResult(bool Changed, bool Clear, string Message);

// Bounded, deterministic search. Only the requested draft label is changed;
// obstacles are collected once per click, never on pointer movement.
public static class SmartTextPlacement
{
    public static SmartTextResult Adjust(Project project, Sheet sheet, Component draft, int target, string? requestedSide = null)
    {
        if (target < 0 || target >= ComponentTexts.List(draft).Count) throw new ArgumentException("Texte inconnu.");
        if (requestedSide is not (null or "top" or "bottom" or "left" or "right")) throw new ArgumentException("Choisir haut, bas, gauche ou droite.");
        var references = new CrossReferences(project);
        var working = DocumentModel.Copy(draft); working.ResolvedDefinition = draft.Definition;
        var placement = Placement(working, target);
        if (!placement.Visible || Labels(project, working, references, target).Count == 0)
            return new(false, true, "Ce texte est vide ou masqué. Choisis un texte visible à ajuster.");
        // Moving a destination independently must not drag the name with it.
        if (CrossReferences.IsPoint(working) && target == 1) working.ReferenceDestinationInline = false;
        var moving = Labels(project, working, references, target);
        List<Rect> obstacles = [];
        void Add(Rect box) { if (!box.IsEmpty) { box.Inflate(3, 3); obstacles.Add(box); } }
        foreach (var c in sheet.Components)
        {
            var component = c.Id == draft.Id ? working : c;
            Add(component.Bounds());
            foreach (var ink in Drawing.ComponentScene(project, component, references).Where(i => i.Kind == "text"))
                if (c.Id != draft.Id || !moving.Contains(ink)) Add(Bounds(ink));
        }
        var components = sheet.Components.ToDictionary(c => c.Id);
        foreach (var wire in sheet.Wires)
        {
            var route = wire.Route(sheet, components);
            for (int i = 1; i < route.Count; i++) Add(new Rect(route[i - 1], route[i]));
            if (Drawing.WireLabel(wire, route,project.Presentation) is Ink label) Add(Bounds(label));
        }
        foreach (var note in sheet.Notes) Add(FreeGraphics.NoteBounds(project,sheet,note));
        var paper = new Rect(30, 40, Math.Max(1, sheet.Width - 60), Math.Max(1, DocumentModel.DrawingBottom(project, sheet) - 40));
        var symbol = working.Bounds();
        var candidates = new List<(LabelPlacement Placement, Rect Bounds, double Preference)>();
        var envelope = Rect.Empty;
        string[] sides = requestedSide is null ? ["right", "left", "top", "bottom"] : [requestedSide];
        for (int sideIndex = 0; sideIndex < sides.Length; sideIndex++)
        foreach (double gap in new[] { 8d, 12, 20, 32, 48, 72, 96 })
        for (int anchor = 0; anchor < 3; anchor++)
        {
            string side = sides[sideIndex]; bool lateral = side is "left" or "right";
            var p = new LabelPlacement { FontSize = placement.FontSize, Color = placement.Color, Rotation = placement.Rotation, Side = side, Gap = gap, Visible = true,
                Horizontal = lateral ? side == "right" ? "left" : "right" : anchor == 0 ? "center" : anchor == 1 ? "left" : "right",
                Vertical = !lateral ? side == "top" ? "bottom" : "top" : anchor == 0 ? "center" : anchor == 1 ? "top" : "bottom",
                OffsetX = !lateral && anchor != 0 ? (anchor == 1 ? -1 : 1) * symbol.Width / 2 : 0,
                OffsetY = lateral && anchor != 0 ? (anchor == 1 ? -1 : 1) * symbol.Height / 2 : 0 };
            Set(working, target, p);
            var bounds = Rect.Empty; foreach (var ink in Labels(project, working, references, target)) bounds.Union(Bounds(ink));
            if (bounds.IsEmpty) continue;
            envelope.Union(bounds); candidates.Add((p, bounds, gap * .1 + sideIndex * .2 + anchor * .4));
        }
        // Discard far-away obstacles before evaluating 21 candidates per allowed side.
        obstacles = obstacles.Where(r => r.IntersectsWith(envelope)).ToList();
        double bestScore = double.PositiveInfinity; (LabelPlacement Placement, Rect Bounds, double Preference) best = default; bool clear = false;
        foreach (var candidate in candidates)
        {
            double score = candidate.Preference; bool available = paper.Contains(candidate.Bounds);
            if (!available) score += 100000000 + OutsideArea(candidate.Bounds, paper) * 100;
            foreach (var obstacle in obstacles)
            {
                var intersection = Rect.Intersect(candidate.Bounds, obstacle);
                if (intersection.IsEmpty || intersection.Width <= .001 || intersection.Height <= .001) continue;
                available = false; score += 1000000 + intersection.Width * intersection.Height;
            }
            if (score < bestScore) { bestScore = score; best = candidate; clear = available; }
        }
        if (best.Placement is null) return new(false, false, "Aucune position proposée pour ce texte.");
        Set(draft, target, best.Placement); draft.ReferenceDestinationInline = working.ReferenceDestinationInline;
        string location = requestedSide switch { "top" => "en haut", "bottom" => "en bas", "left" => "à gauche", "right" => "à droite", _ => "autour du symbole" };
        return new(true, clear, clear ? $"Texte aligné {location}, sans chevauchement détecté." : $"Position proposée {location}. Espace insuffisant : vérifie les chevauchements et les limites du folio.");
    }
    public static SmartTextResult AdjustAll(Project project, Sheet sheet, Component draft, string? side = null)
    {
        if (side is not (null or "top" or "bottom" or "left" or "right")) throw new ArgumentException("Côté inconnu.");
        var refs = new CrossReferences(project); var copy = DocumentModel.Copy(draft); copy.ResolvedDefinition = draft.Definition;
        var visible = ComponentTexts.List(copy).Select(t => (Target: t, Ink: ComponentTexts.Ink(project, copy, refs, t.Key))).Where(t => t.Ink is not null).Select(t => (t.Target, Size: Drawing.TextBounds(t.Ink!).Size)).ToList();
        if (visible.Count == 0) return new(false, true, "Aucun texte visible à aligner.");
        double width = visible.Max(t => t.Size.Width), height = visible.Sum(t => t.Size.Height) + 6 * (visible.Count - 1);
        var obstacles = new List<Rect> { draft.Bounds() };
        foreach (var c in sheet.Components.Where(c => c.Id != draft.Id)) { obstacles.Add(c.Bounds()); obstacles.AddRange(Drawing.ComponentScene(project, c, refs).Where(i => i.Kind == "text").Select(Drawing.TextBounds)); }
        var index = sheet.Components.ToDictionary(c => c.Id);
        foreach (var wire in sheet.Wires) { var path = wire.Route(sheet, index); for (int i = 1; i < path.Count; i++) { var r = new Rect(path[i - 1], path[i]); r.Inflate(3, 3); obstacles.Add(r); } if (Drawing.WireLabel(wire, path,project.Presentation) is Ink label) obstacles.Add(Drawing.TextBounds(label)); }
        obstacles.AddRange(sheet.Notes.Select(n => FreeGraphics.NoteBounds(project,sheet,n)));
        var paper = new Rect(30, 40, sheet.Width - 60, Math.Max(1, DocumentModel.DrawingBottom(project, sheet) - 40)); var bounds = draft.Bounds();
        double bestScore = double.PositiveInfinity; Point best = new(); bool clear = false; string bestSide = "right";
        foreach (var candidateSide in side is null ? new[] { "right", "left", "top", "bottom" } : new[] { side })
        foreach (double gap in new[] { 12d, 20, 32, 48, 72, 96 })
        {
            var origin = candidateSide switch { "left" => new Point(bounds.Left - gap - width, draft.Y - height / 2), "right" => new Point(bounds.Right + gap, draft.Y - height / 2), "top" => new Point(draft.X - width / 2, bounds.Top - gap - height), _ => new Point(draft.X - width / 2, bounds.Bottom + gap) };
            var box = new Rect(origin, new Size(width, height)); bool available = paper.Contains(box); double score = gap + (available ? 0 : 100000000 + OutsideArea(box, paper));
            foreach (var obstacle in obstacles) { var hit = Rect.Intersect(box, obstacle); if (hit.IsEmpty || hit.Width < .001 || hit.Height < .001) continue; score += 1000000 + hit.Width * hit.Height; available = false; }
            if (score < bestScore) { bestScore = score; best = origin; clear = available; bestSide = candidateSide; }
        }
        if (CrossReferences.IsPoint(draft)) draft.ReferenceDestinationInline = false;
        double y = best.Y;
        foreach (var item in visible)
        {
            var placement = DocumentModel.Copy(item.Target.Placement); placement.AnchorPort = ""; placement.Side = "custom"; placement.Horizontal = "left"; placement.Vertical = "top";
            placement.OffsetX = best.X - draft.X + (bestSide is "top" or "bottom" ? (width - item.Size.Width) / 2 : 0); placement.OffsetY = y - draft.Y;
            ComponentTexts.SetPlacement(draft, item.Target.Key, placement); y += item.Size.Height + 6;
        }
        return new(true, clear, clear ? "Tous les textes visibles sont alignés sans chevauchement détecté." : "Textes alignés ensemble ; l’espace disponible reste limité, vérifier le résultat.");
    }
    private static double OutsideArea(Rect box, Rect paper) { var intersection = Rect.Intersect(box, paper); return box.Width * box.Height - (intersection.IsEmpty ? 0 : intersection.Width * intersection.Height); }
    private static LabelPlacement Placement(Component c, int target) => ComponentTexts.List(c)[target].Placement;
    private static void Set(Component c, int target, LabelPlacement p) => ComponentTexts.SetPlacement(c, ComponentTexts.List(c)[target].Key, p);
    private static List<Ink> Labels(Project p, Component c, CrossReferences refs, int target)
    {
        if (target == 0 && CrossReferences.IsPoint(c) && c.ReferenceDestinationInline && c.TagPlacement.Visible) return ReferenceDisplay.Scene(p, c, refs).Where(i => i.Kind == "text").ToList();
        var ink = ComponentTexts.Ink(p, c, refs, ComponentTexts.List(c)[target].Key); return ink is null ? [] : [ink];
    }
    private static Rect Bounds(Ink ink)
    {
        var rect = new Rect(new(ink.X, ink.Y), Drawing.Measure(ink.Text, ink.Size));
        return ink.Rotation == 0 ? rect : new RotateTransform(ink.Rotation, ink.X, ink.Y).TransformBounds(rect);
    }
}
