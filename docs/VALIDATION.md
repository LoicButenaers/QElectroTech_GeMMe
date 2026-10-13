# Vérifications de livraison

## Version 0.14 — 13 octobre 2026

Exécutable autonome Windows x64 au chemin habituel, version fichier **0.14.0.0**. **99 scénarios applicatifs et 90 vérifications MCP réussis**, compilation sans erreur ni avertissement ; 47 outils MCP conservés.

- Alignement de la bande des lettres sur le cartouche, validé par l’utilisateur sur aperçu. Bord commun vérifié dans le modèle, dans l’éditeur de cartouches et dans un PDF A3 rendu par Poppler et inspecté.
- Réglages de repérage communs au projet, exception par case à cocher, héritage des nouveaux folios, limites et rollback. Dialogue rendu hors écran et testé par ses contrôles.
- Format v3 ; migration v1/v2 conservant les nombres locaux des folios, sauvegarde/relecture et annulation. Le fichier utilisateur original conserve l’empreinte `E94D84AB7148318D8F627371FA7734913E0157151C4EC4886A26F5DA2088C89C`.
- Numéro commun sur une même borne, ses dérivations et les bornes de passage/PE ; exclusion des bornes distinctes d’un appareil ; copie personnalisée de borne avec sa continuité. Renommage atomique, reconnexion conservant/adoptant le numéro de destination, branche détachée conservée, rejets invalides, historique et persistance vérifiés aussi via MCP. Les couleurs et trajets restent propres aux fils.
- Maj + glisser sur un texte hors de l’emprise du symbole, déplacement précis, destination calculée détachée sans saut, libellé de borne tournée, maintien du câblage et de la bibliothèque. Aperçu sans mise à jour du snapshot du projet, une seule annulation à la fin, abandon et simple clic sans modification.

### Mesures du glissement et de la numérotation

Processus dédié par taille, WPF hors écran, jeu de données synthétique avec 50 composants par folio, conducteurs, annotations et contacts associés. Mesures de la même machine ; résultats complets dans `build/benchmark.json` (version 0.14).

| Opération | 100 folios / 5 000 composants | 200 folios / 10 000 composants |
| --- | ---: | ---: |
| Début du glissement du texte | 94 ms | 176 ms |
| 120 mises à jour du placement pendant l’aperçu | 1,97 ms au total | 1,59 ms au total |
| Validation finale du glissement | 107 ms | 204 ms |
| Renommer une continuité de conducteurs | 203 ms | 393 ms |
| Enregistrer le projet | 123 ms | 233 ms |
| Ouvrir le projet | 392 ms | 765 ms |

Les 120 mises à jour mesurent le modèle et l’invalidation du dessin, **sans rendre chaque image**. Elles ne mesurent donc pas la cadence graphique ou le délai d’entrée d’une souris réelle. Le début et la validation restent des transactions complètes, avec un coût visible à 10 000 composants ; aucune promesse de fluidité générale n’est tirée de ces chiffres. Les PDF complets ont également été générés pour les deux tailles.

Rapports : `build/self-test.json`, `build/mcp-test.json`, `build/benchmark.json`. Aperçus : `apercu-detail-cartouche.png`, `apercu-cadre-cartouche.png`, `ui-zones-exception.png`, `self-test-frame-continuity.pdf`. Mode d’emploi : `REPERAGE_CONDUCTEURS_TEXTES.md`.


## Version 0.13 — 13 octobre 2026

Exécutable autonome Windows x64 au chemin habituel, version fichier 0.13.0.0. **91 scénarios applicatifs et 78 vérifications MCP réussis**, compilation sans erreur ni avertissement. 47 outils MCP ; les 45 précédents restent disponibles.

