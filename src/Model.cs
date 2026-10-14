using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace GeMMeElec;

public sealed class Project
{
    public const int CurrentVersion = 9;
    public const int MaxSheets = 200;
    public const int MaxFileBytes = 64_000_000;
    public Dictionary<string, SymbolDefinition> CustomSymbols { get; set; } = [];
    public string Format { get; set; } = "GeMMeElec";
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Nouveau projet";
    public string Author { get; set; } = "";
    public string Revision { get; set; } = "A";
    public string Customer { get; set; } = "";
    public string DocumentNumber { get; set; } = "";
    public string Date { get; set; } = "";
    public Dictionary<string, string> CustomFields { get; set; } = [];
    public List<TitleBlockTemplate> TitleBlocks { get; set; } = TitleBlocksDefaults.Create();
    public Dictionary<string, string> Images { get; set; } = [];
    public BomOptions BomOptions { get; set; } = new();
    public NumberingRules NumberingRules { get; set; } = new();
    public List<TerminalStrip> TerminalStrips { get; set; } = [];
    public List<CabinetLayout> Cabinets { get; set; } = [];
    public ProjectPresentation Presentation { get; set; } = new();
    public PdfOptions PdfOptions { get; set; } = new();
    public List<CableDefinition> Cables { get; set; } = [];
    public int ZoneColumns { get; set; } = 20;
    public int ZoneRows { get; set; } = 10;
    public List<Sheet> Sheets { get; set; } = [new()];
}
public sealed class Sheet
{
    public string Kind {get;set;}="drawing";
    public List<SheetGraphic> Graphics {get;set;}=[];
    public List<CableMarker> CableMarkers { get; set; } = [];
    public List<TerminalMarker> TerminalMarkers { get; set; } = [];
    public string TerminalPlanStyle {get;set;}="table";
    public string TerminalPlanId { get; set; } = "";
    public int TerminalPlanPage { get; set; }
    public string CabinetPlanId { get; set; } = "";
    public int CabinetPlanPage { get; set; }
    public bool UseProjectZones { get; set; } = true;
    public int ZoneColumns { get; set; } = 20;
    public int ZoneRows { get; set; } = 10;
    public BomPage? BomPage { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Schéma de puissance";
    public string Number { get; set; } = "";
    public string Author { get; set; } = "";
    public string CheckedBy { get; set; } = "";
    public string ApprovedBy { get; set; } = "";
    public string Date { get; set; } = "";
    public string Revision { get; set; } = "";
    public string Location { get; set; } = "";
    public string Function { get; set; } = "";
    public string TitleBlockId { get; set; } = "simple";
    public Dictionary<string, string> CustomFields { get; set; } = [];
    public double Width { get; set; } = 1400;
    public double Height { get; set; } = 990;
    public List<Component> Components { get; set; } = [];
    public List<Wire> Wires { get; set; } = [];
    public List<Annotation> Notes { get; set; } = [];
    [JsonIgnore] public string Display => (Kind=="cover"?"PG · ":"") + (Number.Length == 0 ? Title : Number + " · " + Title);
}
public sealed class Component
{
    public bool TerminalNumberOnly {get;set;}
    public bool TagLocked { get; set; }
    public string CoilId { get; set; } = "";
    public string RelayFunction { get; set; } = "";
    public LabelPlacement CoilReferencePlacement { get; set; } = new() { Side = "right", Horizontal = "left", Vertical = "top", Gap = 20, OffsetY = 35, FontSize = 11, Color = "#087344" };
    public List<ComponentText> AdditionalTexts { get; set; } = [];
    public Dictionary<string, ComponentText> TextOverrides { get; set; } = [];
    public bool AutoJunction { get; set; }
    public bool ShowPortLabels { get; set; } = true;
    [JsonIgnore] public SymbolDefinition? ResolvedDefinition { get; set; }
    public bool ReferenceIndependentName { get; set; }
    public string ReferenceName { get; set; } = "";
    public bool ReferenceDestinationInline { get; set; } = true;
    public string LinkName { get; set; } = "";
    public string LinkMode { get; set; } = "pair";
    public int LinkOrder { get; set; }
    public bool LinkSource { get; set; }
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
    public string ImageId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public int Rotation { get; set; }
    public Dictionary<string, string> PortLabels { get; set; } = [];
    public LabelPlacement TagPlacement { get; set; } = new() { OffsetY = -12 };
    public LabelPlacement DetailPlacement { get; set; } = new() { OffsetY = 12 };
    public LabelPlacement DescriptionPlacement { get; set; } = new() { Side = "bottom", Horizontal = "center", Vertical = "top", Visible = false };
    [JsonIgnore] public SymbolDefinition Definition => ResolvedDefinition ?? Symbols.All[Symbol];
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
public sealed class LabelPlacement
{
    public string AnchorPort { get; set; } = "";
    public double FontSize { get; set; }
    public string Color { get; set; } = "";
    public double Rotation { get; set; }
    public double EffectiveSize(double fallback) => FontSize > 0 ? FontSize : fallback;
    public string EffectiveColor(string fallback) => Color.Length > 0 ? Color : fallback;
    public string Side { get; set; } = "right";
    public string Horizontal { get; set; } = "left";
    public string Vertical { get; set; } = "center";
    public double Gap { get; set; } = 12;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public bool Visible { get; set; } = true;
    public Point Origin(Component c, double width, double height)
    {
        var r = c.Bounds();
        var anchor = Side switch
        {
            "top" => new Point(c.X, r.Top - Gap), "bottom" => new Point(c.X, r.Bottom + Gap),
            "left" => new Point(r.Left - Gap, c.Y), "right" => new Point(r.Right + Gap, c.Y), _ => AnchorPort.Length > 0 ? c.PortPoint(AnchorPort) : new Point(c.X, c.Y)
        };
        return new(anchor.X + OffsetX - (Horizontal == "center" ? width / 2 : Horizontal == "right" ? width : 0),
            anchor.Y + OffsetY - (Vertical == "center" ? height / 2 : Vertical == "bottom" ? height : 0));
    }
    public void Validate()
    {
        if (AnchorPort is null || AnchorPort.Length > 100) throw new InvalidDataException("Ancrage de texte invalide.");
        if (!double.IsFinite(FontSize) || (FontSize != 0 && (FontSize < 4 || FontSize > 200)) || !double.IsFinite(Rotation) || Math.Abs(Rotation) > 360 || Color is null || (Color.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(Color, "^#[0-9a-fA-F]{6}$"))) throw new InvalidDataException("Style de texte invalide : taille 4 à 200 (0 = automatique), couleur #RRGGBB et rotation de −360° à 360°.");
        if (Side is not ("top" or "bottom" or "left" or "right" or "custom") || Horizontal is not ("left" or "center" or "right") || Vertical is not ("top" or "center" or "bottom") || !double.IsFinite(Gap) || Gap is < 0 or > 1000 || !double.IsFinite(OffsetX) || !double.IsFinite(OffsetY) || Math.Abs(OffsetX) > 10000 || Math.Abs(OffsetY) > 10000) throw new InvalidDataException("Position de texte invalide.");
    }
}
public sealed record Endpoint(string ComponentId, string PortId);
public sealed record Position(double X, double Y)
{
    [JsonIgnore] public Point Point => new(X, Y);
}
public sealed class Wire
{
    public string CableId { get; set; } = "";
    public string CableCore { get; set; } = "";
    public double? SectionMm2 { get; set; }
    public string RoutingMode { get; set; } = "manual";
    public string RoutingWarning { get; set; } = "";
    public WireNumberPlacement NumberPlacement { get; set; } = new();
    public bool NumberIndependent { get; set; }
    public double Thickness { get; set; } = 1.6;
    public string LineStyle { get; set; } = "solid";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public Endpoint From { get; set; } = new("", "");
    public Endpoint To { get; set; } = new("", "");
    public string Number { get; set; } = "";
    public string Color { get; set; } = "#243B53";
    public List<Position> Waypoints { get; set; } = [];
    public List<Point> Route(Sheet sheet, IReadOnlyDictionary<string,Component>? components = null)
    {
        var a = (components is null ? sheet.Components.Single(c => c.Id == From.ComponentId) : components[From.ComponentId]).PortPoint(From.PortId);
        var b = (components is null ? sheet.Components.Single(c => c.Id == To.ComponentId) : components[To.ComponentId]).PortPoint(To.PortId);
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
        return WireRouting.Simplify(route);
    }
}
public static class WireRouting
{
    public static List<Point> Simplify(IEnumerable<Point> points)
    {
        List<Point> result = [];
        foreach (var p in points)
        {
            if (result.Count > 0 && (result[^1] - p).Length < .001) continue;
            while (result.Count >= 2)
            {
                var a = result[^2]; var b = result[^1];
                bool sameAxis = (Math.Abs(a.X - b.X) < .001 && Math.Abs(b.X - p.X) < .001) || (Math.Abs(a.Y - b.Y) < .001 && Math.Abs(b.Y - p.Y) < .001);
                if (!sameAxis || Vector.Multiply(b - a, p - b) < 0) break;
                result.RemoveAt(result.Count - 1);
            }
            result.Add(p);
        }
        // Coincident terminals still represent a valid connection.
        if (result.Count == 1) result.Add(result[0]);
        return result;
    }
    public static List<Position> ShiftSegment(IReadOnlyList<Point> route, int index, Point target)
    {
        if (index < 1 || index >= route.Count) throw new ArgumentException("Segment introuvable.");
        var moved = route.ToList(); var a = route[index - 1]; var b = route[index];
        if (Math.Abs(a.Y - b.Y) < .001) { var y = Editor.Snap(target.Y); moved[index - 1] = new(a.X, y); moved[index] = new(b.X, y); }
        else { var x = Editor.Snap(target.X); moved[index - 1] = new(x, a.Y); moved[index] = new(x, b.Y); }
        // Endpoint anchors are never moved; shifting an end segment creates a dogleg.
        if (index == 1) moved.Insert(0, route[0]);
        if (index == route.Count - 1) moved.Add(route[^1]);
        var clean = Simplify(moved);
        return clean.Skip(1).Take(Math.Max(0, clean.Count - 2)).Select(p => new Position(p.X, p.Y)).ToList();
    }
}
public sealed class Annotation
{
    public double FontSize {get;set;}=16;
    public string Color {get;set;}="#203348";
    public double Rotation {get;set;}
    public string Align {get;set;}="left";
    public bool ResolveFields {get;set;}
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double X { get; set; }
    public double Y { get; set; }
    public string Text { get; set; } = "Texte";
}
public sealed record Finding(string Level, string SheetId, string ComponentId, string Message);
public static class ProjectStore
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    internal static readonly JsonSerializerOptions ProjectJson = new(Json) { WriteIndented = false };
    public static string Serialize(Project project) => JsonSerializer.Serialize(project, ProjectJson);
    public static Project Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != "GeMMeElec" || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !root.TryGetProperty("sheets", out var sheets) || sheets.ValueKind != JsonValueKind.Array) throw new InvalidDataException("En-tête de projet absent ou invalide.");
        var project = JsonSerializer.Deserialize<Project>(json, Json) ?? throw new InvalidDataException("Projet vide.");
        if (project.Version is 1 or 2)
        {
            var first = project.Sheets?.FirstOrDefault(s => s is not null && s.BomPage is null);
            project.ZoneColumns = first?.ZoneColumns ?? 20; project.ZoneRows = first?.ZoneRows ?? 10;
            foreach (var sheet in project.Sheets ?? []) if (sheet is not null) sheet.UseProjectZones = sheet.ZoneColumns == project.ZoneColumns && sheet.ZoneRows == project.ZoneRows;
            project.Version = Project.CurrentVersion;
            project.PdfOptions.Contents=false;
        }
        else if (project.Version is 3 or 4 or 5 or 6) {project.Version = Project.CurrentVersion;project.PdfOptions.Contents=false;}
        else if (project.Version is 7 or 8) project.Version=Project.CurrentVersion;
        ValidateStructure(project);
        return project;
    }
    public static void ValidateStructure(Project p)
    {
        if (p.Format != "GeMMeElec" || p.Version != Project.CurrentVersion) throw new InvalidDataException("Format ou version de projet non pris en charge.");
        void Text(string? value) { if (value is null || value.Length > 2000) throw new InvalidDataException("Champ texte absent ou trop long."); }
        Text(p.Title); Text(p.Author); Text(p.Revision);
        if(p.NumberingRules is null)throw new InvalidDataException("Règles de numérotation absentes.");p.NumberingRules.Validate(p);
        if(p.Presentation is null)throw new InvalidDataException("Présentation absente.");p.Presentation.Validate();
        if(p.PdfOptions is null)throw new InvalidDataException("Options PDF absentes.");
        if (p.ZoneColumns is < 1 or > 40 || p.ZoneRows is < 1 or > 26) throw new InvalidDataException("Repérage du projet : 1 à 40 colonnes et 1 à 26 lignes.");
        CustomSymbols.Validate(p);
        DocumentModel.Validate(p);
        if (p.Sheets is null || p.Sheets.Count is < 1 or > Project.MaxSheets) throw new InvalidDataException($"Le projet doit contenir entre 1 et {Project.MaxSheets} folios.");
        var ids = new HashSet<string>();
        void Id(string id) { if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new InvalidDataException("Identifiant absent ou dupliqué."); }
        foreach (var s in p.Sheets)
        {
            if (s is null) throw new InvalidDataException("Folio vide.");
            Text(s.Title);
            Text(s.TerminalPlanId);if(s.TerminalPlanPage<0||s.TerminalPlanPage>249)throw new InvalidDataException("Page de bornier invalide.");
            Text(s.CabinetPlanId);if(s.CabinetPlanId.Length>0&&s.TerminalPlanId.Length>0)throw new InvalidDataException("Type de plan ambigu.");
            if(s.CabinetPlanPage<0||s.CabinetPlanPage>199)throw new InvalidDataException("Page d’implantation invalide.");
            if (s.ZoneColumns is < 1 or > 40 || s.ZoneRows is < 1 or > 26) throw new InvalidDataException("Repérage : 1 à 40 colonnes et 1 à 26 lignes.");
            Id(s.Id);
            if (!double.IsFinite(s.Width) || !double.IsFinite(s.Height) || s.Width is < 200 or > 10000 || s.Height is < 200 or > 10000) throw new InvalidDataException("Dimensions de folio invalides.");
            if (s.Components is null || s.Wires is null || s.Notes is null || s.Components.Count > 10000 || s.Wires.Count > 20000 || s.Notes.Count > 5000) throw new InvalidDataException("Contenu du folio invalide ou trop volumineux.");
            if((s.TerminalPlanId.Length>0||s.CabinetPlanId.Length>0)&&(s.Components.Count>0||s.Wires.Count>0||s.Notes.Count>0||s.CableMarkers?.Count>0||s.TerminalMarkers?.Count>0))throw new InvalidDataException("Modifier les plans générés depuis leur gestionnaire.");
            BomFolios.Validate(p, s);FreeGraphics.Validate(s,p);foreach(var graphic in s.Graphics)Id(graphic.Id);
            foreach (var c in s.Components)
            {
                if (c is null) throw new InvalidDataException("Composant vide.");
                Text(c.Tag); Text(c.Description); Text(c.Brand); Text(c.Range); Text(c.Reference); Text(c.Rating); Text(c.Source); Text(c.CatalogId);
                Text(c.LinkName); Text(c.ReferenceName);
                if (c.LinkMode is not ("pair" or "star") || c.LinkOrder is < 0 or > 99999 || c.LinkName.Length > 80) throw new InvalidDataException("Paramètres de renvoi invalides.");
                Id(c.Id);
                if (c.Symbol is null) throw new InvalidDataException("Symbole non pris en charge.");
                c.ResolvedDefinition = CustomSymbols.Resolve(p, c.Symbol);
                if (c.AutoJunction && c.Symbol != "junction") throw new InvalidDataException("Nœud de câblage invalide.");
                if (!double.IsFinite(c.X) || !double.IsFinite(c.Y) || c.X < 0 || c.X > s.Width || c.Y < 0 || c.Y > s.Height || c.Rotation % 90 != 0) throw new InvalidDataException("Position ou rotation de symbole invalide.");
                c.PortLabels ??= [];
                if (c.TagPlacement is null || c.DetailPlacement is null || c.DescriptionPlacement is null) throw new InvalidDataException("Position de texte absente.");
                c.TagPlacement.Validate(); c.DetailPlacement.Validate(); c.DescriptionPlacement.Validate(); ComponentTexts.Validate(c);
                foreach (var label in c.PortLabels) { Text(label.Value); if (!c.Definition.Ports.Any(port => port.Id == label.Key)) throw new InvalidDataException("Libellé de borne inconnue."); }
            }
            var componentIndex=s.Components.ToDictionary(c=>c.Id);
            foreach (var w in s.Wires)
            {
                if (w is null) throw new InvalidDataException("Conducteur vide.");
                Text(w.Number);
                Text(w.RoutingWarning);if(w.RoutingMode is not ("manual" or "smart"))throw new InvalidDataException("Mode de routage invalide.");
                if (w.NumberPlacement is null) throw new InvalidDataException("Position du numéro de fil absente."); w.NumberPlacement.Validate();
                if (!double.IsFinite(w.Thickness) || w.Thickness is < .25 or > 10 || w.LineStyle is not ("solid" or "dashed" or "dotted" or "dashdot")) throw new InvalidDataException("Trait du conducteur : épaisseur 0,25 à 10 ; continu, tirets, pointillés ou mixte.");
                if (w.Color is null || !System.Text.RegularExpressions.Regex.IsMatch(w.Color, "^#[0-9A-Fa-f]{6}$")) throw new InvalidDataException("Couleur de conducteur : utiliser #RRGGBB.");
                Id(w.Id);
                if (w.From is null || w.To is null || w.From == w.To) throw new InvalidDataException("Connexion invalide.");
                foreach (var end in new[] { w.From, w.To })
                {
                    var c = componentIndex.GetValueOrDefault(end.ComponentId) ?? throw new InvalidDataException("Connexion vers un symbole absent.");
                    if (!c.Definition.Ports.Any(x => x.Id == end.PortId)) throw new InvalidDataException("Connexion vers une borne absente.");
                }
                if (w.Waypoints is null || w.Waypoints.Count > 100 || w.Waypoints.Any(x => x is null || !double.IsFinite(x.X) || !double.IsFinite(x.Y) || x.X < 0 || x.X > s.Width || x.Y < 0 || x.Y > s.Height)) throw new InvalidDataException("Parcours de conducteur invalide.");
            }
            var pairs = new HashSet<string>();
            foreach (var w in s.Wires) { var ends = new[] { w.From.ComponentId + ":" + w.From.PortId, w.To.ComponentId + ":" + w.To.PortId }.Order(StringComparer.Ordinal); if (!pairs.Add(string.Join("|", ends))) throw new InvalidDataException("Ces bornes sont déjà reliées."); }
            foreach (var n in s.Notes) { if (n is null) throw new InvalidDataException("Annotation vide."); Id(n.Id);FreeGraphics.ValidateNote(n); if (!double.IsFinite(n.X) || !double.IsFinite(n.Y) || n.X < 0 || n.X > s.Width || n.Y < 0 || n.Y > s.Height || n.Text is null || n.Text.Length > 2000) throw new InvalidDataException("Annotation invalide."); }
        }
        CoilContacts.Validate(p);
        TerminalStrips.Validate(p);
        Cabinets.Validate(p);
        CableDefinitions.Validate(p);TerminalGraphics.Validate(p);
    }
    public static Project Load(string path)
    {
        if (new FileInfo(path).Length > Project.MaxFileBytes) throw new InvalidDataException("Le fichier dépasse 64 Mo.");
        return Deserialize(File.ReadAllText(path));
    }
    public static void Save(Project p, string path)
    {
        ValidateStructure(p);
        if (!path.EndsWith(".gemelec", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Utiliser l’extension .gemelec.");
        var json = Serialize(p);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > Project.MaxFileBytes) throw new InvalidDataException("Le projet dépasse 64 Mo ; réduire les images avant d’enregistrer.");
        AtomicWrite(path, json);
    }
    public static void AtomicWrite(string path, string data)
    {
        var full = Path.GetFullPath(path);
        var temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temp, data, new System.Text.UTF8Encoding(false)); File.Move(temp, full, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
public sealed partial class Editor
{
    public string CurrentWireColor { get; set; } = "#243B53";
    public Project Project { get; private set; } = new();
    public string CurrentSheetId { get; private set; }
    public string? FilePath { get; private set; }
    public bool InteractionInProgress { get; set; }
    public event Action? Changed;
    private readonly List<HistorySnapshot> undo = [];
    private readonly List<HistorySnapshot> redo = [];
    private string saved;
    private string currentState;
    public Editor() { CurrentSheetId = Project.Sheets[0].Id; currentState = saved = ProjectStore.Serialize(Project); }
    public Sheet Sheet => Project.Sheets.FirstOrDefault(s => s.Id == CurrentSheetId) ?? Project.Sheets[0];
    public bool Dirty => saved != currentState;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    internal string ExportSnapshot => currentState;
    public void Notify() => Notify(ProjectStore.Serialize(Project));
    private void Notify(string state) { sheetStateCache = null; currentState = state; Changed?.Invoke(); }
    public void SelectSheet(string id)
    {
        if (!Project.Sheets.Any(s => s.Id == id)) throw new ArgumentException("Folio introuvable.");
        EndPropertyEdit(); CurrentSheetId = id; Changed?.Invoke();
    }
    public void New(string title)
    {
        var created = new Project { Title = title }; ProjectStore.ValidateStructure(created);
        Project = created; CurrentSheetId = Project.Sheets[0].Id; FilePath = null; undo.Clear(); redo.Clear(); EndPropertyEdit(); saved = ProjectStore.Serialize(Project); Notify(saved);
    }
    public void Open(string path)
    {
        var loaded = ProjectStore.Load(path);
        Project = loaded; CurrentSheetId = Project.Sheets[0].Id; FilePath = Path.GetFullPath(path); undo.Clear(); redo.Clear(); EndPropertyEdit(); saved = ProjectStore.Serialize(Project); Notify(saved);
    }
    public void Save(string path) { EndPropertyEdit(); ProjectStore.Save(Project, path); FilePath = Path.GetFullPath(path); saved = ProjectStore.Serialize(Project); Notify(saved); }
    private string? propertyHistoryKey;
    private DateTime propertyHistoryTime;
    public void EndPropertyEdit() => propertyHistoryKey = null;
    public void Change(Action action, string? historyKey = null)
    {
        if (InteractionInProgress) throw new InvalidOperationException("Terminer le déplacement en cours.");
        var before = historyKey is not null && historyKey == propertyHistoryKey ? currentState : ProjectStore.Serialize(Project);
        try { action(); Commit(before, historyKey); }
        catch { Project = ProjectStore.Deserialize(before); Notify(before); throw; }
    }
    public void Commit(string before, string? historyKey = null)
    {
        ProjectStore.ValidateStructure(Project);
        RefreshSmartRoutes();
        if (Junctions.Cleanup(Project)) ProjectStore.ValidateStructure(Project);
        var after = ProjectStore.Serialize(Project);
        if (before != after) {
            if (historyKey is null || propertyHistoryKey != historyKey || undo.Count == 0 || DateTime.UtcNow - propertyHistoryTime > TimeSpan.FromSeconds(2)) undo.Add(HistorySnapshot.Capture(before));
            redo.Clear(); TrimHistory(); propertyHistoryKey = historyKey; propertyHistoryTime = DateTime.UtcNow;
        } else if (historyKey is null) EndPropertyEdit();
        Notify(after);
    }
    public void CancelPreview(string before) { Project = ProjectStore.Deserialize(before); InteractionInProgress = false; RepairSelection(); Notify(before); }
    public void Reconnect(string wireId, bool from, Endpoint endpoint)
    {
        var wire = Sheet.Wires.Single(w => w.Id == wireId);
        var number = wire.NumberIndependent ? wire.Number : new ConductorNetworks(Project).ExistingNumber(endpoint, from ? wire.To : wire.From, wire.Id) ?? wire.Number;
        Change(() => { if (from) wire.From = endpoint; else wire.To = endpoint; SynchronizeWireNumbers(wire, number); });
    }
    public void Undo()
    {
        EndPropertyEdit(); if (!CanUndo || InteractionInProgress) return;
        if (undo[^1].SheetIds is not null) { RestoreSheets(undo, redo); return; }
        var state = undo[^1].Restore(); var restored = ProjectStore.Deserialize(state); var outgoing = HistorySnapshot.Capture(ProjectStore.Serialize(Project));
        redo.Add(outgoing); Project = restored; undo.RemoveAt(undo.Count - 1); TrimHistory(); RepairSelection(); Notify(state);
    }
    public void Redo()
    {
        EndPropertyEdit(); if (!CanRedo || InteractionInProgress) return;
        if (redo[^1].SheetIds is not null) { RestoreSheets(redo, undo); return; }
        var state = redo[^1].Restore(); var restored = ProjectStore.Deserialize(state); var outgoing = HistorySnapshot.Capture(ProjectStore.Serialize(Project));
        undo.Add(outgoing); Project = restored; redo.RemoveAt(redo.Count - 1); TrimHistory(); RepairSelection(); Notify(state);
    }
    private void TrimHistory()
    {
        // Large embedded images must not multiply indefinitely through undo snapshots.
        while(undo.Count>100)undo.RemoveAt(0);while(redo.Count>100)redo.RemoveAt(0);
        long bytes=undo.Concat(redo).Sum(s=>s.MemoryBytes);
        while(bytes>64_000_000 && undo.Count+redo.Count>1){var list=undo.Count>1?undo:redo.Count>1?redo:undo.Count>0?undo:redo;bytes-=list[0].MemoryBytes;list.RemoveAt(0);}
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
        CatalogEntry? entry = catalogId is null ? Catalog.Entries.SingleOrDefault(c => c.CanPlace && c.Symbol == symbol) : Catalog.Entries.SingleOrDefault(c => c.Id == catalogId) ?? throw new ArgumentException("Article de bibliothèque introuvable.");
        if (entry is { CanPlace: false }) throw new ArgumentException("Aperçu officiel uniquement : définition électrique et bornes non disponibles.");
        var d = CustomSymbols.Resolve(Project, symbol);
        if (entry is not null && entry.Symbol != symbol) throw new ArgumentException("Le symbole ne correspond pas à la gamme.");
        var c = new Component { Symbol = symbol, ResolvedDefinition = d, X = Snap(x), Y = Snap(y), Tag = NextTag(d.Prefix), Description = d.Name,
            CatalogId = entry?.Id ?? "", Brand = entry?.Brand ?? "", Range = entry?.Range ?? "", Reference = entry?.Reference ?? "", Source = entry?.Source ?? "", ShowPortLabels = !d.PrintedTerminals };
        c.RelayFunction = CoilContacts.Role(c);
        if (CrossReferences.IsPoint(c)) { c.TagPlacement = new() { Side = "right", Horizontal = "left", Vertical = "center", Gap = 4 }; c.DetailPlacement = new() { Side = "right", Horizontal = "left", Vertical = "center", Gap = 4 }; }
        Change(() => Sheet.Components.Add(c)); return c;
    }
    public Wire Connect(Endpoint from, Endpoint to, IEnumerable<Position>? points = null, string number = "", string? color = null, bool numberIndependent = false, double thickness = 1.6, string lineStyle = "solid")
    {
        if (from == to) throw new ArgumentException("Choisir deux bornes distinctes.");
        if (Sheet.Wires.Any(w => (w.From == from && w.To == to) || (w.To == from && w.From == to))) throw new ArgumentException("Ces bornes sont déjà reliées.");
        var w = new Wire { From = from, To = to, Waypoints = points?.ToList() ?? [], Number = number.Length > 0 ? number : numberIndependent ? $"W{NextWireNumber()}" : NumberForConnection(from, to), Color = color ?? CurrentWireColor, NumberIndependent = numberIndependent, Thickness = thickness, LineStyle = lineStyle };
        Change(() => { Sheet.Wires.Add(w); SynchronizeWireNumbers(w, w.Number); }); return w;
    }
    private int NextWireNumber() { var used = Project.Sheets.SelectMany(s => s.Wires).Select(w => w.Number).ToHashSet(); int i = 1; while (used.Contains($"W{i}")) i++; return i; }
    public void Delete(IEnumerable<string> selected)
    {
        var ids = selected.ToHashSet();
        Change(() => { RemoveTerminalAssignments(ids);foreach (var c in Project.Sheets.SelectMany(s => s.Components).Where(c => ids.Contains(c.CoilId))) c.CoilId = ""; Sheet.Wires.RemoveAll(w => ids.Contains(w.Id) || ids.Contains(w.From.ComponentId) || ids.Contains(w.To.ComponentId)); Sheet.Components.RemoveAll(c => ids.Contains(c.Id)); Sheet.Notes.RemoveAll(n => ids.Contains(n.Id));Sheet.Graphics.RemoveAll(g=>ids.Contains(g.Id)); });
    }
    public static double Snap(double value) => Math.Round(value / 10) * 10;
    public List<Finding> Validate()
    {
        List<Finding> list = [];
        var tags = Project.Sheets.SelectMany(s => s.Components).Where(c => !string.IsNullOrWhiteSpace(c.Tag)).GroupBy(c => c.Tag, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(c => c.CoilId.Length > 0 ? c.CoilId : c.Id).Distinct().Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var s in Project.Sheets) foreach (var c in s.Components)
        {
            if (string.IsNullOrWhiteSpace(c.Tag)) list.Add(new("Attention", s.Id, c.Id, "Repère manquant"));
            else if (tags.Contains(c.Tag)) list.Add(new("Attention", s.Id, c.Id, $"Repère utilisé plusieurs fois : {c.Tag}"));
            if (c.Brand.Length > 0 && c.Reference.Length == 0) list.Add(new("Article", s.Id, c.Id, $"{c.Tag} : référence exacte à renseigner ({c.Brand} {c.Range})"));
            var free = c.Definition.Ports.Count(p => !s.Wires.Any(w => w.From == new Endpoint(c.Id, p.Id) || w.To == new Endpoint(c.Id, p.Id)));
            if (free > 0) list.Add(new("Connexion", s.Id, c.Id, $"{c.Tag} : {free} borne(s) non raccordée(s)"));
            var bounds = c.Bounds(); if (bounds.Left < 30 || bounds.Top < 40 || bounds.Right > s.Width - 30 || bounds.Bottom > DocumentModel.DrawingBottom(Project,s)) list.Add(new("Mise en page", s.Id, c.Id, $"{c.Tag} : symbole hors de la zone de dessin"));
        }
        list.AddRange(CrossReferences.Findings(Project));
        list.AddRange(CoilContacts.Findings(Project));
        foreach (var s in Project.Sheets) foreach (var w in s.Wires) { if (string.IsNullOrWhiteSpace(w.Number)) list.Add(new("Conducteur", s.Id, w.Id, "Numéro de conducteur manquant"));if(w.RoutingWarning.Length>0)list.Add(new("Routage",s.Id,w.Id,w.RoutingWarning)); }
        var networks = new ConductorNetworks(Project); var checkedWires = new HashSet<string>();
        foreach (var s in Project.Sheets) foreach (var wire in s.Wires) if (checkedWires.Add(wire.Id)) { var connected = networks.Connected(wire.From).ToList(); foreach (var w in connected) checkedWires.Add(w.Id); var numbers = connected.Where(w => !w.NumberIndependent && w.Number.Length > 0).Select(w => w.Number).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); if (numbers.Count > 1) list.Add(new("Conducteur", s.Id, wire.Id, "Numéros communs différents sur une même continuité : " + string.Join(", ", numbers))); }
        return list;
    }
}
