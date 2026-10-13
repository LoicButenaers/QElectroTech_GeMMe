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
| 12 | Sauvegarde automatique et récupération | À développer |
| 14 | PDF avec signets et renvois cliquables | À développer |
| 15 | Gabarits de projets | À développer |
| 16 | Alignement et espacement guidés | Implémenté et vérifié en 0.16 |
| 17 | Réduction du coût des transactions | À mesurer et développer |
| 18 | Présentation commune avec aperçu | À développer |

Chaque fonction doit être utilisable depuis l’interface, persistante lorsqu’elle touche le projet, annulable, testée et décrite dans le récapitulatif Word/PDF. Les tests utilisent des copies et des sessions isolées. Exécutable au chemin habituel ; commits et push uniquement sur `origin/GeMMeElec`.

## Premier lot 0.16

Déplacement précis des numéros de fils, taille/couleur/rotation/visibilité, surlignage de la continuité et navigation entre folios, recherche globale accentuée, tableau d’anomalies filtrable/actualisable, alignement/répartition et guides de déplacement. 111 scénarios intégrés et 101 contrôles MCP sur 51 outils ; format v5 avec migration des anciennes versions. Les autres lignes restent à développer ; le document final sera rédigé après leur validation.

## Deuxième lot 0.17

Numérotation avec formats, compteurs, empreinte d’aperçu et verrous ; borniers avec ports réels, niveaux, pontages et plans paginés sans perte ; implantation de face en millimètres, rails/goulottes, dimensions saisies ou sourcées, chevauchements et folios de cotes ; fiches exactes Schneider LC1D09BD, Phoenix Contact 3209510 et Pilz 750104. Deux PDF fabricants intègres embarqués ; la fiche Phoenix est disponible hors ligne sous forme de caractéristiques, car le téléchargement du PDF a été refusé par son serveur. Les éditions sont explicites ; aucune nouvelle définition électrique native n’est prétendue.

119 scénarios applicatifs et 110 contrôles MCP sur 61 outils ; format v6 et migration v1–v5. Plans de bornier et d’armoire rendus avec Poppler et inspectés. Les propositions 12, 14, 15, 17 et 18, le récapitulatif Word/PDF et la validation finale de charge restent à réaliser.
