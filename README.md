# GeMMeElec

Atelier Windows de conception de schémas électriques industriels, réécrit en **C# / WPF / .NET 10**. Branche `GeMMeElec`, créée depuis `main`. Les anciens fichiers suivis ont été retirés de cette branche ; l’historique Git et la branche `EPLAN_DESIGN` sont conservés. Aucun code QElectroTech n’est utilisé par cette nouvelle application.

## Démarrer

Ouvrir **`C:\dev\QElectroTech_GeMMe\build\GeMMeElec.exe`**. L’exécutable Windows x64 embarque le runtime et la bibliothèque ; aucune installation de Qt ou de .NET n’est nécessaire pour l’utiliser. Le logo fourni est intégré.

Le dossier contenant l’exécutable doit être accessible en écriture pour le fichier de session MCP et les journaux. Une seule fenêtre d’application est ouverte par compte Windows. Le même exécutable, lancé avec `--mcp`, sert de passerelle MCP sans ouvrir une deuxième interface.

## Fonctionnalités (version 0.11)

- Projets JSON `.gemelec`, sauvegarde atomique, ouverture et contrôle de structure.
- Folios A3 paysage : ajout, renommage, suppression annulable, cartouche projet/auteur/révision.
- 92 symboles génériques GeMMeElec ; deux bibliothèques documentées distinctes : 3 dessins fabricants raccordables (Schneider LC1D09BD, ABB AF09-30-01-13, Eaton 276705) et 4 aperçus officiels EPLAN en consultation. Les 75 anciennes associations de gammes ont été retirées du catalogue.
- Cadre de repérage avec colonnes numérotées et lignes lettrées, configurable par folio ; localisation des composants dans la BOM, par exemple `K1 : 3 / C7`.
- Renvois interfolios départ/arrivée liés par nom, modes chaîne et étoile, références réciproques, navigation et continuité des connexions.
- Reports noirs avec triangle évidé : départ raccordé à gauche, arrivée à droite. Liaison guidée par choix de la contrepartie ; double-clic sur le symbole ou son nom pour naviguer, dans tous les modes.
- Jonctions calculées à partir des fils partageant une borne : point au départ commun ou à la divergence, suppression automatique quand il ne reste qu’un fil. Les nœuds de dérivation suivent le déplacement des segments.
- Atelier interactif de symboles : création vide ou copie, dessin à la souris (ligne, rectangle, cercle, texte), bornes, poignées, coordonnées précises, zoom, grille et historique local. Bibliothèque personnelle embarquée et échange `.gemsymbol`.
- Import de symboles fabricants DXF 2000+ : blocs, traits, cercles, arcs, polylignes et textes ; échelle réglable et ajout manuel des bornes.
- Conversion QElectroTech `.qet` avec symboles embarqués et connexions ; import graphique multipage des exports DXF EPLAN/CAO, avec rapport de limites avant ouverture.
- Recherche, filtres et aperçu des symboles ; placement répété sur grille.
- Sélection multiple par rectangle (clic gauche maintenu dans le vide), Ctrl pour ajouter ; déplacement des groupes avec leurs coudes internes, duplication, rotation et suppression.
- Menus de clic droit sur les symboles, reports, fils, fond du folio et éléments de bibliothèque : propriétés, rotation, copie, suppression, liaison/navigation, couleur et parcours automatique.
- Connexions entre bornes, tracé orthogonal, coudes manuels, repères de conducteurs, suivi des déplacements.
- Câblage par glisser entre bornes, déplacement des segments et changement de borne par poignées, coordonnées des points de passage.
- Dérivation en tirant une borne sur un fil existant : point de jonction, division du conducteur et arrivée perpendiculaire, annulables ensemble.
- Grille réglable (affichage et intensité), réservée à l’éditeur ; palette de conducteurs avec couleurs personnalisées enregistrées.
- Position indépendante du repère, des caractéristiques et de la description : côtés, alignements, décalages précis et visibilité ; rendu commun à l’écran, au SVG et au PDF.
- Panneaux Bibliothèque, Folios et Propriétés épinglables, masquables automatiquement et fermables ; largeur réglable et disposition mémorisée.
- Cartouches personnalisables : cellules, dimensions, texte multiligne, alignements, logo, champs liés, modèles Simple et Technique, import/export `.gemcartouche`.
- Propriétés détaillées des folios : numéro automatique ou libre, titre, auteur, vérificateur, approbateur, date, révision, fonction, localisation et champs personnalisés. Ordre modifiable.
- BOM configurable : choix et ordre des colonnes, regroupement, filtres folio/fabricant, photos intégrées ou symboles ; export HTML autonome imprimable et CSV.
- Folios BOM intégrés au projet et au PDF, pagination automatique, en-têtes répétés, longues lignes poursuivies sur la page suivante, sans cartouche.
- Repères et propriétés des appareils, référence commerciale libre, caractéristiques, libellés de bornes, documentation fabricant.
- Annotations, zoom à la molette, déplacement de la vue, ajustement du folio.
- Historique commun à l’interface et au MCP : jusqu’à 100 annulations/rétablissements, avec un budget mémoire de 64 Mo pour les projets contenant des images.
- Vérifications de structure, repères dupliqués ou absents, bornes libres, références à compléter, débordements des symboles.
- Export du folio en SVG, du projet multipage en PDF vectoriel A3, nomenclature CSV UTF-8.
- Serveur MCP local avec 45 outils, schémas d’arguments, retour d’erreurs et protection des modifications non enregistrées.

