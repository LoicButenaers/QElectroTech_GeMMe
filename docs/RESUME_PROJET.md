# GeMMeElec — résumé du travail réalisé

État au **13 octobre 2026**, version publiée **0.14.0.0**.

Ce document résume les réalisations présentes dans le dépôt et la dernière livraison vérifiée. L’objectif initial reste un outil de conception de schémas électriques industriels pour la Belgique et l’Europe, avec une richesse fonctionnelle inspirée d’EPLAN P8. La version actuelle constitue une base fonctionnelle ; l’équivalence complète avec EPLAN n’est pas réalisée.

## 1. Projet et livraison

- Une première phase de travail s’appuyait sur QElectroTech et la branche `EPLAN_DESIGN`.
- À la demande de l’utilisateur, le développement est ensuite reparti de zéro sur la branche **`GeMMeElec`**, créée depuis `main`. Les anciennes branches et leur historique sont conservés.
- L’application actuelle est indépendante des sources QElectroTech, développée en **C# / WPF / .NET 10**, pour Windows x64.
- Le logo fourni par l’utilisateur est intégré.
- Dépôt de travail : `C:\dev\QElectroTech_GeMMe`.
- Exécutable autonome : `C:\dev\QElectroTech_GeMMe\build\GeMMeElec.exe`.
- Une seule version de l’exécutable est conservée à ce chemin ; le runtime et les bibliothèques nécessaires y sont embarqués.
- Les projets utilisent le format JSON **`.gemelec` version 3**, avec migration en mémoire des fichiers versions 1 et 2, sauvegarde atomique, contrôle de structure et historique compressé d’annulation/rétablissement. L’ouverture ne réécrit pas le fichier source. Limites : 200 folios, 64 Mo à la lecture et à l’enregistrement.

Le travail reste dans le dépôt de l’utilisateur ; le dépôt public de QElectroTech n’est pas modifié. Le projet utilisateur `C:\Users\pravi\Documents\LaserSieve_GH.gemelec`, travaillé lors d’une étape précédente à partir du schéma LaserSieve, est laissé intact pendant les évolutions actuelles du logiciel.

## Ajustements 0.14 validés avec l’utilisateur

- Cartouche étendu jusqu’au bord gauche extérieur du cadre : la dernière case de lettre touche son bord supérieur. Aperçu validé dans la conversation.
- Repérage projet (1–40 colonnes / 1–26 lettres), exception par folio par case à cocher et héritage pour les nouveaux folios. Migration v1/v2 préservant les nombres des anciens folios.
- Numérotation commune aux fils partageant la même borne, aux dérivations et aux bornes de passage/PE. Les bornes distinctes d’un équipement restent distinctes ; précision confirmée par l’utilisateur. Les copies de bornes conservent leur continuité déclarée.
- Maj + clic gauche puis glisser un texte sur le schéma : aperçu immédiat, symbole et conducteurs conservés, une seule annulation, Échap pour abandonner. Les destinations calculées peuvent être déplacées.
- Mode d’emploi : `REPERAGE_CONDUCTEURS_TEXTES.md`. Mesures du glissement et du renommage sur 100/5 000 et 200/10 000 dans `VALIDATION.md`.

## 2. Éditeur de schémas

- Folios A3 paysage : création, renommage, changement d’ordre et suppression annulable.
- Placement de composants depuis la bibliothèque, déplacement, rotation, duplication et suppression.
- Sélection multiple par rectangle avec le clic gauche maintenu ; Ctrl permet d’ajouter à la sélection.
- Déplacement groupé des composants avec les coudes des conducteurs internes au groupe.
- Zoom à la molette, déplacement de la vue et ajustement du folio à l’écran.
- Dispositions Compacte/Ruban et thèmes Clair/Sombre sélectionnables et mémorisés ; clair/compact par défaut. Commandes communes et couleurs documentaires indépendantes du thème.
- Menus contextuels sur les composants, reports, conducteurs, fond du schéma et éléments de bibliothèque.
- Panneaux Bibliothèque, Folios et Propriétés épinglables, fermables et masquables automatiquement ; disposition et largeur mémorisées.
- Grille de placement dont l’affichage et l’intensité sont réglables. Elle est absente des exports PDF/SVG.
- Palette de couleurs avec ajout et enregistrement de couleurs personnalisées.

