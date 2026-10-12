# GeMMeElec

Atelier Windows de conception de schémas électriques industriels, réécrit en **C# / WPF / .NET 10**. Branche `GeMMeElec`, créée depuis `main`. Les anciens fichiers suivis ont été retirés de cette branche ; l’historique Git et la branche `EPLAN_DESIGN` sont conservés. Aucun code QElectroTech n’est utilisé par cette nouvelle application.

## Démarrer

Ouvrir **`C:\dev\QElectroTech_GeMMe\build\GeMMeElec.exe`**. L’exécutable Windows x64 embarque le runtime et la bibliothèque ; aucune installation de Qt ou de .NET n’est nécessaire pour l’utiliser. Le logo fourni est intégré.

Le dossier contenant l’exécutable doit être accessible en écriture pour le fichier de session MCP et les journaux. Une seule fenêtre d’application est ouverte par compte Windows. Le même exécutable, lancé avec `--mcp`, sert de passerelle MCP sans ouvrir une deuxième interface.

## Fonctionnalités (version 0.3)

- Projets JSON `.gemelec`, sauvegarde atomique, ouverture et contrôle de structure.
- Folios A3 paysage : ajout, renommage, suppression annulable, cartouche projet/auteur/révision.
- 34 symboles vectoriels originaux ; bibliothèque de 75 associations famille/fabricant/gamme dans 25 familles, trois fabricants par famille.
- Recherche, filtres et aperçu des symboles ; placement répété sur grille.
- Déplacement, sélection multiple, duplication des composants, rotation, suppression.
- Connexions entre bornes, tracé orthogonal, coudes manuels, repères de conducteurs, suivi des déplacements.
- Câblage par glisser entre bornes, déplacement des segments et changement de borne par poignées, coordonnées des points de passage.
- Position indépendante du repère, des caractéristiques et de la description : côtés, alignements, décalages précis et visibilité ; rendu commun à l’écran, au SVG et au PDF.
- Panneaux Bibliothèque, Folios et Propriétés épinglables, masquables automatiquement et fermables ; largeur réglable et disposition mémorisée.
- Cartouches personnalisables : cellules, dimensions, texte multiligne, alignements, logo, champs liés, modèles Simple et Technique, import/export `.gemcartouche`.
- Propriétés détaillées des folios : numéro automatique ou libre, titre, auteur, vérificateur, approbateur, date, révision, fonction, localisation et champs personnalisés. Ordre modifiable.
- BOM configurable : choix et ordre des colonnes, regroupement, filtres folio/fabricant, photos intégrées ou symboles ; export HTML autonome imprimable et CSV.
- Repères et propriétés des appareils, référence commerciale libre, caractéristiques, libellés de bornes, documentation fabricant.
- Annotations, zoom à la molette, déplacement de la vue, ajustement du folio.
- Historique commun à l’interface et au MCP : jusqu’à 100 annulations/rétablissements, avec un budget mémoire de 64 Mo pour les projets contenant des images.
- Vérifications de structure, repères dupliqués ou absents, bornes libres, références à compléter, débordements des symboles.
- Export du folio en SVG, du projet multipage en PDF vectoriel A3, nomenclature CSV UTF-8.
- Serveur MCP local avec 30 outils, schémas d’arguments, retour d’erreurs et protection des modifications non enregistrées.

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

## Cartouches, folios et nomenclature

**Folio → Propriétés du folio** renseigne les données de la page et choisit son cartouche. Un numéro vide suit automatiquement l’ordre des folios ; un numéro libre comme `01-A` reste inchangé lors d’un déplacement. L’auteur, la date et la révision vides héritent du projet. **Fichier → Informations du projet** renseigne les valeurs communes, le client et le numéro du document. Les champs personnalisés se saisissent sous la forme `nom=valeur`, un par ligne.

**Folio → Cartouches** ouvre l’éditeur. Dupliquer **Technique · révisions** pour partir d’une grille inspirée du style de l’exemple `see-technical.titleblock`, ou créer un modèle vide. Chaque ligne du tableau est une cellule : coordonnées X/Y et largeur/hauteur en pourcentage du cartouche, texte multiligne, taille, alignements, bordure et image. Une cellule plus grande représente une zone fusionnée ; retirer les cellules couvertes pour éviter les superpositions. Double-cliquer la cellule Texte pour la modifier ; Entrée y ajoute une ligne. L’aperçu se met à jour après validation de la cellule. Le texte est réduit automatiquement pour tenir dans sa zone.

