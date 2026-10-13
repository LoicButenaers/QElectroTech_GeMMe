# GeMMeElec — feuille de route

Demandes métier du 13 octobre 2026. Dépôt `C:\dev\QElectroTech_GeMMe`, branche `GeMMeElec`, livraison `build/GeMMeElec.exe`. Toute étape inclut persistance, annulation, validation et parcours complet. Le résumé doit décrire uniquement les livraisons vérifiées.

## Livré : reprise et interface 0.12

Base 0.11 préservée et vérifiée, puis refonte sur cette base : inspecteur regroupé et immédiat, erreurs locales, propriétés projet/folio/textes immédiates, Compacte/Ruban, Clair/Sombre, commandes communes, panneaux et préférences persistants. Compatibilité des projets, PDF multipage et MCP maintenue. 82 scénarios intégrés et 68 vérifications MCP. Voir `VALIDATION.md` pour les limites de la mesure 100/5 000.

## Livré : références bobines/contacts 0.13

Périmètre : une bobine de relais/contacteur pilote plusieurs fonctions contact, réparties sur les folios. Association explicite par identifiant stable, indépendante de la position, du nom du folio et d’une coïncidence de repères. Les contacts conservent leurs bornes et fils ; cette association ne crée aucune continuité électrique entre bobine et contacts. NO/NF sont des fonctions génériques, sans caractéristiques fabricant déduites.

Parcours : placer une bobine et des contacts génériques → sélectionner un contact → choisir la bobine dans les propriétés → repère commun et adresse de la bobine sous le contact → liste des contacts, bornes et adresses sous la bobine → cliquer une destination dans l’inspecteur pour y naviguer. Renommer le repère commun, déplacer/rotater, changer l’association ou la supprimer ; enregistrement, réouverture et annulation doivent conserver la relation. Supprimer la bobine doit détacher les contacts et signaler la situation, sans supprimer leurs fils.

Livré et vérifié : références, bornages et conducteurs conservés, repère partagé, navigation, détachement, duplication, suppression/annulation de bobine et folio, rôles des symboles personnels, contenu calculé et style éditable, BOM sans double compte et filtre par folio. Format v2 avec migration v1 sans écriture implicite ; 200 folios et 64 Mo, tests 100/5 000 et 200/10 000 dans des processus dédiés. 91 scénarios applicatifs et 78 vérifications MCP sur 47 outils. Historique compressé ; PDF en arrière-plan compatible avec l’édition. Voir `BOBINES_CONTACTS.md` et `VALIDATION.md`.

## Livré : ajustements du dessin 0.14

Alignement lettres/cartouche validé sur aperçu ; repérage commun au projet avec exception par folio ; même numéro sur les branches issues d’une même borne et à travers les bornes de passage/PE ; Maj + glisser pour déplacer les textes directement sur le schéma. Persistance v3 avec migration v1/v2, sauvegarde, annulation, parcours des connexions et MCP vérifiés. Les bornes distinctes d’un équipement restent des départs distincts. Voir `REPERAGE_CONDUCTEURS_TEXTES.md` et `VALIDATION.md`.

## Lot vérifié : conducteur indépendant et styles 0.15

Numéro propre protégé des renommages du réseau ; retour au numéro commun, couleur, épaisseur et type de trait par conducteur. Persistance v4, PDF/SVG, connexions, dérivations et nettoyage vérifiés. Préparation séparée pour préserver l’application ouverte ; publié au chemin habituel après enregistrement et fermeture par l’utilisateur. 105 scénarios applicatifs / 97 contrôles MCP. Voir `REPERAGE_CONDUCTEURS_TEXTES.md` et `VALIDATION.md`.

Une liste de propositions numérotées stables figure dans `PROPOSITIONS_AMELIORATIONS.md`. L’utilisateur choisira les numéros à engager ; ces idées ne sont pas des livraisons annoncées et peuvent infléchir l’ordre des prochains lots.

## Prochain lot : automates et entrées/sorties

Périmètre initial : stations locales et modules génériques, canaux DI/DO/AI/AO avec identifiants stables, adresse explicitement saisie, nom de signal et commentaire. Les caractéristiques et bornages proviennent d’une saisie explicite ou d’une future fiche fabricant sourcée, jamais d’une supposition.

Parcours prévu : Projet → Automates/I/O → créer station et module → définir canaux et adresses → placer une représentation de canal sur un folio → raccorder ses bornes → naviguer entre tableau et dessin → éditer le nom/adresse avec propagation → enregistrer/réouvrir → vérifier doublons d’adresse et canaux non représentés. Inclure suppression/annulation, échanges MCP et PDF. Les symboles PLC/I/O existants restent utilisables sans être reclassés automatiquement.

Traiter avec ce lot la latence des propriétés/historiques sur les grands projets : les mesures de 0.13 mettent en évidence des temps visibles au dimensionnement cible. Conserver les validations atomiques ; envisager des transactions ciblées et une ouverture en arrière-plan plutôt qu’une baisse de validation.

## Ordre métier à respecter

| Ordre | Fonction | Périmètre et parcours à préciser avant le lot |
| --- | --- | --- |
| 1 | Références bobines/contacts | Livré en 0.13 ; première famille générique extensible et rôles personnels explicites. |
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
- La fluidité reste à améliorer : validation et sérialisation de l’ensemble du projet dans les modifications de propriété, malgré la compression de l’historique. Étudier une transaction ciblée seulement avec des tests couvrant rollback, connexions et historique.