## Dessiner

Cliquer un équipement dans la bibliothèque puis cliquer le folio. Échap termine la pose. Pour câbler, maintenir le bouton gauche sur une borne, tirer vers une autre borne et relâcher : cela fonctionne en mode Sélection et immédiatement après la pose. Pour une **dérivation**, relâcher sur le trajet d’un conducteur : un point coloré prévisualise la jonction. Le conducteur cible est scindé en conservant son tracé, sa couleur et son numéro ; le nouveau fil arrive perpendiculairement au point. Une borne ou jonction déjà présente est réutilisée. Un relâchement dans le vide annule la liaison. Le mode Conducteur permet aussi de cliquer une première borne, des points de passage facultatifs, puis la borne ou le fil d’arrivée. Un simple croisement n’établit pas de connexion électrique.

Sélectionner un conducteur puis glisser un segment ou une **poignée carrée** pour ajuster son tracé. Glisser une **poignée ronde** vers une borne pour changer son raccordement. L’inspecteur permet aussi de saisir les points `X ; Y`, un par ligne, et de rétablir le tracé automatique. Échap annule le geste ; Ctrl+Z annule sa validation. Pour créer un nouveau fil depuis une borne occupée par le fil sélectionné, activer Conducteur (W).

Sélectionner un composant puis **Propriétés → Position des textes…** (également au clic droit). Dans l’aperçu interactif, glisser le texte encadré, utiliser les quatre flèches pour choisir un côté et les icônes pour l’alignement. Les coordonnées X/Y et la distance sont visibles immédiatement ; les décimales acceptent une virgule ou un point. La molette zoome ; les flèches du clavier déplacent le texte d’une unité (Maj : dix). Échap annule un glissement. Passer d’un texte à l’autre conserve les réglages du brouillon ; **Valider** enregistre tous les changements en une seule opération annulable, **Annuler** les abandonne. Pour un report, la destination s’affiche par défaut en vert sous le nom. La case **Destination sous le nom** conserve ce placement automatique ; elle peut être décochée pour placer le lien séparément.

