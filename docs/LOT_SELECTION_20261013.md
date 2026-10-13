# Lot demandé le 13 octobre 2026

Sélection autorisée : **2, 3, 4, 5, 8, 9, 10, 11, 12, 14, 15, 16, 17 et 18**. L’utilisateur est absent et répondra aux questions à son retour. Les choix ordinaires sont résolus avec des valeurs modifiables ; aucune caractéristique fabricant n’est inventée. Les fonctions 1, 6, 7 et 13 restent hors de ce lot.

| Numéro | Fonction | État |
| --- | --- | --- |
| 2 | Déplacement des numéros de fils | Implémenté et vérifié en 0.16 |
| 3 | Surlignage et navigation de circuit | Implémenté et vérifié en 0.16 |
| 4 | Numérotation configurable avec aperçu | Vérifié en 0.17 |
| 5 | Borniers structurés et plans | Vérifié en 0.17 |
| 8 | Implantation d’armoire | Vérifié en 0.17 |
| 9 | Fiches fabricants hors ligne | Trois fiches sourcées vérifiées en 0.17 |
| 10 | Anomalies localisables et actualisables | Implémenté et vérifié en 0.16 |
| 11 | Recherche dans tout le projet | Implémenté et vérifié en 0.16 |
| 12 | Sauvegarde automatique et récupération | Vérifié en 0.18 |
| 14 | PDF avec signets et renvois cliquables | Vérifié en 0.18 |
| 15 | Gabarits de projets | Vérifié en 0.18 |
| 16 | Alignement et espacement guidés | Implémenté et vérifié en 0.16 |
| 17 | Réduction du coût des transactions | Mesuré et vérifié en 0.18 |
| 18 | Présentation commune avec aperçu | Vérifié en 0.18 |

Chaque fonction doit être utilisable depuis l’interface, persistante lorsqu’elle touche le projet, annulable, testée et décrite dans le récapitulatif Word/PDF. Les tests utilisent des copies et des sessions isolées. Exécutable au chemin habituel ; commits et push uniquement sur `origin/GeMMeElec`.

## Premier lot 0.16

Déplacement précis des numéros de fils, taille/couleur/rotation/visibilité, surlignage de la continuité et navigation entre folios, recherche globale accentuée, tableau d’anomalies filtrable/actualisable, alignement/répartition et guides de déplacement. 111 scénarios intégrés et 101 contrôles MCP sur 51 outils ; format v5 avec migration des anciennes versions. Les autres lignes restent à développer ; le document final sera rédigé après leur validation.

## Deuxième lot 0.17

Numérotation avec formats, compteurs, empreinte d’aperçu et verrous ; borniers avec ports réels, niveaux, pontages et plans paginés sans perte ; implantation de face en millimètres, rails/goulottes, dimensions saisies ou sourcées, chevauchements et folios de cotes ; fiches exactes Schneider LC1D09BD, Phoenix Contact 3209510 et Pilz 750104. Deux PDF fabricants intègres embarqués ; la fiche Phoenix est disponible hors ligne sous forme de caractéristiques, car le téléchargement du PDF a été refusé par son serveur. Les éditions sont explicites ; aucune nouvelle définition électrique native n’est prétendue.

119 scénarios applicatifs et 110 contrôles MCP sur 61 outils ; format v6 et migration v1–v5. Plans de bornier et d’armoire rendus avec Poppler et inspectés. Les propositions 12, 14, 15, 17 et 18, le récapitulatif Word/PDF et la validation finale de charge restent à réaliser.

## Livraison finale 0.18

Propositions 12, 14, 15, 17 et 18 implémentées et vérifiées. Copies automatiques compressées et contrôlées, restauration sans association au fichier source ; sommaire paginé, signets et liens PDF ; gabarits avec remappage des identités et palette facultative ; présentation avec aperçu et conservation des styles individuels ; transactions ciblées pour les champs usuels du composant et leur annulation. Les autres transactions conservent leur validation complète.

129 scénarios applicatifs ; 116 contrôles MCP ordinaires, 118 avec lecture/sauvegarde/relecture de la copie du projet utilisateur ; 68 outils. Format 7 avec migration des formats 1 à 6. L’original LaserSieve_GH.gemelec garde son empreinte SHA-256. Mesures 100/5 000 et 200/10 000 dans MESURES_0.18.json ; médianes de saisie répétée 98 → 15 ms et 194 → 26 ms ; annulation 481 → 38 ms et 570 → 50 ms. Les opérations globales restent coûteuses.

Récapitulatif pratique de six pages : output/documents/GeMMeElec_Modifications_0.18.docx et output/pdf/GeMMeElec_Modifications_0.18.pdf ; source lisible LIVRAISON_0.18.md. Les propositions 1, 6, 7 et 13 ne font pas partie de la sélection.
