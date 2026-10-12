# Vérifications de livraison

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
