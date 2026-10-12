# MCP GeMMeElec

Le serveur est intégré au logiciel : **`GeMMeElec.exe --mcp`**. Il utilise JSON-RPC 2.0 sur stdin/stdout UTF-8, un objet par ligne. L’interface doit être ouverte pour exécuter les outils ; initialize et tools/list restent disponibles sans fenêtre.

## Configuration Codex

Le dépôt contient `.codex/config.toml`, limité à ce projet :

```toml
[mcp_servers.gemmeelec]
command = 'C:\dev\QElectroTech_GeMMe\build\GeMMeElec.exe'
args = ["--mcp"]
cwd = 'C:\dev\QElectroTech_GeMMe'
startup_timeout_sec = 20
tool_timeout_sec = 30
enabled = true
```

Ouvrir ce dépôt dans Codex comme projet approuvé, puis recharger la session/MCP si nécessaire. Ouvrir le logiciel. Les outils apparaissent sous le serveur `gemmeelec` une fois la configuration chargée ; `/mcp` permet de vérifier la connexion. La configuration n’ajoute pas automatiquement des outils à une conversation déjà en cours.

Le fichier de projet et les champs command/args/cwd suivent la [documentation officielle MCP de Codex](https://learn.chatgpt.com/docs/extend/mcp?surface=cli). Aucun réglage global ni politique d’approbation n’est modifié. Si le dépôt est déplacé, adapter les chemins.

À la livraison, le serveur est testé directement sur stdio. La commande `codex mcp get gemmeelec` de la session existante n’a pas chargé la configuration locale : l’activation dans le client reste à vérifier après réouverture du projet. Si nécessaire, utiliser **Paramètres → MCP servers → Add server → STDIO**, avec la commande et l’argument ci-dessus, puis redémarrer le serveur. Le logiciel et son MCP sont utilisables indépendamment de cette activation.

Pour un autre client MCP acceptant les serveurs locaux stdio :

```json
{
  "mcpServers": {
    "gemmeelec": {
      "command": "C:\\dev\\QElectroTech_GeMMe\\build\\GeMMeElec.exe",
      "args": ["--mcp"]
    }
  }
}
```

## Outils

| Domaine | Outils |
| --- | --- |
| Lecture | `project_get`, `library_search`, `project_validate` |
| Projet | `project_new`, `project_open`, `project_save`, `project_update` |
| Folios | `page_add`, `page_select`, `page_update` |
| Composants | `component_add`, `component_update`, `component_text_update`, `component_delete` |
| Conducteurs | `wire_add`, `wire_update`, `wire_delete` |
| Annotation | `note_add` |
| Exports | `export_svg`, `export_pdf`, `export_bom` |
| Historique et vue | `history_undo`, `history_redo`, `view_fit` |
| Documents (0.3) | `page_move`, `titleblock_list`, `titleblock_put`, `component_image`, `bom_configure`, `export_bom_html` |
| Dérivations et folios BOM (0.4) | `wire_branch`, `bom_generate` |
| Renvois (0.5) | `reference_list`, `reference_update`, `connection_trace` |
| Symboles, import et routage (0.6) | `symbol_get`, `symbol_copy`, `symbol_import`, `symbol_export`, `project_import`, `wire_segment_move` |

`tools/list` fournit les paramètres, types et champs requis. Les retours de `tools/call` sont des contenus texte JSON ; une erreur d’opération renvoie `isError: true`.

La version 0.7 conserve les 41 outils. `component_text_update` prend `id`, `target` (`tag`, `detail`, `description`) et les réglages facultatifs `side` (`top`, `bottom`, `left`, `right`, `custom`), `horizontal` (`left`, `center`, `right`), `vertical` (`top`, `center`, `bottom`), `gap`, `offsetX`, `offsetY`, `visible`. Les décalages sont en unités de dessin, sans arrondi. `custom` ancre au centre du composant ; les autres positions ancrent au bord choisi. Les alignements indiquent quel point du texte rejoint cette ancre. Pour les reports en ligne, le placement du nom ancre l’ensemble nom/destination ; la visibilité de la destination reste indépendante.

## Symboles et imports (0.6)

- `symbol_get` prend `id` et retourne `id`, `name`, `prefix`, `width`, `height`, `category`, `ports` et `shapes`. Une borne contient `id`, `x`, `y`. Une primitive contient `kind` (line/rect/circle/dot/text), `x`, `y`, `a`, `b`, `text`, `rotation`. Pour une ligne A/B sont le second point ; pour un rectangle les dimensions ; pour un cercle A est le rayon ; pour un texte A est la taille.
- `symbol_copy` prend `definition` complet et `componentId` facultatif. Il crée toujours un nouvel ID. Le composant ne peut être remplacé si une borne raccordée disparaît. Les originaux restent intacts ; l’opération est annulable.
- `symbol_import` prend `path` absolu `.dxf` ou `.gemsymbol` et `scale` facultatif (défaut 1). Il retourne la copie enregistrée, les unités et avertissements. Un DXF n’a pas de bornes : les ajouter à une copie avant câblage.
- `symbol_export` prend `id`, `path` absolu `.gemsymbol`, `overwrite` facultatif. Il exporte le dessin et ses bornes sans dépendre du projet.
- `project_import` prend `path` absolu `.qet` ou `.dxf`. Par défaut il analyse et retourne le rapport sans modifier le projet. `apply: true` ouvre la conversion comme nouveau projet non enregistré ; `discardUnsaved` protège le projet courant. Les formats `.elk`, `.zw1`, `.epj` sont refusés explicitement. Un DXF est graphique, sans reconstruction électrique.
- `wire_segment_move` prend `id`, `segment` (à partir de 1 dans le tracé orthogonal), `x`, `y`. Il déplace le segment et les nœuds automatiques situés à ses extrémités. Les bornes d’appareils restent fixes ; l’opération est annulable.

`library_search` inclut les copies du projet. `project_get` expose `customSymbols` ; chaque composant les référence par son champ `symbol`. Les définitions sont enregistrées avec le document, sans modifier la bibliothèque intégrée. Les points de jonction sont calculés au rendu et ne constituent pas des articles BOM.

## Zones et renvois (0.5)

- `page_update` accepte `zoneColumns` (1–40) et `zoneRows` (1–26) pour le folio actif. Les valeurs par défaut sont 20 et 10. La colonne BOM `pages` contient maintenant le repère, le folio et la zone de chaque appareil.
- `library_search` retourne également la catégorie de chaque symbole. Poser les flèches avec `component_add` et `symbol: "folio_next"` ou `"folio_previous"` ; leur borne porte l’ID `1`.
- `reference_update` reçoit `id`, `name`, `mode` (`pair` ou `star`), et éventuellement `order` (0–99999, défaut 0) et `source` (défaut false). Il modifie le renvoi du folio actif, harmonise le mode des points de même nom et garantit une seule source explicite. L’opération est annulable ; un nom vide délie le point.
- `reference_list` retourne tous les renvois avec leur adresse et leurs destinations résolues. Les noms sont comparés sans tenir compte de la casse ni des espaces en début/fin.
- `connection_trace` reçoit `componentId` et `portId`. Cette lecture retourne les bornes accessibles en suivant les fils et les renvois résolus, sans traverser l’intérieur des appareils. Il ne s’agit pas d’une simulation électrique.

Les coordonnées affichées se recalculent après déplacement, changement de numéro ou réorganisation des folios. Les folios BOM restent des instantanés à régénérer avec `bom_generate`. Voir [les règles de liaison](RENVOIS.md).

`wire_branch` reçoit `fromComponent`, `fromPort`, `targetWire`, `x`, `y` sur le folio actif. Le point est projeté sur le segment le plus proche et aligné le long de ce segment. Une position à plus de 100 unités est refusée. Le fil cible est divisé sans perte de son tracé, numéro ou couleur ; la nouvelle branche arrive perpendiculairement à une jonction commune. Si le point est déjà une extrémité, celle-ci est réutilisée. L’ensemble est annulable en une action.

`bom_generate` n’a pas de paramètres : il utilise `project.bomOptions`, remplace les folios BOM existants et sélectionne le premier nouveau folio. Les folios sont enregistrés dans `sheet.bomPage`, sans cartouche, et inclus dans `export_pdf`. Ils sont des instantanés : relancer après modification du schéma. Les composants et annotations y sont interdits. La limite de 100 folios s’applique avec rejet atomique en cas de dépassement.

`wire_update` prend `id` et les champs facultatifs `number`, `color`, `fromComponent`, `fromPort`, `toComponent`, `toPort`, `waypoints` (tableau de `{ "x": 320, "y": 250 }`). Un tableau vide rétablit le tracé automatique. Les mises à jour de texte et de conducteur sont atomiques, validées et annulables ; un raccordement dupliqué est refusé.

## Cartouches, métadonnées et BOM (0.3)

- `project_update` accepte aussi `customer`, `documentNumber`, `date`, `customFields` (dictionnaire de textes, remplace les champs personnalisés).
- `page_update` accepte les champs facultatifs `title`, `number`, `author`, `checkedBy`, `approvedBy`, `date`, `revision`, `location`, `function`, `titleBlockId`, `customFields`. Auteur/date/révision vides héritent du projet. `number` vide utilise l’ordre courant ; `titleBlockId` vide masque le cartouche.
- `page_move` déplace le folio actif à `index`, de 1 au nombre de folios.
- `titleblock_list` retourne les modèles, les champs disponibles et les valeurs résolues du folio courant.
- `titleblock_put` reçoit `template` : `id`, `name`, `height`, `cells`. Chaque cellule comprend `x`, `y`, `width`, `height` en pourcentage, `text`, `fontSize`, `align` (left/center/right), `vertical` (top/center/bottom), `border`, `imageId`. L’ID existant est remplacé, sinon ajouté. Utiliser des champs comme `{sheet.title}` dans `text`. Les folios sont affectés avec `page_update`.
- `component_image` reçoit `id` et `path`, chemin absolu PNG/JPEG/BMP. Il retourne l’`imageId` réutilisable dans une cellule de cartouche. Un chemin vide retire l’image du composant. Aucun téléchargement réseau.
- `bom_configure` reçoit `options` complet : `columns` (liste ordonnée de `{key,label,enabled}`), `groupArticles`, `includeAuxiliary`, `sheetId` (vide = tous), `brand` (vide = tous). Clés : quantity, image, tags, pages, description, brand, range, reference, rating, symbol, catalog, source.
- `export_bom_html` reçoit `path` absolu `.html` et `overwrite` facultatif. Il utilise les options du projet et intègre les images. `export_bom` utilise les mêmes options pour le CSV, avec description textuelle de l’image.

Les limites sont contrôlées par le modèle commun : 100 cartouches, 300 cellules par modèle, hauteur de 20 à 500 unités, cellules dans 0–100 %, 200 images et 12 Mo encodés au total. Les imports d’images sont normalisés à 512 pixels et dédupliqués. Toutes les modifications passent par l’historique.

## Procédure conseillée

1. Lire `project_get` et identifier le folio actif.
2. Rechercher un équipement avec `library_search` et récupérer le `catalogId` ou un ID de symbole.
3. Ajouter les composants. Les coordonnées correspondent au centre du symbole ; le folio par défaut mesure 1400 × 990 unités, grille 10.
4. Relire `project_get` : `ports` donne les IDs stables, libellés et coordonnées absolues des bornes.
5. Utiliser `wire_add` avec les IDs de composants et de bornes. Un changement de libellé ne modifie pas l’ID de connexion.
6. Renseigner les références et caractéristiques vérifiées, appeler `project_validate`, enregistrer puis exporter.

Exemple de paramètres pour une pose :

```json
{"catalogId":"f06-1","x":400,"y":250}
```

Exemple de connexion, en remplaçant les IDs par ceux retournés :

```json
{"fromComponent":"id-Q1","fromPort":"2","toComponent":"id-KM1","toPort":"1","number":"101"}
```

## Comportement local

Le transport entre passerelle et fenêtre utilise un named pipe Windows `CurrentUserOnly`, un nom aléatoire et un jeton de session. Le fichier `build/GeMMeElec.session.json` est créé au démarrage, retiré à la fermeture et ignoré par Git. Aucun port réseau n’est ouvert et aucun shell n’est exposé par les outils.

Les opérations sont exécutées sur le thread de l’interface et utilisent le même historique que la souris. Le MCP refuse les modifications pendant un déplacement ou lorsqu’une boîte de dialogue est ouverte. Les erreurs structurelles restaurent le projet précédent. `project_new` et `project_open` refusent un projet modifié sans `discardUnsaved: true`. Les exports et sauvegardes vers un autre fichier existant exigent `overwrite: true`. Le client reste responsable de l’autorisation de l’utilisateur pour ces options explicites.

Les versions négociées sont `2024-11-05`, `2025-03-26`, `2025-06-18` et `2025-11-25`. Le serveur n’annonce que la capacité tools ; il n’implémente pas de ressources, prompts, abonnement ou accès distant. Spécification de référence : [MCP tools](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2025-11-25/server/tools.mdx).

## Diagnostic

- « Ouvrir GeMMeElec » : démarrer le logiciel depuis le même dossier que le serveur.
- Connexion expirée : vérifier que la fenêtre répond et la relancer si nécessaire.
- « Modifications non enregistrées » : enregistrer le projet avant de l’ouvrir ou le remplacer.
- « Folio actif » : appeler `page_select` avec l’ID voulu.
- « Le fichier existe déjà » : choisir un autre chemin ou autoriser explicitement l’écrasement.

`Test-Mcp.ps1 -Headless` utilise le vrai transport stdio et une instance WPF invisible, séparée de la session utilisateur. Le mode de test a son propre fichier de session et s’arrête après les essais (au plus trois minutes). Aucun contrôle souris/clavier. Sans `-Headless`, le test utilise la fenêtre réelle et protège le projet courant.