## 3. Conducteurs et jonctions

- Création de liaisons en tirant d’une borne vers une autre, ou par clics successifs.
- Routage orthogonal, points de passage manuels, déplacement des segments et reconnexion par poignées.
- Les conducteurs suivent les bornes lorsque les composants sont déplacés ou tournés.
- Dérivation sur le trajet d’un fil existant, avec division du conducteur et conservation de ses propriétés.
- Points de jonction dynamiques pour les trajets communs ; ils suivent les déplacements et disparaissent lorsque la connexion ne nécessite plus de point.
- Les croisements de fils indépendants ne créent pas automatiquement une connexion.
- Numéros de conducteurs orientés le long du fil, sur son plus long segment, à l’écran comme dans les exports.

Le routage ne contourne pas automatiquement tous les obstacles du schéma.

## 4. Reports interfolios

- Deux symboles noirs distincts : **Folio suivant / départ** et **Folio précédent / arrivée**, avec triangles évidés et raccordements adaptés.
- Liaison par nom ou par sélection guidée d’une contrepartie ; modes chaîne et étoile.
- Double-clic sur le report ou son nom pour rejoindre le report lié.
- Continuité des connexions entre folios et navigateur des renvois.
- Nom affiché libre, propagé aux reports liés lors du renommage.
- Case **Nom indépendant** pour conserver un nom différent sur un report ; la décocher reprend le nom partagé.
- Destination calculée automatiquement avec numéro de folio et zone, affichée en vert sous le nom par défaut.
- Style et position de la destination réglables séparément ; son contenu reste calculé.

## 5. Textes et propriétés

- Fenêtre **Position des textes** avec aperçu interactif, sélection directe et déplacement à la souris.
- Édition individuelle du repère, des caractéristiques, de la description, des libellés de bornes, des textes vectoriels internes et des textes ajoutés.
- Ajout de textes attachés au composant et suppression des textes ajoutés depuis cette fenêtre.
- Placement haut, bas, gauche, droite ou par coordonnées précises ; icônes d’alignement du texte.
- Taille, couleur, rotation et visibilité propres à chaque texte.
- Ajustement intelligent individuel tenant compte du symbole, des autres textes, des conducteurs et des limites du folio.
- Case **Aligner tous les textes visibles** pour ranger ensemble les textes du composant, du côté choisi ou automatiquement, en conservant leurs styles.
- Signalement des conflits restants lorsqu’aucune position libre n’est trouvée parmi les propositions.
- Propriétés du panneau principal appliquées **en temps réel**, sans bouton Appliquer : composants, reports, fils et annotations. La saisie conserve le curseur et les valeurs invalides ne remplacent pas la dernière valeur valide.
- Annulation des modifications ; regroupement des saisies successives d’un même champ dans l’historique.

La fenêtre Position des textes applique désormais les modifications valides immédiatement au schéma ; **Fermer** les conserve. **Ctrl+Z / Ctrl+Y** agit sur l’historique du projet et remet à jour les champs. Un glissement est publié au plus toutes les 120 ms et à son terme ; l’aperçu suit directement le geste. Les propriétés du projet et du folio sont également immédiates, avec **Fermer** et sans enregistrement disque implicite. Les textes incorporés dans une image fabricant ne deviennent pas éditables individuellement. L’enregistrement sur disque reste une action distincte, avec Ctrl+S.

### Références bobines/contacts (0.13)

