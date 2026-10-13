# GeMMeElec résumé des améliorations

Version 0.18 du 13 octobre 2026

Les 14 améliorations que tu as choisies sont intégrées dans cette version. Ce document explique ce qui change, où trouver chaque commande et quelles limites restent à prendre en compte.

L’application reste utilisable hors ligne. Les essais ont été réalisés dans des sessions séparées ; le fichier LaserSieve_GH.gemelec a été conservé intact. Les fonctions choisies ont été livrées progressivement en versions 0.16, 0.17 et 0.18.

| N° | Amélioration | Accès principal |
| --- | --- | --- |
| 2 | Déplacer les numéros des fils | Maj et glisser sur le numéro |
| 3 | Parcourir un circuit | Projet / Surligner et parcourir le circuit |
| 4 | Numérotation automatique | Projet / Numérotation automatique |
| 5 | Borniers et raccordement | Projet / Borniers et plans de raccordement |
| 8 | Implantation d’armoire | Projet / Implantation d’armoire |
| 9 | Fiches fabricants | Projet / Fiches fabricants hors ligne |
| 10 | Anomalies localisables | Projet / Vérifier les connexions et les repères |
| 11 | Recherche globale | Édition / Rechercher dans le projet |
| 12 | Copies et récupération | Fichier / Sauvegarde automatique et récupérer |
| 14 | PDF navigable | Exporter / Options du PDF navigable |
| 15 | Gabarits de projets | Fichier / Enregistrer comme gabarit |
| 16 | Alignement et répartition | Édition / Aligner et répartir la sélection |
| 17 | Propriétés et historique plus rapides | Automatique dans le panneau Propriétés |
| 18 | Présentation commune | Affichage / Présentation commune du schéma |

Les numéros correspondent à tes choix. Les menus sont disponibles dans les dispositions Compacte et Ruban. Ctrl+Z annule les changements du projet ; les réglages personnels de l’interface sont mémorisés séparément.

## Dessiner et retrouver les objets

### 2 Déplacer les numéros des fils

Maintenir Maj, cliquer le numéro et glisser pour le placer librement. Le fil conserve ses connexions et son trajet. Les propriétés du fil permettent aussi de régler les décalages, la taille, la couleur, la rotation et la visibilité. Échap abandonne le déplacement ; Ctrl+Z annule le déplacement terminé.

### 3 Parcourir un circuit complet

Sélectionner un fil puis ouvrir Projet → Surligner / parcourir le circuit. Les fils de sa continuité sont surlignés, y compris à travers les bornes de passage, les pontages déclarés et les reports interfolios. Le tableau permet de rejoindre chaque folio. Effacer le surlignage est accessible depuis le même menu.

La continuité dépend des ports et liaisons déclarés. Un croisement graphique et une association bobine/contact ne créent pas de connexion électrique. Un numéro indépendant reste inclus dans le parcours du circuit.

### 10 Localiser et actualiser les anomalies

Projet → Vérifier les connexions et les repères ouvre une liste filtrable par famille, message ou folio. Actualiser relance les contrôles ; Localiser ou un double-clic rejoint l’objet. Les contrôles couvrent notamment les repères manquants ou doublonnés, bornes libres, reports sans destination, références à compléter et numéros incohérents sur une continuité.

### 11 Rechercher dans tout le projet

Édition → Rechercher dans le projet retrouve les composants, numéros de fils, références, textes et titres de folios. La recherche ignore les accents et la casse. Un double-clic ouvre le bon folio et centre l’objet. Ctrl+F ouvre cette recherche lorsque le dessin a le focus.

### 16 Aligner et répartir les appareils

Sélectionner plusieurs appareils ou textes, puis Édition → Aligner / répartir la sélection. Les commandes alignent leurs centres sur une ligne ou une colonne et répartissent régulièrement au moins trois objets. Pendant un déplacement, les guides indiquent les centres voisins ; Alt désactive cet accrochage. Les fils suivent les bornes.

## Numéroter et documenter les borniers

### 4 Numérotation automatique configurable

Projet → Numérotation automatique permet de choisir les appareils, les conducteurs ou les deux, pour tout le projet ou un folio. Définir le premier compteur, le préfixe des fils et les formats. Exemples : {prefix}{n:000} donne K001 ; {prefix}{folio}-{n:00} donne W3-01. Le compteur peut repartir par folio.

