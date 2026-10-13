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
        new("symbol_get", "Lire le dessin et les bornes d’un symbole intégré ou du projet.", new() { ["id"] = S("ID du symbole") }, ["id"], true),
        new("symbol_copy", "Enregistrer une définition éditée comme nouvelle copie du projet ; remplacer éventuellement un composant en conservant ses bornes connectées.", new() { ["definition"] = new JsonObject { ["type"] = "object" }, ["componentId"] = S("ID du composant à remplacer, facultatif") }, ["definition"]),
        new("symbol_import", "Importer un DXF 2000+ ou un .gemsymbol dans la bibliothèque du projet. Un DXF ne fournit aucune borne électrique ; ajouter les bornes avec symbol_copy.", new() { ["path"] = S("Chemin absolu .dxf ou .gemsymbol"), ["scale"] = N("Échelle du dessin, défaut 1") }, ["path"]),
        new("symbol_export", "Exporter un symbole autonome réutilisable .gemsymbol.", new() { ["id"] = S("ID du symbole"), ["path"] = S("Chemin absolu .gemsymbol"), ["overwrite"] = B("Autoriser le remplacement") }, ["id", "path"]),
        new("project_import", "Analyser un .qet ou un folio .dxf et retourner le rapport. apply=true ouvre la conversion ; les fichiers EPLAN natifs ne sont pas pris en charge.", new() { ["path"] = S("Chemin absolu .qet ou .dxf"), ["apply"] = B("Ouvrir le projet converti, défaut false"), ["discardUnsaved"] = B("Autoriser l’abandon du projet modifié") }, ["path"]),
        new("wire_segment_move", "Déplacer un segment du fil avec ses jonctions automatiques.", new() { ["id"] = S("ID du fil"), ["segment"] = N("Index du segment à partir de 1"), ["x"] = N("X cible"), ["y"] = N("Y cible") }, ["id", "segment", "x", "y"]),
        new("reference_list", "Lire les renvois, noms communs, destinations et adresses folio / zone.", new(), [], true),
        new("reference_update", "Configurer un renvoi du folio actif. Même nom = liaison ; pair : paires triées ; star : une source et ses cibles. Le mode est appliqué au groupe.", new() { ["id"] = S("ID du renvoi"), ["name"] = S("Nom commun, vide pour délier"), ["displayName"] = S("Nom affiché partagé avec les reports liés, sauf nom indépendant"), ["independentName"] = B("Nom indépendant de ceux des reports liés"), ["mode"] = S("pair ou star"), ["order"] = N("Ordre 0..99999"), ["source"] = B("Source de l’étoile") }, ["id"]),
        new("connection_trace", "Suivre les conducteurs et renvois liés depuis une borne, à travers les folios. Ne traverse pas l’intérieur des appareils.", new() { ["componentId"] = S("ID du composant"), ["portId"] = S("ID de la borne") }, ["componentId", "portId"], true),
        new("bom_generate", "Ajouter ou actualiser les folios BOM selon les options enregistrées. Pagination sans cartouche, opération annulable.", new(), []),
        new("wire_branch", "Raccorder une borne au trajet d’un fil, créer une jonction et scinder ce fil en conservant son tracé.", new() { ["fromComponent"] = S("Composant source"), ["fromPort"] = S("Borne source"), ["targetWire"] = S("Conducteur cible"), ["x"] = N("X sur le fil"), ["y"] = N("Y sur le fil") }, ["fromComponent", "fromPort", "targetWire", "x", "y"]),
        new("project_get", "Lire le projet ouvert, le folio actif, les IDs et les bornes en coordonnées absolues.", new(), [], true),
        new("library_search", "Rechercher les références fabricants documentées et les aperçus EPLAN. canPlace=false interdit le raccordement. Les symboles génériques restent séparés.", new() { ["query"] = S("Recherche libre"), ["family"] = S("Famille exacte, facultatif"), ["brand"] = S("Fabricant exact, facultatif"), ["library"] = S("manufacturer ou eplan, facultatif") }, [], true),
        new("project_new", "Créer un projet. Refuse d’abandonner les modifications non enregistrées sans discardUnsaved.", new() { ["title"] = S("Nom du projet"), ["discardUnsaved"] = B("Autoriser la perte des modifications courantes") }, ["title"]),
        new("project_open", "Ouvrir un fichier .gemelec existant.", new() { ["path"] = S("Chemin absolu .gemelec"), ["discardUnsaved"] = B("Autoriser la perte des modifications courantes") }, ["path"]),
        new("project_save", "Enregistrer le projet .gemelec. overwrite nécessaire pour écraser un autre fichier existant.", new() { ["path"] = S("Chemin absolu .gemelec"), ["overwrite"] = B("Autoriser l’écrasement d’un fichier existant") }, ["path"]),
        new("project_update", "Modifier les informations du projet et le repérage commun aux folios.", new() { ["zoneColumns"]=N("Colonnes du projet 1..40"),["zoneRows"]=N("Lettres du projet 1..26"),["title"] = S("Nom"), ["author"] = S("Auteur"), ["revision"] = S("Révision"), ["customer"]=S("Client"),["documentNumber"]=S("Numéro document"),["date"]=S("Date"),["customFields"]=new JsonObject{["type"]="object",["additionalProperties"]=new JsonObject{["type"]="string"}} }, []),
        new("page_add", "Ajouter et sélectionner un folio A3 paysage.", new() { ["title"] = S("Titre du folio") }, ["title"]),
        new("page_select", "Sélectionner le folio utilisé par les commandes suivantes.", new() { ["id"] = S("ID du folio") }, ["id"]),
        new("page_update", "Modifier les propriétés du folio actif ; les valeurs vides auteur/date/révision héritent du projet.", new() { ["title"] = S("Titre"),["number"]=S("Numéro, vide = ordre automatique"),["author"]=S("Auteur"),["checkedBy"]=S("Vérificateur"),["approvedBy"]=S("Approbateur"),["date"]=S("Date"),["revision"]=S("Révision"),["location"]=S("Localisation"),["function"]=S("Fonction"),["titleBlockId"]=S("ID du cartouche, vide = aucun"),["useProjectZones"]=B("Hériter du repérage du projet ; false = exception du folio"),["zoneColumns"]=N("Colonnes locales 1..40"),["zoneRows"]=N("Lignes 1..26"),["customFields"]=new JsonObject{["type"]="object",["additionalProperties"]=new JsonObject{["type"]="string"}} }, []),
        new("page_move","Déplacer le folio actif vers un index 1..nombre de folios.",new(){["index"]=N("Position dans le projet")},["index"]),
        new("titleblock_list","Lire les modèles du projet, champs disponibles et valeurs du folio actif.",new(),[],true),
        new("titleblock_put","Créer ou remplacer un modèle : id, name, height (unités), cells (x/y/width/height en %, text, fontSize, align, vertical, border, imageId).",new(){["template"]=new JsonObject{["type"]="object"}},["template"]),
        new("component_image","Importer une image PNG/JPEG/BMP dans le projet et l’affecter au composant. Chemin vide : retirer son image.",new(){["id"]=S("ID composant"),["path"]=S("Chemin local absolu, ou vide")},["id","path"]),
        new("bom_configure","Configurer la BOM : columns (key,label,enabled), groupArticles, includeAuxiliary, sheetId et brand. Remplace les options fournies.",new(){["options"]=new JsonObject{["type"]="object"}},["options"]),
        new("export_bom_html","Exporter une BOM HTML autonome avec images, selon les options enregistrées.",new(){["path"]=S("Chemin absolu .html"),["overwrite"]=B("Autoriser remplacement")},["path"]),
        new("component_add", "Placer un symbole sur le folio actif. Coordonnées logiques 0..1400, 0..990 ; grille 10. Fournir symbol ou catalogId.", new() { ["symbol"] = S("ID de symbole"), ["catalogId"] = S("ID de référence raccordable obtenu par library_search"), ["x"] = N("Centre X"), ["y"] = N("Centre Y") }, ["x", "y"]),
        new("component_update", "Modifier un composant du folio actif. Les fils suivent sa position et sa rotation.", new() { ["id"] = S("ID du composant"), ["x"] = N("Centre X"), ["y"] = N("Centre Y"), ["rotation"] = N("Rotation multiple de 90 degrés"), ["tag"] = S("Repère"), ["description"] = S("Description"), ["brand"] = S("Fabricant"), ["range"] = S("Gamme"), ["reference"] = S("Référence exacte"), ["rating"] = S("Caractéristiques vérifiées"), ["relayFunction"] = S("Fonction déclarée : coil, no, nc, changeover, power ou vide"), ["portLabels"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = new JsonObject { ["type"] = "string" }, ["description"] = "Libellés des bornes, clés = IDs fixes des ports" } }, ["id"]),
        new("component_delete", "Supprimer un composant et ses connexions ; annulable.", new() { ["id"] = S("ID") }, ["id"]),
        new("coil_contact_assign", "Associer une fonction contact à une bobine, avec repère partagé ; coilId vide détache la fonction. Ne crée aucune liaison électrique.", new() { ["contactId"] = S("ID du contact"), ["coilId"] = S("ID de bobine, vide pour détacher") }, ["contactId", "coilId"]),
        new("coil_contacts_list", "Lister les fonctions bobines/contacts, leurs associations, bornes et références de folio/zone recalculées.", new(), [], true),
        new("component_text_list", "Lister tous les textes du composant, leurs clés, contenu et style, y compris bornes et textes ajoutés.", new() { ["id"] = S("ID composant") }, ["id"], true),
        new("component_text_add", "Ajouter un texte attaché au composant. Retourne sa clé extra:… ; modifier ensuite sa position avec component_text_update.", new() { ["id"] = S("ID composant"), ["text"] = S("Contenu") }, ["id", "text"]),
        new("component_text_delete", "Supprimer un texte ajouté (clé extra:…). Pour masquer un texte existant, utiliser visible=false.", new() { ["id"] = S("ID composant"), ["target"] = S("Clé extra:…") }, ["id", "target"]),
        new("component_text_align", "Aligner un texte ou tous les textes visibles sur un côté du symbole. Conserve les styles et signale les collisions restantes.", new() { ["id"] = S("ID composant"), ["target"] = S("Clé du texte ou all, défaut all"), ["side"] = S("top, bottom, left, right ou auto, défaut auto") }, ["id"]),
        new("component_text_update", "Modifier le contenu et le placement individuel. En mode custom, décalages depuis anchorPort si renseigné, sinon depuis le centre du composant.", new() { ["id"] = S("ID composant"), ["target"] = S("Clé de component_text_list : tag, detail, description, port:…, shape:… ou extra:…"), ["text"] = S("Nouveau contenu ; destination des reports en lecture seule"), ["side"] = S("top, bottom, left, right ou custom"), ["horizontal"] = S("left, center ou right"), ["vertical"] = S("top, center ou bottom"), ["gap"] = N("Distance au bord, 0..1000"), ["offsetX"] = N("Décalage horizontal précis"), ["offsetY"] = N("Décalage vertical précis"), ["visible"] = B("Afficher le texte"), ["fontSize"] = N("Taille 4..200, 0 = automatique"), ["color"] = S("Couleur #RRGGBB, vide = automatique"), ["rotation"] = N("Rotation du texte −360..360 degrés"), ["destinationInline"] = B("Report : destination sous le nom") }, ["id", "target"]),
        new("wire_update", "Modifier numéro, couleur, parcours et/ou bornes d’un conducteur. waypoints vide rétablit le tracé automatique. Opération annulable.", new() { ["id"] = S("ID conducteur"), ["number"] = S("Numéro"), ["color"] = S("Couleur #RRGGBB"), ["fromComponent"] = S("ID composant de départ"), ["fromPort"] = S("ID borne de départ"), ["toComponent"] = S("ID composant d’arrivée"), ["toPort"] = S("ID borne d’arrivée"), ["waypoints"] = new JsonObject { ["type"] = "array", ["maxItems"] = 100, ["items"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["x"] = N("X"), ["y"] = N("Y") }, ["required"] = new JsonArray("x", "y"), ["additionalProperties"] = false } } }, ["id"]),
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
            case "symbol_get": result = CustomSymbols.Resolve(e.Project, Str("id")); break;
            case "symbol_copy": var definition = a.GetProperty("definition").Deserialize<SymbolDefinition>(ProjectStore.Json) ?? throw new ArgumentException("Définition absente."); result = e.SaveSymbolCopy(definition, Str("componentId").Length > 0 ? Str("componentId") : null); break;
            case "symbol_import":
                var symbolPath = Str("path"); if (!Path.IsPathFullyQualified(symbolPath)) throw new ArgumentException("Chemin absolu requis.");
                var imported = symbolPath.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ? DxfImport.Read(symbolPath) : symbolPath.EndsWith(".gemsymbol", StringComparison.OrdinalIgnoreCase) ? new DxfImportResult(CustomSymbols.Load(symbolPath), [], "GeMMeElec") : throw new ArgumentException("Utiliser .dxf ou .gemsymbol.");
                var copy = e.SaveSymbolCopy(DxfImport.Scale(imported.Symbol, a.TryGetProperty("scale", out _) ? Num("scale") : 1)); result = new { symbol = copy, imported.Warnings, imported.Units }; break;
            case "symbol_export": CustomSymbols.Export(CustomSymbols.Resolve(e.Project, Str("id")), PathFor(".gemsymbol", true)); result = new { exported = true }; break;
            case "project_import":
                var importPath = Str("path"); if (!Path.IsPathFullyQualified(importPath)) throw new ArgumentException("Chemin absolu requis.");
                if (Flag("apply")) ReplaceCheck();
                var conversion = importPath.EndsWith(".qet", StringComparison.OrdinalIgnoreCase) ? ProjectImport.Qet(importPath) : importPath.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ? ProjectImport.DxfPages([importPath]) : throw new ArgumentException("Utiliser .qet ou .dxf ; EPLAN natif .zw1/.elk/.epj non pris en charge.");
                if (Flag("apply")) e.ImportProject(conversion.Project); result = new { conversion.Report, conversion.Warnings, sheets = conversion.Project.Sheets.Count, applied = Flag("apply") }; break;
            case "wire_segment_move": var movingWire = e.Sheet.Wires.Single(w => w.Id == Str("id")); var route = movingWire.Route(e.Sheet); int seg = a.GetProperty("segment").GetInt32(); if (seg < 1 || seg >= route.Count) throw new ArgumentException("Segment absent."); e.Change(() => Junctions.ShiftSegment(e.Sheet, movingWire.Id, route, seg, new(Num("x"), Num("y")))); result = movingWire; break;
            case "reference_list": var links = new CrossReferences(e.Project); result = CrossReferences.Points(e.Project).Select(x => new { sheetId = x.Sheet.Id, id = x.Component.Id, displayName = ReferenceDisplay.Name(x.Component), x.Component.ReferenceIndependentName, x.Component.LinkName, x.Component.LinkMode, x.Component.LinkOrder, x.Component.LinkSource, address = SheetZones.Address(e.Project, x.Sheet, x.Component), targets = links.Targets(x.Component.Id).Select(t => new { sheetId = t.Sheet.Id, id = t.Component.Id, address = SheetZones.Address(e.Project, t.Sheet, t.Component) }) }).ToArray(); break;
            case "reference_update":
                var reference = e.Sheet.Components.SingleOrDefault(c => c.Id == Str("id")) ?? throw new ArgumentException("Report absent du folio actif.");
                e.ConfigureReference(reference.Id, Str("name", reference.LinkName), Str("mode", reference.LinkMode), a.TryGetProperty("order", out var orderValue) ? orderValue.GetInt32() : reference.LinkOrder, a.TryGetProperty("source", out var sourceValue) ? sourceValue.GetBoolean() : reference.LinkSource, a.TryGetProperty("displayName", out var displayNameValue) ? displayNameValue.GetString() : null, a.TryGetProperty("independentName", out var independentValue) ? independentValue.GetBoolean() : null); result = reference; break;
            case "connection_trace": result = CrossReferences.Trace(e.Project, new(Str("componentId"), Str("portId"))); break;
            case "bom_generate": result = e.GenerateBom(e.Project.BomOptions); break;
            case "wire_branch": result = e.Branch(new(Str("fromComponent"), Str("fromPort")), Str("targetWire"), new(Num("x"), Num("y"))); break;
            case "project_get": result = Snapshot(); break;
            case "library_search": result = new { equipment = Catalog.Search(Str("query"), Str("family"), Str("brand"), Str("library")).ToArray(), symbols = Symbols.All.Values.Concat(e.Project.CustomSymbols.Values).Where(s => Symbols.SearchKey(s.Name + " " + s.Id).Contains(Symbols.SearchKey(Str("query")))).Select(s => new { s.Id, s.Name, s.Category, s.Prefix, s.Width, s.Height, s.Ports }) }; break;
            case "project_new": ReplaceCheck(); e.New(Str("title")); result = Snapshot(); break;
            case "project_open": var loadPath = PathFor(".gemelec", false); ReplaceCheck(); e.Open(loadPath); result = Snapshot(); break;
            case "project_save": e.Save(PathFor(".gemelec", true)); result = new { path = e.FilePath, saved = true }; break;
            case "project_update": e.Change(() => { e.Project.Title = Str("title", e.Project.Title); e.Project.Author = Str("author", e.Project.Author); e.Project.Revision = Str("revision", e.Project.Revision);e.Project.Customer=Str("customer",e.Project.Customer);e.Project.DocumentNumber=Str("documentNumber",e.Project.DocumentNumber);e.Project.Date=Str("date",e.Project.Date);if(a.TryGetProperty("zoneColumns",out var projectColumns))e.Project.ZoneColumns=projectColumns.GetInt32();if(a.TryGetProperty("zoneRows",out var projectRows))e.Project.ZoneRows=projectRows.GetInt32();if(a.TryGetProperty("customFields",out var projectCustom))e.Project.CustomFields=projectCustom.Deserialize<Dictionary<string,string>>(ProjectStore.Json)!; }); result = Snapshot(); break;
            case "page_add": result = e.AddSheet(Str("title")); break;
            case "page_select": window.Canvas.SetMode("select"); e.SelectSheet(Str("id")); result = e.Sheet; break;
            case "page_update": e.Change(() => {var sheet=e.Sheet;sheet.Title=Str("title",sheet.Title);sheet.Number=Str("number",sheet.Number);sheet.Author=Str("author",sheet.Author);sheet.CheckedBy=Str("checkedBy",sheet.CheckedBy);sheet.ApprovedBy=Str("approvedBy",sheet.ApprovedBy);sheet.Date=Str("date",sheet.Date);sheet.Revision=Str("revision",sheet.Revision);sheet.Location=Str("location",sheet.Location);sheet.Function=Str("function",sheet.Function);sheet.TitleBlockId=Str("titleBlockId",sheet.TitleBlockId);if(a.TryGetProperty("zoneColumns",out var zoneColumns)){sheet.ZoneColumns=zoneColumns.GetInt32();sheet.UseProjectZones=false;}if(a.TryGetProperty("zoneRows",out var zoneRows)){sheet.ZoneRows=zoneRows.GetInt32();sheet.UseProjectZones=false;}if(a.TryGetProperty("useProjectZones",out var inheritedZones))sheet.UseProjectZones=inheritedZones.GetBoolean();if(a.TryGetProperty("customFields",out var sheetCustom))sheet.CustomFields=sheetCustom.Deserialize<Dictionary<string,string>>(ProjectStore.Json)!; }); result = e.Sheet; break;
            case "page_move": var moving=e.Sheet;int index=a.GetProperty("index").GetInt32()-1;if(index<0||index>=e.Project.Sheets.Count)throw new ArgumentException("Index de folio invalide.");e.Change(()=>{e.Project.Sheets.Remove(moving);e.Project.Sheets.Insert(index,moving);});result=Snapshot();break;
            case "titleblock_list": result=new{templates=e.Project.TitleBlocks,fields=DocumentModel.Fields,values=DocumentModel.Values(e.Project,e.Sheet)};break;
            case "titleblock_put":var template=a.GetProperty("template").Deserialize<TitleBlockTemplate>(ProjectStore.Json)??throw new ArgumentException("Modèle absent.");e.Change(()=>{int at=e.Project.TitleBlocks.FindIndex(t=>t.Id==template.Id);if(at>=0)e.Project.TitleBlocks[at]=template;else e.Project.TitleBlocks.Add(template);});result=template;break;
            case "component_image":var imageComponent=e.Sheet.Components.Single(c=>c.Id==Str("id"));var imagePath=Str("path");if(imagePath.Length==0)e.Change(()=>imageComponent.ImageId="");else{if(!Path.IsPathFullyQualified(imagePath)||!new[]{".png",".jpg",".jpeg",".bmp"}.Contains(Path.GetExtension(imagePath).ToLowerInvariant()))throw new ArgumentException("Image locale PNG/JPEG/BMP requise.");var asset=ImageAssets.Import(imagePath);e.Change(()=>{e.Project.Images[asset.Id]=asset.Data;imageComponent.ImageId=asset.Id;});}result=new{imageComponent.Id,imageComponent.ImageId};break;
            case "bom_configure":var configured=a.GetProperty("options").Deserialize<BomOptions>(ProjectStore.Json)??throw new ArgumentException("Options absentes.");e.Change(()=>{configured.Validate();if(configured.SheetId.Length>0&&!e.Project.Sheets.Any(s=>s.Id==configured.SheetId))throw new ArgumentException("Folio de BOM absent.");e.Project.BomOptions=configured;});result=configured;break;
            case "export_bom_html":var htmlPath=PathFor(".html",true);ProjectStore.AtomicWrite(htmlPath,BomReport.Html(e.Project,e.Project.BomOptions));result=new{path=htmlPath};break;
            case "component_add":
                var catalogId = Str("catalogId"); var symbol = Str("symbol"); if (symbol.Length == 0 && catalogId.Length > 0) symbol = Catalog.Entries.FirstOrDefault(c => c.Id == catalogId)?.Symbol ?? throw new ArgumentException("Article introuvable.");
                var added = e.AddComponent(symbol, Num("x"), Num("y"), catalogId.Length == 0 ? null : catalogId); window.Canvas.Select(added.Id); result = added; break;
            case "component_update":
                var c = e.Sheet.Components.SingleOrDefault(c => c.Id == Str("id")) ?? throw new ArgumentException("Composant absent du folio actif.");
                e.Change(() => { if (a.TryGetProperty("relayFunction", out var role)) e.SetRelayFunction(c, role.GetString()!); if (a.TryGetProperty("x", out var x)) c.X = Editor.Snap(x.GetDouble()); if (a.TryGetProperty("y", out var y)) c.Y = Editor.Snap(y.GetDouble()); if (a.TryGetProperty("rotation", out var r)) c.Rotation = r.GetInt32(); e.SetApparatusTag(c, Str("tag", c.Tag)); c.Description = Str("description", c.Description); c.Brand = Str("brand", c.Brand); c.Range = Str("range", c.Range); c.Reference = Str("reference", c.Reference); c.Rating = Str("rating", c.Rating); if (a.TryGetProperty("portLabels", out var labels)) foreach (var label in labels.EnumerateObject()) { if (!c.Definition.Ports.Any(p => p.Id == label.Name) || label.Value.ValueKind != JsonValueKind.String) throw new ArgumentException("Borne ou libellé inconnu."); c.PortLabels[label.Name] = label.Value.GetString()!; } }); result = c; break;
            case "component_delete": if (!e.Sheet.Components.Any(c => c.Id == Str("id"))) throw new ArgumentException("Composant introuvable."); e.Delete([Str("id")]); result = new { deleted = Str("id") }; break;
            case "coil_contact_assign": e.AssociateContact(Str("contactId"), Str("coilId")); result = e.Project.Sheets.SelectMany(s => s.Components).Single(c => c.Id == Str("contactId")); break;
            case "coil_contacts_list": var coilRefs = new CoilContacts(e.Project); result = e.Project.Sheets.SelectMany(s => s.Components.Where(c => CoilContacts.IsCoil(c) || CoilContacts.IsContact(c)).Select(c => new { id = c.Id, c.Tag, role = CoilContacts.Role(c), c.CoilId, sheetId = s.Id, address = SheetZones.Address(e.Project, s, c), terminals = CoilContacts.Terminals(c), references = coilRefs.Label(c) })).ToArray(); break;
            case "component_text_list":
                var listedComponent = e.Sheet.Components.Single(c => c.Id == Str("id")); var textRefs = new CrossReferences(e.Project);
                result = ComponentTexts.List(listedComponent).Select(t => new { key = t.Key, name = t.Name, text = t.Key == "coilReferences" ? textRefs.Coils.Label(listedComponent) : t.ReadOnly ? textRefs.Label(e.Project, listedComponent) : t.Text, placement = t.Placement, readOnly = t.ReadOnly }).ToArray(); break;
            case "component_text_add":
                var addTextComponent = e.Sheet.Components.Single(c => c.Id == Str("id")); string newTextKey = "";
                e.Change(() => newTextKey = ComponentTexts.Add(addTextComponent, Str("text"))); result = new { key = newTextKey }; break;
            case "component_text_delete":
                var deleteTextComponent = e.Sheet.Components.Single(c => c.Id == Str("id")); var deleteKey = Str("target");
                if (!deleteKey.StartsWith("extra:") || !deleteTextComponent.AdditionalTexts.Any(t => "extra:" + t.Id == deleteKey)) throw new ArgumentException("Seuls les textes ajoutés peuvent être supprimés.");
                e.Change(() => deleteTextComponent.AdditionalTexts.RemoveAll(t => "extra:" + t.Id == deleteKey)); result = new { deleted = deleteKey }; break;
            case "component_text_align":
                var alignComponent = e.Sheet.Components.Single(c => c.Id == Str("id")); string alignKey = Str("target", "all"), alignSide = Str("side", "auto"); SmartTextResult? aligned = null;
                e.Change(() => { ComponentTexts.Materialize(alignComponent); aligned = alignKey == "all" ? SmartTextPlacement.AdjustAll(e.Project, e.Sheet, alignComponent, alignSide == "auto" ? null : alignSide) : SmartTextPlacement.Adjust(e.Project, e.Sheet, alignComponent, ComponentTexts.List(alignComponent).FindIndex(t => t.Key == alignKey), alignSide == "auto" ? null : alignSide); }); result = aligned!; break;
            case "component_text_update":
                var textComponent = e.Sheet.Components.SingleOrDefault(c => c.Id == Str("id")) ?? throw new ArgumentException("Composant absent du folio actif.");
                var target = Str("target"); var textTarget = ComponentTexts.Get(textComponent, target); LabelPlacement placement = DocumentModel.Copy(textTarget.Placement);
                e.Change(() => { if (target.StartsWith("port:")) ComponentTexts.Materialize(textComponent); ComponentTexts.SetPlacement(textComponent, target, placement);
                    if (a.TryGetProperty("text", out var contentValue)) { if (target == "tag" && CrossReferences.IsPoint(textComponent)) e.SetReferenceName(textComponent, contentValue.GetString()!); else if (target == "tag") e.SetApparatusTag(textComponent, contentValue.GetString()!); else ComponentTexts.SetText(textComponent, target, contentValue.GetString()!); }
                    if (a.TryGetProperty("destinationInline", out var inlineValue)) { if (!CrossReferences.IsPoint(textComponent)) throw new ArgumentException("destinationInline concerne uniquement les reports."); textComponent.ReferenceDestinationInline = inlineValue.GetBoolean(); } else if (CrossReferences.IsPoint(textComponent) && target == "detail" && new[] { "side", "horizontal", "vertical", "gap", "offsetX", "offsetY" }.Any(key => a.TryGetProperty(key, out _))) textComponent.ReferenceDestinationInline = false; placement.Side = Str("side", placement.Side); placement.Horizontal = Str("horizontal", placement.Horizontal); placement.Vertical = Str("vertical", placement.Vertical); if (a.TryGetProperty("gap", out var gap)) placement.Gap = gap.GetDouble(); if (a.TryGetProperty("offsetX", out var ox)) placement.OffsetX = ox.GetDouble(); if (a.TryGetProperty("offsetY", out var oy)) placement.OffsetY = oy.GetDouble(); if (a.TryGetProperty("visible", out var visible)) placement.Visible = visible.GetBoolean(); if (a.TryGetProperty("fontSize", out var fontSizeValue)) placement.FontSize = fontSizeValue.GetDouble(); placement.Color = Str("color", placement.Color); if (a.TryGetProperty("rotation", out var textRotationValue)) placement.Rotation = textRotationValue.GetDouble(); }); result = placement; break;
            case "wire_update":
                var editedWire = e.Sheet.Wires.SingleOrDefault(w => w.Id == Str("id")) ?? throw new ArgumentException("Conducteur absent du folio actif.");
                e.Change(() => {
                    Endpoint nextFrom = new(Str("fromComponent", editedWire.From.ComponentId), Str("fromPort", editedWire.From.PortId)), nextTo = new(Str("toComponent", editedWire.To.ComponentId), Str("toPort", editedWire.To.PortId));
                    bool reconnecting = nextFrom != editedWire.From || nextTo != editedWire.To, explicitNumber = a.TryGetProperty("number", out _);
                    string number = explicitNumber ? Str("number") : editedWire.Number;
                    if (reconnecting && !explicitNumber) { var destination = nextFrom != editedWire.From ? nextFrom : nextTo; number = new ConductorNetworks(e.Project).ExistingNumber(destination, destination == nextFrom ? nextTo : nextFrom, editedWire.Id) ?? number; }
                    editedWire.Color = Str("color", editedWire.Color); editedWire.From = nextFrom; editedWire.To = nextTo;
                    if (a.TryGetProperty("waypoints", out var routePoints)) editedWire.Waypoints = JsonSerializer.Deserialize<List<Position>>(routePoints, ProjectStore.Json) ?? throw new ArgumentException("Parcours invalide.");
                    if (explicitNumber || reconnecting) e.SynchronizeWireNumbers(editedWire, number);
                }); result = editedWire; break;
            case "wire_add": List<Position>? pts = a.TryGetProperty("waypoints", out var points) ? JsonSerializer.Deserialize<List<Position>>(points, ProjectStore.Json) : null; result = e.Connect(new(Str("fromComponent"), Str("fromPort")), new(Str("toComponent"), Str("toPort")), pts, Str("number")); break;
            case "wire_delete": if (!e.Sheet.Wires.Any(w => w.Id == Str("id"))) throw new ArgumentException("Conducteur introuvable."); e.Delete([Str("id")]); result = new { deleted = Str("id") }; break;
            case "note_add": var note = new Annotation { X = Editor.Snap(Num("x")), Y = Editor.Snap(Num("y")), Text = Str("text") }; e.Change(() => e.Sheet.Notes.Add(note)); result = note; break;
            case "project_validate": result = new { findings = e.Validate() }; break;
            case "export_svg": var svg = PathFor(".svg", true); Export.Svg(e.Project, e.Sheet, svg); result = new { path = svg }; break;
            case "export_pdf": var pdf = PathFor(".pdf", true); result = window.ExportPdfAsync(pdf); break;
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
    public static string SessionPath => Path.Combine(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION")=="1" ? "GeMMeElec.test-session.json" : "GeMMeElec.session.json");
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
                        var pending = await window.Dispatcher.InvokeAsync(() => {
                            var value = McpTools.Execute(window, name, args);
                            return value is Task<object> work ? (object)work : JsonSerializer.SerializeToElement(value, McpServer.Compact);
                        });
                        var result = pending is Task<object> export ? JsonSerializer.SerializeToElement(await export, McpServer.Compact) : (JsonElement)pending;
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
                        result = new { protocolVersion = requested is "2024-11-05" or "2025-03-26" or "2025-06-18" or "2025-11-25" ? requested : "2025-11-25", capabilities = new { tools = new { listChanged = false } }, serverInfo = new { name = "gemmeelec", version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!.ToString(3) }, instructions = "Pilote le projet visible dans GeMMeElec. Lire project_get avant modification. Demander l’accord utilisateur avant d’abandonner des modifications ou écraser un fichier. Les dessins fabricants ont une référence et une provenance documentées. Les entrées canPlace=false sont des aperçus non raccordables." }; initialized = true; break;
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
