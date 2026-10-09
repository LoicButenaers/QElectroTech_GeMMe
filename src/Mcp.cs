using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GeMMeElec;

public sealed record ToolSpec(string Name, string Description, JsonObject Properties, string[] Required, bool ReadOnly = false);
public static class McpTools
{
    private static JsonObject S(string description) => new() { ["type"] = "string", ["description"] = description };
    private static JsonObject N(string description) => new() { ["type"] = "number", ["description"] = description };
    private static JsonObject B(string description) => new() { ["type"] = "boolean", ["description"] = description };
    public static readonly ToolSpec[] Specs =
    [
        new("project_get", "Lire le projet ouvert, le folio actif, les IDs et les bornes en coordonnées absolues.", new(), [], true),
        new("library_search", "Rechercher les gammes fabricants et les symboles génériques. Les gammes ne sont pas des références de commande.", new() { ["query"] = S("Recherche libre"), ["family"] = S("Famille exacte, facultatif"), ["brand"] = S("Fabricant exact, facultatif") }, [], true),
        new("project_new", "Créer un projet. Refuse d’abandonner les modifications non enregistrées sans discardUnsaved.", new() { ["title"] = S("Nom du projet"), ["discardUnsaved"] = B("Autoriser la perte des modifications courantes") }, ["title"]),
        new("project_open", "Ouvrir un fichier .gemelec existant.", new() { ["path"] = S("Chemin absolu .gemelec"), ["discardUnsaved"] = B("Autoriser la perte des modifications courantes") }, ["path"]),
        new("project_save", "Enregistrer le projet .gemelec. overwrite nécessaire pour écraser un autre fichier existant.", new() { ["path"] = S("Chemin absolu .gemelec"), ["overwrite"] = B("Autoriser l’écrasement d’un fichier existant") }, ["path"]),
        new("project_update", "Modifier les informations du cartouche.", new() { ["title"] = S("Nom"), ["author"] = S("Auteur"), ["revision"] = S("Révision") }, []),
        new("page_add", "Ajouter et sélectionner un folio A3 paysage.", new() { ["title"] = S("Titre du folio") }, ["title"]),
        new("page_select", "Sélectionner le folio utilisé par les commandes suivantes.", new() { ["id"] = S("ID du folio") }, ["id"]),
        new("page_update", "Renommer le folio actif.", new() { ["title"] = S("Titre") }, ["title"]),
        new("component_add", "Placer un symbole sur le folio actif. Coordonnées logiques 0..1400, 0..990 ; grille 10. Fournir symbol ou catalogId.", new() { ["symbol"] = S("ID de symbole"), ["catalogId"] = S("ID de gamme obtenu par library_search"), ["x"] = N("Centre X"), ["y"] = N("Centre Y") }, ["x", "y"]),
        new("component_update", "Modifier un composant du folio actif. Les fils suivent sa position et sa rotation.", new() { ["id"] = S("ID du composant"), ["x"] = N("Centre X"), ["y"] = N("Centre Y"), ["rotation"] = N("Rotation multiple de 90 degrés"), ["tag"] = S("Repère"), ["description"] = S("Description"), ["brand"] = S("Fabricant"), ["range"] = S("Gamme"), ["reference"] = S("Référence exacte"), ["rating"] = S("Caractéristiques vérifiées"), ["portLabels"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = new JsonObject { ["type"] = "string" }, ["description"] = "Libellés des bornes, clés = IDs fixes des ports" } }, ["id"]),
        new("component_delete", "Supprimer un composant et ses connexions ; annulable.", new() { ["id"] = S("ID") }, ["id"]),
        new("wire_add", "Relier deux bornes du folio actif. Les croisements visuels ne créent pas de jonction.", new() { ["fromComponent"] = S("ID source"), ["fromPort"] = S("ID borne source"), ["toComponent"] = S("ID destination"), ["toPort"] = S("ID borne destination"), ["number"] = S("Numéro facultatif"), ["waypoints"] = new JsonObject { ["type"] = "array", ["maxItems"] = 100, ["items"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["x"] = N("X"), ["y"] = N("Y") }, ["required"] = new JsonArray("x", "y"), ["additionalProperties"] = false } } }, ["fromComponent", "fromPort", "toComponent", "toPort"]),
        new("wire_delete", "Supprimer un conducteur ; annulable.", new() { ["id"] = S("ID") }, ["id"]),
        new("note_add", "Ajouter une annotation au folio.", new() { ["text"] = S("Texte"), ["x"] = N("X"), ["y"] = N("Y") }, ["text", "x", "y"]),
        new("project_validate", "Vérifier structure, repères, bornes libres et références à compléter. Ne calcule pas la conformité électrique.", new(), [], true),
        new("export_svg", "Exporter le folio actif en SVG vectoriel.", new() { ["path"] = S("Chemin absolu .svg"), ["overwrite"] = B("Autoriser remplacement") }, ["path"]),
        new("export_pdf", "Exporter tous les folios en PDF A3 paysage.", new() { ["path"] = S("Chemin absolu .pdf"), ["overwrite"] = B("Autoriser remplacement") }, ["path"]),
        new("export_bom", "Exporter la nomenclature en CSV UTF-8, séparateur point-virgule.", new() { ["path"] = S("Chemin absolu .csv"), ["overwrite"] = B("Autoriser remplacement") }, ["path"]),
        new("history_undo", "Annuler la dernière modification, qu’elle vienne de l’interface ou du MCP.", new(), []),
        new("history_redo", "Rétablir la dernière modification annulée.", new(), []),
        new("view_fit", "Ajuster le folio à la fenêtre.", new(), [])
    ];
    public static object List() => new { tools = Specs.Select(t => new { name = t.Name, description = t.Description, inputSchema = new { type = "object", properties = t.Properties, required = t.Required, additionalProperties = false }, annotations = new { readOnlyHint = t.ReadOnly, destructiveHint = !t.ReadOnly, openWorldHint = false } }) };
    public static void Check(string name, JsonElement args)
    {
        var spec = Specs.FirstOrDefault(x => x.Name == name) ?? throw new ArgumentException("Outil inconnu.");
        if (args.ValueKind != JsonValueKind.Object) throw new ArgumentException("arguments doit être un objet.");
        foreach (var required in spec.Required) if (!args.TryGetProperty(required, out _)) throw new ArgumentException("Argument requis : " + required);
        foreach (var p in args.EnumerateObject())
        {
            if (!spec.Properties.TryGetPropertyValue(p.Name, out var schema)) throw new ArgumentException("Argument inconnu : " + p.Name);
            var type = schema!["type"]!.GetValue<string>();
            bool valid = type switch { "string" => p.Value.ValueKind == JsonValueKind.String && p.Value.GetString()!.Length <= 2000, "number" => p.Value.ValueKind == JsonValueKind.Number && double.IsFinite(p.Value.GetDouble()), "boolean" => p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False, "object" => p.Value.ValueKind == JsonValueKind.Object, "array" => p.Value.ValueKind == JsonValueKind.Array, _ => false };
            if (!valid) throw new ArgumentException("Type ou taille invalide : " + p.Name);
        }
    }
    public static object Execute(MainWindow window, string name, JsonElement a)
    {
        Check(name, a); var e = window.Editor;
        if (!window.IsEnabled) throw new InvalidOperationException("Fermer la boîte de dialogue ouverte dans GeMMeElec avant la commande MCP.");
        if (e.InteractionInProgress) throw new InvalidOperationException("Un déplacement est en cours dans l’interface. Réessayer après relâchement.");
        string Str(string k, string fallback = "") => a.TryGetProperty(k, out var v) ? v.GetString()! : fallback;
        double Num(string k) => a.GetProperty(k).GetDouble();
        bool Flag(string k) => a.TryGetProperty(k, out var v) && v.GetBoolean();
        void ReplaceCheck() { if (e.Dirty && !Flag("discardUnsaved")) throw new InvalidOperationException("Modifications non enregistrées. Enregistrer d’abord ou fournir discardUnsaved=true avec accord de l’utilisateur."); window.Canvas.SetMode("select"); }
        string PathFor(string extension, bool writing)
        {
            var path = Str("path"); if (!Path.IsPathFullyQualified(path) || !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Chemin absolu " + extension + " requis.");
            path = Path.GetFullPath(path);
            if (writing && File.Exists(path) && !Flag("overwrite") && !(name == "project_save" && string.Equals(path, e.FilePath, StringComparison.OrdinalIgnoreCase))) throw new IOException("Le fichier existe déjà. Fournir overwrite=true pour le remplacer."); return path;
        }
        object Snapshot() => new { project = e.Project, currentSheetId = e.Sheet.Id, filePath = e.FilePath, dirty = e.Dirty, canUndo = e.CanUndo, canRedo = e.CanRedo, ports = e.Project.Sheets.SelectMany(s => s.Components.Select(c => new { sheetId = s.Id, componentId = c.Id, c.Tag, terminals = c.Definition.Ports.Select(p => new { id = p.Id, label = c.PortLabels.GetValueOrDefault(p.Id, p.Id), x = c.PortPoint(p.Id).X, y = c.PortPoint(p.Id).Y }) })) };
        object result;
        switch (name)
        {
            case "project_get": result = Snapshot(); break;
            case "library_search": result = new { equipment = Catalog.Search(Str("query"), Str("family"), Str("brand")).ToArray(), symbols = Symbols.All.Values.Where(s => s.Name.Contains(Str("query"), StringComparison.OrdinalIgnoreCase) || s.Id.Contains(Str("query"), StringComparison.OrdinalIgnoreCase)).Select(s => new { s.Id, s.Name, s.Prefix, s.Width, s.Height, s.Ports }) }; break;
            case "project_new": ReplaceCheck(); e.New(Str("title")); result = Snapshot(); break;
            case "project_open": var loadPath = PathFor(".gemelec", false); ReplaceCheck(); e.Open(loadPath); result = Snapshot(); break;
            case "project_save": e.Save(PathFor(".gemelec", true)); result = new { path = e.FilePath, saved = true }; break;
            case "project_update": e.Change(() => { e.Project.Title = Str("title", e.Project.Title); e.Project.Author = Str("author", e.Project.Author); e.Project.Revision = Str("revision", e.Project.Revision); }); result = Snapshot(); break;
            case "page_add": result = e.AddSheet(Str("title")); break;
            case "page_select": window.Canvas.SetMode("select"); e.SelectSheet(Str("id")); result = e.Sheet; break;
            case "page_update": e.Change(() => e.Sheet.Title = Str("title")); result = e.Sheet; break;
            case "component_add":
                var catalogId = Str("catalogId"); var symbol = Str("symbol"); if (symbol.Length == 0 && catalogId.Length > 0) symbol = Catalog.Entries.FirstOrDefault(c => c.Id == catalogId)?.Symbol ?? throw new ArgumentException("Gamme introuvable.");
                var added = e.AddComponent(symbol, Num("x"), Num("y"), catalogId.Length == 0 ? null : catalogId); window.Canvas.Select(added.Id); result = added; break;
            case "component_update":
                var c = e.Sheet.Components.SingleOrDefault(c => c.Id == Str("id")) ?? throw new ArgumentException("Composant absent du folio actif.");
                e.Change(() => { if (a.TryGetProperty("x", out var x)) c.X = Editor.Snap(x.GetDouble()); if (a.TryGetProperty("y", out var y)) c.Y = Editor.Snap(y.GetDouble()); if (a.TryGetProperty("rotation", out var r)) c.Rotation = r.GetInt32(); c.Tag = Str("tag", c.Tag); c.Description = Str("description", c.Description); c.Brand = Str("brand", c.Brand); c.Range = Str("range", c.Range); c.Reference = Str("reference", c.Reference); c.Rating = Str("rating", c.Rating); if (a.TryGetProperty("portLabels", out var labels)) foreach (var label in labels.EnumerateObject()) { if (!c.Definition.Ports.Any(p => p.Id == label.Name) || label.Value.ValueKind != JsonValueKind.String) throw new ArgumentException("Borne ou libellé inconnu."); c.PortLabels[label.Name] = label.Value.GetString()!; } }); result = c; break;
            case "component_delete": if (!e.Sheet.Components.Any(c => c.Id == Str("id"))) throw new ArgumentException("Composant introuvable."); e.Delete([Str("id")]); result = new { deleted = Str("id") }; break;
            case "wire_add": List<Position>? pts = a.TryGetProperty("waypoints", out var points) ? JsonSerializer.Deserialize<List<Position>>(points, ProjectStore.Json) : null; result = e.Connect(new(Str("fromComponent"), Str("fromPort")), new(Str("toComponent"), Str("toPort")), pts, Str("number")); break;
            case "wire_delete": if (!e.Sheet.Wires.Any(w => w.Id == Str("id"))) throw new ArgumentException("Conducteur introuvable."); e.Delete([Str("id")]); result = new { deleted = Str("id") }; break;
            case "note_add": var note = new Annotation { X = Editor.Snap(Num("x")), Y = Editor.Snap(Num("y")), Text = Str("text") }; e.Change(() => e.Sheet.Notes.Add(note)); result = note; break;
            case "project_validate": result = new { findings = e.Validate() }; break;
            case "export_svg": var svg = PathFor(".svg", true); Export.Svg(e.Project, e.Sheet, svg); result = new { path = svg }; break;
            case "export_pdf": var pdf = PathFor(".pdf", true); Export.Pdf(e.Project, pdf); result = new { path = pdf }; break;
            case "export_bom": var csv = PathFor(".csv", true); Export.Csv(e.Project, csv); result = new { path = csv }; break;
            case "history_undo": e.Undo(); result = Snapshot(); break;
            case "history_redo": e.Redo(); result = Snapshot(); break;
            case "view_fit": window.Canvas.Fit(); result = new { zoom = window.Canvas.Zoom }; break;
            default: throw new ArgumentException("Outil inconnu.");
        }
        window.McpActivity(name); return result;
    }
}