- Références bobines/contacts : association explicite interfolios, repère partagé, NO/NF/inverseur/temporisé/puissance, déclaration des symboles personnels, adresses recalculées, navigation, suppression de bobine/folio sans perte des contacts ou conducteurs, détachement, duplication et annulation/rétablissement. Pas d’arête électrique ajoutée par une association.
- Nomenclature : un article par bobine avec fonctions associées par défaut ; localisations de toutes les représentations et filtre par folio retrouvant le parent même depuis un folio de contacts. Les doublons de bornes de même fonction sont signalés.
- Format JSON version 2, migration version 1 en mémoire sans écriture du fichier source, sauvegarde/relecture et refus des versions inconnues. Limites harmonisées : 200 folios, 64 Mo. JSON compact ; les données et couleurs sont conservées.
- Historique compressé dans le budget de 64 Mo : huit changements successifs sur 5 000 composants restaurés exactement, avec connexions et références. La profondeur reste plafonnée à 100 et peut baisser selon les images et le volume des snapshots.
- Échec d’enregistrement : modèle, état modifié et fichier courant préservés ; aucun temporaire restant. La fermeture/remplacement ne poursuit pas si l’enregistrement demandé échoue.
- Export PDF en arrière-plan sur un instantané depuis l’interface et le MCP, caches de rendu séparés par fil. Test de rendu/édition concurrent avec dessin officiel et PDF de deux pages : résultat identique octet pour octet au PDF de référence malgré les modifications suivantes. Le retour MCP reste {path} après achèvement. La fermeture attend la fin d’un export.
- Quatre combinaisons de dispositions/thèmes, changement de disposition libérant réellement l’espace du ruban, propriétés immédiates, couleurs/PDF inchangés même avec références liées ; contrôles de connexions, ports, textes, images, cartouches, BOM, imports et MCP de la suite complète.
- Original utilisateur inchangé : SHA-256 de `LaserSieve_GH.gemelec` `E94D84AB7148318D8F627371FA7734913E0157151C4EC4886A26F5DA2088C89C`. Travail de persistance uniquement sur une copie.

### Mesures de dimensionnement sur le PC

`Benchmark.ps1` lance **un processus caché neuf par taille**. Par folio : 50 composants (bobines, contacts NO/NF, bornes, disjoncteurs), 49 conducteurs et une annotation. Au total : 2 000 / 4 000 contacts associés. Rendu du folio actif de 50 composants, 725 primitives, dans une interface 1440 × 900. Résultats du dernier passage dans `build/benchmark.json` :

| Opération | 100 folios / 5 000 composants | 200 folios / 10 000 composants |
| --- | --- | --- |
| Fichier JSON | 7.47 Mo | 14.95 Mo |
| Sauvegarde | 275 ms | 392 ms |
| Ouverture avec mise à jour WPF | 730 ms | 1307 ms |
| Propriété, cinq modifications | 120–286 ms | 218–512 ms |
| Annulation / rétablissement | 604 ms / 619 ms | 863 ms / 785 ms |
| Déplacement et rotation | 263 ms | 552 ms |
| Construction de la scène active | 6.41 ms | 21.67 ms |
| Rendu WPF logiciel hors écran | 156 ms | 172 ms |
| Génération du PDF complet | 2.89 s | 6.63 s |
| Mémoire privée après ouverture (collecte effectuée) | 229 Mo | 369 Mo |
| Mémoire privée après édition/historique/rendu (collecte effectuée) | 400 Mo | 625 Mo |
| Mémoire privée relevée après PDF (sans collecte finale) | 465 Mo | 746 Mo |

Ces jeux démarrent à froid ; les durées varient entre passages. Le temps PDF ci-dessus mesure la génération ; l’interface lance maintenant cette opération en arrière-plan. La mémoire est celle du processus dédié complet, pas seulement celle des objets du modèle. Les grandes opérations de propriétés/historique restent visibles et demandent une optimisation ciblée ; le dimensionnement est **mesuré**, sans prétendre que toute interaction est fluide à 10 000 composants. La cadence GPU, les mouvements réels à la souris et les différents réglages DPI n’ont pas été mesurés.

### Contrôle visuel et rapports

PDF A3 de deux folios avec références réciproques et PDF de charge de 200 pages contrôlés par Poppler ; pages de références, première/dernière page de charge et rendu avec image officielle inspectés. Captures hors écran de l’interface et fenêtres, dans les deux dispositions et thèmes. Aucune manipulation d’un projet utilisateur ni essai manuel à la souris n’est annoncé.

Rapports historiques : les mesures 0.13 sont conservées dans `build/benchmark-0.13.json` ; les rapports courants correspondent à la dernière version. Principaux artefacts : `build/ui-coil-contacts.png`, `self-test-coil-contacts.gemelec`, `self-test-coil-contacts.pdf`, `self-test-pdf-background.pdf`, `benchmark-100.gemelec`, `benchmark-200.gemelec` et leurs PDF. Le résumé et la feuille de route décrivent le lot suivant : automates/I/O et correction de la latence des transactions sur les grands projets.

## Version 0.12 — 13 octobre 2026

Exécutable autonome Windows x64 publié au chemin habituel. **82 scénarios intégrés et 68 vérifications MCP réussis**, compilation sans avertissement. La reprise 0.11 a d’abord été vérifiée séparément (75/68), sauvegardée en copie locale et commitée/poussée dans `306a101be`.

