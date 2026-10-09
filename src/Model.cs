using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace GeMMeElec;

public sealed record CatalogEntry(string Id, string Family, string Brand, string Range, string Symbol, string Source, string Reference = "", string Note = "Gamme : choisir et vérifier la référence exacte avant fabrication.")
{
    [JsonIgnore] public string Display => $"{Family} · {Brand} · {Range}";
}
public static class Catalog
{
    public static readonly List<CatalogEntry> Entries = Load();
    private static List<CatalogEntry> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("catalog.json")!;
        var items = JsonSerializer.Deserialize<List<CatalogEntry>>(stream, ProjectStore.Json)!;
        if (items.Select(x => x.Id).Distinct().Count() != items.Count) throw new InvalidDataException("Identifiants de catalogue dupliqués.");
        foreach (var e in items) if (!Symbols.All.ContainsKey(e.Symbol)) throw new InvalidDataException($"Symbole inconnu : {e.Symbol}");
        return items;
    }
    public static IEnumerable<CatalogEntry> Search(string query = "", string family = "", string brand = "") => Entries.Where(e =>
        (family == "" || e.Family == family) && (brand == "" || e.Brand == brand) &&
        $"{e.Family} {e.Brand} {e.Range} {e.Reference} {e.Id}".Contains(query, StringComparison.OrdinalIgnoreCase));
}
public sealed class Project
{
    public string Format { get; set; } = "GeMMeElec";
    public int Version { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Nouveau projet";
    public string Author { get; set; } = "";
    public string Revision { get; set; } = "A";
    public List<Sheet> Sheets { get; set; } = [new()];
}
public sealed class Sheet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Schéma de puissance";
    public double Width { get; set; } = 1400;
    public double Height { get; set; } = 990;
    public List<Component> Components { get; set; } = [];
    public List<Wire> Wires { get; set; } = [];
    public List<Annotation> Notes { get; set; } = [];
    [JsonIgnore] public string Display => Title;
}
public sealed class Component
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Symbol { get; set; } = "breaker1";
    public string CatalogId { get; set; } = "";
    public string Tag { get; set; } = "Q1";
    public string Description { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Range { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Rating { get; set; } = "";
    public string Source { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public int Rotation { get; set; }
    public Dictionary<string, string> PortLabels { get; set; } = [];
    [JsonIgnore] public SymbolDefinition Definition => Symbols.All[Symbol];
    public Point Transform(double x, double y)
    {
        var angle = Rotation * Math.PI / 180;
        return new(X + x * Math.Cos(angle) - y * Math.Sin(angle), Y + x * Math.Sin(angle) + y * Math.Cos(angle));
    }
    public Point PortPoint(string portId)
    {
        var port = Definition.Ports.FirstOrDefault(p => p.Id == portId) ?? throw new InvalidDataException($"Borne inconnue : {Tag}/{portId}");
        return Transform(port.X, port.Y);
    }
    public Rect Bounds()
    {
        var d = Definition;
        var points = new[] { Transform(-d.Width / 2, -d.Height / 2), Transform(d.Width / 2, -d.Height / 2), Transform(d.Width / 2, d.Height / 2), Transform(-d.Width / 2, d.Height / 2) };
        return new Rect(new Point(points.Min(p => p.X), points.Min(p => p.Y)), new Point(points.Max(p => p.X), points.Max(p => p.Y)));
    }
}
public sealed record Endpoint(string ComponentId, string PortId);
public sealed record Position(double X, double Y)
{
    [JsonIgnore] public Point Point => new(X, Y);
}
public sealed class Wire
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public Endpoint From { get; set; } = new("", "");
    public Endpoint To { get; set; } = new("", "");
    public string Number { get; set; } = "";
    public string Color { get; set; } = "#243B53";
    public List<Position> Waypoints { get; set; } = [];
    public List<Point> Route(Sheet sheet)
    {
        var a = sheet.Components.Single(c => c.Id == From.ComponentId).PortPoint(From.PortId);
        var b = sheet.Components.Single(c => c.Id == To.ComponentId).PortPoint(To.PortId);
        List<Point> route = [a];
        foreach (var target in Waypoints.Select(p => p.Point).Append(b))
        {
            var last = route[^1];
            if (Math.Abs(last.X - target.X) > .01 && Math.Abs(last.Y - target.Y) > .01)
            {
                if (Waypoints.Count == 0)
                {
                    var mid = Math.Round((last.Y + target.Y) / 20) * 10;
                    route.Add(new Point(last.X, mid)); route.Add(new Point(target.X, mid));
                }
                else route.Add(new Point(target.X, last.Y));
            }
            route.Add(target);
        }
        return route;
    }
}
public sealed class Annotation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double X { get; set; }
    public double Y { get; set; }
    public string Text { get; set; } = "Texte";
}
public sealed record Finding(string Level, string SheetId, string ComponentId, string Message);
public static class ProjectStore
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    public static string Serialize(Project project) => JsonSerializer.Serialize(project, Json);
    public static Project Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != "GeMMeElec" || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !root.TryGetProperty("sheets", out var sheets) || sheets.ValueKind != JsonValueKind.Array) throw new InvalidDataException("En-tête de projet absent ou invalide.");
        var project = JsonSerializer.Deserialize<Project>(json, Json) ?? throw new InvalidDataException("Projet vide.");
        ValidateStructure(project);
        return project;
    }
    public static void ValidateStructure(Project p)
    {
        if (p.Format != "GeMMeElec" || p.Version != 1) throw new InvalidDataException("Format ou version de projet non pris en charge.");
        void Text(string? value) { if (value is null || value.Length > 2000) throw new InvalidDataException("Champ texte absent ou trop long."); }
        Text(p.Title); Text(p.Author); Text(p.Revision);
        if (p.Sheets is null || p.Sheets.Count is < 1 or > 100) throw new InvalidDataException("Le projet doit contenir entre 1 et 100 folios.");
        var ids = new HashSet<string>();
        void Id(string id) { if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new InvalidDataException("Identifiant absent ou dupliqué."); }
        foreach (var s in p.Sheets)
        {
            if (s is null) throw new InvalidDataException("Folio vide.");
            Text(s.Title);
            Id(s.Id);
            if (!double.IsFinite(s.Width) || !double.IsFinite(s.Height) || s.Width is < 200 or > 10000 || s.Height is < 200 or > 10000) throw new InvalidDataException("Dimensions de folio invalides.");
            if (s.Components is null || s.Wires is null || s.Notes is null || s.Components.Count > 10000 || s.Wires.Count > 20000 || s.Notes.Count > 5000) throw new InvalidDataException("Contenu du folio invalide ou trop volumineux.");
            foreach (var c in s.Components)
            {
                if (c is null) throw new InvalidDataException("Composant vide.");
                Text(c.Tag); Text(c.Description); Text(c.Brand); Text(c.Range); Text(c.Reference); Text(c.Rating); Text(c.Source); Text(c.CatalogId);
                Id(c.Id);
                if (c.Symbol is null || !Symbols.All.ContainsKey(c.Symbol)) throw new InvalidDataException("Symbole non pris en charge.");
                if (!double.IsFinite(c.X) || !double.IsFinite(c.Y) || c.X < 0 || c.X > s.Width || c.Y < 0 || c.Y > s.Height || c.Rotation % 90 != 0) throw new InvalidDataException("Position ou rotation de symbole invalide.");
                c.PortLabels ??= [];
                foreach (var label in c.PortLabels) { Text(label.Value); if (!c.Definition.Ports.Any(port => port.Id == label.Key)) throw new InvalidDataException("Libellé de borne inconnue."); }
            }
            foreach (var w in s.Wires)
            {
                if (w is null) throw new InvalidDataException("Conducteur vide.");
                Text(w.Number);
                if (w.Color is null || !System.Text.RegularExpressions.Regex.IsMatch(w.Color, "^#[0-9A-Fa-f]{6}$")) throw new InvalidDataException("Couleur de conducteur : utiliser #RRGGBB.");
                Id(w.Id);
                if (w.From is null || w.To is null || w.From == w.To) throw new InvalidDataException("Connexion invalide.");
                foreach (var end in new[] { w.From, w.To })
                {
                    var c = s.Components.FirstOrDefault(x => x.Id == end.ComponentId) ?? throw new InvalidDataException("Connexion vers un symbole absent.");
                    if (!c.Definition.Ports.Any(x => x.Id == end.PortId)) throw new InvalidDataException("Connexion vers une borne absente.");
                }
                if (w.Waypoints is null || w.Waypoints.Count > 100 || w.Waypoints.Any(x => x is null || !double.IsFinite(x.X) || !double.IsFinite(x.Y) || x.X < 0 || x.X > s.Width || x.Y < 0 || x.Y > s.Height)) throw new InvalidDataException("Parcours de conducteur invalide.");
            }
            foreach (var n in s.Notes) { if (n is null) throw new InvalidDataException("Annotation vide."); Id(n.Id); if (!double.IsFinite(n.X) || !double.IsFinite(n.Y) || n.X < 0 || n.X > s.Width || n.Y < 0 || n.Y > s.Height || n.Text is null || n.Text.Length > 2000) throw new InvalidDataException("Annotation invalide."); }
        }
    }
    public static Project Load(string path)
    {
        if (new FileInfo(path).Length > 20_000_000) throw new InvalidDataException("Le fichier dépasse 20 Mo.");
        return Deserialize(File.ReadAllText(path));
    }
    public static void Save(Project p, string path)
    {
        ValidateStructure(p);
        if (!path.EndsWith(".gemelec", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Utiliser l’extension .gemelec.");
        AtomicWrite(path, Serialize(p));
    }
    public static void AtomicWrite(string path, string data)
    {
        var full = Path.GetFullPath(path);
        var temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temp, data, new System.Text.UTF8Encoding(false)); File.Move(temp, full, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
public sealed class Editor
{
    public Project Project { get; private set; } = new();
    public string CurrentSheetId { get; private set; }
    public string? FilePath { get; private set; }
    public bool InteractionInProgress { get; set; }
    public event Action? Changed;
    private readonly List<string> undo = [];
    private readonly List<string> redo = [];
    private string saved;
    public Editor() { CurrentSheetId = Project.Sheets[0].Id; saved = ProjectStore.Serialize(Project); }
    public Sheet Sheet => Project.Sheets.FirstOrDefault(s => s.Id == CurrentSheetId) ?? Project.Sheets[0];
    public bool Dirty => saved != ProjectStore.Serialize(Project);
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public void Notify() => Changed?.Invoke();
    public void SelectSheet(string id)
    {
        if (!Project.Sheets.Any(s => s.Id == id)) throw new ArgumentException("Folio introuvable.");
        CurrentSheetId = id; Notify();
    }
    public void New(string title)
    {
        var created = new Project { Title = title }; ProjectStore.ValidateStructure(created);
        Project = created; CurrentSheetId = Project.Sheets[0].Id; FilePath = null; undo.Clear(); redo.Clear(); saved = ProjectStore.Serialize(Project); Notify();
    }
    public void Open(string path)
    {
        var loaded = ProjectStore.Load(path);
        Project = loaded; CurrentSheetId = Project.Sheets[0].Id; FilePath = Path.GetFullPath(path); undo.Clear(); redo.Clear(); saved = ProjectStore.Serialize(Project); Notify();
    }
    public void Save(string path) { ProjectStore.Save(Project, path); FilePath = Path.GetFullPath(path); saved = ProjectStore.Serialize(Project); Notify(); }
    public void Change(Action action)
    {
        if (InteractionInProgress) throw new InvalidOperationException("Terminer le déplacement en cours.");
        var before = ProjectStore.Serialize(Project);
        try { action(); ProjectStore.ValidateStructure(Project); Commit(before); }
        catch { Project = ProjectStore.Deserialize(before); Notify(); throw; }
    }
    public void Commit(string before)
    {
        ProjectStore.ValidateStructure(Project);
        if (before != ProjectStore.Serialize(Project)) { undo.Add(before); if (undo.Count > 100) undo.RemoveAt(0); redo.Clear(); }
        Notify();
    }
    public void Undo()
    {
        if (!CanUndo || InteractionInProgress) return;
        redo.Add(ProjectStore.Serialize(Project)); Project = ProjectStore.Deserialize(undo[^1]); undo.RemoveAt(undo.Count - 1); RepairSelection(); Notify();
    }
    public void Redo()
    {
        if (!CanRedo || InteractionInProgress) return;
        undo.Add(ProjectStore.Serialize(Project)); Project = ProjectStore.Deserialize(redo[^1]); redo.RemoveAt(redo.Count - 1); RepairSelection(); Notify();
    }
    private void RepairSelection() { if (!Project.Sheets.Any(s => s.Id == CurrentSheetId)) CurrentSheetId = Project.Sheets[0].Id; }
    public Sheet AddSheet(string title)
    {
        var sheet = new Sheet { Title = title }; Change(() => Project.Sheets.Add(sheet)); SelectSheet(sheet.Id); return sheet;
    }
    public string NextTag(string prefix)
    {
        var tags = Project.Sheets.SelectMany(s => s.Components).Select(c => c.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int n = 1; while (tags.Contains(prefix + n)) n++; return prefix + n;
    }
    public Component AddComponent(string symbol, double x, double y, string? catalogId = null)
    {
        if (!Symbols.All.TryGetValue(symbol, out var d)) throw new ArgumentException("Symbole inconnu.");
        CatalogEntry? entry = catalogId is null ? null : Catalog.Entries.SingleOrDefault(c => c.Id == catalogId) ?? throw new ArgumentException("Article de bibliothèque introuvable.");
        if (entry is not null && entry.Symbol != symbol) throw new ArgumentException("Le symbole ne correspond pas à la gamme.");
        var c = new Component { Symbol = symbol, X = Snap(x), Y = Snap(y), Tag = NextTag(d.Prefix), Description = d.Name,
            CatalogId = entry?.Id ?? "", Brand = entry?.Brand ?? "", Range = entry?.Range ?? "", Reference = entry?.Reference ?? "", Source = entry?.Source ?? "" };
        Change(() => Sheet.Components.Add(c)); return c;
    }
    public Wire Connect(Endpoint from, Endpoint to, IEnumerable<Position>? points = null, string number = "")
    {
        if (from == to) throw new ArgumentException("Choisir deux bornes distinctes.");
        if (Sheet.Wires.Any(w => (w.From == from && w.To == to) || (w.To == from && w.From == to))) throw new ArgumentException("Ces bornes sont déjà reliées.");
        var w = new Wire { From = from, To = to, Waypoints = points?.ToList() ?? [], Number = number.Length > 0 ? number : $"W{NextWireNumber()}" };
        Change(() => Sheet.Wires.Add(w)); return w;
    }
    private int NextWireNumber() { var used = Project.Sheets.SelectMany(s => s.Wires).Select(w => w.Number).ToHashSet(); int i = 1; while (used.Contains($"W{i}")) i++; return i; }
    public void Delete(IEnumerable<string> selected)
    {
        var ids = selected.ToHashSet();
        Change(() => { Sheet.Wires.RemoveAll(w => ids.Contains(w.Id) || ids.Contains(w.From.ComponentId) || ids.Contains(w.To.ComponentId)); Sheet.Components.RemoveAll(c => ids.Contains(c.Id)); Sheet.Notes.RemoveAll(n => ids.Contains(n.Id)); });
    }
    public static double Snap(double value) => Math.Round(value / 10) * 10;
    public List<Finding> Validate()
    {
        List<Finding> list = [];
        var tags = Project.Sheets.SelectMany(s => s.Components).Where(c => !string.IsNullOrWhiteSpace(c.Tag)).GroupBy(c => c.Tag, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var s in Project.Sheets) foreach (var c in s.Components)
        {
            if (string.IsNullOrWhiteSpace(c.Tag)) list.Add(new("Attention", s.Id, c.Id, "Repère manquant"));
            else if (tags.Contains(c.Tag)) list.Add(new("Attention", s.Id, c.Id, $"Repère utilisé plusieurs fois : {c.Tag}"));
            if (c.Brand.Length > 0 && c.Reference.Length == 0) list.Add(new("Article", s.Id, c.Id, $"{c.Tag} : référence exacte à renseigner ({c.Brand} {c.Range})"));
            var free = c.Definition.Ports.Count(p => !s.Wires.Any(w => w.From == new Endpoint(c.Id, p.Id) || w.To == new Endpoint(c.Id, p.Id)));
            if (free > 0) list.Add(new("Connexion", s.Id, c.Id, $"{c.Tag} : {free} borne(s) non raccordée(s)"));
            var bounds = c.Bounds(); if (bounds.Left < 30 || bounds.Top < 40 || bounds.Right > s.Width - 30 || bounds.Bottom > s.Height - 90) list.Add(new("Mise en page", s.Id, c.Id, $"{c.Tag} : symbole hors de la zone de dessin"));
        }
        return list;
    }
}