public sealed class LocalBridge : IDisposable
{
    public static string SessionPath => Path.Combine(AppContext.BaseDirectory, "GeMMeElec.session.json");
    private readonly CancellationTokenSource stop = new();
    private readonly string token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    private readonly string pipeName = "GeMMeElec-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
    private readonly Task loop;
    public LocalBridge(MainWindow window)
    {
        ProjectStore.AtomicWrite(SessionPath, JsonSerializer.Serialize(new { processId = Environment.ProcessId, pipeName, token }));
        loop = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(stop.Token);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    using var reader = new StreamReader(server, Encoding.UTF8, false, 8192, true); using var writer = new StreamWriter(server, new UTF8Encoding(false), 8192, true) { AutoFlush = true };
                    var line = await McpServer.ReadLine(reader, 4_000_000, timeout.Token); if (line is null) continue;
                    object response;
                    try
                    {
                        using var doc = JsonDocument.Parse(line); var request = doc.RootElement;
                        if (request.GetProperty("token").GetString() != token) throw new UnauthorizedAccessException("Session MCP invalide.");
                        string name = request.GetProperty("name").GetString()!; var args = request.GetProperty("arguments").Clone();
                        // Materialize the response while still on the UI thread: the
                        // user may resume editing as soon as this dispatcher call ends.
                        var result = await window.Dispatcher.InvokeAsync(() => JsonSerializer.SerializeToElement(McpTools.Execute(window, name, args), McpServer.Compact));
                        response = new { ok = true, result };
                    }
                    catch (Exception ex) { response = new { ok = false, error = ex.GetBaseException().Message }; }
                    await writer.WriteLineAsync(JsonSerializer.Serialize(response, McpServer.Compact).AsMemory(), timeout.Token);
                }
                catch (OperationCanceledException) { }
                catch (IOException) { }
                catch (Exception ex) { Debug.WriteLine(ex); }
            }
        });
    }
    public void Dispose() { stop.Cancel(); try { if (File.Exists(SessionPath) && JsonDocument.Parse(File.ReadAllText(SessionPath)).RootElement.GetProperty("processId").GetInt32() == Environment.ProcessId) File.Delete(SessionPath); } catch (IOException) { } }
}