- Dispositions Compacte et Ruban avec commandes et raccourcis communs ; thèmes Clair et Sombre, clair par défaut. Tests des quatre combinaisons, activation des commandes, conservation des panneaux et migration des anciennes préférences.
- Inspecteur avec groupes, aide, unités de dessin, erreurs visibles sous les champs et sélection multiple explicite. Coordonnées et rotations valides suivent les connexions ; saisie invalide préservant le document et le contrôle. Ctrl+Z/Ctrl+Y partage l’historique du schéma, y compris depuis les champs de propriétés.
- Propriétés du projet, du folio et fenêtre Position des textes immédiates. La dernière valeur valide est conservée ; Fermer n’enregistre pas le fichier. Prévisualisation du glissement directe, publication au schéma temporisée à 120 ms, publication finale à la fin du geste. Contrôle des annulations et styles par les commandes des dialogues.
- Copie du projet utilisateur lue/enregistrée/relue à l’identique dans le modèle. Original préservé. Aucun changement du format `.gemelec` dans ce lot.
- PDF identique octet pour octet entre dispositions/thèmes ; export vectoriel A3 et exports multipages de la suite existante. Interface et PDF rendus hors écran puis inspectés visuellement, y compris les listes déroulantes et symboles dans le thème sombre.
- Scénario représentatif de 100 folios / 5 000 composants / 4 900 conducteurs / 100 textes : sauvegarde 90 ms, ouverture avec rafraîchissement WPF 285 ms, modifications de propriété 121–138 ms, scène active 3,25 ms au dernier passage. Les durées varient entre passages. La mémoire du processus de toute la suite (735 Mo) ne caractérise pas une session isolée ; la cadence à la souris n’a pas été mesurée.

Rapports : `build/self-test.json`, `build/mcp-test.json`, `build/interface-performance.json`. Captures : `build/ui-compact-light.png`, `ui-compact-dark.png`, `ui-ribbon-light.png`, `ui-ribbon-dark.png`, `ui-property-error.png`, `ui-interface-settings.png`, `ui-live-project.png`. La limite historique de 100 folios reste présente ; la charge 200/10 000 est le prochain volet de validation. Ateliers de cartouches et symboles : brouillons explicites conservés.

## Version 0.11 — 12 octobre 2026

Exécutable **0.11.0.0** publié au même chemin `build/GeMMeElec.exe`. **75 scénarios intégrés et 68 vérifications MCP réussis sur l’exécutable publié**, compilation sans avertissement. Contrôles WPF hors écran, sans contrôle souris/clavier du PC ni ouverture du projet utilisateur LaserSieve.

- Tous les textes vectoriels d’un composant sont sélectionnables séparément dans l’aperçu : repère, caractéristiques, description, bornes, textes internes et textes ajoutés. Test du clic direct sur un libellé de borne, déplacement décimal, modification du contenu et suivi de la borne après rotation.
- Ajout et suppression de textes attachés au composant ; alignement collectif sur un côté ou automatique, sans superposition entre les textes du groupe et avec styles conservés. Persistance, export SVG et annulation vérifiés. Les dessins intégrés sous forme d’image conservent leur texte incorporé non éditable.
- Propriétés appliquées immédiatement : composants, noms de reports liés, case Nom indépendant, ordre, conducteurs et annotations. Pas de reconstruction de l’inspecteur pendant la saisie ; les entrées invalides gardent la dernière valeur valide. L’historique regroupe les changements successifs d’un même champ, avec séparation après une pause de deux secondes, changement de champ ou autre commande. Le dialogue Position des textes conserve son aperçu instantané et sa validation groupée.
- MCP : **45 outils**, dont `component_text_list`, `component_text_add`, `component_text_delete`, `component_text_align`. `component_text_update` accepte les nouvelles clés individuelles et le contenu. Vérifications du renommage partagé, de la destination en lecture seule, des clés de bornes, des styles, des alignements, des erreurs atomiques, de l’annulation et de la sauvegarde/relecture.
- Aperçu contrôlé visuellement : `build/self-test-all-texts-dialog.png`. Mesure locale : dix modifications immédiates dans un projet synthétique de 1 000 composants en **370 ms**, sans recréer le champ de saisie (`build/self-test-live-properties-performance.json`). Cette mesure inclut modèle, validation et historique ; elle ne mesure pas la cadence d’affichage GPU.

L’alignement collectif concerne les textes du composant sélectionné. Si le côté choisi est encombré, il conserve ce côté et signale les conflits restants. Les textes internes personnalisés n’altèrent pas la définition de bibliothèque ; remplacer le symbole réinitialise ces personnalisations internes, en conservant les textes ajoutés.

## Version 0.10 — 12 octobre 2026

Exécutable **0.10.0.0** publié au chemin habituel. **69 scénarios intégrés et 56 vérifications MCP réussis** sur cet exécutable, compilation sans avertissement et contrôles hors écran.

