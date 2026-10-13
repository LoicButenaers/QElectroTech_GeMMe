using System.Windows;

namespace GeMMeElec;

public sealed class ComponentText
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public LabelPlacement Placement { get; set; } = new() { Side = "custom", Horizontal = "left", Vertical = "top" };
}
public sealed record ComponentTextTarget(string Key, string Name, string Text, LabelPlacement Placement, double Size, string Color, bool ReadOnly = false);

public static class ComponentTexts
{
    public static List<ComponentTextTarget> List(Component c)
    {
        bool report = CrossReferences.IsPoint(c);
        List<ComponentTextTarget> result = [new("tag", report ? "Nom du report" : "Repère", report ? ReferenceDisplay.Name(c) : c.Tag, c.TagPlacement, report ? 14 : 16, report ? "#000000" : "#203348"), new("detail", report ? "Destination(s)" : "Caractéristiques / référence", report ? "" : Drawing.Detail(c), c.DetailPlacement, report ? 12 : 10, report ? "#008000" : "#64748B", report), new("description", "Description", c.Description, c.DescriptionPlacement, 12, "#64748B")];
        foreach (var port in c.Definition.Ports)
        {
            string key = "port:" + port.Id;
            var placement = c.TextOverrides.GetValueOrDefault(key)?.Placement ?? new LabelPlacement { Side = "custom", Horizontal = "left", Vertical = "top", AnchorPort = port.Id, OffsetX = 4, OffsetY = -13 };
            if (!c.ShowPortLabels || (report && !c.TextOverrides.ContainsKey(key))) { placement = DocumentModel.Copy(placement); placement.Visible = false; }
            result.Add(new(key, "Borne " + port.Id, c.PortLabels.GetValueOrDefault(port.Id, port.Id), placement, 9, "#64748B"));
        }
        for (int i = 0; i < c.Definition.Shapes.Count; i++)
        {
            var shape = c.Definition.Shapes[i]; if (shape.Kind != "text") continue;
            string key = "shape:" + i; var value = c.TextOverrides.GetValueOrDefault(key); var point = c.Transform(shape.X, shape.Y);
            var original = new Ink("text", point.X, point.Y, Text: shape.Text, Size: shape.A, Rotation: c.Rotation + shape.Rotation);
            var bounds = Drawing.TextBounds(original); var placement = value?.Placement ?? Free(c, bounds.TopLeft); if (value is null) placement.Rotation = original.Rotation % 360;
            result.Add(new(key, "Texte du symbole " + (i + 1), value?.Text ?? shape.Text, placement, shape.A, "#203348"));
        }
        result.AddRange(c.AdditionalTexts.Select((t, i) => new ComponentTextTarget("extra:" + t.Id, "Texte ajouté " + (i + 1), t.Text, t.Placement, 12, "#203348")));
        return result;
    }
    private static LabelPlacement Free(Component c, Point topLeft) => new() { Side = "custom", Horizontal = "left", Vertical = "top", OffsetX = topLeft.X - c.X, OffsetY = topLeft.Y - c.Y };
    public static ComponentTextTarget Get(Component c, string key) => List(c).SingleOrDefault(t => t.Key == key) ?? throw new ArgumentException("Texte introuvable : " + key);
    public static void Materialize(Component c)
    {
        foreach (var t in List(c).Where(t => t.Key.StartsWith("port:") || t.Key.StartsWith("shape:")))
            c.TextOverrides[t.Key] = new() { Id = t.Key, Text = t.Text, Placement = DocumentModel.Copy(t.Placement) };
        c.ShowPortLabels = true;
    }
    public static void SetPlacement(Component c, string key, LabelPlacement placement)
    {
        if (key == "tag") c.TagPlacement = placement;
        else if (key == "detail") c.DetailPlacement = placement;
        else if (key == "description") c.DescriptionPlacement = placement;
        else if (key.StartsWith("extra:")) c.AdditionalTexts.Single(t => "extra:" + t.Id == key).Placement = placement;
        else { var t = Get(c, key); c.TextOverrides[key] = new() { Id = t.Key, Text = t.Text, Placement = placement }; }
    }
    public static void SetText(Component c, string key, string text)
    {
        if (text.Length > 2000) throw new ArgumentException("Texte limité à 2000 caractères.");
        var target = Get(c, key); if (target.ReadOnly) throw new ArgumentException("La destination est calculée automatiquement.");
        if (key == "tag") { if (CrossReferences.IsPoint(c)) c.ReferenceName = text; else c.Tag = text; }
        else if (key == "detail") { if (c.Rating.Length > 0 || (c.Reference.Length == 0 && c.Range.Length == 0)) c.Rating = text; else if (c.Reference.Length > 0) c.Reference = text; else c.Range = text; }
        else if (key == "description") c.Description = text;
        else if (key.StartsWith("port:")) c.PortLabels[key[5..]] = text;
        else if (key.StartsWith("extra:")) c.AdditionalTexts.Single(t => "extra:" + t.Id == key).Text = text;
        else c.TextOverrides[key] = new() { Id = key, Text = text, Placement = target.Placement };
    }
    public static string Add(Component c, string text)
    {
        if (c.AdditionalTexts.Count >= 100 || text.Length > 2000) throw new ArgumentException("Maximum 100 textes ajoutés, de 2000 caractères chacun.");
        var added = new ComponentText { Text = text, Placement = new() { Side = "bottom", Horizontal = "center", Vertical = "top", OffsetY = c.AdditionalTexts.Count * 22 } }; c.AdditionalTexts.Add(added); return "extra:" + added.Id;
    }
    public static Ink? Ink(Project project, Component c, CrossReferences refs, string key)
    {
        if (CrossReferences.IsPoint(c) && key is "tag" or "detail")
        {
            var labels = ReferenceDisplay.Scene(project, c, refs).Where(i => i.Kind == "text").ToList();
            return key == "tag" ? c.TagPlacement.Visible ? labels.FirstOrDefault() : null : c.DetailPlacement.Visible ? labels.LastOrDefault() : null;
        }
        var t = Get(c, key); return Drawing.Label(c, t.Placement, t.Text, t.Size, t.Color);
    }
    public static IEnumerable<Ink> Scene(Project p, Component c, CrossReferences refs)
    {
        foreach (var t in List(c)) if (!(CrossReferences.IsPoint(c) && t.Key is "tag" or "detail") && Drawing.Label(c, t.Placement, t.Text, t.Size, t.Color) is Ink ink) yield return ink;
    }
    public static void Validate(Component c)
    {
        if (c.AdditionalTexts is null || c.TextOverrides is null || c.AdditionalTexts.Count > 100 || c.TextOverrides.Count > 4096) throw new ArgumentException("Collection de textes invalide.");
        foreach (var placement in new[] { c.TagPlacement, c.DetailPlacement, c.DescriptionPlacement }) if (placement.AnchorPort.Length > 0 && !c.Definition.Ports.Any(p => p.Id == placement.AnchorPort)) throw new ArgumentException("Ancrage sur une borne inconnue.");
        var ids = new HashSet<string>();
        foreach (var t in c.AdditionalTexts.Concat(c.TextOverrides.Values)) { if (t is null || t.Text is null || t.Text.Length > 2000 || t.Placement is null) throw new ArgumentException("Texte de composant invalide."); t.Placement.Validate(); if (t.Placement.AnchorPort.Length > 0 && !c.Definition.Ports.Any(p => p.Id == t.Placement.AnchorPort)) throw new ArgumentException("Ancrage sur une borne inconnue."); }
        foreach (var t in c.AdditionalTexts) if (string.IsNullOrWhiteSpace(t.Id) || t.Id.Length > 100 || !ids.Add(t.Id)) throw new ArgumentException("Identifiant de texte invalide ou dupliqué.");
        foreach (var key in c.TextOverrides.Keys) if (!(key.StartsWith("port:") && c.Definition.Ports.Any(p => "port:" + p.Id == key)) && !(key.StartsWith("shape:") && int.TryParse(key[6..], out var i) && i >= 0 && i < c.Definition.Shapes.Count && c.Definition.Shapes[i].Kind == "text")) throw new ArgumentException("Texte du symbole inconnu.");
    }
}