Actualiser l’aperçu affiche les anciennes et nouvelles valeurs. Les conflits empêchent l’application ; une modification des règles impose de revoir l’aperçu. Appliquer réalise les changements en une action annulable. Les règles sont enregistrées avec le projet.

La case de verrouillage du repère, dans les propriétés d’un appareil, protège sa famille bobine/contact. Les fils marqués Numéro indépendant conservent leur numéro. Un réseau électrique qui traverse plusieurs folios garde un numéro commun, même lorsque le périmètre demandé est un seul folio.

### 5 Borniers structurés et plans de raccordement

Placer puis sélectionner les symboles de bornes sur le schéma. Dans Projet → Borniers et plans de raccordement, créer un bornier, lui donner un nom et une localisation, puis Ajouter la sélection du schéma.

Chaque affectation comporte un numéro, un niveau, les ports interne et externe et un commentaire. L’onglet Pontages relie des ports existants et participe réellement à la continuité électrique. Les plans affichent les départs, arrivées, numéros de fils et adresses folio/zone.

Enregistrer le bornier conserve ses affectations. Générer les folios du plan crée ou régénère les pages dans le projet ; les longs commentaires et les pontages sont paginés. Les pages font partie du PDF et ne se modifient pas directement sur le dessin.

Un niveau supplémentaire exige une paire de ports avec une continuité explicitement définie dans le symbole. On peut aussi affecter deux symboles distincts au même numéro avec des niveaux différents. Les deux ports d’une bobine ne constituent pas une borne de passage.

## Implanter une armoire et consulter les fiches

### 8 Implantation d’armoire

Projet → Implantation d’armoire ouvre une vue de face en millimètres. Créer l’enveloppe, puis ajouter les appareils sélectionnés du schéma. Leurs dimensions sont saisies explicitement ou reprises d’une fiche exacte lorsque la largeur et la hauteur sont connues.

Ajouter des rails et goulottes, régler les dimensions et les positions dans le tableau, puis glisser les objets dans l’aperçu. Le déplacement s’accroche tous les 5 mm. Enregistrer conserve l’implantation ; Générer le folio ajoute la vue et les pages de cotes au projet.

Vérifier les chevauchements signale les objets qui se recouvrent et les appareils physiques représentés plusieurs fois. Le placement sur un rail est autorisé. Les objets sont des encombrements rectangulaires en vue de face ; les dégagements thermiques, profondeurs utiles et règles de montage fabricant restent à vérifier.

### 9 Fiches fabricants disponibles hors ligne

Projet → Fiches fabricants hors ligne permet de chercher une référence, lire ses caractéristiques et affecter les données au composant sélectionné. La fiche conserve sa source et son édition. L’affectation ne change pas la géométrie ni le bornage électrique du symbole.

| Référence exacte | Disponibilité et édition |
| --- | --- |
| Schneider LC1D09BD | Fiche et PDF embarqué du 8 septembre 2017. Cotes issues de cette édition. |
| Phoenix Contact 3209510 PT 2,5 | Caractéristiques hors ligne vérifiées le 13 octobre 2026. PDF non embarqué car son serveur a refusé le téléchargement. |
| Pilz 750104 PNOZ s4 | Fiche exacte et manuel PDF EN 14 embarqué. Largeur documentée ; hauteur et profondeur non renseignées. |

Ces trois fiches constituent une sélection limitée. Les éditions anciennes doivent être confrontées au matériel livré. Aucune caractéristique ni dimension manquante n’a été déduite. Les bibliothèques et les documents embarqués restent consultables sans Internet.

