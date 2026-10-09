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
| Composants | `component_add`, `component_update`, `component_delete` |
| Conducteurs | `wire_add`, `wire_delete` |
| Annotation | `note_add` |
| Exports | `export_svg`, `export_pdf`, `export_bom` |
| Historique et vue | `history_undo`, `history_redo`, `view_fit` |

`tools/list` fournit les paramètres, types et champs requis. Les retours de `tools/call` sont des contenus texte JSON ; une erreur d’opération renvoie `isError: true`.

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

Le test `Test-Mcp.ps1` utilise le vrai transport stdio et la fenêtre réelle ; il ne simule pas les appels du serveur.