- Reports : renommage propagé aux reports effectivement liés, en paire ou étoile ; case **Nom indépendant** pour une exception locale, réintégration au nom partagé en la décochant, connexions conservées, sauvegarde et annulation. La clé MCP `independentName` expose le même mode.
- Destination verte `#008000` sous le nom par défaut, mise à jour du numéro/zone de destination et zone de double-clic calculée sur les textes tournés. Le placement séparé existant reste disponible ; `destinationInline` conserve sa clé de fichier/MCP historique mais signifie désormais « sous le nom ».
- Taille, couleur et rotation propres à chaque texte : repère, caractéristiques, description, nom et destination des reports. Aperçu immédiat, palette existante, code couleur libre, validation des valeurs, persistance et annulation. Les anciens projets utilisent les tailles et couleurs par défaut, sans migration du fichier sur disque.
- Ajustement intelligent : styles conservés et collisions évaluées sur les limites réelles du texte après rotation. Recherche parmi 1 000 composants : **26,4 ms** sur le jeu local, déclenchée uniquement au clic.
- SVG : couleur, taille et rotation contrôlées ; PDF : taille et matrice de rotation vérifiées. Les textes multilignes sont décomposés avec leur rotation pour les deux exports. Aperçus WPF inspectés : `build/self-test-report-style.png` et `build/self-test-text-style-dialog.png`.

Le projet utilisateur LaserSieve n’a pas été ouvert ni enregistré pendant ces modifications du logiciel.

## Version 0.9.2 — 12 octobre 2026

Ajustement intelligent directionnel : **Haut, Bas, Gauche, Droite**. Recherche limitée à 21 dispositions du côté demandé, avec alignement sur le symbole et détection des obstacles. Le mode automatique tous côtés reste disponible. En cas d’encombrement ou de débordement, le côté choisi est conservé et le résultat est signalé comme à vérifier.

Exécutable **0.9.2.0** remplacé au chemin habituel ; **65 scénarios intégrés et 49 vérifications MCP réussis** sur cet exécutable. Vérifications des quatre côtés à 0°, 90°, 180° et 270°, centrage sur le symbole, maintien du côté encombré, clics répétés, reports liés et destination séparée. Les quatre boutons sont exercés hors écran, avec validation, sauvegarde/relecture et annulation/rétablissement. Compilation sans avertissement ; aperçu inspecté sans contrôle du PC. Recherche toujours déclenchée uniquement au clic, avec moins de candidats que le mode automatique.

## Version 0.9.1 — 12 octobre 2026

Bouton **Ajustement intelligent** dans le dialogue de position des textes. Recherche déterministe de 84 dispositions alignées sur le centre ou les bords du symbole, avec prise en compte de sa rotation, des bornes, des textes, des conducteurs et de la zone de dessin. La destination d’un report peut être ajustée séparément ; sa liaison reste intacte. Un message indique les conflits résiduels si aucune proposition libre n’est disponible. Les textes masqués ou vides restent inchangés.

Publication au même chemin `build/GeMMeElec.exe`, version **0.9.1.0** ; **63 scénarios intégrés et 49 vérifications MCP réussis** sur cet exécutable, sans avertissement de compilation. Tests : obstacles, bord du folio, rotation, stabilité des clics répétés, connexion interfolio, validation du brouillon, sauvegarde/relecture et annulation/rétablissement. Aperçu contrôlé hors écran : `build/self-test-smart-text.png`.

Recherche parmi 1 000 composants : **16,5 ms** sur le jeu local (`build/self-test-smart-text-performance.json`). Les obstacles sont collectés une fois par clic ; aucun calcul supplémentaire au survol. Le test dense signale correctement qu’aucune des propositions n’est libre. Ce bouton propose une disposition locale et ne constitue pas un placement global optimal de tous les textes du folio.

## Version 0.9 — 12 octobre 2026

Exécutable autonome `build/GeMMeElec.exe`, version **0.9.0.0**. **59 scénarios intégrés et 49 vérifications MCP réussis sur l’exécutable publié**. Compilation sans erreur ni avertissement, tests hors écran sans contrôle du PC.

