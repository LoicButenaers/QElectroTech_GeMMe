using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GeMMeElec;

// Definitions belong to the document: opening or undoing a project never changes
// the built-in library or the symbols of another open/test document.
public static class CustomSymbols
{
    public static SymbolDefinition Resolve(Project p, string id) => p.CustomSymbols.TryGetValue(id, out var d) ? d : Symbols.All.TryGetValue(id, out d) ? d : throw new InvalidDataException("Symbole absent : " + id);
    public static void Validate(Project p)
    {
        if (p.CustomSymbols is null || p.CustomSymbols.Count > 2000) throw new InvalidDataException("Bibliothèque personnalisée invalide (maximum 2 000 symboles).");
        foreach (var pair in p.CustomSymbols) { Validate(pair.Value); if (pair.Key != pair.Value.Id || Symbols.All.ContainsKey(pair.Key)) throw new InvalidDataException("Identifiant de symbole réservé ou incohérent."); }
    }
    public static void Validate(SymbolDefinition d)
    {
        if (d is null || string.IsNullOrWhiteSpace(d.Id) || d.Id.Length > 100 || string.IsNullOrWhiteSpace(d.Name) || d.Name.Length > 200 || d.Prefix is null || d.Prefix.Length > 20 || d.Category is null || d.Category.Length > 100 || !double.IsFinite(d.Width) || !double.IsFinite(d.Height) || d.Width is < 1 or > 10000 || d.Height is < 1 or > 10000 || d.Ports is null || d.Shapes is null || d.Ports.Count > 256 || d.Shapes.Count > 20000) throw new InvalidDataException("Dimensions ou contenu du symbole invalides.");
        if (d.Ports.Any(t => t is null || string.IsNullOrWhiteSpace(t.Id) || t.Id.Length > 100 || !Finite(t.X, t.Y)) || d.Ports.Select(t => t.Id).Distinct().Count() != d.Ports.Count) throw new InvalidDataException("Bornes : identifiants uniques et coordonnées finies requis.");
        if (d.InternalConnections is null || d.InternalConnections.Count > 256 || d.InternalConnections.Any(c => c is null || c.From == c.To || !d.Ports.Any(p => p.Id == c.From) || !d.Ports.Any(p => p.Id == c.To))) throw new InvalidDataException("Continuité interne : choisir deux bornes distinctes existantes.");
        foreach (var s in d.Shapes)
        {
            if (s is null || s.Kind is not ("line" or "rect" or "circle" or "dot" or "text" or "image") || !Finite(s.X, s.Y, s.A, s.B, s.Rotation) || s.Text is null || s.Text.Length > (s.Kind == "image" ? 1_000_000 : 2000) || (s.Kind is "rect" or "image" && (s.A < 0 || s.B < 0)) || (s.Kind is "circle" or "dot" or "text") && (s.A < 0 || s.A > 10000)) throw new InvalidDataException("Primitive de symbole invalide.");
            if(s.Kind=="image") ImageAssets.Validate(s.Text);
        }
    }
    private static bool Finite(params double[] values) => values.All(v => double.IsFinite(v) && Math.Abs(v) <= 20000);
    public static SymbolDefinition Copy(SymbolDefinition d, string name) => d with { Id = "custom-" + Guid.NewGuid().ToString("N"), Name = name, Category = "Mes symboles", Ports = d.Ports.ToList(), Shapes = d.Shapes.ToList(), InternalConnections = d.InternalConnections.ToList() };
    public static void Export(SymbolDefinition d, string path) { Validate(d); if (!path.EndsWith(".gemsymbol", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Utiliser .gemsymbol."); ProjectStore.AtomicWrite(path, JsonSerializer.Serialize(d, ProjectStore.Json)); }
    public static SymbolDefinition Load(string path)
    {
        if (new FileInfo(path).Length > 4_000_000) throw new InvalidDataException("Symbole trop volumineux.");
        var d = JsonSerializer.Deserialize<SymbolDefinition>(File.ReadAllText(path), ProjectStore.Json) ?? throw new InvalidDataException("Symbole absent."); Validate(d); return d;
    }
}

public sealed partial class Editor
{
    public SymbolDefinition SaveSymbolCopy(SymbolDefinition draft, string? replaceComponent = null)
    {
        CustomSymbols.Validate(draft); var copy = CustomSymbols.Copy(draft, draft.Name);
        Change(() => {
            Project.CustomSymbols.Add(copy.Id, copy);
            if (replaceComponent is not null)
            {
                var c = Sheet.Components.Single(c => c.Id == replaceComponent);
                if (CrossReferences.IsPoint(c) || c.Symbol == "junction") throw new ArgumentException("Les nœuds et reports sont des symboles de connexion spécialisés.");
                var used = Sheet.Wires.SelectMany(w => new[] { w.From, w.To }).Where(e => e.ComponentId == c.Id).Select(e => e.PortId);
                if (used.Any(id => !copy.Ports.Any(p => p.Id == id))) throw new ArgumentException("Conserver les IDs des bornes raccordées avant de remplacer ce composant.");
                c.RelayFunction = CoilContacts.Role(c); c.Symbol = copy.Id; c.ResolvedDefinition = copy; c.CatalogId = ""; c.TextOverrides.Clear();
                c.PortLabels = c.PortLabels.Where(x => copy.Ports.Any(p => p.Id == x.Key)).ToDictionary(x => x.Key, x => x.Value);
            }
        }); return copy;
    }
    public void ImportProject(Project project)
    {
        ProjectStore.ValidateStructure(project); Project = project; CurrentSheetId = project.Sheets[0].Id; FilePath = null; undo.Clear(); redo.Clear(); saved = ""; Notify();
    }
}