- Association explicite par ID stable entre une bobine et plusieurs fonctions contact, sur tous les folios. Repère partagé et propagation du renommage depuis chaque fonction.
- Fonctions NO, NF, inverseur, temporisé et puissance génériques ; bobines simples, temporisées et bistables. Les copies conservent le rôle ; les anciens symboles personnels peuvent recevoir une déclaration explicite dans leurs propriétés.
- Adresses réciproques et libellés de bornes dans le dessin, recalculés après déplacement/réorganisation ; navigation par inspecteur, référence calculée ou navigateur des fonctions.
- Contenu calculé en lecture seule, avec position/style propres dans Position des textes. Aucune liaison électrique entre bobine et contacts n’est ajoutée à la connectivité.
- Détachement, changement de bobine, suppression de composant/folio et duplication cohérents et annulables. Contrôle des fonctions sans bobine et des répétitions de bornes.
- La BOM compte l’article de la bobine une seule fois par défaut, avec toutes les localisations ; le filtre par folio retrouve l’article depuis ses contacts. Les fonctions restent consultables via l’option auxiliaire.
- [Parcours, format et limites](BOBINES_CONTACTS.md). Pas de simulation ni de déduction de contacts disponibles ou de caractéristiques fabricant.

## 6. Folios, cartouches et nomenclature

### Folios et cartouches

- Numéro de page automatique ou libre, titre, auteur, date, révision, vérificateur, approbateur, fonction, localisation et champs personnalisés.
- Valeurs communes héritées du projet lorsque les valeurs du folio sont vides.
- Cadre de repérage : lettres à gauche et numéros en haut, dimensions configurables. Ce cadre reste dans le PDF.
- Création, duplication et modification de cartouches par cellules : dimensions, texte, alignements, bordures et images.
- Champs liés aux données du projet et du folio, recalculés à l’écran et à l’export.
- Modèles Simple et Technique ; import/export du format **`.gemcartouche`**.

Le fichier QElectroTech `see-technical.titleblock` a servi de référence de présentation ; son import direct n’est pas implémenté.

### BOM / nomenclature

- Colonnes sélectionnables, renommables et réordonnables ; regroupement d’articles et filtres par folio ou fabricant.
- Photos intégrées aux composants ou utilisation de leurs symboles.
- Localisation associant chaque repère à son folio et sa zone, y compris dans les articles regroupés.
- Ajout de la nomenclature au projet sous forme de folios **sans cartouche**.
- Pagination automatique, en-têtes répétés et continuation des longues lignes.
- Exports CSV et HTML avec images ; les folios BOM sont inclus dans le PDF du projet.

Les folios BOM sont des instantanés : la commande d’actualisation doit être relancée après modification du schéma. Le CSV n’embarque pas d’images et l’export XLSX natif n’est pas disponible.

## 7. Bibliothèques et création de symboles

Trois ensembles sont distingués pour éviter de confondre dessins génériques et sources officielles :

| Ensemble | Contenu actuel | Utilisation |
| --- | --- | --- |
| Symboles industriels GeMMeElec | 92 symboles génériques | Placement et raccordement ; aucune identité revendiquée avec les symboles EPLAN |
| GeMMe fabricant · dessins officiels | Schneider LC1D09BD, ABB AF09-30-01-13 / 1SBL137001R1301, Eaton 276705 / DILM9-10(24VDC) | Images issues de documents officiels, avec bornes relevées et raccordables |
| EPLAN · aperçus officiels | 4 illustrations officielles | Consultation uniquement, sans placement ni raccordement |

Les sources, références, éditions, méthodes et empreintes SHA-256 des dessins officiels sont documentées. Les images fabricants sont conservées sans redessin ; les bornes ont été relevées manuellement. Le contrôle d’intégrité ne constitue pas une certification électrique.

Un atelier interactif permet de créer un symbole vide ou une copie : lignes, rectangles, cercles, textes, bornes, poignées, coordonnées, grille, zoom et historique local. Les copies sont embarquées dans le projet et échangeables au format **`.gemsymbol`**. La bibliothèque d’origine reste intacte.