- Nom affiché des reports indépendant de la liaison : rendu, continuité électrique, sauvegarde/relecture, annulation/rétablissement et compatibilité avec les anciens projets. Propriétés, menu contextuel, navigateur et MCP exposent le nom libre.
- Dialogue de placement interactif : brouillon isolé, glissement précis sans arrondi, annulation du geste, quatre flèches, icônes accessibles d’alignement, champs numériques mis à jour immédiatement. Validation de plusieurs textes en une seule opération annulable ; abandon sans mutation du projet. Destination d’un report détachable et déplaçable indépendamment.
- Noms des câbles sur le plus long segment, centrés et parallèles au conducteur, quelle que soit la direction du parcours. Routes vides et segments nuls gérés. Orientation SVG contrôlée ; même scène utilisée par le PDF. Aperçus WPF du dialogue et des conducteurs inspectés dans `build/self-test-text-dialog.png` et `build/self-test-wire-orientation.png`.
- Fluidité : 5 000 déplacements et reconstructions de la scène d’un seul composant dans un projet de 1 000 composants en **21,7 ms**. Cette mesure porte sur la logique et la scène, pas sur 5 000 affichages WPF. Le projet complet n’est ni sérialisé ni redessiné à chaque déplacement de l’aperçu.
- `LaserSieve_GH.gemelec` n’a pas été ouvert ni enregistré par l’application : SHA-256 inchangé, `FE799B60123762D3D2E213D2D621A241EB2B0F8FA9482155404E6140D0C0A3A6`.

## Version 0.8 — 12 octobre 2026

Exécutable autonome remplacé au même chemin `build/GeMMeElec.exe`, version **0.8.0.0**. **54 scénarios intégrés et 46 vérifications MCP réussis sur cet exécutable**. Compilation sans erreur ni avertissement ; tests hors écran, sans prise de contrôle du PC.

- Catalogue reconstruit : 3 références de contacteurs (Schneider Electric, ABB, Eaton), 4 aperçus EPLAN distincts, 92 symboles génériques conservés. Les 75 anciennes associations de gammes ont été retirées du catalogue, sans réassigner les composants des projets existants.
- Sources : SHA-256 contrôlé pour chaque dessin et document embarqué ; corruption d’empreinte rejetée. Les dessins intégrés n’ont pas été redessinés. Vérification visuelle des documents et de la correspondance avec les références ; bornes relevées manuellement, pas de certification fabricant. Les documents historiques ne prouvent pas une disponibilité commerciale actuelle.
- EPLAN : aperçus seulement, sans bornes déduites ; placement refusé dans le modèle et via MCP. Ce n’est pas une validation d’une bibliothèque EPLAN native.
- Fabricants : 12 / 10 / 10 bornes, distinction auxiliaires NO/NC, raccordement après rotation, sauvegarde/relecture, copie personnelle, annulation/rétablissement. Les copies perdent le lien au catalogue officiel.
- PDF/SVG/BOM : PNG conservés sans recompression JPEG, rotation SVG prise en compte, dessins dans la BOM HTML. Rendu PDF inspecté ; fond des images sous les fils pour garder les raccordements visibles. Fiches de provenance testées et rendues hors écran.
- Fluidité mesurée : scène de 1 000 composants génériques / 999 fils **14,3 ms**, BOM **1,5 ms** ; création de la scène et du dessin WPF de 1 000 images fabricants **251,5 ms**. Les images identiques sont décodées une seule fois via le cache borné existant ; ces mesures ne sont pas une garantie de fréquence d’affichage sur toutes les machines.

## Version 0.7 — 12 octobre 2026

Publication Release Windows x64 autonome au même chemin `build/GeMMeElec.exe`, version fichier **0.7.0.0**, après fermeture du logiciel par l’utilisateur. Compilation sans erreur ni avertissement. **51 scénarios intégrés et 44 vérifications MCP** réussis sur l’exécutable publié, sans prise de contrôle du PC. `Build.ps1` vérifie désormais le verrouillage de l’exécutable avant publication.

- Reports : paire explicite depuis le dialogue, rejet des noms déjà occupés, liaison réciproque et continuité électrique, sauvegarde/relecture, annulation ; navigation depuis les quatre modes du schéma. Bibliothèque et exports utilisent les mêmes triangles noirs à raccordements opposés, avec bornes inchangées.
- Sélection : rectangle englobant les composants, notes et conducteurs, ajout avec Ctrl, conservation du groupe au clic droit, aucune mutation du projet. Déplacement groupé avec translation des points de passage internes et annulation complète.
- Menus : commandes de rotation et de duplication groupée avec notes et fils internes, puis annulation ; présence des commandes de liaison et de modification des symboles.
- Atelier : dessin avec accrochage, poignées, déplacement, bornes uniques, annulation/rétablissement, suppression des tracés de taille nulle, rejet atomique d’une borne dupliquée, sauvegarde de la copie et intégrité de l’original. Aperçu WPF généré hors écran et inspecté ; les PDF des deux reports ont également été rendus et contrôlés.
- Fluidité : sélection de 1 000 composants autour de **1,5 ms** sur le jeu local. Le survol sans geste ne modifie pas le brouillon et conserve le dessin mis en cache ; 10 000 appels sur un symbole de 2 000 primitives autour de **8 ms**. Ce dernier chiffre mesure la logique sans rendu, pas la cadence de l’interface. Historique de l’atelier borné à 40 opérations.
- Audit du dépôt : sources, bibliothèque, documents, logo, configuration et SDK .NET encore nécessaires. Ancien éditeur tabulaire retiré du code, atelier et menus isolés dans leurs fichiers. Plus de 3 Go de restes Qt/MSYS2/CMake identifiés ; suppression refusée par le contrôle automatique avec « blocked by policy ». Aucun de ces restes, aucun projet et aucune préférence utilisateur n’ont été supprimés.

