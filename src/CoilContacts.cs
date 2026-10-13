using System.IO;

namespace GeMMeElec;

// An apparatus association is a navigation/reference relationship, never an
// electrical graph edge. IDs stay stable across folio moves and renaming.
public sealed class CoilContacts
{
    private readonly Project project;
    private readonly Dictionary<string, ReferencePoint> points;
    private readonly Dictionary<string, List<ReferencePoint>> contacts;
    public static string Role(Component c) => c.RelayFunction.Length > 0 ? c.RelayFunction : c.Symbol switch {
        "coil" or "timer_on" or "timer_off" or "relay_latch" => "coil",
        "contact_no" or "timer_contact" => "no", "contact_nc" => "nc", "changeover" => "changeover",
        "contactor" or "contactor2" or "contactor4" => "power", _ => ""
    };
    public static bool IsCoil(Component c) => Role(c) == "coil";
    public static bool IsContact(Component c) => Role(c) is "no" or "nc" or "changeover" or "power";
    public static string FunctionName(Component c) => Role(c) switch { "coil" => "Bobine", "no" => "NO", "nc" => "NF", "changeover" => "Inverseur", "power" => "Puissance", _ => "" };
    public CoilContacts(Project p)
    {
        project = p; points = p.Sheets.SelectMany(s => s.Components.Select(c => new ReferencePoint(s, c))).ToDictionary(x => x.Component.Id);
        contacts = points.Values.Where(x => x.Component.CoilId.Length > 0).GroupBy(x => x.Component.CoilId).ToDictionary(g => g.Key, g => g.OrderBy(x => p.Sheets.IndexOf(x.Sheet)).ThenBy(x => x.Component.Y).ThenBy(x => x.Component.X).ToList());
    }
    public IReadOnlyList<ReferencePoint> Contacts(string coilId) => contacts.GetValueOrDefault(coilId) ?? [];
    public ReferencePoint? Coil(Component contact) => points.GetValueOrDefault(contact.CoilId);
    public IEnumerable<ReferencePoint> Coils => points.Values.Where(x => IsCoil(x.Component));
    public string Label(Component c)
    {
        if (IsCoil(c)) return string.Join("\n", Contacts(c.Id).Select(x => $"{FunctionName(x.Component)} · {Terminals(x.Component)} · {SheetZones.Address(project, x.Sheet, x.Component)}"));
        return Coil(c) is ReferencePoint coil ? "Bobine : " + SheetZones.Address(project, coil.Sheet, coil.Component) : "";
    }
    public static string Terminals(Component c) => string.Join("–", c.Definition.Ports.Select(p => c.PortLabels.GetValueOrDefault(p.Id, p.Id)));
    public static void Validate(Project p)
    {
        var index = p.Sheets.SelectMany(s => s.Components).ToDictionary(c => c.Id);
        foreach (var c in index.Values)
        {
            if (c.CoilId is null || c.CoilId.Length > 100 || c.RelayFunction is not ("" or "coil" or "no" or "nc" or "changeover" or "power") || c.CoilReferencePlacement is null) throw new InvalidDataException("Fonction de relais invalide.");
            c.CoilReferencePlacement.Validate();
            if (Role(c).Length > 0 && c.Definition.Ports.Count < 2) throw new InvalidDataException("Une fonction de relais doit posséder au moins deux bornes explicites.");
            if (c.CoilReferencePlacement.AnchorPort.Length > 0 && !c.Definition.Ports.Any(p => p.Id == c.CoilReferencePlacement.AnchorPort)) throw new InvalidDataException("Ancrage de référence sur une borne inconnue.");
            if (c.CoilId.Length > 0 && (!IsContact(c) || !index.TryGetValue(c.CoilId, out var coil) || !IsCoil(coil) || coil.Id == c.Id || c.Tag != coil.Tag)) throw new InvalidDataException("Association bobine/contact invalide ou repère non partagé.");
        }
    }
    public static IEnumerable<Finding> Findings(Project p)
    {
        foreach (var s in p.Sheets) foreach (var c in s.Components.Where(c => IsContact(c) && c.CoilId.Length == 0)) yield return new("Référence", s.Id, c.Id, $"{c.Tag} : fonction {FunctionName(c)} sans bobine associée");
        foreach (var group in p.Sheets.SelectMany(s => s.Components.Where(c => c.CoilId.Length > 0).Select(c => new ReferencePoint(s, c))).GroupBy(x => (x.Component.CoilId, Role(x.Component), Ports: Terminals(x.Component).ToUpperInvariant())).Where(g => g.Count() > 1))
            foreach (var x in group) yield return new("Référence", x.Sheet.Id, x.Component.Id, $"{x.Component.Tag} : bornes {Terminals(x.Component)} représentées plusieurs fois pour la même fonction ; vérifier les libellés de bornes");
    }
}

public sealed partial class Editor
{
    public void AssociateContact(string contactId, string coilId, string? historyKey = null)
    {
        var contact = Project.Sheets.SelectMany(s => s.Components).SingleOrDefault(c => c.Id == contactId) ?? throw new ArgumentException("Contact introuvable.");
        if (!CoilContacts.IsContact(contact)) throw new ArgumentException("Choisir une fonction contact de relais/contacteur.");
        var coil = coilId.Length == 0 ? null : Project.Sheets.SelectMany(s => s.Components).SingleOrDefault(c => c.Id == coilId && CoilContacts.IsCoil(c)) ?? throw new ArgumentException("Bobine introuvable.");
        Change(() => { contact.CoilId = coilId; if (coil is not null) contact.Tag = coil.Tag; }, historyKey);
    }
    internal void SetApparatusTag(Component c, string value)
    {
        string id = c.CoilId.Length > 0 ? c.CoilId : c.Id;
        foreach (var part in Project.Sheets.SelectMany(s => s.Components).Where(x => x.Id == id || x.CoilId == id)) part.Tag = value;
    }
    internal void SetRelayFunction(Component c, string role)
    {
        if (role is not ("" or "coil" or "no" or "nc" or "changeover" or "power")) throw new ArgumentException("Fonction de relais inconnue.");
        if (role.Length > 0 && c.Definition.Ports.Count < 2) throw new ArgumentException("Définir au moins deux bornes dans le symbole avant de déclarer cette fonction.");
        bool wasCoil = CoilContacts.IsCoil(c); c.RelayFunction = role;
        if (wasCoil && !CoilContacts.IsCoil(c)) foreach (var child in Project.Sheets.SelectMany(s => s.Components).Where(x => x.CoilId == c.Id)) child.CoilId = "";
        if (!CoilContacts.IsContact(c)) c.CoilId = "";
    }
    public void DeleteSheet(string id)
    {
        if (Project.Sheets.Count == 1) throw new InvalidOperationException("Conserver au moins un folio.");
        var sheet = Project.Sheets.Single(s => s.Id == id); var removed = sheet.Components.Select(c => c.Id).ToHashSet();
        Change(() => { foreach (var c in Project.Sheets.SelectMany(s => s.Components).Where(c => removed.Contains(c.CoilId))) c.CoilId = ""; Project.Sheets.Remove(sheet); });
        if (CurrentSheetId == id) SelectSheet(Project.Sheets[0].Id);
    }
}