Tous les textes sont accessibles dans cette fenêtre : repère, caractéristiques, description, libellés de bornes, textes internes du symbole et textes ajoutés. Cliquer directement un texte dans l’aperçu ou le choisir dans la liste, puis modifier son contenu, sa position ou son style. **Ajouter du texte** crée un texte attaché au composant ; **Supprimer ce texte** retire uniquement un texte ajouté. Les textes internes sont personnalisés pour ce composant sans modifier le symbole de bibliothèque. La destination des reports reste calculée automatiquement.

Cocher **Aligner tous les textes visibles**, puis cliquer **Haut**, **Bas**, **Gauche**, **Droite** ou **Automatique** pour ranger les textes du composant ensemble, sans superposition entre eux. Les tailles, couleurs et rotations sont conservées ; les textes vides ou masqués restent inchangés. Décocher la case pour ajuster seulement le texte sélectionné.

La section **Ajustement intelligent** propose quatre boutons **↑ Haut**, **↓ Bas**, **← Gauche** et **→ Droite** : le texte sélectionné reste du côté demandé, aligné sur le centre ou les bords du symbole. Le bouton **Automatique · tous les côtés** conserve la recherche libre autour du symbole. Il recherche une place libre autour de celui-ci en tenant compte des bornes, des fils, des autres textes et des limites du folio. Le résultat apparaît dans l’aperçu ; **Valider** l’enregistre et **Annuler** l’abandonne. Si aucun emplacement libre n’est trouvé parmi les positions proposées du côté choisi, un message invite à vérifier le résultat ; le texte ne bascule pas automatiquement de l’autre côté. Les textes vides ou masqués restent inchangés.

Le **nom affiché du report** est partagé automatiquement avec ses reports liés quand on le renomme depuis les propriétés ou **clic droit → Renommer le report…**. La case **Nom indépendant**, dans les propriétés du report, permet de conserver un nom différent pour celui-ci. Décocher cette case reprend immédiatement le nom partagé. La liaison électrique et la navigation par double-clic sont conservées.

Dans **Position des textes → Taille, couleur et rotation**, régler séparément le repère, les caractéristiques, la description, ou le nom et la destination des reports. Taille de 4 à 200 unités (0 = automatique), couleur de la palette ou code `#RRGGBB` (vide = couleur automatique), rotation de −360° à 360°. L’aperçu est immédiat ; **Valider** enregistre tous les réglages en une opération annulable. L’ajustement intelligent conserve ces styles et tient compte de l’encombrement du texte tourné. Les styles sont enregistrés dans le projet et conservés dans les exports SVG/PDF.

Les noms des câbles suivent automatiquement leur plus long segment : horizontaux au-dessus du fil, verticaux le long du fil (lecture de bas en haut), à l’écran comme dans les exports SVG/PDF.

Dans les en-têtes des panneaux, **● / ○** épingle ou désépingle ; **×** ferme. Un panneau désépinglé reste accessible par son onglet latéral et se replie après une action sur le folio. Le menu **Affichage** rouvre les panneaux ou rétablit la disposition initiale. Les séparateurs règlent leur largeur. La disposition est enregistrée dans `build/GeMMeElec.layout.json`.

**Affichage → Grille du plan** règle la visibilité de 0 à 100 % ou masque la grille, avec aperçu immédiat. L’accrochage reste de 10 unités. Les exports PDF/SVG et les folios BOM ne contiennent jamais cette grille. **Affichage → Palette des conducteurs** définit la couleur des prochains fils ; **Propriétés du fil → Palette de couleurs** choisit sa couleur et l’applique immédiatement. Composer une teinte avec les curseurs rouge/vert/bleu ou le code hexadécimal, lui donner un nom et cliquer **Enregistrer cette couleur**. Un nom existant remplace sa teinte. La palette et les réglages de grille sont conservés dans `build/GeMMeElec.preferences.json` ; les couleurs appliquées aux fils sont également dans le projet.