Les résultats de la livraison se trouvent dans `build/self-test.json`, avec les aperçus et `build/interaction-performance.json`. Le contrôle MCP écrit `build/mcp-test.json`. Les primitives dessinables dans l’atelier sont lignes, rectangles, cercles et textes ; les arcs/polylignes DXF restent importables sous forme de segments. Les limites d’import et les modes avancés des renvois sont conservés.

## Version 0.6 — 12 octobre 2026

Publication autonome au chemin habituel, sans contrôle du PC. **45 scénarios intégrés et 44 vérifications MCP** réussis sur l’exécutable publié ; compilation sans erreur ni avertissement. Le MCP expose 41 outils.

- Jonctions : absence de point avec un seul fil, apparition à la divergence réelle de deux trajets partageant une borne, recalcul après déplacement, suppression et annulation ; dissolution des nœuds de dérivation à deux tronçons compatibles et déplacement avec les segments.
- Reports : flèche pleine rouge, nom bleu, destination verte et zone cliquable couvrant le texte. Rendu vectoriel commun à l’éditeur, au SVG et au PDF.
- Symboles : copie indépendante, maintien des bornes raccordées, rejet atomique d’une suppression de borne utilisée, sauvegarde/relecture, échange `.gemsymbol`, BOM et commandes de l’éditeur hors écran.
- DXF : fixture créée avec netDxf, blocs transformés, cercle, arc et texte ; import sans bornes inventées, pages graphiques exclues des articles BOM. Il ne s’agit pas d’une validation exhaustive des DXF de tous les fabricants ni d’un essai sur une installation EPLAN.
- QET : fixture de connectivité, rotations et métadonnées ; rejet XML invalide et des déclarations d’entités ; quatre exemples installés convertis, dont un projet de 50 folios. Les statistiques et avertissements sont dans `build/conversion-validation.json`.
- Performance locale : environ **8 ms pour les jonctions de 1 000 conducteurs sur une même borne**, via un arbre des trajets communs ; environ **222 ms pour lire et convertir le projet QET de 50 folios**. Mesures synthétiques/locales, hors rendu complet et écriture disque. Résultats dans `build/junction-performance.json` et le rapport de conversion.
- Inspection hors écran du dialogue d’édition et des jonctions ; PDF des deux reports, du schéma industriel QET converti et de la BOM avec symbole personnalisé rendus avec Poppler et contrôlés visuellement.

Les imports utilisent des définitions propres au projet. Aucune modification de bibliothèque globale à l’ouverture, à l’annulation ou pendant les tests. La scène reste en cache au survol ; la liste des copies n’est pas reconstruite lors d’un simple déplacement. Les limites de conversion sont détaillées dans [IMPORTS.md](IMPORTS.md).

## Version 0.5 — 12 octobre 2026

Publication Windows x64 autonome au même chemin `build/GeMMeElec.exe`, version fichier 0.5.0.0. Travail sur `GeMMeElec`, sans prise de contrôle du PC. Le logiciel utilisateur était fermé avant remplacement de l’exécutable.

- **36 scénarios intégrés réussis** : les 29 précédents et sept scénarios couvrant les limites des zones, les numéros de page explicites, les articles regroupés dans la BOM, les renvois réciproques, la continuité entre folios, le déplacement/renommage/suppression avec annulation, la sauvegarde, les chaînes ordonnées et étoiles, la lecture des projets 0.4 et les dialogues hors écran.
- **34 vérifications MCP réussies**, exécutées sur l’exécutable publié dans une instance invisible séparée : 35 outils, 92 symboles, adresses des destinations, parcours des connexions, refus de zones invalides, suppression/annulation, sauvegarde/relecture et coordonnées dans les cellules BOM.
- Contrôle visuel des dix planches de symboles, des dialogues de zones/renvois et de l’éditeur hors écran. PDF de trois pages rendu avec Poppler et inspecté : deux folios liés avec numéros en haut et lettres à gauche, références réciproques et nomenclature avec emplacement des appareils, sans cartouche. Les points de la grille de placement sont absents du PDF.
- Jeu synthétique de **1 000 renvois** : résolution et libellés autour de **8 ms**, résultat dans `build/references-performance.json`. Les groupes sont indexés une fois par reconstruction de scène ; aucune résolution supplémentaire au simple survol. Cette mesure locale ne constitue pas une garantie pour toutes les tailles de projet.