**La bibliothèque équivalente à l’intégralité d’EPLAN et la couverture de trois fabricants pour chaque famille ne sont pas réalisées.** La collection officielle actuelle couvre trois références dans une famille et quatre aperçus EPLAN.

## 8. Imports et exports

| Format | Fonctionnement actuel |
| --- | --- |
| QElectroTech `.qet` | Conversion partielle des folios, symboles embarqués, bornes, propriétés et conducteurs ; rapport des pertes avant ouverture |
| Symboles `.dxf` | Import de géométrie, blocs et textes ; réglage d’échelle et ajout manuel des bornes électriques |
| Folios EPLAN / CAO exportés en `.dxf` | Récupération graphique multipage ; aucune reconstruction automatique des appareils, réseaux ou articles BOM |
| EPLAN `.elk`, `.zw1`, `.epj` | Import non implémenté |
| PDF | Export vectoriel multipage du projet, avec cartouches, repérage, références bobines/contacts et folios BOM ; génération en arrière-plan sur un instantané |
| SVG | Export du folio avec les styles et positions des textes |
| CSV / HTML | Exports de nomenclature configurables |

La conversion QET comporte des simplifications : cartouches remplacés, certaines géométries/styles non repris, images et dessins libres non récupérés. Les liens des reports QET doivent être recréés avec les reports GeMMeElec. Un DXF graphique ne fournit pas à lui seul un bornage ou une connectivité fiable. Les sources importées ne sont pas écrasées automatiquement.

## 9. MCP intégré

- Le même exécutable, lancé avec **`--mcp`**, sert de passerelle vers l’application ouverte.
- **47 outils** couvrent projets, folios, composants, conducteurs, reports, textes, symboles, imports, cartouches, nomenclature, exports et historique.
- La passerelle utilise le modèle de l’application : les modifications sont visibles et annulables dans le logiciel.
- Les nouvelles commandes permettent de lister les textes, en ajouter, les modifier individuellement, les supprimer et les aligner.
- Transport JSON-RPC sur stdin/stdout, avec communication locale vers la fenêtre par un canal Windows limité au compte utilisateur.
- Les paramètres sont validés et les projets non enregistrés sont protégés contre un remplacement implicite.
- La configuration locale du client est fournie dans `.codex/config.toml` ; son chargement dépend de la session MCP du client.

Le serveur a été testé directement par son transport MCP. Une configuration fournie ne signifie pas que les outils sont automatiquement chargés dans toutes les conversations déjà ouvertes.

## 10. Fluidité et validation

Les améliorations comprennent le cache de la scène et de la grille, l’indexation des composants et renvois, des caches bornés pour les textes/images, la virtualisation des tableaux, les imports en arrière-plan et la pagination BOM à la demande. L’aperçu des textes travaille sur le composant sélectionné et le panneau de propriétés n’est pas reconstruit à chaque caractère.

Dernière livraison vérifiée : **0.14.0.0**, le 13 octobre 2026.

