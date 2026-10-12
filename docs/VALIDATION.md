# Vérifications de livraison

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