Les identifiants et bornes des anciens symboles restent stables. Les symboles complexes sont des blocs fonctionnels ; les références croisées bobine/contacts et la simulation ne sont pas incluses. Les folios BOM doivent être actualisés après déplacement d’un appareil, modification du repérage ou de l’ordre des pages. Les renvois relient leurs bornes dans le modèle de connexions, sans traverser l’intérieur des appareils.

## Version 0.4 — 12 octobre 2026

Publication autonome au même chemin `build/GeMMeElec.exe`. Travail sur `GeMMeElec`, sans contrôle souris/clavier ni modification d’un projet utilisateur.

- **29 scénarios intégrés** : aux validations précédentes s’ajoutent division du fil avec conservation du tracé, vraie connexion à trois branches, point plein, arrivée perpendiculaire et sortie extérieure de la borne source ; réutilisation d’une borne, reconnexion, undo/redo et rollback ; folios BOM multipages avec photos, en-têtes répétés, sauvegarde, régénération sans doublons, longues lignes continuées intégralement, protection des pages générées et limite de 100 folios ; persistance de la grille et de la palette ; commandes des dialogues hors écran. Le PDF est identique octet pour octet avec la grille affichée ou masquée.
- **27 vérifications MCP** via l’exécutable publié et une session invisible isolée : 32 outils, dérivation, annulation, génération/régénération BOM, refus de placer un composant dans une BOM et relecture de la topologie avec les folios générés.
- Contrôle visuel hors écran des dialogues Grille, Palette et BOM. PDF de 8 pages (schéma + 7 folios BOM pour 70 articles avec images), première et dernière pages de nomenclature rendues avec Poppler ; inspection du PDF de dérivation, point plein et arrivée en T. Aucun cartouche sur la nomenclature.
- Jeux synthétiques : 1 000 articles sur 42 pages sans images, pagination autour de **298 ms** ; recherche du point sur 999 tracés en cache autour de **0,93 ms**. Les durées exactes sont dans `build/workflow-performance.json`. Le rendu bitmap WPF hors écran du folio dense est également mesuré (`offscreenRasterMs`) ; cette mesure logicielle incluant la copie des pixels ne représente pas la cadence GPU de l’interface.

La grille et la scène sont mises en cache ; les tracés nécessaires au survol d’un fil sont calculés une fois après modification. La pagination ne se déclenche que sur demande. Les folios BOM sont des instantanés : actualiser après modification des composants, du regroupement ou de l’ordre des folios. Les croisements ordinaires restent sans connexion ; le routage ne contourne pas automatiquement tous les obstacles du schéma. Les limites des anciennes versions ci-dessous décrivent leur état historique.

## Version 0.3 — 12 octobre 2026

Publication Windows x64 autonome au même chemin `build/GeMMeElec.exe`, sans erreur ni avertissement de compilation. Aucun contrôle souris/clavier du PC pour cette livraison.

- **22 scénarios intégrés réussis** : sauvegarde, connexions et historique existants ; héritage des métadonnées, numéro libre et automatique, ordre des folios, champs personnalisés, rollback des cellules invalides, affectation de modèles, lecture des projets 0.2 ; regroupement/filtrage/ordre des colonnes BOM, échappement HTML et protection contre les formules CSV ; import et déduplication des images, persistance, images PDF/SVG/HTML ; création et rendu hors écran des quatre dialogues, duplication de cartouche, ajout de cellule et affectation par leurs commandes.
- **22 vérifications MCP réussies**, dans une instance WPF invisible isolée : 30 outils déclarés, métadonnées, modèle de cartouche, refus de cellule hors limites, déplacement de folio, import d’image, BOM HTML et sauvegarde/relecture complète, en plus des tests de connexion et d’historique.
- Les aperçus `build/ui-titleblock.png`, `build/ui-bom.png`, `build/ui-sheet.png` et `build/ui-project.png` sont générés hors écran. Contrôle visuel des tableaux, boutons, champs, photo et symbole de remplacement. Il ne s’agit pas d’une session de manipulation manuelle des fenêtres.
- Le cartouche du PDF de test est rendu séparément avec Poppler pour contrôler le logo et les textes. Le fichier de référence QElectroTech a été lu pour analyser sa grille et ses champs ; aucun importeur `.titleblock` n’est annoncé.
- Mesure locale de reconstruction de scène : **15,6 ms** pour 1 000 composants, 999 conducteurs et 12 012 primitives ; calcul BOM : **0,65 ms**. Il s’agit d’un jeu synthétique, pas d’une garantie de cadence pour tous les projets. Résultat détaillé : `build/document-performance.json`.