| Action | Raccourci |
| --- | --- |
| Sélection / conducteur / texte | V / W / T |
| Rotation / ajuster le folio | R / F |
| Déplacer un symbole | Glisser à la souris |
| Sélectionner un groupe | Glisser depuis une zone vide en mode Sélection |
| Ajouter à la sélection | Ctrl+clic ou Ctrl+glisser |
| Zoom / déplacement de la vue | Molette / bouton central |
| Enregistrer / enregistrer sous | Ctrl+S / Ctrl+Maj+S |
| Ouvrir / nouveau projet | Ctrl+O / Ctrl+N |
| Annuler / rétablir | Ctrl+Z / Ctrl+Y |
| Dupliquer / supprimer | Ctrl+D / Suppr |

Les valeurs de l’inspecteur sont appliquées en temps réel au schéma, sans bouton Appliquer. La saisie reste en place sans perdre le curseur ; les valeurs incomplètes ou invalides sont signalées sans remplacer la dernière valeur valide. Ctrl+Z annule une séquence de saisie continue dans un même champ. Ctrl+S reste nécessaire pour enregistrer le projet sur disque. Le menu Fichier propose un exemple de départ moteur, explicitement incomplet côté commande.

## Bibliothèque et périmètre

Les trois dessins fabricants proviennent de documents officiels conservés dans le dépôt. Les images sont intégrées sans redessin ; leurs bornes ont été relevées sur ces documents. La fiche de provenance précise la référence, l’édition historique, la méthode et les empreintes SHA-256. Les aperçus EPLAN ne fournissent pas de définition électrique native : ils restent non raccordables. Cette sélection ne couvre pas toutes les familles ni tous les fabricants. Les 92 symboles génériques restent distincts et ne revendiquent aucune identité avec EPLAN ou un fabricant. Voir [le catalogue documenté](docs/CATALOGUE.md).

Cette base ne comprend pas encore le dimensionnement électrique, la simulation, les références croisées entre bobines et contacts d’un appareil, les borniers automatiques, l’import des formats EPLAN natifs `.elk`, `.zw1` et `.epj`, la gestion de câbles multicœurs, les implantations d’armoires, ni les catalogues complets des fabricants. Elle ne revendique pas une certification IEC/RGIE ou une équivalence à EPLAN. Le DXF EPLAN récupère le dessin, sans reconstruire les appareils ni les réseaux électriques.

## Symboles personnels et conversion

Sélectionner un composant puis **Propriétés → Modifier le symbole en copie…**, ou choisir un symbole dans la bibliothèque et utiliser **Insérer → Modifier un symbole en copie…**. Les tableaux modifient lignes, rectangles, cercles, textes et bornes ; le clic dans l’aperçu positionne l’objet sélectionné. **Enregistrer la copie** l’ajoute à **Mes symboles** dans le projet. La case dédiée remplace le composant sélectionné, en préservant les IDs des bornes raccordées. **Exporter .gemsymbol** crée un fichier réutilisable dans un autre projet.

