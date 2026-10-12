using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeMMeElec;

// A published illustration is not a native electrical definition. Only entries
// with an explicitly reviewed terminal map can be placed and connected.
public sealed record CatalogEntry(string Id, string Family, string Brand, string Range, string Symbol, string Source, string Reference, string Note)
{
    public string Library { get; init; } = "manufacturer";
    public string Asset { get; init; } = "";
    public string AssetSha256 { get; init; } = "";
    public string Document { get; init; } = "";
    public string DocumentSha256 { get; init; } = "";
    public string DrawingSource { get; init; } = "";
    public string Edition { get; init; } = "";
    public string CheckedOn { get; init; } = "";
    public string Method { get; init; } = "";
    public double Width { get; init; }
    public double Height { get; init; }
    public List<Port> Ports { get; init; } = [];
    public bool CanPlace { get; init; }
    [JsonIgnore] public string Display => $"{Brand} · {Reference} · {Range}";
    [JsonIgnore] public string Status => CanPlace ? "Dessin officiel · bornes relevées sur la source" : "Aperçu officiel · non raccordable";
}

public static class Catalog
{
    public static readonly IReadOnlyList<CatalogEntry> Entries = Load();
    private static IReadOnlyList<CatalogEntry> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("catalog.json")!;
        var items = JsonSerializer.Deserialize<List<CatalogEntry>>(stream, ProjectStore.Json)!;
        if (items.Select(e => e.Id).Distinct().Count() != items.Count) throw new InvalidDataException("Identifiants de catalogue dupliqués.");
        foreach (var e in items)
        {
            if (e.Library is not ("eplan" or "manufacturer") || string.IsNullOrWhiteSpace(e.Reference) || string.IsNullOrWhiteSpace(e.Edition) || string.IsNullOrWhiteSpace(e.Method) || !DateOnly.TryParseExact(e.CheckedOn, "yyyy-MM-dd", out _) || !Https(e.Source) || !Https(e.DrawingSource)) throw new InvalidDataException("Provenance incomplète : " + e.Id);
            ReadChecked(e.Asset, e.AssetSha256);
            if (e.Document.Length > 0) ReadChecked(e.Document, e.DocumentSha256);
            if (e.CanPlace ? e.Library != "manufacturer" || !e.Symbol.StartsWith("official-") || e.Ports.Count == 0 || e.Width <= 0 || e.Height <= 0 : e.Symbol.Length != 0 || e.Ports.Count != 0) throw new InvalidDataException("Statut électrique incohérent : " + e.Id);
        }
        return items.AsReadOnly();
    }
    private static bool Https(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https";
    public static byte[] ReadChecked(string name, string expectedHash)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("official." + name) ?? throw new InvalidDataException("Source absente : " + name);
        using var memory = new MemoryStream(); stream.CopyTo(memory); var bytes = memory.ToArray();
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Source altérée : " + name);
        return bytes;
    }
    public static IEnumerable<CatalogEntry> Search(string query = "", string family = "", string brand = "", string library = "") => Entries.Where(e =>
        (family == "" || e.Family == family) && (brand == "" || e.Brand == brand) && (library == "" || e.Library == library) &&
        Symbols.SearchKey($"{e.Family} {e.Brand} {e.Range} {e.Reference} {e.Id}").Contains(Symbols.SearchKey(query)));
    public static IEnumerable<SymbolDefinition> Definitions() => Entries.Where(e => e.CanPlace).Select(e => new SymbolDefinition(e.Symbol, e.Display, "K", e.Width, e.Height, e.Ports.ToList(),
        [new("image", -e.Width / 2, -e.Height / 2, e.Width, e.Height, Convert.ToBase64String(ReadChecked(e.Asset, e.AssetSha256)))]) { Category = "Dessins officiels fabricants", PrintedTerminals = true });
}
