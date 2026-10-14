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
        new("table_create","Créer un tableau libre avec lignes et colonnes, tailles initiales en unités de dessin. Les cellules sont ensuite modifiables.",new(){["x"]=N("X"),["y"]=N("Y"),["rows"]=N("Lignes 1 à 100"),["columns"]=N("Colonnes 1 à 40"),["columnWidth"]=N("Largeur initiale, défaut 120"),["rowHeight"]=N("Hauteur initiale, défaut 40")},["x","y","rows","columns"]),
        new("table_cell_put","Modifier le texte formaté d’une cellule de tableau : text, bold, italic, runs, fontSize, color, align, vertical. Indices depuis zéro.",new(){["id"]=S("ID tableau"),["row"]=N("Indice ligne"),["column"]=N("Indice colonne"),["content"]=new JsonObject{["type"]="object"}},["id","row","column","content"]),
        new("text_style","Appliquer gras/italique à une note, un texte de symbole, un numéro de conducteur, un rectangle avec texte ou toutes les cellules d’un tableau.",new(){["id"]=S("ID objet"),["textKey"]=S("Texte du symbole, défaut tag"),["bold"]=B("Gras"),["italic"]=B("Italique")},["id"]),
        new("element_position","Positionner un symbole, texte ou dessin libre par rapport à la page : X/Y, Width/Height pour les formes. unit=units ou percent.",new(){["id"]=S("ID élément"),["axis"]=S("X, Y, Width ou Height"),["value"]=N("Coordonnée"),["unit"]=S("units (défaut) ou percent")},["id","axis","value"]),
        new("selection_align_page","Aligner le groupe sélectionné sur la page entière, avec déplacement des coudes internes, en une action annulable.",new(){["ids"]=new JsonObject{["type"]="array"},["operation"]=S("left, right, top, bottom, centerX, centerY ou center")},["ids","operation"]),
        new("page_kind","Définir le folio actif comme schéma ou page de garde ; aucun objet supprimé. La page de garde n’affiche pas les zones.",new(){["kind"]=S("drawing ou cover"),["titleBlockId"]=S("Cartouche, vide pour aucun")},["kind"]),
        new("graphic_put","Créer ou remplacer une forme libre : rect, ellipse, line, image, textbox ou table, avec texte formaté. Aucune connexion électrique.",new(){["graphic"]=new JsonObject{["type"]="object"}},["graphic"]),
        new("graphic_delete","Supprimer une forme libre, opération annulable.",new(){["id"]=S("ID dessin libre")},["id"]),
        new("graphic_image_import","Importer un logo/image et le placer sur le folio actif ; une action annulable.",new(){["path"]=S("Chemin absolu de l’image"),["x"]=N("X"),["y"]=N("Y"),["width"]=N("Largeur, défaut 280")},["path","x","y"]),
        new("note_put","Créer ou remplacer un texte libre : taille, couleur, angle, alignement et champs du projet.",new(){["note"]=new JsonObject{["type"]="object"}},["note"]),
        new("projects_list","Lister les onglets, fichiers, modifications et projet actif.",new(),[],true),
        new("project_tab_add","Ouvrir un projet dans un nouvel onglet, ou en créer un avec title. Les autres projets sont conservés.",new(){["path"]=S("Chemin absolu .gemelec, facultatif"),["title"]=S("Nom du nouveau projet")},[]),
        new("project_tab_select","Activer un onglet existant.",new(){["id"]=S("ID onglet")},["id"]),
        new("project_tab_close","Fermer un onglet ; refuser la perte de modifications sans discardUnsaved.",new(){["id"]=S("ID onglet"),["discardUnsaved"]=B("Autoriser l’abandon")},["id"]),
        new("block_save","Exporter les symboles sélectionnés, notes et fils internes comme bloc autonome .gemblock.",new(){["ids"]=new JsonObject{["type"]="array"},["name"]=S("Nom"),["category"]=S("Catégorie"),["tags"]=S("Mots-clés"),["path"]=S("Chemin absolu .gemblock"),["overwrite"]=B("Autoriser remplacement")},["ids","name","path"]),
        new("block_insert","Insérer un bloc autonome avec nouveaux identifiants et repères ; une action annulable.",new(){["path"]=S("Chemin absolu .gemblock"),["x"]=N("Coin gauche"),["y"]=N("Coin haut")},["path","x","y"]),
        new("personal_library_list","Lister les favoris et circuits conservés localement.",new(){["query"]=S("Nom catégorie mots-clés")},[],true),
        new("personal_library_put","Enregistrer un favori intégré, une copie de symbole ou un bloc avec catégorie et mots-clés.",new(){["entry"]=new JsonObject{["type"]="object"}},["entry"]),
        new("personal_library_delete","Supprimer le favori local sans modifier les projets qui l’utilisent.",new(){["id"]=S("ID favori")},["id"]),
        new("personal_library_place","Placer un favori ou un bloc du catalogue personnel.",new(){["id"]=S("ID favori"),["x"]=N("X"),["y"]=N("Y")},["id","x","y"]),
        new("view_reading","Configurer la taille et les détails à l’écran, sans modifier le document ni ses exports.",new(){["options"]=new JsonObject{["type"]="object"}},["options"]),
        new("view_zoom_selection","Cadrer une sélection du folio actif.",new(){["ids"]=new JsonObject{["type"]="array"}},["ids"]),
        new("wire_route_smart","Router les fils choisis avec obstacles et maintien des bornes.",new(){["ids"]=new JsonObject{["type"]="array"}},["ids"]),
        new("wire_insert_component","Insérer un symbole sur un fil avec deux ports explicites, en une action.",new(){["id"]=S("ID fil"),["symbol"]=S("Symbole"),["x"]=N("X"),["y"]=N("Y"),["inputPort"]=S("Port d’entrée"),["outputPort"]=S("Port de sortie")},["id","symbol","x","y","inputPort","outputPort"]),
        new("terminal_line_create","Insérer les bornes aux intersections d’une ligne avec les fils.",new(){["a"]=new JsonObject{["type"]="object"},["b"]=new JsonObject{["type"]="object"},["name"]=S("Repère bornier"),["first"]=N("Premier numéro"),["level"]=N("Niveau")},["a","b","name"]),
        new("cable_line_create","Regrouper les fils traversés par une ligne ; section saisie, aucune liaison électrique créée.",new(){["a"]=new JsonObject{["type"]="object"},["b"]=new JsonObject{["type"]="object"},["name"]=S("Repère câble"),["sectionMm2"]=N("Section mm²"),["type"]=S("Type de câble")},["a","b","name"]),
        new("cables_list","Lire les définitions et affectations de conducteurs des câbles.",new(),[],true),
        new("cable_put","Créer ou modifier les données d’un câble, sans modifier les connexions.",new(){["cable"]=new JsonObject{["type"]="object"}},["cable"]),
        new("cable_delete","Supprimer la définition et ses marquages, détacher les fils sans les supprimer.",new(){["id"]=S("ID câble")},["id"]),
        new("presentation_apply", "Appliquer une présentation commune en conservant les réglages individuels selon preserveIndividual.", new(){["style"]=new JsonObject{["type"]="object"}},["style"]),
        new("pdf_options_update", "Configurer le sommaire, les signets et les liens du PDF sans modifier les folios.",new(){["contents"]=B("Sommaire"),["bookmarks"]=B("Signets"),["links"]=B("Reports et bobines/contacts cliquables")},[]),
        new("template_save", "Enregistrer un gabarit réutilisable, vide ou avec schémas.",new(){["path"]=S("Chemin absolu .gemtemplate"),["includeObjects"]=B("Inclure le contenu des schémas"),["overwrite"]=B("Autoriser remplacement")},["path"]),
        new("template_open", "Créer un projet indépendant depuis un gabarit.",new(){["path"]=S("Chemin absolu .gemtemplate"),["title"]=S("Nom du nouveau projet"),["discardUnsaved"]=B("Autoriser abandon des modifications")},["path"]),
        new("recovery_list", "Lister les copies automatiques séparées des fichiers de travail.",new(),[],true),
        new("recovery_write", "Créer une copie de récupération sans écraser le fichier de travail.",new(),[]),
        new("recovery_open", "Restaurer une copie comme projet non enregistré.",new(){["path"]=S("Chemin absolu .gemrecovery"),["discardUnsaved"]=B("Autoriser abandon des modifications")},["path"]),
        new("article_fiches_list", "Rechercher les fiches exactes hors ligne avec provenance, caractéristiques, dimensions vérifiées et documents disponibles.", new() { ["query"]=S("Fabricant, référence ou famille") }, [], true),
        new("article_fiche_assign", "Affecter une fiche à un composant fonctionnel, sans modifier son bornage ou sa géométrie.", new() { ["componentId"]=S("ID composant"),["ficheId"]=S("ID de fiche") }, ["componentId","ficheId"]),
        new("numbering_preview", "Préparer la numérotation des appareils et continuités ; règles, changements et empreinte du projet.", new() { ["rules"] = new JsonObject { ["type"]="object" } }, ["rules"], true),
        new("numbering_apply", "Appliquer un aperçu de numérotation correspondant à sourceHash, sans modifier les verrous.", new() { ["rules"] = new JsonObject { ["type"]="object" },["sourceHash"]=S("Empreinte retournée par numbering_preview") }, ["rules","sourceHash"]),
        new("terminal_strips_list", "Lire les borniers, affectations et pontages déclarés.", new(), [], true),
        new("terminal_strip_put", "Créer ou remplacer un bornier, ses affectations et pontages sur des ports existants.", new() { ["strip"] = new JsonObject { ["type"]="object" } }, ["strip"]),
        new("terminal_plan_generate", "Générer ou régénérer les folios du plan de raccordement d’un bornier.", new() { ["id"]=S("ID bornier"),["style"]=S("table ou graphic") }, ["id"]),
        new("cabinets_list", "Lire les implantations d’armoire et objets en millimètres.", new(), [], true),
        new("cabinet_put", "Créer ou remplacer une implantation : dimensions connues, appareils, rails et goulottes.", new() { ["cabinet"] = new JsonObject { ["type"]="object" } }, ["cabinet"]),
        new("cabinet_plan_generate", "Créer le folio d’implantation lié à l’armoire.", new() { ["id"]=S("ID armoire") }, ["id"]),
        new("project_search", "Rechercher repères, conducteurs, références, textes et folios dans tout le projet.", new() { ["query"] = S("Texte recherché") }, ["query"], true),
        new("circuit_read", "Lire tous les conducteurs de la continuité électrique d’un fil, à travers les bornes et reports.", new() { ["id"] = S("ID du conducteur"), ["highlight"] = B("Surligner dans l’interface") }, ["id"], true),
        new("selection_align", "Aligner les centres des objets du folio actif ou les répartir régulièrement. Opération annulable.", new() { ["ids"] = new JsonObject { ["type"] = "array", ["maxItems"] = 10000, ["items"] = new JsonObject { ["type"] = "string" } }, ["operation"] = S("left, right, top, bottom, centerX, centerY, distributeX, distributeY") }, ["ids", "operation"]),
        new("wire_label_update", "Régler le placement et le style du numéro de fil ; offsets en unités du dessin, rotation null = trajet.", new() { ["id"] = S("ID conducteur"), ["placement"] = new JsonObject { ["type"] = "object" } }, ["id", "placement"]),
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
        new("component_update", "Modifier un composant du folio actif. Les fils suivent sa position et sa rotation.", new() { ["id"] = S("ID du composant"), ["x"] = N("Centre X"), ["y"] = N("Centre Y"), ["rotation"] = N("Rotation multiple de 90 degrés"), ["tag"] = S("Repère"), ["tagLocked"]=B("Conserver le repère en numérotation automatique"), ["description"] = S("Description"), ["brand"] = S("Fabricant"), ["range"] = S("Gamme"), ["reference"] = S("Référence exacte"), ["rating"] = S("Caractéristiques vérifiées"), ["relayFunction"] = S("Fonction déclarée : coil, no, nc, changeover, power ou vide"), ["portLabels"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = new JsonObject { ["type"] = "string" }, ["description"] = "Libellés des bornes, clés = IDs fixes des ports" } }, ["id"]),
        new("component_delete", "Supprimer un composant et ses connexions ; annulable.", new() { ["id"] = S("ID") }, ["id"]),
        new("coil_contact_assign", "Associer une fonction contact à une bobine, avec repère partagé ; coilId vide détache la fonction. Ne crée aucune liaison électrique.", new() { ["contactId"] = S("ID du contact"), ["coilId"] = S("ID de bobine, vide pour détacher") }, ["contactId", "coilId"]),
        new("coil_contacts_list", "Lister les fonctions bobines/contacts, leurs associations, bornes et références de folio/zone recalculées.", new(), [], true),
        new("component_text_list", "Lister tous les textes du composant, leurs clés, contenu et style, y compris bornes et textes ajoutés.", new() { ["id"] = S("ID composant") }, ["id"], true),
        new("component_text_add", "Ajouter un texte attaché au composant. Retourne sa clé extra:… ; modifier ensuite sa position avec component_text_update.", new() { ["id"] = S("ID composant"), ["text"] = S("Contenu") }, ["id", "text"]),
        new("component_text_delete", "Supprimer un texte ajouté (clé extra:…). Pour masquer un texte existant, utiliser visible=false.", new() { ["id"] = S("ID composant"), ["target"] = S("Clé extra:…") }, ["id", "target"]),
        new("component_text_align", "Aligner un texte ou tous les textes visibles sur un côté du symbole. Conserve les styles et signale les collisions restantes.", new() { ["id"] = S("ID composant"), ["target"] = S("Clé du texte ou all, défaut all"), ["side"] = S("top, bottom, left, right ou auto, défaut auto") }, ["id"]),
        new("component_text_update", "Modifier le contenu et le placement individuel. En mode custom, décalages depuis anchorPort si renseigné, sinon depuis le centre du composant.", new() { ["id"] = S("ID composant"), ["target"] = S("Clé de component_text_list : tag, detail, description, port:…, shape:… ou extra:…"), ["text"] = S("Nouveau contenu ; destination des reports en lecture seule"), ["side"] = S("top, bottom, left, right ou custom"), ["horizontal"] = S("left, center ou right"), ["vertical"] = S("top, center ou bottom"), ["gap"] = N("Distance au bord, 0..1000"), ["offsetX"] = N("Décalage horizontal précis"), ["offsetY"] = N("Décalage vertical précis"), ["visible"] = B("Afficher le texte"), ["fontSize"] = N("Taille 4..200, 0 = automatique"), ["color"] = S("Couleur #RRGGBB, vide = automatique"), ["rotation"] = N("Rotation du texte −360..360 degrés"), ["destinationInline"] = B("Report : destination sous le nom") }, ["id", "target"]),
        new("wire_update", "Modifier numéro, couleur, parcours et/ou bornes d’un conducteur. waypoints vide rétablit le tracé automatique. Opération annulable.", new() { ["id"] = S("ID conducteur"), ["number"] = S("Numéro"), ["sectionMm2"]=N("Section du fil mm²"),["clearSection"]=B("Section de fil non renseignée"),["detachCable"]=B("Détacher du câble, sans supprimer le fil"), ["numberIndependent"]=B("Numéro propre à ce conducteur, protégé des renommages communs"),["thickness"]=N("Épaisseur 0.25..10"),["lineStyle"]=S("solid, dashed, dotted ou dashdot"), ["color"] = S("Couleur #RRGGBB"), ["fromComponent"] = S("ID composant de départ"), ["fromPort"] = S("ID borne de départ"), ["toComponent"] = S("ID composant d’arrivée"), ["toPort"] = S("ID borne d’arrivée"), ["waypoints"] = new JsonObject { ["type"] = "array", ["maxItems"] = 100, ["items"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["x"] = N("X"), ["y"] = N("Y") }, ["required"] = new JsonArray("x", "y"), ["additionalProperties"] = false } } }, ["id"]),
        new("wire_add", "Relier deux bornes du folio actif. Les croisements visuels ne créent pas de jonction.", new() { ["fromComponent"] = S("ID source"), ["fromPort"] = S("ID borne source"), ["toComponent"] = S("ID destination"), ["toPort"] = S("ID borne destination"), ["number"] = S("Numéro facultatif"), ["numberIndependent"]=B("Numéro indépendant"),["color"]=S("Couleur #RRGGBB"),["thickness"]=N("Épaisseur 0.25..10"),["lineStyle"]=S("solid, dashed, dotted ou dashdot"), ["waypoints"] = new JsonObject { ["type"] = "array", ["maxItems"] = 100, ["items"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["x"] = N("X"), ["y"] = N("Y") }, ["required"] = new JsonArray("x", "y"), ["additionalProperties"] = false } } }, ["fromComponent", "fromPort", "toComponent", "toPort"]),
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
            case "table_create":double rowsValue=Num("rows"),columnsValue=Num("columns");if(rowsValue!=Math.Truncate(rowsValue)||columnsValue!=Math.Truncate(columnsValue)||rowsValue is <1 or >100||columnsValue is <1 or >40)throw new ArgumentException("Nombres entiers de lignes/colonnes requis.");var freeTable=FreeTable.Create((int)rowsValue,(int)columnsValue,a.TryGetProperty("columnWidth",out var columnWidthValue)?columnWidthValue.GetDouble():120,a.TryGetProperty("rowHeight",out var rowHeightValue)?rowHeightValue.GetDouble():40);result=e.PutGraphic(new(){Kind="table",Table=freeTable,X=Num("x"),Y=Num("y"),Width=freeTable.ColumnWidths.Sum(),Height=freeTable.RowHeights.Sum()});break;
            case "table_cell_put":double cellRow=Num("row"),cellColumn=Num("column");if(cellRow!=Math.Truncate(cellRow)||cellColumn!=Math.Truncate(cellColumn)||cellRow is <0 or >99||cellColumn is <0 or >39)throw new ArgumentException("Indices entiers de cellule requis.");e.SetTableCell(Str("id"),(int)cellRow,(int)cellColumn,a.GetProperty("content").Deserialize<RichTextContent>(ProjectStore.Json)??throw new ArgumentException("Texte absent."));result=e.Sheet.Graphics.Single(g=>g.Id==Str("id"));break;
            case "text_style":e.SetTextStyle(Str("id"),Str("textKey"),a.TryGetProperty("bold",out var boldValue)?boldValue.GetBoolean():null,a.TryGetProperty("italic",out var italicValue)?italicValue.GetBoolean():null);result=e.Sheet;break;
            case "element_position":string positionUnit=Str("unit","units");if(positionUnit is not ("units" or "percent"))throw new ArgumentException("Choisir units ou percent.");e.SetElementCoordinate(Str("id"),Str("axis"),Num("value"),positionUnit=="percent");result=e.Sheet;break;
            case "selection_align_page":e.AlignToPage(a.GetProperty("ids").Deserialize<string[]>()??[],Str("operation"));result=e.Sheet;break;
            case "page_kind":e.Change(()=>{FreeGraphics.SetKind(e.Sheet,Str("kind"));if(a.TryGetProperty("titleBlockId",out _))e.Sheet.TitleBlockId=Str("titleBlockId");});result=e.Sheet;break;
            case "graphic_put":result=e.PutGraphic(a.GetProperty("graphic").Deserialize<SheetGraphic>(ProjectStore.Json)??throw new ArgumentException("Dessin absent."));break;
            case "graphic_delete":if(!e.Sheet.Graphics.Any(g=>g.Id==Str("id")))throw new ArgumentException("Dessin absent.");e.Delete([Str("id")]);result=new{deleted=Str("id")};break;
            case "graphic_image_import":string coverImagePath=Str("path");if(!Path.IsPathFullyQualified(coverImagePath))throw new ArgumentException("Chemin absolu requis.");var imageAsset=ImageAssets.Import(coverImagePath);var bitmap=ImageAssets.Decode(imageAsset.Data);double imageWidth=a.TryGetProperty("width",out var widthValue)?widthValue.GetDouble():280;SheetGraphic? importedGraphic=null;e.Change(()=>{e.Project.Images.TryAdd(imageAsset.Id,imageAsset.Data);importedGraphic=new(){Kind="image",ImageId=imageAsset.Id,X=Editor.Snap(Num("x")),Y=Editor.Snap(Num("y")),Width=imageWidth,Height=imageWidth*bitmap.PixelHeight/bitmap.PixelWidth};e.Sheet.Graphics.Add(importedGraphic);});result=importedGraphic!;break;
            case "note_put":result=e.PutNote(a.GetProperty("note").Deserialize<Annotation>(ProjectStore.Json)??throw new ArgumentException("Texte absent."));break;
            case "projects_list":result=new{activeId=window.ActiveSessionId,projects=window.Sessions.Select(s=>new{s.Id,s.Editor.Project.Title,s.Editor.FilePath,s.Editor.Dirty,s.Editor.CanUndo,s.Editor.CanRedo})};break;
            case "project_tab_add":if(a.TryGetProperty("path",out _)){if(a.TryGetProperty("title",out _))throw new ArgumentException("Choisir path ou title.");result=new{id=window.AddProjectTab(path:PathFor(".gemelec",false)).Id};}else result=new{id=window.AddProjectTab(title:Str("title","Nouveau projet")).Id};break;
            case "project_tab_select":window.SelectProjectTab(Str("id"));result=new{activeId=window.ActiveSessionId};break;
            case "project_tab_close":window.CloseProjectTab(Str("id"),Flag("discardUnsaved"));result=new{activeId=window.ActiveSessionId};break;
            case "block_save":var block=CircuitBlocks.Extract(e.Project,e.Sheet,a.GetProperty("ids").Deserialize<string[]>()??[],Str("name"),Str("category","Circuits"),Str("tags"));string blockPath=PathFor(".gemblock",true);CircuitBlocks.Save(block,blockPath);result=new{path=blockPath,block.DetachedConnections};break;
            case "block_insert":result=new{ids=e.InsertBlock(CircuitBlocks.Load(PathFor(".gemblock",false)),new(Num("x"),Num("y")))};break;
            case "personal_library_list":result=PersonalLibrary.List().Where(entry=>Symbols.SearchKey(entry.Name+" "+entry.Category+" "+entry.Tags).Contains(Symbols.SearchKey(Str("query")))).ToList();break;
            case "personal_library_put":var favorite=a.GetProperty("entry").Deserialize<PersonalEntry>(ProjectStore.Json)??throw new ArgumentException("Favori absent.");PersonalLibrary.Save(favorite);window.ReloadLibraryFromMcp();result=favorite;break;
            case "personal_library_delete":PersonalLibrary.Delete(Str("id"));window.ReloadLibraryFromMcp();result=new{deleted=Str("id")};break;
            case "personal_library_place":result=new{ids=window.PlaceLibraryFromMcp(Str("id"),new(Num("x"),Num("y")))};break;
            case "view_reading":window.Canvas.SetReading(a.GetProperty("options").Deserialize<ReadingOptions>(ProjectStore.Json)??throw new ArgumentException("Réglages absents."));result=window.Canvas.Reading;break;
            case "view_zoom_selection":var selectedIds=a.GetProperty("ids").Deserialize<string[]>()??[];var available=e.Sheet.Components.Select(c=>c.Id).Concat(e.Sheet.Wires.Select(w=>w.Id)).Concat(e.Sheet.Notes.Select(n=>n.Id)).Concat(e.Sheet.Graphics.Select(g=>g.Id)).ToHashSet();if(selectedIds.Length==0||selectedIds.Any(id=>!available.Contains(id)))throw new ArgumentException("Sélection absente du folio.");window.Canvas.Selected.Clear();foreach(var id in selectedIds)window.Canvas.Selected.Add(id);window.Canvas.ZoomSelection();result=new{window.Canvas.Zoom};break;
            case "wire_route_smart":result=new{routed=e.RouteWires(a.GetProperty("ids").Deserialize<string[]>()??[])};break;
            case "wire_insert_component":result=e.InsertOnWire(Str("id"),Str("symbol"),new(Num("x"),Num("y")),Str("inputPort"),Str("outputPort"));break;
            case "terminal_line_create":var terminalA=a.GetProperty("a").Deserialize<Position>(ProjectStore.Json)??throw new ArgumentException("Point absent.");var terminalB=a.GetProperty("b").Deserialize<Position>(ProjectStore.Json)??throw new ArgumentException("Point absent.");result=e.InsertTerminalLine(terminalA.Point,terminalB.Point,Str("name"),a.TryGetProperty("first",out var firstNumber)?firstNumber.GetInt32():1,a.TryGetProperty("level",out var terminalLevel)?terminalLevel.GetInt32():1);break;
            case "cable_line_create":var cableA=a.GetProperty("a").Deserialize<Position>(ProjectStore.Json)??throw new ArgumentException("Point absent.");var cableB=a.GetProperty("b").Deserialize<Position>(ProjectStore.Json)??throw new ArgumentException("Point absent.");result=e.MarkCableLine(cableA.Point,cableB.Point,Str("name"),a.TryGetProperty("sectionMm2",out var sectionValue)?sectionValue.GetDouble():null,Str("type"));break;
            case "cables_list":result=new{definitions=e.Project.Cables,assignments=e.Project.Sheets.SelectMany(s=>s.Wires.Where(w=>w.CableId.Length>0).Select(w=>new{s.Id,wireId=w.Id,w.CableId,w.CableCore,w.SectionMm2})),markers=e.Project.Sheets.SelectMany(s=>s.CableMarkers)};break;
            case "cable_put":result=e.PutCable(a.GetProperty("cable").Deserialize<CableDefinition>(ProjectStore.Json)??throw new ArgumentException("Câble absent."));break;
            case "cable_delete":e.DeleteCable(Str("id"));result=new{deleted=Str("id")};break;
            case "presentation_apply":var presentation=a.GetProperty("style").Deserialize<ProjectPresentation>(ProjectStore.Json)??throw new ArgumentException("Présentation absente.");e.Change(()=>Presentation.Apply(e.Project,presentation));result=Snapshot();break;
            case "pdf_options_update":e.Change(()=>{if(a.TryGetProperty("contents",out var contents))e.Project.PdfOptions.Contents=contents.GetBoolean();if(a.TryGetProperty("bookmarks",out var bookmarks))e.Project.PdfOptions.Bookmarks=bookmarks.GetBoolean();if(a.TryGetProperty("links",out var links))e.Project.PdfOptions.Links=links.GetBoolean();});result=e.Project.PdfOptions;break;
            case "template_save":var templatePath=PathFor(".gemtemplate",true);ProjectTemplates.Save(e.Project,templatePath,Flag("includeObjects"),window.TemplatePreferences);result=new{path=templatePath};break;
            case "template_open":var projectTemplate=ProjectTemplates.Load(PathFor(".gemtemplate",false));var createdTemplate=ProjectTemplates.Instantiate(projectTemplate.Project,Str("title",projectTemplate.Project.Title),projectTemplate.IncludeObjects);ReplaceCheck();window.ApplyTemplatePalette(projectTemplate.Palette);e.ImportProject(createdTemplate);result=Snapshot();break;
            case "recovery_list":result=RecoveryStore.List();break;
            case "recovery_write":result=window.SaveRecoveryAsync();break;
            case "recovery_open":var recovered=RecoveryStore.Read(PathFor(".gemrecovery",false));ReplaceCheck();e.ImportProject(recovered.Project);if(e.Project.Sheets.Any(s=>s.Id==recovered.Info.SheetId))e.SelectSheet(recovered.Info.SheetId);result=Snapshot();break;
            case "article_fiches_list":result=ArticleFiches.Search(Str("query")).ToArray();break;
            case "article_fiche_assign":e.AssignArticleFiche(Str("componentId"),Str("ficheId"));result=Snapshot();break;
            case "numbering_preview": result=AutoNumbering.Preview(e.Project,a.GetProperty("rules").Deserialize<NumberingRules>(ProjectStore.Json)??throw new ArgumentException("Règles absentes."));break;
            case "numbering_apply": var numbering=AutoNumbering.Preview(e.Project,a.GetProperty("rules").Deserialize<NumberingRules>(ProjectStore.Json)??throw new ArgumentException("Règles absentes."));if(numbering.SourceHash!=Str("sourceHash"))throw new InvalidOperationException("Aperçu périmé : relancer numbering_preview.");e.ApplyNumbering(numbering);result=numbering;break;
            case "terminal_strips_list":result=e.Project.TerminalStrips.Select(s=>new{strip=s,rows=TerminalStrips.Rows(e.Project,s)}).ToArray();break;
            case "terminal_strip_put":result=e.PutTerminalStrip(a.GetProperty("strip").Deserialize<TerminalStrip>(ProjectStore.Json)??throw new ArgumentException("Bornier absent."));break;
            case "terminal_plan_generate":result=e.GenerateTerminalPlan(Str("id"),Str("style","table"));break;
            case "cabinets_list":result=e.Project.Cabinets.Select(c=>new{cabinet=c,findings=Cabinets.Findings(e.Project,c)}).ToArray();break;
            case "cabinet_put":result=e.PutCabinet(a.GetProperty("cabinet").Deserialize<CabinetLayout>(ProjectStore.Json)??throw new ArgumentException("Armoire absente."));break;
            case "cabinet_plan_generate":result=e.GenerateCabinetPlan(Str("id"));break;
            case "project_search": result = ProjectNavigation.Search(e.Project, Str("query")); break;
            case "circuit_read": result = ProjectNavigation.Circuit(e.Project, Str("id")); if (Flag("highlight")) window.Canvas.HighlightCircuit(Str("id")); break;
            case "selection_align": e.AlignSelection(a.GetProperty("ids").Deserialize<string[]>() ?? [], Str("operation")); result = Snapshot(); break;
            case "wire_label_update": var labelPlacement = a.GetProperty("placement").Deserialize<WireNumberPlacement>(ProjectStore.Json) ?? throw new ArgumentException("Placement absent."); e.Change(() => e.Sheet.Wires.Single(w => w.Id == Str("id")).NumberPlacement = labelPlacement); result = labelPlacement; break;
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
            case "wire_segment_move": var movingWire = e.Sheet.Wires.Single(w => w.Id == Str("id")); var route = movingWire.Route(e.Sheet); int seg = a.GetProperty("segment").GetInt32(); if (seg < 1 || seg >= route.Count) throw new ArgumentException("Segment absent."); e.Change(() => { movingWire.RoutingMode="manual";movingWire.RoutingWarning="";Junctions.ShiftSegment(e.Sheet, movingWire.Id, route, seg, new(Num("x"), Num("y"))); }); result = movingWire; break;
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
            case "project_save":string savePath=PathFor(".gemelec", true);window.CheckProjectSavePath(savePath); e.Save(savePath); result = new { path = e.FilePath, saved = true }; break;
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
                e.Change(() => { if (a.TryGetProperty("relayFunction", out var role)) e.SetRelayFunction(c, role.GetString()!); if (a.TryGetProperty("x", out var x)) c.X = Editor.Snap(x.GetDouble()); if (a.TryGetProperty("y", out var y)) c.Y = Editor.Snap(y.GetDouble()); if (a.TryGetProperty("rotation", out var r)) c.Rotation = r.GetInt32(); e.SetApparatusTag(c, Str("tag", c.Tag));if(a.TryGetProperty("tagLocked",out var tagLocked))c.TagLocked=tagLocked.GetBoolean(); c.Description = Str("description", c.Description); c.Brand = Str("brand", c.Brand); c.Range = Str("range", c.Range); c.Reference = Str("reference", c.Reference); c.Rating = Str("rating", c.Rating); if (a.TryGetProperty("portLabels", out var labels)) foreach (var label in labels.EnumerateObject()) { if (!c.Definition.Ports.Any(p => p.Id == label.Name) || label.Value.ValueKind != JsonValueKind.String) throw new ArgumentException("Borne ou libellé inconnu."); c.PortLabels[label.Name] = label.Value.GetString()!; } }); result = c; break;
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
                    if (a.TryGetProperty("numberIndependent", out var independentNumber)) e.ConfigureIndependentNumber(editedWire, independentNumber.GetBoolean());
                    Endpoint nextFrom = new(Str("fromComponent", editedWire.From.ComponentId), Str("fromPort", editedWire.From.PortId)), nextTo = new(Str("toComponent", editedWire.To.ComponentId), Str("toPort", editedWire.To.PortId));
                    bool reconnecting = nextFrom != editedWire.From || nextTo != editedWire.To, explicitNumber = a.TryGetProperty("number", out _);
                    string number = explicitNumber ? Str("number") : editedWire.Number;
                    if (reconnecting && !explicitNumber && !editedWire.NumberIndependent) { var destination = nextFrom != editedWire.From ? nextFrom : nextTo; number = new ConductorNetworks(e.Project).ExistingNumber(destination, destination == nextFrom ? nextTo : nextFrom, editedWire.Id) ?? number; }
                    editedWire.Color = Str("color", editedWire.Color); editedWire.From = nextFrom; editedWire.To = nextTo;
                    if(a.TryGetProperty("sectionMm2",out var wireSection))editedWire.SectionMm2=wireSection.GetDouble();if(Flag("clearSection"))editedWire.SectionMm2=null;if(Flag("detachCable")){editedWire.CableId="";editedWire.CableCore="";}
                    if (a.TryGetProperty("thickness", out var thickness)) editedWire.Thickness = thickness.GetDouble(); editedWire.LineStyle = Str("lineStyle", editedWire.LineStyle);
                    if (a.TryGetProperty("waypoints", out var routePoints)) {editedWire.Waypoints = JsonSerializer.Deserialize<List<Position>>(routePoints, ProjectStore.Json) ?? throw new ArgumentException("Parcours invalide.");editedWire.RoutingMode="manual";editedWire.RoutingWarning="";}
                    if (explicitNumber || reconnecting) e.SynchronizeWireNumbers(editedWire, number);
                }); result = editedWire; break;
            case "wire_add": List<Position>? pts = a.TryGetProperty("waypoints", out var points) ? JsonSerializer.Deserialize<List<Position>>(points, ProjectStore.Json) : null; result = e.Connect(new(Str("fromComponent"), Str("fromPort")), new(Str("toComponent"), Str("toPort")), pts, Str("number"), a.TryGetProperty("color", out _) ? Str("color") : null, a.TryGetProperty("numberIndependent", out var wireIndependent) && wireIndependent.GetBoolean(), a.TryGetProperty("thickness", out var wireThickness) ? wireThickness.GetDouble() : 1.6, Str("lineStyle", "solid")); break;
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
