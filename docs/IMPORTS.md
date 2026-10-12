# Symboles personnalisés et imports — version 0.7

## Modifier un symbole

Sélectionner un appareil et utiliser **Propriétés → Modifier le symbole en copie…**. Le menu **Insérer → Modifier un symbole en copie…** utilise le composant sélectionné, ou à défaut le dernier symbole choisi dans la bibliothèque.

**Insérer → Nouveau symbole…** ouvre un dessin vide. L’atelier comporte une palette d’outils, une liste de parties, une grande grille et un panneau de propriétés. Choisir Ligne, Rectangle ou Cercle, puis maintenir le bouton gauche et tirer. Pour Texte ou Borne, cliquer à l’endroit souhaité. Le texte initial se modifie dans les propriétés. En mode Sélection, glisser une partie ou ses poignées ; les objets tournés se règlent par leurs champs numériques. Les coordonnées sont relatives au centre du symbole, avec accrochage facultatif de 5 unités. Les bornes portent des IDs uniques, éditables.

La molette zoome autour du pointeur ; le bouton central déplace la vue ; **Ajuster la vue** recadre. Les changements à la souris sont immédiats ; les champs précis se valident avec **Appliquer à la partie**. Échap annule un geste et revient à la sélection. Ctrl+Z / Ctrl+Y dans la zone de dessin et les boutons Annuler / Rétablir parcourent l’historique local (40 opérations). Suppr et le clic droit agissent sur la partie sélectionnée. Fermer sans enregistrer ne change pas le projet.

Renseigner le nom, le préfixe et l’encombrement du symbole. **Appliquer** valide ces champs ; **Redimensionner** multiplie géométrie, bornes, dimensions et tailles de texte par le facteur Échelle ×. **Enregistrer la copie** l’ajoute à **Bibliothèque → Mes symboles** dans le projet courant. Le remplacement facultatif du composant sélectionné conserve ses propriétés et raccordements ; la suppression d’une borne déjà raccordée est refusée. Les jonctions et reports sont des éléments spécialisés, modifiables par leurs propres propriétés plutôt que par cet éditeur.

Le fichier `.gemelec` embarque les copies. **Exporter .gemsymbol…** permet de réutiliser une copie dans d’autres projets via **Insérer → Importer un symbole DXF / GeMMeElec…**. La bibliothèque intégrée n’est jamais remplacée. Les fichiers échangés contiennent le dessin et les bornes, pas un catalogue fabricant ni des caractéristiques commerciales vérifiées.

## Importer un symbole fabricant DXF

Télécharger le fichier DXF sur le site du fabricant, puis l’ouvrir par **Insérer → Importer un symbole DXF / GeMMeElec…**. L’import n’effectue aucun téléchargement automatique. L’éditeur affiche les unités déclarées et le rapport de lecture. Régler l’échelle pour obtenir la taille souhaitée sur le schéma, ajouter les bornes avec leurs IDs puis enregistrer la copie. Un dessin d’encombrement mécanique ne devient pas automatiquement un symbole électrique.