Insérer des champs comme `{project.title}`, `{sheet.title}`, `{sheet.number}`, `{sheet.total}`, `{sheet.author}`, `{sheet.checkedBy}` ou `{sheet.custom.armoire}`. Un champ inconnu reste visible entre accolades. Les valeurs sont recalculées à l’écran et dans les exports SVG/PDF. Enregistrer affecte le modèle au folio actif, ou à tous les folios avec la case dédiée. Modifier un modèle partagé met à jour tous les folios qui l’utilisent ; dupliquer le modèle pour une variante locale. **Exporter / Importer modèle** échange des fichiers `.gemcartouche` avec leurs images. Le fichier QElectroTech fourni sert de référence de style ; son import direct et ses logos ne sont pas intégrés.

**Projet → Nomenclature configurable** permet de cocher, renommer et réordonner les colonnes, choisir un folio ou fabricant, regrouper les articles identiques et inclure les symboles auxiliaires. Cliquer **Actualiser l’aperçu** après modification et **Mémoriser ces options** pour les sauvegarder dans le projet. Les articles ne sont regroupés que si symbole, fabricant, gamme, référence, description, caractéristiques, image, ID catalogue et source concordent. Les appareils sans référence exacte restent donc des articles à compléter.

**Propriétés du composant → Ajouter une image BOM** intègre une photo PNG/JPEG/BMP ; sinon la BOM affiche le symbole fonctionnel. Les images sont ramenées à 512 pixels et conservées dans le projet, une seule fois par contenu. Le HTML contient les images, fonctionne hors ligne et peut être imprimé depuis le navigateur. Le CSV contient les données et une indication textuelle de l’image ; il n’embarque pas de photo. L’export XLSX natif et la création de folios BOM ne sont pas encore proposés.

Pour garder l’éditeur réactif : scène vectorielle réutilisée pendant le survol/zoom, index des composants pour les fils, caches bornés pour les textes et images, tableaux virtualisés et aperçu du cartouche temporisé. Les projets 0.2 restent lisibles.

## Compiler et vérifier

Depuis le dépôt, sous PowerShell 7 :

```powershell
./Build.ps1 -Test
```

Le script installe si nécessaire le SDK officiel .NET 10.0.401 dans `build/dotnet`, restaure les dépendances sous `build/packages`, puis publie **le même chemin `build/GeMMeElec.exe`** à chaque compilation. Fermer GeMMeElec avant de le recompiler. Les outils, caches et résultats de compilation ne sont pas versionnés.

Le choix .NET 10/WPF permet un éditeur Windows natif, un rendu vectoriel et un exécutable autonome ; [SDK officiel .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

Les tests intégrés écrivent `build/self-test.json`. Pour le test d’intégration MCP sans ouvrir de fenêtre :

```powershell
./Test-Mcp.ps1 -Headless
```

Le mode `-Headless` crée une instance invisible et une session MCP séparée : aucune manipulation du PC, aucun projet utilisateur remplacé. Il écrit `build/mcp-test.json` et `build/MCP-headless-demo.*`. Sans ce commutateur, le test utilise la fenêtre ouverte et refuse de remplacer un autre projet non vide ou non enregistré. Les tests intégrés produisent aussi des aperçus hors écran des fenêtres documentaires et `build/document-performance.json`.

## Structure

| Fichier | Rôle |
| --- | --- |
| `src/Model.cs` | Projet, validation structurelle, transactions, historique |
| `src/Symbols.cs` | Géométrie et bornes des symboles |
| `src/Drawing.cs` | Scène vectorielle partagée |
| `src/SheetCanvas.cs` | Interaction du dessinateur |
| `src/MainWindow.cs` | Interface, inspecteur, commandes et exemple |
| `src/WorkspacePanels.cs` | Panneaux, masquage automatique et disposition persistante |
| `src/Documents.cs` | Cartouches, champs liés, images et validation |
| `src/DocumentDialogs.cs` | Éditeurs de cartouches, folios et nomenclature |
| `src/Bom.cs` | Regroupement et exports configurables CSV/HTML |
| `src/Export.cs` | SVG, PDF, nomenclature |
| `src/Mcp.cs` | MCP stdio et canal local vers l’interface |
| `library/catalog.json` | Gammes et sources officielles |

Voir [la configuration MCP](docs/MCP.md) et [les vérifications de livraison](docs/VALIDATION.md).

L’ancien dossier de compilation QElectroTech est encore présent localement : sa suppression a été rejetée par le contrôle automatique (« blocked by policy »). Il reste ignoré par Git et n’est pas utilisé par GeMMeElec.
