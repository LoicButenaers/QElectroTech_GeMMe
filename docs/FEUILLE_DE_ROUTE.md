# GeMMeElec — feuille de route

Demandes métier du 13 octobre 2026. Dépôt `C:\dev\QElectroTech_GeMMe`, branche `GeMMeElec`, livraison `build/GeMMeElec.exe`. Toute étape inclut persistance, annulation, validation et parcours complet. Le résumé doit décrire uniquement les livraisons vérifiées.

## Livré : reprise et interface 0.12

Base 0.11 préservée et vérifiée, puis refonte sur cette base : inspecteur regroupé et immédiat, erreurs locales, propriétés projet/folio/textes immédiates, Compacte/Ruban, Clair/Sombre, commandes communes, panneaux et préférences persistants. Compatibilité des projets, PDF multipage et MCP maintenue. 82 scénarios intégrés et 68 vérifications MCP. Voir `VALIDATION.md` pour les limites de la mesure 100/5 000.

## Prochain lot : références bobines/contacts

Périmètre : une bobine de relais/contacteur pilote plusieurs fonctions contact, réparties sur les folios. Association explicite par identifiant stable, indépendante de la position, du nom du folio et d’une coïncidence de repères. Les contacts conservent leurs bornes et fils ; cette association ne crée aucune continuité électrique entre bobine et contacts. NO/NF sont des fonctions génériques, sans caractéristiques fabricant déduites.

Parcours : placer une bobine et des contacts génériques → sélectionner un contact → choisir la bobine dans les propriétés → repère commun et adresse de la bobine sous le contact → liste des contacts, bornes et adresses sous la bobine → cliquer une destination dans l’inspecteur pour y naviguer. Renommer le repère commun, déplacer/rotater, changer l’association ou la supprimer ; enregistrement, réouverture et annulation doivent conserver la relation. Supprimer la bobine doit détacher les contacts et signaler la situation, sans supprimer leurs fils.

Vérifications : deux folios et plusieurs contacts NO/NF, références recalculées après réorganisation des folios, bornages génériques conservés, changement de bobine, navigation, suppression/annulation, duplication, cohérence du repère, contrôles d’orphelins, PDF multipage, migration versionnée des fichiers et MCP. Étendre en parallèle la limite historique de 100 folios, puis mesurer 100/5 000 et 200/10 000 dans une instance dédiée.

## Ordre métier à respecter

| Ordre | Fonction | Périmètre et parcours à préciser avant le lot |
| --- | --- | --- |
| 1 | Références bobines/contacts | Parcours défini ci-dessus ; première famille générique extensible. |
| 2 | Automates et I/O | Créer station/module → configurer canaux DI/DO/AI/AO et adresses explicites → placer/raccorder les représentations → table des I/O et navigation → contrôle des doublons et canaux manquants. Aucun bornage constructeur déduit. |
| 3 | Borniers et raccordement | Créer bornier → affecter bornes numérotées et niveaux → raccordements internes/externes → générer un plan actualisable → vérifier numéros et extrémités. |
| 4 | Câbles multiconducteurs | Définir câble et conducteurs → affecter chaque conducteur à des extrémités → éditer couleurs/repères → plan et contrôles d’affectation. |
| 5 | Implantation d’armoire | Définir enveloppe et rails/goulottes → affecter appareils du schéma → placer avec dimensions explicitement saisies ou sourcées → contrôler encombrement et correspondance. |
| 6 | Composants fabricants | Sélection limitée Schneider Electric (disjoncteurs/contacteurs/variateurs/boutons), Phoenix Contact (borniers/alimentations), Pilz (sécurité). Sources officielles et provenance ; mises à jour de bibliothèques séparées d’Internet, application hors ligne. |
| 7 | Numérotation automatique | Choisir règles/périmètre → aperçu des changements et conflits → exécuter en une transaction → actualiser les références et plans ; respecter les repères verrouillés. |
| 8 | Contrôles électriques | Règles documentées et limites explicites → liste des anomalies localisables → correction et relance. Ne pas confondre contrôle de cohérence, dimensionnement et certification. |

## Exigences continues

- Préserver les documents utilisateur, anciennes branches, logo et nom ; aucune réécriture complète prévue.
- Conserver connexions/dérivations, reports, textes, cartouches, nomenclature, PDF multipage et MCP.
- Utiliser des copies pour les essais ; tester le format et sa migration lorsque nécessaire.
- Compiler/tester le lot, inspecter les rendus, publier au chemin habituel, commit descriptif et push sans force sur `origin/GeMMeElec`.
- Mesurer les opérations coûteuses sur le PC, avec folios, composants, fils et textes représentatifs. Les mesures hors écran ne prouvent pas une cadence d’affichage.
- La fluidité reste à améliorer : validation et snapshots de l’ensemble du projet dans les modifications de propriété. Étudier une transaction ciblée seulement avec des tests couvrant rollback, connexions et historique.
