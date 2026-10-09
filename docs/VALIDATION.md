# Vérifications de la première livraison

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
