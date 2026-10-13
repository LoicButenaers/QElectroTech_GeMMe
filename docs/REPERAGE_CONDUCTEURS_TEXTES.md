# Repérage, continuité et déplacement des textes — 0.14

## Cadre et cartouche

La bande des lettres se termine directement sur le bord supérieur du cartouche. Le cartouche commence au même bord gauche extérieur que cette bande. Les proportions des cellules et la hauteur du modèle restent conservées. Ce rendu est partagé par l’écran, le SVG et le PDF ; l’aperçu a été validé par l’utilisateur.

## Configuration du projet et exception d’un folio

Dans **Folio → Repérage lignes / colonnes…**, régler le nombre de colonnes du projet (1 à 40) et de lettres (1 à 26, A à Z). Ces réglages s’appliquent aux folios de schéma qui héritent du projet et aux nouveaux folios. Ils servent aussi aux adresses des renvois et références bobine/contact.

Cocher **Ce folio utilise une configuration différente du projet** pour activer ses deux nombres locaux. Les modifications ultérieures du projet ne modifient pas ce folio. Décocher rétablit l’héritage. Enregistrer valide les quatre réglages en une action annulable ; Annuler abandonne les modifications du dialogue. Les folios de nomenclature n’utilisent pas ce cadre.

Le format `.gemelec` passe en version 3. L’ouverture d’un fichier v1/v2 reprend les nombres du premier folio de schéma comme réglages du projet ; les folios différents deviennent des exceptions. Aucun fichier source n’est réécrit à l’ouverture. Les nombres existants restent conservés.

## Numéros des conducteurs

Les fils raccordés à une même borne ont le même numéro. Les bornes de passage et PE génériques prolongent cette continuité entre leurs ports 1 et 2. Leurs copies personnalisées conservent ces liens déclarés. Les renvois interfolios résolus participent au parcours de connexion ; les croisements graphiques seuls ne créent pas de liaison.

Un nouveau fil reprend le numéro déjà présent, en priorité du côté du départ. Une dérivation reprend le numéro du fil cible. Relier deux continuités différentes harmonise les numéros de la continuité résultante. Lors d’une reconnexion, le numéro de la destination est repris s’il existe ; sinon le fil conserve son numéro.

Modifier **Numéro du conducteur** dans l’inspecteur renomme toute la continuité, avec une seule annulation pour la saisie groupée. Les couleurs et les trajets de chaque fil sont conservés. Les bornes distinctes d’un équipement restent distinctes, même lorsque son repère est commun : aucune continuité interne n’est supposée pour les contacts, bobines ou appareils.

L’ouverture d’un ancien projet ne renumérote pas ses conducteurs. Pour harmoniser un réseau ancien présentant plusieurs numéros, sélectionner un de ses fils et saisir le numéro souhaité dans l’inspecteur. Cette numérotation porte sur les conducteurs ; la gestion de câbles multicœurs reste un lot distinct de la feuille de route.

## Maj + glisser sur le schéma

Maintenir **Maj**, cliquer avec le bouton gauche sur un texte visible du symbole, puis glisser. Le texte suit le pointeur pendant le déplacement. Relâcher termine une action annulable avec **Ctrl+Z** ; **Échap** ou la perte de capture avant relâchement abandonne le déplacement.

Les repères, caractéristiques, descriptions, libellés de bornes, textes internes et ajoutés, destinations des reports et références bobine/contact sont déplaçables. Les destinations calculées conservent leur contenu automatique. Déplacer une destination de report la détache de sa position sous le nom ; son déplacement devient indépendant.

Le symbole, ses bornes physiques et ses conducteurs gardent leur position. Les changements de textes internes sont propres à l’instance et ne modifient pas la bibliothèque. Un simple Maj+clic sans glissement ne crée pas de modification. Les positions précises, styles et rotations sont conservés à l’enregistrement.

L’aperçu de déplacement ne sérialise pas le projet à chaque mouvement. Le début et la validation utilisent encore les transactions complètes du projet ; leur coût sur les grands dossiers est mesuré dans `VALIDATION.md`. Les essais hors écran ne constituent pas une mesure de cadence GPU ni d’entrée souris réelle.
