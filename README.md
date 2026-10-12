# GeMMeElec

Atelier Windows de conception de schémas électriques industriels, réécrit en **C# / WPF / .NET 10**. Branche `GeMMeElec`, créée depuis `main`. Les anciens fichiers suivis ont été retirés de cette branche ; l’historique Git et la branche `EPLAN_DESIGN` sont conservés. Aucun code QElectroTech n’est utilisé par cette nouvelle application.

## Démarrer

Ouvrir **`C:\dev\QElectroTech_GeMMe\build\GeMMeElec.exe`**. L’exécutable Windows x64 embarque le runtime et la bibliothèque ; aucune installation de Qt ou de .NET n’est nécessaire pour l’utiliser. Le logo fourni est intégré.

Le dossier contenant l’exécutable doit être accessible en écriture pour le fichier de session MCP et les journaux. Une seule fenêtre d’application est ouverte par compte Windows. Le même exécutable, lancé avec `--mcp`, sert de passerelle MCP sans ouvrir une deuxième interface.

## Fonctionnalités (version 0.2)

- Projets JSON `.gemelec`, sauvegarde atomique, ouverture et contrôle de structure.
- Folios A3 paysage : ajout, renommage, suppression annulable, cartouche projet/auteur/révision.
- 34 symboles vectoriels originaux ; bibliothèque de 75 associations famille/fabricant/gamme dans 25 familles, trois fabricants par famille.
- Recherche, filtres et aperçu des symboles ; placement répété sur grille.
- Déplacement, sélection multiple, duplication des composants, rotation, suppression.
- Connexions entre bornes, tracé orthogonal, coudes manuels, repères de conducteurs, suivi des déplacements.
- Câblage par glisser entre bornes, déplacement des segments et changement de borne par poignées, coordonnées des points de passage.
- Position indépendante du repère, des caractéristiques et de la description : côtés, alignements, décalages précis et visibilité ; rendu commun à l’écran, au SVG et au PDF.
- Panneaux Bibliothèque, Folios et Propriétés épinglables, masquables automatiquement et fermables ; largeur réglable et disposition mémorisée.
- Repères et propriétés des appareils, référence commerciale libre, caractéristiques, libellés de bornes, documentation fabricant.
- Annotations, zoom à la molette, déplacement de la vue, ajustement du folio.
- Historique commun à l’interface et au MCP : 100 annulations/rétablissements.
- Vérifications de structure, repères dupliqués ou absents, bornes libres, références à compléter, débordements des symboles.
- Export du folio en SVG, du projet multipage en PDF vectoriel A3, nomenclature CSV UTF-8.
- Serveur MCP local avec 24 outils, schémas d’arguments, retour d’erreurs et protection des modifications non enregistrées.

## Dessiner

Cliquer un équipement dans la bibliothèque puis cliquer le folio. Échap termine la pose. Pour câbler, maintenir le bouton gauche sur une borne, tirer vers une autre borne et relâcher : cela fonctionne en mode Sélection et immédiatement après la pose. Une borne survolée se colore. Un relâchement dans le vide annule la liaison. Le mode Conducteur permet aussi de cliquer une première borne, des points de passage facultatifs, puis la borne d’arrivée. Les croisements n’établissent pas de liaison électrique : utiliser un symbole **Jonction électrique** pour créer une dérivation.

Sélectionner un conducteur puis glisser un segment ou une **poignée carrée** pour ajuster son tracé. Glisser une **poignée ronde** vers une borne pour changer son raccordement. L’inspecteur permet aussi de saisir les points `X ; Y`, un par ligne, et de rétablir le tracé automatique. Échap annule le geste ; Ctrl+Z annule sa validation. Pour créer un nouveau fil depuis une borne occupée par le fil sélectionné, activer Conducteur (W).

Sélectionner un composant puis **Propriétés → Position des textes…**. Choisir le repère, les caractéristiques ou la description ; régler haut/bas/gauche/droite, alignement horizontal et vertical, distance et décalages X/Y. Le mode **Coordonnées libres depuis le centre** utilise le centre du composant comme origine. Les décimales acceptent une virgule ou un point, sans arrondi à la grille. Appliquer avant de changer de texte ; les positions suivent le composant et sont enregistrées avec le projet.