Optimisations : cache de scène WPF invalidé sur modification, index des composants pour les connexions, caches bornés de texte/images, virtualisation des tableaux, aperçu temporisé (250 ms), budget mémoire de 64 Mo pour l’historique. Les photos sont ramenées à 512 pixels et stockées une fois par contenu.

Limites : édition de cartouche par cellules et coordonnées avec aperçu, sans dessin libre à la souris ; texte réduit pour tenir dans la cellule ; historique de révisions composé de cellules éditables ; HTML imprimable et CSV pour la BOM, sans export XLSX natif ni génération de folios BOM.

## Version 0.2 — 12 octobre 2026

Windows x64, SDK .NET 10.0.401. Publication Release autonome au même chemin `build/GeMMeElec.exe`.

`Build.ps1 -Test` : 15 scénarios réussis. Aux neuf scénarios initiaux s’ajoutent : déplacement orthogonal des segments avec conservation des bornes et historique ; reconnexion et refus des doublons ; position des textes selon les côtés et alignements, après déplacement/rotation ; compatibilité des anciens projets et sauvegarde des décimales ; annulation d’un déplacement provisoire ; positions et visibilité communes à la scène vectorielle et au SVG.

`Test-Mcp.ps1` : 16 vérifications réussies, 24 outils annoncés. Les nouveaux outils de mise à jour des conducteurs et textes sont exercés via le transport stdio et la fenêtre réelle, avec sauvegarde/relecture et refus d’une position invalide.

Essais dans l’application native : désépingler, ouvrir l’onglet latéral, réépingler, fermer les propriétés et les rouvrir par Affichage ; créer une liaison par glisser en mode Sélection ; déplacer un segment et changer sa borne d’arrivée avec les poignées ; positionner le repère par coordonnées libres centrées, saisir `-82,75` et `-30,5`, appliquer et vérifier les décimales dans le projet enregistré. La description dispose d’un placement séparé, masqué par défaut.

Le panneau Propriétés désépinglé est bien retrouvé sous forme d’onglet après fermeture et redémarrage de l’exécutable. Le menu Affichage rétablit ensuite les trois panneaux.

Les positions sont relatives au composant, en unités de dessin. Les fils suivent les bornes sans contourner automatiquement les obstacles. Les panneaux restent ancrés aux côtés de l’éditeur ; ils ne deviennent pas des fenêtres flottantes.

## Première livraison — 9 octobre 2026

Windows x64, 9 octobre 2026. SDK .NET 10.0.401, publication Release autonome en un fichier.

## Tests automatisés

`Build.ps1 -Test` : 9 scénarios réussis.

- Catalogue : 25 familles, trois fabricants distincts, 75 IDs et sources officielles HTTPS.
- Symboles : 34 définitions, bornes uniques et coordonnées finies.
- Connexions conservées après déplacement, rotation, annulation et rétablissement.
- Suppression d’un composant et de ses fils, puis restauration par annulation.
- Rejet atomique des coordonnées invalides et des connexions vers un composant absent.
- Enregistrement/relecture multipage sans perte, accents et état modifié.
- Rejet des en-têtes invalides et IDs dupliqués ; échec d’ouverture préservant le projet courant.
- XML SVG valide, PDF multipage avec table Unicode, nomenclature.
- Schémas MCP et refus de types/arguments incorrects.

`Test-Mcp.ps1` : 14 vérifications réussies sur l’application en cours d’exécution. Négociation, liste de 22 outils, bibliothèque, création, connexion, coordonnées des bornes, undo/redo, erreurs atomiques, refus d’abandon de modifications, suppression incidente, sauvegarde/relecture, UTF-8, trois exports et refus d’écrasement implicite.

## Interface et rendu

Contrôle visuel de l’application native avec le logo fourni, les aperçus des symboles et les panneaux bibliothèque/propriétés. Essai à la souris : recherche « voyant », placement d’un Harmony XB5, raccordement de bornes, déplacement avec suivi du fil, Ctrl+Z puis Ctrl+S. Les compteurs et l’inspecteur suivent ces actions.

Le PDF généré par le MCP a été rendu avec Poppler et inspecté : géométrie, traits, cartouche et caractères français lisibles.

## Limites

Ces vérifications portent sur une première version fonctionnelle. Pas d’essai de très gros projets, de certification électrique ni d’import de documents QElectroTech/EPLAN. Le catalogue référence des gammes, sans constituer un catalogue exhaustif de références commandables. Le tracé orthogonal ne contourne pas automatiquement les obstacles.