**Insérer → Importer un symbole DXF / GeMMeElec…** ouvre le même éditeur. Pour un DXF fabricant, régler l’échelle, ajouter les bornes électriques puis enregistrer la copie. La géométrie seule ne fournit ni référence commerciale vérifiée ni connectivité. Le lecteur DXF [netDxf 3.0.1](https://github.com/haplokuon/netDxf) est distribué sous [licence MIT](docs/netDxf-LICENSE.txt), également consultable dans **Aide**.

Le menu **Importer** convertit un projet `.qet`, ou plusieurs folios `.dxf` exportés depuis EPLAN/une CAO. La conversion s’effectue en arrière-plan ; un rapport présente les simplifications avant l’ouverture du résultat. Enregistrer ensuite en `.gemelec`. Les fichiers sources restent intacts. Voir [le guide et les limites des imports](docs/IMPORTS.md).

Les jonctions de borne commune sont calculées sans ajouter un composant. Une dérivation dessinée sur le trajet d’un fil utilise un nœud interne automatique : il n’est plus sélectionnable comme un appareil ni présent dans la BOM. Lorsque deux tronçons seuls restent, le point disparaît ; ils sont fusionnés si leurs numéros et couleurs concordent, sinon leurs propriétés distinctes sont conservées. Un croisement de fils indépendants n’établit pas de connexion.

## Repérage et renvois interfolios

**Folio → Repérage lignes / colonnes…** règle les colonnes (20 par défaut) et les lignes (A à J par défaut). Le cadre reste visible dans le PDF, contrairement aux points de la grille de placement. La zone correspond au centre du composant. La colonne BOM **Folios / zones** associe chaque repère à son emplacement, même pour des articles regroupés. Actualiser les folios BOM déjà générés pour y reporter les nouvelles positions.

Dans **Symboles industriels → 10 · Renvois interfolios**, placer **Folio suivant · départ** puis **Folio précédent · arrivée** sur le folio destinataire et raccorder leurs bornes aux fils. Sélectionner chaque flèche, saisir le même **Nom de liaison** et cliquer **Appliquer le renvoi**. Le mode chaîne relie les points deux par deux ; le mode étoile relie une source à plusieurs arrivées. Le libellé indique automatiquement le folio et la zone de la contrepartie. Double-cliquer une flèche pour la rejoindre ; **Projet → Navigateur des renvois…** affiche toutes les liaisons. Voir [le fonctionnement et les sources EPLAN](docs/RENVOIS.md).

## Cartouches, folios et nomenclature

**Folio → Propriétés du folio** renseigne les données de la page et choisit son cartouche. Un numéro vide suit automatiquement l’ordre des folios ; un numéro libre comme `01-A` reste inchangé lors d’un déplacement. L’auteur, la date et la révision vides héritent du projet. **Fichier → Informations du projet** renseigne les valeurs communes, le client et le numéro du document. Les champs personnalisés se saisissent sous la forme `nom=valeur`, un par ligne.

**Folio → Cartouches** ouvre l’éditeur. Dupliquer **Technique · révisions** pour partir d’une grille inspirée du style de l’exemple `see-technical.titleblock`, ou créer un modèle vide. Chaque ligne du tableau est une cellule : coordonnées X/Y et largeur/hauteur en pourcentage du cartouche, texte multiligne, taille, alignements, bordure et image. Une cellule plus grande représente une zone fusionnée ; retirer les cellules couvertes pour éviter les superpositions. Double-cliquer la cellule Texte pour la modifier ; Entrée y ajoute une ligne. L’aperçu se met à jour après validation de la cellule. Le texte est réduit automatiquement pour tenir dans sa zone.

Insérer des champs comme `{project.title}`, `{sheet.title}`, `{sheet.number}`, `{sheet.total}`, `{sheet.author}`, `{sheet.checkedBy}` ou `{sheet.custom.armoire}`. Un champ inconnu reste visible entre accolades. Les valeurs sont recalculées à l’écran et dans les exports SVG/PDF. Enregistrer affecte le modèle au folio actif, ou à tous les folios avec la case dédiée. Modifier un modèle partagé met à jour tous les folios qui l’utilisent ; dupliquer le modèle pour une variante locale. **Exporter / Importer modèle** échange des fichiers `.gemcartouche` avec leurs images. Le fichier QElectroTech fourni sert de référence de style ; son import direct et ses logos ne sont pas intégrés.

**Projet → Nomenclature configurable** permet de cocher, renommer et réordonner les colonnes, choisir un folio ou fabricant, regrouper les articles identiques et inclure les symboles auxiliaires. Cliquer **Actualiser l’aperçu** après modification et **Mémoriser ces options** pour les sauvegarder dans le projet. Les articles ne sont regroupés que si symbole, fabricant, gamme, référence, description, caractéristiques, image, ID catalogue et source concordent. Les appareils sans référence exacte restent donc des articles à compléter.

**Ajouter / actualiser les folios BOM** crée les pages nécessaires en fin de projet, sans cartouche. Les colonnes choisies, photos ou symboles, en-têtes répétés et numéros de page sont inclus dans le PDF. Les longues lignes continuent sur les pages suivantes. Cette commande remplace les précédents folios BOM en une seule opération annulable ; ils constituent un instantané enregistré. Après modification du schéma, relancer la commande pour actualiser les quantités et références de folio. Ces pages ne reçoivent pas de composants de schéma. La limite totale reste de 100 folios ; un dépassement annule entièrement la génération.

**Propriétés du composant → Ajouter une image BOM** intègre une photo PNG/JPEG/BMP ; sinon la BOM affiche le symbole fonctionnel. Les images sont ramenées à 512 pixels et conservées dans le projet, une seule fois par contenu. Le HTML contient les images, fonctionne hors ligne et peut être imprimé depuis le navigateur. Le CSV contient les données et une indication textuelle de l’image ; il n’embarque pas de photo. L’export XLSX natif n’est pas encore proposé.

Pour garder l’éditeur réactif : scène vectorielle et grille réutilisées pendant le survol, index des composants et cache des tracés pour les fils, caches bornés pour les textes et images, tableaux virtualisés et aperçu du cartouche temporisé. Les renvois sont indexés une fois par reconstruction de scène ; les jonctions utilisent un arbre des trajets communs. La BOM est paginée à la demande. Les imports UI s’exécutent en arrière-plan et la bibliothèque personnelle n’est reconstruite qu’après changement de ses définitions. Les projets 0.2 à 0.5 restent lisibles.

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
| `src/IndustrialSymbols.cs` | Catégories et symboles industriels supplémentaires |
| `src/CrossReferences.cs` | Zones, résolution des renvois et continuité des connexions |
| `src/ReferenceDialogs.cs` | Réglage des zones, propriétés et navigateur des renvois |
| `src/ReferenceDisplay.cs` | Reports en ligne et zone cliquable du texte |
| `src/Junctions.cs` | Jonctions dynamiques, nettoyage et déplacement des nœuds |
| `src/CustomSymbols.cs`, `src/SymbolDialogs.cs`, `src/SymbolCanvas.cs` | Définitions personnelles, atelier interactif, historique local et échanges |
| `src/DxfImport.cs`, `src/ProjectImport.cs` | Lecture DXF, conversion QET et rapports |
| `src/Drawing.cs` | Scène vectorielle partagée |
| `src/SheetCanvas.cs` | Interaction du dessinateur |
| `src/MainWindow.cs` | Interface, inspecteur, commandes et exemple |
| `src/ContextActions.cs` | Menus rapides du schéma |
| `src/WorkspacePanels.cs` | Panneaux, masquage automatique et disposition persistante |
| `src/Documents.cs` | Cartouches, champs liés, images et validation |
| `src/DocumentDialogs.cs` | Éditeurs de cartouches, folios et nomenclature |
| `src/Bom.cs` | Regroupement et exports configurables CSV/HTML |
| `src/Export.cs` | SVG, PDF, nomenclature |
| `src/Mcp.cs` | MCP stdio et canal local vers l’interface |
| `library/catalog.json` | Gammes et sources officielles |

Voir [la configuration MCP](docs/MCP.md) et [les vérifications de livraison](docs/VALIDATION.md).

Audit 0.7 : les sources actives, la bibliothèque, le logo, les documents et la configuration MCP sont conservés. L’ancien éditeur tabulaire a été remplacé par l’atelier ; les interactions et menus sont séparés de la fenêtre principale. Les anciens outils QElectroTech/Qt/MSYS2 et caches CMake représentent plus de 3 Go inutilisés. Leur suppression, ainsi que celle de l’ancien `obj/` à la racine, a été rejetée par le contrôle automatique (« blocked by policy »). Ces fichiers restent donc présents localement, ignorés par Git. Les projets et préférences utilisateur n’ont pas été modifiés.
