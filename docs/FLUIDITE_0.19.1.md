# GeMMeElec 0.19.1 — optimisation de la fluidité

Cette mise à jour optimise le déplacement des symboles et textes, le zoom et le déplacement de la vue. Les fonctionnalités, réglages, outils MCP et le format des projets restent ceux de la version 0.19.

Pendant un glisser, le dessin des objets immobiles reste en mémoire. Seuls l’objet déplacé, ses fils et les indications qui en dépendent sont actualisés. Les renvois, associations bobines/contacts, points de jonction et marquages de câbles restent mis à jour. L’ordre de peinture est conservé, notamment pour les dessins fabricants avec fond opaque.

La grille utilise un motif vectoriel répété au lieu de créer des milliers de points à chaque cran de zoom. Son pas, son intensité et la taille des points restent réglés de la même manière. Le survol recherche la borne la plus proche sans trier toutes les bornes ni créer un nouvel objet pour chacune. Un simple mouvement de souris qui ne change aucun élément visible ne demande plus un nouveau dessin.

Les caches sont limités au folio actif de chaque onglet et sont reconstruits lors d’une modification validée, d’une annulation, d’un changement de folio ou des réglages de lecture. Les objets du projet et les exports PDF/SVG ne dépendent pas de ces caches.

## Vérifier et mesurer

Les nouveaux tests comparent les primitives du dessin optimisé à celles du moteur commun après déplacement : textes, fils, notes, renvois réordonnés, références bobines/contacts, jonctions et marquages de câble. Ils vérifient aussi les aperçus, l’annulation/rétablissement, les bornes survolées après rotation et les points de grille. La suite applicative et les contrôles MCP sont exécutés sur l’exécutable publié.

La commande `--benchmark-interaction` prépare 120 images de déplacement et 60 images de zoom, sur des folios de 50 ou 400 composants appartenant à un projet de 5 000 composants. Les déplacements suivent les mêmes positions dans les deux versions. Elle mesure le temps CPU et les allocations nécessaires à la préparation des dessins WPF hors écran. Ces chiffres ne constituent pas une mesure de cadence GPU ou de latence réelle du pointeur.

Les résultats avant/après sont enregistrés dans `MESURES_FLUIDITE_0.19.1.json`. Le comportement fonctionnel est couvert par les tests ; le gain ressenti dépend aussi de la taille du folio, des images fabricants et du matériel graphique.

## Résultats de la version publiée

**160 tests applicatifs et 135 contrôles MCP passent** sur le même exécutable Windows x64, avec 87 outils MCP.

| Composants sur le folio | Geste | Avant, temps moyen par image | Après |
| --- | --- | --- | --- |
| 50 | Déplacer un texte | 52.48 ms | 0.99 ms |
| 50 | Déplacer un symbole | 27.72 ms | 3.15 ms |
| 50 | Zoom | 52.99 ms | 0.13 ms |
| 400 | Déplacer un texte | 179.66 ms | 1.16 ms |
| 400 | Déplacer un symbole | 165.20 ms | 6.08 ms |
| 400 | Zoom | 10.69 ms | 0.13 ms |

Chaque folio appartient à un projet de 5 000 composants. Les valeurs comprennent la préparation des dessins WPF et excluent la rasterisation GPU. Les compteurs de survol et de déplacement de vue incluent une première construction après annulation ; ils ne représentent pas uniquement le survol à cache chaud.

Pour 120 images de déplacement du texte, les allocations sur le folio dense passent de 2321 Mo à 4.4 Mo. Ce sont les allocations cumulées du thread de test, et non la mémoire résidente de l’application.

SHA256 du binaire vérifié : `2DC066C1947683E77ABE92979E1BCF3E38C393005EA0550D979CF95472D06EA5`.