- [Schneider Electric LC1D09BD](https://iportal.se.com/Contents/docs/SQD-LC1D09BD.PDF)
- [Phoenix Contact PT 2,5 référence 3209510](https://www.phoenixcontact.com/en-gb/products/feed-through-terminal-block-pt-25-3209510?type=pdf)
- [Pilz PNOZ s4 référence 750104](https://www.pilz.com/en-INT/eshop/product/750104)
- [Manuel Pilz PNOZ s4 édition EN 14](https://www.pilz.com/download/open/PNOZ_s4_Operat_Man_21396-EN-14.pdf)

## Sauvegarder réutiliser et exporter

### 12 Sauvegarde automatique et récupération

Fichier → Sauvegarde automatique / récupérer règle l’activation, l’intervalle et le nombre de copies. Valeurs par défaut : une copie toutes les 120 secondes pour un projet modifié, avec cinq copies par projet. L’intervalle va de 30 à 3 600 secondes et la conservation de 1 à 20 copies.

Les copies sont compressées dans le dossier local GeMMeElec-recovery du compte Windows. Une écriture s’exécute en arrière-plan à partir d’un instantané, hors déplacement en cours. Les copies disponibles sont proposées au démarrage et peuvent être ouvertes depuis le tableau de récupération.

Restaurer ouvre un projet non enregistré : utiliser Enregistrer sous pour garder le fichier d’origine. L’intégrité de la copie est contrôlée avant ouverture. La copie automatique complète l’enregistrement manuel ; elle ne récupère pas les changements intervenus depuis sa dernière exécution.

### 14 PDF navigable

Exporter → Options du PDF navigable propose trois réglages : sommaire avec liens vers les folios, signets de navigation et liens sur les reports ou références bobine/contact. Exporter ensuite Tous les folios en PDF. Le sommaire est paginé et ne crée pas de folio dans le projet.

Les nouveaux projets activent ces options. À l’ouverture d’un ancien projet, le sommaire reste désactivé pour conserver le nombre de pages habituel ; il peut être activé manuellement. Les numéros de folios ne changent pas quand le PDF reçoit un sommaire.

### 15 Gabarits de projets

Fichier → Enregistrer comme gabarit crée un fichier .gemtemplate avec les cartouches, images, symboles personnels, repérage, règles et organisation des folios. Par défaut, le nouveau dossier contient des schémas vides. Une case permet d’inclure les appareils, fils, borniers et implantations ; une autre inclut la palette et la couleur des prochains fils.

Fichier → Nouveau depuis un gabarit demande le nom du dossier et crée des identifiants indépendants, avec les associations remappées. La palette incluse est reprise dans les préférences de dessin. Le projet créé doit ensuite être enregistré en .gemelec ; le gabarit reste disponible pour d’autres dossiers.

## Présentation performances et vérifications

### 18 Présentation commune avec aperçu

Affichage → Présentation commune du schéma propose Technique, Noir et Lecture agrandie. Ajuster tailles des repères, descriptions, caractéristiques et numéros de fils, puis couleurs des symboles et fils et épaisseur commune. Actualiser l’aperçu montre le résultat sur une copie du folio avant application.

Conserver les réglages individuels protège les styles personnalisés et les conducteurs à numéro indépendant. Décocher autorise l’uniformisation. Une modification après aperçu demande de vérifier le nouvel aperçu avant de cliquer de nouveau sur Appliquer. Les images fabricants restent dans leurs couleurs d’origine ; les positions et connexions sont conservées.

### 17 Propriétés et annulation plus rapides

La saisie du repère, de la description, des données d’article, des coordonnées, de la rotation et des libellés de bornes utilise des transactions limitées aux folios concernés. Les contacts associés restent synchronisés et l’historique local se combine avec les opérations structurelles.

| Composants | Opération | Version 0.17 | Version 0.18 |
| --- | --- | --- | --- |
| 5 000 | Saisie répétée | 98 ms | 15 ms |
| 5 000 | Annuler | 481 ms | 38 ms |
| 5 000 | Rétablir | 453 ms | 25 ms |
| 10 000 | Saisie répétée | 194 ms | 26 ms |
| 10 000 | Annuler | 570 ms | 50 ms |
| 10 000 | Rétablir | 519 ms | 38 ms |

Mesures locales en processus isolés, sur 100 et 200 folios avec 4 900 et 9 800 fils. La saisie répétée est la médiane des quatre modifications après la première. La première modification mesurée reste à 52 ms pour 5 000 composants et 83 ms pour 10 000 composants. Les opérations globales et certains exports demeurent coûteux ; ces résultats ne mesurent pas une cadence GPU ou de souris.

### Vérifications et compatibilité

129 scénarios applicatifs et 118 contrôles MCP ont réussi sur la livraison, avec 68 outils MCP. Sauvegarde, relecture, connexions, annulation et migrations des formats 1 à 6 vers le format 7 sont vérifiées. Les liens et signets du PDF ont été relus puis les pages rendues et inspectées.

Limites conservées : 200 folios, fichier de projet de 64 Mo, contrôles de cohérence sans certification électrique, import QET partiel et DXF graphique. Les anciens exécutables ne lisent pas le format 7. Les plans générés se régénèrent depuis leur gestionnaire après modification de leurs données.