Dans les en-têtes des panneaux, **● / ○** épingle ou désépingle ; **×** ferme. Un panneau désépinglé reste accessible par son onglet latéral et se replie après une action sur le folio. Le menu **Affichage** rouvre les panneaux ou rétablit la disposition initiale. Les séparateurs règlent leur largeur. La disposition est enregistrée dans `build/GeMMeElec.layout.json`.

| Action | Raccourci |
| --- | --- |
| Sélection / conducteur / texte | V / W / T |
| Rotation / ajuster le folio | R / F |
| Déplacer un symbole | Glisser à la souris |
| Ajouter à la sélection | Ctrl+clic |
| Zoom / déplacement de la vue | Molette / bouton central |
| Enregistrer / enregistrer sous | Ctrl+S / Ctrl+Maj+S |
| Ouvrir / nouveau projet | Ctrl+O / Ctrl+N |
| Annuler / rétablir | Ctrl+Z / Ctrl+Y |
| Dupliquer / supprimer | Ctrl+D / Suppr |

Les valeurs de l’inspecteur sont enregistrées dans le projet après **Appliquer les propriétés**. Le menu Fichier propose un exemple de départ moteur, explicitement incomplet côté commande.

## Bibliothèque et périmètre

Les fabricants sont une sélection industrielle pertinente en Europe, **pas un classement de parts de marché**. Le catalogue contient des **gammes**, pas toutes leurs références commandables. Les symboles fonctionnels sont indépendants du fabricant ; les blocs PLC, E/S, variateur et sécurité ne reproduisent pas tout le bornage d’un appareil réel. Choisir la référence, vérifier sa documentation et compléter les propriétés avant fabrication. Voir [le catalogue documenté](docs/CATALOGUE.md).

Cette base ne comprend pas encore le dimensionnement électrique, la simulation, les renvois interfolios, les borniers automatiques, l’import QET/EPLAN, la gestion de câbles multicœurs, les implantations d’armoires, ni les catalogues complets des fabricants. Elle ne revendique pas une certification IEC/RGIE ou une équivalence à EPLAN.

## Compiler et vérifier

Depuis le dépôt, sous PowerShell 7 :

```powershell
./Build.ps1 -Test
```

Le script installe si nécessaire le SDK officiel .NET 10.0.401 dans `build/dotnet`, restaure les dépendances sous `build/packages`, puis publie **le même chemin `build/GeMMeElec.exe`** à chaque compilation. Fermer GeMMeElec avant de le recompiler. Les outils, caches et résultats de compilation ne sont pas versionnés.

Le choix .NET 10/WPF permet un éditeur Windows natif, un rendu vectoriel et un exécutable autonome ; [SDK officiel .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

Les tests intégrés écrivent `build/self-test.json`. Pour le test d’intégration MCP, ouvrir GeMMeElec sur un projet vierge puis exécuter :

```powershell
./Test-Mcp.ps1
```

Le test crée un circuit de commande dans la fenêtre, vérifie les refus d’entrées invalides et la protection des fichiers, puis écrit `build/mcp-test.json`, un projet et ses exports. Il refuse de remplacer un autre projet non vide ou non enregistré. Il peut reprendre son propre fichier de test enregistré.

## Structure

| Fichier | Rôle |
| --- | --- |
| `src/Model.cs` | Projet, validation structurelle, transactions, historique |
| `src/Symbols.cs` | Géométrie et bornes des symboles |
| `src/Drawing.cs` | Scène vectorielle partagée |
| `src/SheetCanvas.cs` | Interaction du dessinateur |
| `src/MainWindow.cs` | Interface, inspecteur, commandes et exemple |
| `src/WorkspacePanels.cs` | Panneaux, masquage automatique et disposition persistante |
| `src/Export.cs` | SVG, PDF, nomenclature |
| `src/Mcp.cs` | MCP stdio et canal local vers l’interface |
| `library/catalog.json` | Gammes et sources officielles |

Voir [la configuration MCP](docs/MCP.md) et [les vérifications de livraison](docs/VALIDATION.md).

L’ancien dossier de compilation QElectroTech est encore présent localement : sa suppression a été rejetée par le contrôle automatique (« blocked by policy »). Il reste ignoré par Git et n’est pas utilisé par GeMMeElec.