Le lecteur accepte les DXF ASCII ou binaires AutoCAD 2000 et versions ultérieures prises en charge par [netDxf 3.0.1](https://www.nuget.org/packages/netDxf/3.0.1). Les anciens DXF R12 doivent être réenregistrés dans une version compatible.

Géométries traitées : lignes, cercles, arcs, polylignes 2D avec arcs, ellipses, splines, blocs insérés avec transformations, textes et attributs. Les courbes sont approchées par segments ; les aplats deviennent des contours. Couleurs, épaisseurs, styles et mises en forme complexes sont simplifiés. Les textes conservent leur rotation, avec une approximation des alignements. Les entités invisibles sont ignorées ; les entités non traitées figurent dans le rapport. Le modèle 3D, les images externes, les blocs dynamiques, les hachures et les mises en page papier ne sont pas garantis. La lecture porte sur l’espace objet.

Limites : fichier de 20 Mo, 50 000 entités parcourues, 20 000 primitives par symbole, profondeur de blocs 12, 256 bornes. Les `.gemsymbol` importés sont limités à 4 Mo. Les copies du projet sont limitées à 2 000. Aucun point de connexion, référence commerciale ou calibre n’est deviné à partir du DXF.

## Convertir un projet QElectroTech

**Importer → Projet QElectroTech (.qet)…** lit le XML du projet et sa collection de symboles embarqués. Le rapport indique le nombre de folios, d’éléments et de conducteurs ainsi que les pertes ou simplifications. **Ouvrir le projet converti** crée un nouveau projet GeMMeElec à enregistrer sous un nouveau nom. Un projet courant modifié reste protégé par la procédure habituelle d’enregistrement.

Sont repris : titres et auteurs des folios, repérage de page, positions et rotations, dessins vectoriels des symboles embarqués, bornes, textes d’instance, repères et informations fabricant disponibles, conducteurs avec extrémités réelles, numéros, couleurs et points de passage. Les définitions converties sont embarquées dans le projet et éditables comme copies. Les identifiants internes des bornes sont remappés en conservant leurs connexions ; ils ne sont pas ajoutés comme numéros visibles sur le schéma.

Les cartouches sont remplacés par le modèle GeMMeElec. Les styles, textes enrichis, géométries complexes et miroirs peuvent nécessiter une correction. Les images et dessins libres des folios ne sont pas repris. Les symboles absents de la collection embarquée sont remplacés par des blocs marqués `?`, en conservant les bornes décrites dans le fichier. Les reports QET restent graphiques : leurs liens interfolios doivent être recréés avec les reports GeMMeElec. Le convertisseur ne lit aucune ressource externe indiquée par le fichier.

Validation locale sur quatre exemples QElectroTech : `schema_indus.qet` (1 folio, 48 éléments, 69 fils), `industrial.qet` (50 folios, 618 éléments, 671 fils), `perceuse.qet` (4 folios, 552 éléments, 156 fils), `convertisseur.qet` (7 folios, 163 éléments, 188 fils). Le succès structurel ne garantit pas une fidélité parfaite ; le rapport reste nécessaire. La structure a été étudiée dans les fichiers locaux et le [code officiel QElectroTech](https://github.com/qelectrotech/qelectrotech-source-mirror/tree/master/sources/qetgraphicsitem).

## Prévoir la récupération d’un projet EPLAN

Cette version offre **Importer → Folios EPLAN / CAO exportés en DXF…** : sélectionner les DXF des pages, un fichier par folio. L’ordre suit celui des fichiers sélectionnés. Chaque page est conservée comme un dessin vectoriel éditable, ajusté à la page si nécessaire. Le rapport indique le facteur appliqué. Ce dessin peut servir de base de reprise ; il ne fournit pas de connexions électriques, d’appareils exploitables dans la BOM ni de liens interfolios automatiques.

EPLAN documente l’export de pages/projets en DXF et l’export de projet en EPJ. Son export EPJ peut inclure les connexions via un réglage explicite ; l’extension seule ne garantit donc pas qu’elles soient présentes. [EPLAN : action d’export](https://www.eplan.help/en-us/Infoportal/Content/api/2023/export.html).

**L’ouverture directe des formats natifs `.zw1`, `.elk` et du format `.epj` n’est pas implémentée.** Aucun EPLAN ni projet client représentatif n’était disponible pour valider une migration sémantique. Si seuls ces fichiers sont disponibles, demander à leur auteur un export DXF pour la récupération graphique actuellement proposée. Une conversion complète des données électriques nécessitera un convertisseur distinct validé sur des exports réels.

## Conservation et exécution

Les fichiers sources sont lus seulement. La lecture UI s’effectue en arrière-plan ; le projet courant n’est remplacé qu’après présentation du rapport. Le résultat n’écrase jamais automatiquement le fichier d’origine. Les mêmes opérations existent par MCP avec analyse préalable et application explicite. La licence du lecteur DXF est embarquée dans l’exécutable et consultable par **Aide → Licence du lecteur DXF** ; [copie de la licence MIT](netDxf-LICENSE.txt).