public static class McpServer
{
    public static readonly JsonSerializerOptions Compact = new(ProjectStore.Json) { WriteIndented = false };
    public static async Task<string?> ReadLine(StreamReader reader, int max, CancellationToken ct)
    {
        var b = new StringBuilder(); char[] ch = new char[1];
        while (await reader.ReadAsync(ch.AsMemory(), ct) > 0) { if (ch[0] == '\n') return b.ToString().TrimEnd('\r'); b.Append(ch[0]); if (b.Length > max) throw new IOException("Message trop volumineux."); }
        return b.Length > 0 ? b.ToString() : null;
    }
    public static async Task<object> Call(string name, JsonElement args)
    {
        McpTools.Check(name, args);
        if (!File.Exists(LocalBridge.SessionPath)) throw new InvalidOperationException("Ouvrir GeMMeElec avant d’utiliser le MCP.");
        using var session = JsonDocument.Parse(File.ReadAllText(LocalBridge.SessionPath)); var s = session.RootElement;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var pipe = new NamedPipeClientStream(".", s.GetProperty("pipeName").GetString()!, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(3000, timeout.Token);
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 8192, true); using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 8192, true) { AutoFlush = true };
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { token = s.GetProperty("token").GetString(), name, arguments = args }, Compact).AsMemory(), timeout.Token);
        var response = await ReadLine(reader, 32_000_000, timeout.Token) ?? throw new IOException("L’application a fermé la connexion.");
        using var doc = JsonDocument.Parse(response); if (!doc.RootElement.GetProperty("ok").GetBoolean()) throw new InvalidOperationException(doc.RootElement.GetProperty("error").GetString());
        return doc.RootElement.GetProperty("result").Clone();
    }
    public static async Task<int> Run()
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8); using var writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        bool initialized = false;
        while (true)
        {
            string? line; try { line = await ReadLine(reader, 4_000_000, CancellationToken.None); } catch { return 1; }
            if (line is null) return 0; if (string.IsNullOrWhiteSpace(line)) continue;
            JsonElement id = default; object response;
            try
            {
                using var doc = JsonDocument.Parse(line); var request = doc.RootElement;
                if (request.ValueKind != JsonValueKind.Object || !request.TryGetProperty("jsonrpc", out var version) || version.GetString() != "2.0" || !request.TryGetProperty("method", out var methodValue)) throw new ArgumentException("Requête JSON-RPC 2.0 invalide.");
                if (!request.TryGetProperty("id", out id)) continue;
                id = id.Clone(); string? method = methodValue.GetString(); object result;
                switch (method)
                {
                    case "initialize":
                        var requested = request.GetProperty("params").GetProperty("protocolVersion").GetString();
                        result = new { protocolVersion = requested is "2024-11-05" or "2025-03-26" or "2025-06-18" or "2025-11-25" ? requested : "2025-11-25", capabilities = new { tools = new { listChanged = false } }, serverInfo = new { name = "gemmeelec", version = "0.1.0" }, instructions = "Pilote le projet visible dans GeMMeElec. Lire project_get avant modification. Demander l’accord utilisateur avant d’abandonner des modifications ou écraser un fichier. Les gammes fabricants nécessitent une référence exacte et une vérification de bornage." }; initialized = true; break;
                    case "ping": result = new { }; break;
                    case "tools/list": if (!initialized) throw new InvalidOperationException("initialize requis."); result = McpTools.List(); break;
                    case "tools/call":
                        if (!initialized) throw new InvalidOperationException("initialize requis.");
                        try { var p = request.GetProperty("params"); var args = p.TryGetProperty("arguments", out var a) ? a : JsonSerializer.SerializeToElement(new { }); var called = await Call(p.GetProperty("name").GetString()!, args); result = new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(called, Compact) } }, isError = false }; }
                        catch (Exception ex) { result = new { content = new[] { new { type = "text", text = ex.GetBaseException().Message } }, isError = true }; } break;
                    default: await writer.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code = -32601, message = "Méthode inconnue" } }, Compact)); continue;
                }
                response = new { jsonrpc = "2.0", id, result };
            }
            catch (Exception ex) { response = new { jsonrpc = "2.0", id = id.ValueKind == JsonValueKind.Undefined ? (object?)null : id, error = new { code = ex is JsonException ? -32700 : -32600, message = ex.GetBaseException().Message } }; }
            await writer.WriteLineAsync(JsonSerializer.Serialize(response, Compact));
        }
    }
}