- **99 scénarios applicatifs réussis** via `Build.ps1 -Test`, sans erreur ni avertissement.
- **90 vérifications MCP réussies** via `Test-Mcp.ps1 -Headless` ; 47 outils, avec les 45 anciens conservés.
- Quatre combinaisons Compacte/Ruban × Clair/Sombre, commandes communes activables, conservation des panneaux, rendu à 1440 × 900 et disposition à 1000 × 600. Le compact libère 68 unités verticales de plus que le ruban.
- Propriétés immédiates, erreurs en ligne, curseur conservé pendant la saisie, connexion suivant les coordonnées/rotations, historique cohérent ; propriétés documentaires et textes immédiats.
- Associations bobines/contacts complètes : persistance, migration v1/v2 → v3, repère partagé, navigation, réorganisation des folios, duplication, détachement et suppression/annulation sans perte de conducteurs ; déclaration des symboles personnels et contrôles de bornes.
- Historique compressé et budget de 64 Mo ; huit opérations successives sur 5 000 composants annulées/rétablies exactement, avec connexions et associations conservées.
- PDF multipage en arrière-plan sur un instantané, depuis l’interface et le MCP. Les caches WPF sont propres au fil de rendu. Test d’édition/rendu concurrent avec image officielle : résultat identique octet pour octet à l’export de référence, indépendant des modifications suivantes. L’application attend la fin de l’export avant de fermer.
- PDF invariant entre thèmes/dispositions, y compris avec les nouvelles références. Exports A3 de deux folios et PDF de charge de 200 pages contrôlés avec Poppler ; interface et fenêtres documentaires/texte inspectées hors écran. Aucune manipulation souris/clavier d’un projet utilisateur.
- Lecture/enregistrement/relecture d’une **copie** de `LaserSieve_GH.gemelec`, original intact : SHA-256 `E94D84AB7148318D8F627371FA7734913E0157151C4EC4886A26F5DA2088C89C`. Échec d’enregistrement préservant le modèle, son état modifié et l’absence de fichier temporaire.
- Mesures représentatives dans **un processus dédié par taille** : 100/5 000 et 200/10 000, avec conducteurs, textes et contacts associés. Détails et limites dans `VALIDATION.md` et `build/benchmark.json`. Les gros projets restent coûteux en propriétés/historique ; ces mesures ne garantissent pas une cadence de souris ou GPU.

Les préférences et panneaux des tests utilisent des fichiers séparés. Le budget de fichier est contrôlé des deux côtés pour éviter une sauvegarde impossible à rouvrir. JSON compact et snapshots compressés réduisent les volumes sans modifier les données. La validation de structure reste exigée pour les transactions ; la seconde validation n’est exécutée après nettoyage des jonctions que si ce nettoyage a modifié le modèle.

Reprise : modifications 0.11 vérifiées et préservées dans `306a101be`, puis refonte 0.12 dans `358d4f13e`, tous deux poussés sur `origin/GeMMeElec`. Les 0.13 et 0.14 poursuivent cette base. Aucune ancienne branche n’a été réécrite ; `upstream` et les documents utilisateur restent intacts.

## 11. Limites restantes et règles de poursuite

Restent notamment à développer : bibliothèque exhaustive, import EPLAN natif, automates et entrées/sorties structurés, gestion de câbles multicœurs, borniers automatiques, implantation d’armoires, dimensionnement et simulation électriques. Aucune équivalence complète à EPLAN P8 ni certification IEC/RGIE n’est annoncée.

Le nettoyage du dépôt a été étudié et les sources actives réorganisées, mais la suppression de certains anciens outils/caches QElectroTech/Qt/MSYS2 avait été bloquée lors d’une étape précédente. Le nettoyage intégral de ces fichiers locaux ne doit donc pas être considéré comme terminé.

Pour poursuivre : travailler sur `GeMMeElec`, préserver les anciennes branches et les documents utilisateur, remplacer le même exécutable à chaque livraison, conserver la distinction entre symboles génériques et sources officielles, et vérifier la fluidité ainsi que les connexions, la persistance, l’annulation et le MCP après les changements de modèle.

## 12. Documents et commandes utiles

- [Feuille de route et périmètres des prochains lots](FEUILLE_DE_ROUTE.md)
- [Références bobines/contacts](BOBINES_CONTACTS.md)

- [Guide d’utilisation et structure du dépôt](../README.md)
- [Configuration et outils MCP](MCP.md)
- [Origine et limites des bibliothèques](CATALOGUE.md)
- [Imports et édition de symboles](IMPORTS.md)
- [Fonctionnement des renvois](RENVOIS.md)
- [Historique des validations](VALIDATION.md)

Depuis `C:\dev\QElectroTech_GeMMe`, sous PowerShell, après fermeture de l’application pour la compilation :

```powershell
./Build.ps1 -Test
./Test-Mcp.ps1 -Headless
./Benchmark.ps1
```

Les rapports sont enregistrés dans `build/self-test.json` et `build/mcp-test.json`. Les outils de compilation, dépendances et caches restent sous `build/`, hors du suivi Git.
