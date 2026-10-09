# GeMMe — recherche EPLAN et cahier des charges industriel

Date : 9 octobre 2026. Branche : `EPLAN_DESIGN`.
Décisions utilisateur : schémas industriels IEC, Belgique/Europe ; fabricants
choisis par famille. Ce document est le résultat de la phase de recherche et
de l'audit initial. Les exigences ci-dessous ne sont pas des fonctions nouvelles
déjà livrées. La base QElectroTech a été compilée le 8 octobre ; les évolutions
effectivement développées sont consignées dans le suivi de livraison ci-dessous.

## Suivi de livraison — identité GeMMe et interface industrielle

Demande complémentaire du 9 octobre : intégrer le logo fourni et viser les
fonctionnalités ainsi que l'organisation de l'interface d'EPLAN Electric P8.
Cette cible ne signifie pas que la parité fonctionnelle est déjà atteinte.

Changements de ce lot :

- Logo original conservé dans `ico/gemme-logo.png`, utilisé par la fenêtre,
  l'écran de démarrage, le ruban et la boîte À propos. Icône Windows dérivée
  aux tailles 16, 24, 32, 48, 64, 128 et 256 pixels, sans redessiner le logo.
- Ruban à huit onglets : Accueil, Insertion, Édition, Folios, Appareils,
  Rapports, Affichage et Outils. Groupes de commandes et menus déroulants.
- Menus Insertion, Folios, Appareils et Rapports, avec sous-menus Borniers
  et Connexions. Les commandes appellent les fonctions existantes du logiciel.
- Nouveau navigateur « Appareils et fonctions » couvrant tous les folios du
  projet actif : filtre par repère, fabricant, référence ou désignation, colonnes
  triables et navigation au symbole par double-clic/Entrée. Actualisation après
  modification par l'historique, changement de projet, ouverture du panneau,
  ou bouton Actualiser. Il liste les représentations, pas un décompte des
  appareils physiques pour la nomenclature.
- Recherche de commandes, ouverture/enregistrement, édition, placement de
  symboles, gestion des folios, numérotation, borniers, nomenclature et exports
  accessibles depuis le ruban. Les commandes conservent leur état contextuel
  et leurs raccourcis grâce au partage des mêmes `QAction`.
- Ruban réductible, dernier onglet mémorisé, défilement horizontal sur les
  petits écrans. Visibilité réglable dans Configuration. Dispositions
  existantes conservées ; les nouvelles configurations privilégient le ruban,
  regroupent Projets/Collections à gauche et masquent la numérotation jusqu'à
  son ouverture depuis Appareils.
- Nom GeMMe dans la fenêtre principale et la boîte À propos ; crédits,
  licences, format `.qet` et identifiant des réglages QElectroTech conservés.

Référence d'interface : [aide officielle EPLAN 2027](https://eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/userinterface_k_hintergrund.htm).
Elle décrit des onglets par activité, des groupes, des listes déroulantes,
une recherche de commandes et des navigateurs ancrables. Le ruban GeMMe
adopte cette organisation générale avec ses propres commandes et son logo.

Restent notamment à développer/qualifier : gestion unifiée appareil/fonctions,
catalogue fabricant contrôlé, diagnostics électriques complets, gestion
avancée des E/S, câbles et borniers, variantes de macros, révisions,
interfaces AutomationML et rapports paramétrables. Les boîtes de dialogue
métier existantes restent celles de QElectroTech ; cette livraison ne prétend
pas reproduire toutes les fenêtres ou fonctions P8. Les exigences V1–V4
ci-dessous restent le suivi de ces écarts.

Vérifications du lot sous Windows, le 9 octobre 2026 : compilation Release
Qt 6/GCC réussie ; exécution avec seulement les dossiers Windows dans le PATH ;
ouverture d'une copie de `tremie_vibrante.qet` (3 folios, 98 éléments,
77 conducteurs) ; export PDF de 3 pages et nomenclature CSV conservant la
référence fabricant de test. Dans la fenêtre : navigateur à 98 représentations,
filtre ramenant la liste à une référence, activation du symbole correspondant,
ajout d'un quatrième folio depuis le ruban puis annulation vers trois folios.
Vérification visuelle du logo dans À propos, des titres des menus, des commandes
désactivées sans projet, du ruban réduit/développé et du changement d'onglet en
mode réduit. Dernier onglet conservé après redémarrage.
La référence `TEST-NAV-001` est une donnée de recette dans `build/` uniquement,
pas un article de catalogue distribué. La suite QtTest complète n'a pas été
exécutée ; ces contrôles ne valent pas qualification complète de la V1.

## 1. Décision technique

Conserver QElectroTech et le faire évoluer. La base dispose déjà d'un éditeur
vectoriel, d'un modèle électrique, d'un format de projet transportable, d'un
historique annuler/rétablir et d'une collection importante. La réécriture
complète obligerait à refaire et fiabiliser ces éléments avant de produire un
premier schéma utilisable. C++17 et Qt 6 restent adaptés à une application
Windows locale, utilisable sans connexion Internet.

L'objectif est une alternative indépendante destinée au même métier. La parité
avec toute la suite EPLAN est un programme en plusieurs lots, à mesurer par des
scénarios de travail. Une apparence similaire ne démontrerait pas cette parité.
Le nom du produit reste GeMMe ; `EPLAN_DESIGN` est le nom du chantier Git.

## 2. Référence EPLAN étudiée

EPLAN Platform 2027 est disponible depuis septembre 2026. La recherche combine
la présentation 2027, l'aide publique 2027 et la description détaillée P8 2026.
Les différences d'éditions, de modules et de licences devront être conservées
dans toute comparaison commerciale. La nouvelle plateforme met notamment en
avant les données d'appareils, leur synchronisation et un assistant intégré.
Ces capacités connectées relèvent de lots ultérieurs pour GeMMe.
[Annonce officielle 2027](https://www.eplan.com/de-en/about-us/press/the-eplan-platform-2027-has-arrived/).

Electric P8 est la référence principale pour le schéma électrique : connexions
assistées, numérotation, références croisées, choix d'appareils, documentation
et rapports. La conception à partir de modèles permet de réutiliser les
circuits et les données. [Présentation Electric P8](https://www.eplan.com/gb-en/products/eplan-electric-p8/).

La description de performance consultée couvre les éditions Advanced, Pro et
Premium 2026. Elle distingue l'ingénierie électrique, les fonctions fluidiques,
la collaboration et l'intégration ; elle décrit aussi les services de gestion
des données, de consultation et de génération de projets.
[Description P8 2026](https://www.eplan.com/content/dam/eplan/corporate/performance-descriptions/2026/en/performance-description-eplan-electric-p8.pdf).

Il faut distinguer les périmètres suivants :

| Périmètre EPLAN | Conséquence pour GeMMe |
| --- | --- |
| Electric P8 | Priorité : conception et documentation électrique 2D. |
| Data Portal et gestion d'articles | Catalogue de pièces, caractéristiques, bornes et documents ; indispensable à terme. |
| Pro Panel | Implantation d'armoires 3D, routage physique et préparation de fabrication ; après le schéma. |
| Preplanning | Préparation des fonctions et équipements avant les folios ; après la V1. |
| Cable proD / Harness proD | Câblage de machines et faisceaux 3D ; chantier distinct. |
| Engineering Configuration | Génération paramétrée de dossiers ; après stabilisation du modèle métier. |
| Applications de collaboration et intégrations ERP/PDM/PLM | Révisions, partage et échanges d'entreprise ; après la version locale. |

Cette séparation suit le [portefeuille officiel](https://www.eplan.com/us-en/products/).
Elle évite de confondre « dessiner un schéma » avec l'ensemble de la chaîne
d'ingénierie et de fabrication.

## 3. Audit de la base locale

Référence du code avant cette phase : `2b89aa48d`, issue de l'import officiel
`a022d0333`. L'audit est fondé sur les sources, la documentation locale et les
vérifications de compilation déjà réalisées. Présence dans le code ne signifie
ni validation métier complète ni équivalence à EPLAN.

| Capacité observée | Point d'entrée local | Conclusion |
| --- | --- | --- |
| Éditeur, projets et folios | `sources/qetdiagrameditor.cpp`, `qetproject.cpp`, `diagram.cpp` | Socle à conserver. |
| Conducteurs, routage et raccordement | `sources/conductorrouter.cpp`, `autobreakconductor.cpp` | Présent ; comportements à tester sur les cas industriels. |
| Repérage automatique | `sources/autoNum/` | Présent ; configurer des conventions de projet homogènes. |
| Bobines, contacts et références croisées | `sources/qetgraphicsitem/masterelement.cpp`, `crossrefitem.cpp` | Présent ; contrôler la cohérence avec les articles physiques. |
| Borniers et pontages | `sources/TerminalStrip/` | Présent ; audit des niveaux et accessoires nécessaire. |
| Champs fabricant et nomenclature | `sources/properties/deviceinformation.cpp`, `sources/bomexport.cpp` | Présent, documenté dans `docs/smart-device-bom.md`. |
| Répertoire d'articles CSV | `sources/materiallist/` | Présent ; point d'appui pour le catalogue initial. |
| Listes de fils et connexions | `sources/wiringlistexport.cpp`, `conductornumexport.cpp` | Présent ; valider les correspondances entre données et dessin. |
| Contrôles de raccordement | `sources/wiringrules.h` | Limites sur le nombre de fils ; ce n'est pas un moteur général de validation électrique. |
| Automates | `sources/editor/graphicspart/partplctable.cpp`, `sources/ui/plclinkwidget.cpp` | Représentation présente ; gestion complète des E/S et échanges à auditer. |
| Automatisation | `sources/scripting/` | API et macros existantes ; réutilisables pour les essais et futurs modèles. |
| PDF et exports | `sources/cli_export.cpp`, `sources/pdf_links.cpp` | Lecture et export PDF déjà vérifiés sur un exemple de trois folios. |
| Import EDZ | `sources/import/edz/README.md` | Import limité : données portables et symbole générique ; pas de restitution des macros graphiques EPLAN. |

L'inventaire du sous-module `elements` donne **8 839 fichiers `.elmt`**, dont
**4 103** dans **134 dossiers de fabricants**. Ce total inclut des familles,
vues et niveaux de détail hétérogènes ; il ne correspond pas à 8 839 articles
industriels IEC qualifiés. Les 82 fichiers `tst_*.cpp` repérés dans
`tests/qttest` ne constituent pas non plus une preuve que tous les essais passent.

Exemples de couverture brute dans `10_electric/20_manufacturers_articles` :

| Fabricant | Fichiers de symboles |
| --- | ---: |
| WAGO | 1 982 |
| Siemens | 452 |
| Schneider Electric | 300 |
| Beckhoff | 81 |
| Weidmüller | 62 |
| Omron | 57 |
| Pilz | 44 |
| Eaton / Moeller | 23 |
| ABB | 22 |
| SICK | 14 |
| Phoenix Contact | 13 |
| ifm | 13 |
| Mean Well | 12 |

Ces nombres décrivent uniquement le contenu local, pas les parts de marché ni
la qualité des bibliothèques. Une référence n'est pas considérée validée parce
que le nom du fabricant figure dans son chemin.

## 4. Exigences proposées pour le logiciel

Les exigences suivantes sont notre conception produit, construite à partir
des besoins industriels et de l'audit. « Socle » signifie qu'une partie existe
dans QElectroTech et doit être intégrée et testée dans le parcours GeMMe.

| ID | Besoin et résultat attendu | Traitement |
| --- | --- | --- |
| V1-01 | Créer un projet industriel avec langue, installation, armoire et cartouche. | Socle + assistant de création. |
| V1-02 | Ajouter, nommer, dupliquer et ordonner les folios ; naviguer entre eux. | Socle + ergonomie. |
| V1-03 | Placer un symbole par recherche, aperçu et glisser-déposer ; rotation et alignement. | Socle + palette organisée. |
| V1-04 | Relier les bornes avec des conducteurs orthogonaux ; distinguer croisement et jonction. | Socle + cas de régression. |
| V1-05 | Déplacer un appareil sans perdre ses connexions ; annuler/rétablir toute modification. | Socle + essais. |
| V1-06 | Gérer potentiels, PE, fils, couleurs, sections et repères, sans assimiler cela à un calcul de dimensionnement. | Socle + données structurées. |
| V1-07 | Renuméroter sans collisions, avec aperçu ; conserver les repères verrouillés. | Socle + validation. |
| V1-08 | Associer bobine et contacts ; naviguer par les références croisées et reports inter-folios. | Socle + validation. |
| V1-09 | Associer un article à un appareil, conserver ses caractéristiques et sa provenance. | Socle + catalogue qualifié. |
| V1-10 | Gérer borniers simples, PE et pontages ; produire un plan de bornier exploitable. | Socle + qualification. |
| V1-11 | Générer nomenclature, liste de fils et liste de câbles depuis les données du projet. | Socle + harmonisation. |
| V1-12 | Exporter un PDF multipage lisible et les tableaux en CSV UTF-8. | Socle + vérification systématique. |
| V1-13 | Enregistrer et rouvrir sans perte ; inclure les symboles nécessaires dans le projet. | Socle + tests aller-retour. |
| V1-14 | Retrouver les commandes ; propriétés contextuelles ; dispositions de panneaux mémorisées. | Socle + espace de travail industriel. |
| V1-15 | Signaler les doublons de repères, références manquantes et bornes incohérentes ; naviguer vers le défaut. | Contrôle de cohérence à compléter. |
| V2-01 | Un appareil physique possède plusieurs fonctions et représentations, sans double comptage en nomenclature. | Modèle métier à consolider. |
| V2-02 | Racks, cartes et canaux d'automates ; adresses, textes fonctionnels et vues d'ensemble. | Fonctions à étendre. |
| V2-03 | Câbles multiconducteurs, réserves, blindages et connecteurs avec brochage. | Modèle et rapports à compléter. |
| V2-04 | Borniers multiniveaux, accessoires, embouts et plans de raccordement détaillés. | Audit + extensions. |
| V2-05 | Macros paramétrées de départ moteur, variateur, alimentation et automate, avec variantes. | Réutiliser l'API et les macros. |
| V2-06 | Rapports configurables, sommaire, listes d'appareils et changements propagés. | Socle + modèles de rapports. |
| V2-07 | Importer des articles CSV/JSON avec prévisualisation, unités et gestion des doublons. | Catalogue à développer. |
| V2-08 | Contrôles électriques documentés et paramétrables avec niveau de gravité et justification. | Moteur de règles à développer. |
| V3-01 | Révisions du dossier, comparaison des objets et états de validation. | À développer. |
| V3-02 | Import/export d'E/S et échanges avec les outils d'automatisme. | Étudier AutomationML et les formats fabricants. |
| V3-03 | Implantation d'armoire 2D à l'échelle, encombrements, rails et goulottes. | Qualifier le socle graphique avant toute 3D. |
| V3-04 | Éditions multilingues du même dossier et données communes à une équipe. | À étendre. |
| V4-01 | Implantation 3D, collisions, routage physique et longueurs de coupe. | Chantier distinct après le schéma fiable. |
| V4-02 | Gestion partagée des catalogues, travail simultané et droits d'accès. | Architecture serveur ultérieure. |
| V4-03 | Connecteurs ERP/PDM/PLM, génération industrielle et suivi de fabrication. | Intégrations spécifiques. |
| V4-04 | Assistance IA explicable, revue des propositions et annulation des actions. | Après stabilisation des données et contrôles. |

Points de comparaison ciblés : EPLAN distingue plusieurs représentations et
variantes de macros ; les objets purement graphiques ne participent pas aux
mêmes rapports que les fonctions électriques.
[Aide macros 2027](https://www.eplan.help/en-US/Infoportal/Content/Plattform/2027/Content/htm/macrosgui_r_makrokasten.htm).
Ses échanges d'automates utilisent notamment AutomationML, tandis que la
comparaison de projets aide à retrouver les changements après import.
[Interfaces automates](https://www.eplan.help/techtipps/en-us/SPS/Overview-of-supported-PLC-systems.pdf),
[comparaison après échange PLC](https://eplan.help/techtipps/en-us/SPS/TechTip-Finding-and-checking-project-changes-from-PLC-data-exchange-in-EPLAN.pdf).

## 5. Bibliothèque : symboles, articles et fabricants

### Modèle retenu

Un symbole décrit une fonction et ses points de connexion. Un article décrit
un matériel commandable. Plusieurs articles peuvent partager un symbole, mais
leurs bornes, fonctions disponibles et caractéristiques doivent correspondre.
La tension d'une bobine ne doit pas être confondue avec la tension admissible
de ses contacts ; les variantes AC/DC et NO/NF doivent rester explicites.

Un catalogue utile contient donc plus que des images. Le Data Portal associe
les données techniques et commerciales aux macros, représentations et
documents. GeMMe doit proposer sa propre structure de données et un catalogue
local utilisable hors ligne. [Données du Data Portal](https://www.eplan.com/gb-en/products/eplan-data-portal/).

### Trois fabricants par famille

Il s'agit d'une **sélection de travail adaptée aux armoires européennes**, pas
d'un classement des trois leaders par parts de marché. Les gammes sont des
candidats de qualification ; elles ne sont pas des équivalents interchangeables.

| Famille initiale | Fabricant 1 | Fabricant 2 | Fabricant 3 |
| --- | --- | --- | --- |
| Disjoncteurs et protections modulaires | Schneider Electric | Siemens | ABB |
| Protection moteur et relais thermiques | Schneider Electric | Siemens | Eaton |
| Contacteurs et blocs auxiliaires | Schneider Electric | Siemens | Eaton |
| Relais d'interface et temporisation | Omron | Finder | Phoenix Contact |
| Relais de sécurité | Pilz | Siemens | Schneider Electric |
| Boutons, sélecteurs et voyants | Schneider Electric | Siemens | Eaton |
| Bornes, PE et accessoires de borniers | Phoenix Contact | Weidmüller | WAGO |
| Alimentations de commande 24 V DC | Siemens | Phoenix Contact | Mean Well |
| Automates et E/S déportées | Siemens | Schneider Electric | Beckhoff |
| Variateurs de fréquence | Siemens | Schneider Electric | ABB |
| Capteurs de proximité et photoélectriques | ifm | SICK | Pepperl+Fuchs |
| Moteurs et motoréducteurs | ABB | SEW-EURODRIVE | WEG |
| Connecteurs industriels | Phoenix Contact | HARTING | Weidmüller |
| Câbles de puissance, commande et données | LAPP | HELUKABEL | Nexans |
| Interrupteurs-sectionneurs | ABB | Schneider Electric | Eaton |
| Armoires et coffrets, pour nomenclature et future implantation | Rittal | Schneider Electric | Eaton |

La sélection couvre 16 familles et 48 associations famille/fabricant. Le premier
catalogue qualifié visera cinq références courantes par association, soit
240 fiches d'articles ; c'est un objectif de développement, pas un inventaire
déjà livré. Les symboles génériques nécessaires aux schémas peuvent être
disponibles avant que toutes ces fiches soient qualifiées. Fusibles spéciaux,
parafoudres, transformateurs, mesure, réseaux et accessoires d'armoires seront
ajoutés comme familles explicites lorsque leur première application sera cadrée.

### Sources fabricants repérées

Sources de qualification et de recherche des références, consultées le
9 octobre 2026. La disponibilité d'un téléchargement CAD ne garantit pas
qu'il s'agisse d'un symbole électrique ni qu'il soit redistribuable.

- [Schneider Electric — dessins CAD](https://www.se.com/us/en/work/support/resources-and-tools/cad-drawings/).
- [Siemens — documentation et CAx Download Manager](https://www.siemens.com/en-gb/support/documentation-downloads/).
- [ABB — intégration des données électriques](https://new.abb.com/low-voltage/products/eplan-partnership-efficient-electrical-system-engineering), [variateurs](https://product-data.motion.abb.com/).
- [Eaton — exemple de protection moteur et documents](https://www.eaton.com/in/en-us/skuPage.265350.html).
- [Phoenix Contact — bornes](https://www.phoenixcontact.com/en-us/products/terminal-blocks).
- [Weidmüller — catalogues techniques](https://www.weidmuller.com/en/service/technical_product_catalogues.jsp), [données d'ingénierie](https://www.weidmuller.com/en/service/engineering_data.jsp).
- [WAGO — TOPJOB S](https://www.wago.com/global/products/electrical-interconnections/discover-rail-mount-terminal-blocks/topjob-s).
- [Omron — relais G2RS](https://industrial.omron.eu/en/products/g2rs).
- [Finder — série 40](https://cdn.findernet.com/app/uploads/S40EN.pdf).
- [Pilz — téléchargements](https://www.pilz.com/fr-FR/support/downloads).
- [Beckhoff — import de données ECAD dans TwinCAT](https://infosys.beckhoff.com/content/1033/tcecadimport/11276615947.html), [procédure de macros publiée en 2023](https://download.beckhoff.com/download/document/fi/Install_EPLAN_Macros.pdf) ; conditions et couverture actuelles à vérifier par article.
- [Mean Well — SDR-120](https://www.meanwell.com/Upload/PDF/SDR-120/SDR-120-SPEC.PDF).
- [ifm — données eclass et catalogues](https://www.ifm.com/nl/nl/shared/eclass/eclass-downloads).
- [SICK — téléchargements CAD](https://www.sick.com/at/en/catalog/support/downloads/cad/c/g569837).
- [Pepperl+Fuchs — capteurs de proximité](https://www.pepperl-fuchs.com/nl-nl/products/industrial-sensors/proximity-sensors-gp30083).
- [SEW-EURODRIVE — moteurs](https://kz.sew-eurodrive.com/products/motors/motors.html), [WEG — moteurs IEC W22](https://www.weg.net/catalog/weg/GE/en/Electric-Motors/Low-Voltage-IEC-Motors/Three-Phase/W22-/W22---Cast-Iron-TEFC-/p/MKT_WMO_TEXT_IMAGE_CASTIRON_TEFC_GENERAL_W22).
- [HARTING — connecteurs Han](https://www.harting.com/en-GB/c/Industrial-connectors-Han-57904).
- [LAPP — câbles de données](https://www.lapp.com/en_GB/gb/GBP/products/cables/data-cables/ethernet-cable/c/113897), [HELUKABEL — câbles industriels](https://www.helukabel.com/us/products/products-overview.html), [Nexans — automatismes](https://www.nexans.com/activities/markets/other-activities/industries/automation/).
- [Rittal — système VX25](https://www.rittal.com/com_en/vx25/index.php?lng=en).

### Qualification d'une fiche d'article

Chaque fiche devra contenir un identifiant stable, fabricant, référence exacte,
famille, désignation, description française, bornes et fonctions, variantes,
caractéristiques avec unités, liens documentaires, date de vérification,
origine, conditions de réutilisation et état de validation.

États proposés : `brouillon`, `symbole contrôlé`, `article contrôlé`, `obsolète`.
Les variantes non vérifiées ne seront pas annoncées comme du matériel prêt à
commander. Une modification de référence devra contrôler les bornes et fonctions
avant de conserver les fils du schéma. Un ancien projet gardera un instantané
des données employées, même si le catalogue évolue ensuite.

Départ : réutiliser et contrôler les `.elmt` existants ; créer les symboles
génériques manquants dans le format natif. Pour les données d'articles, consulter
les sources fabricants et conserver des liens lorsque le droit de redistribution
n'est pas établi. Les téléchargements soumis à compte ou licence ne font pas
partie d'une collecte automatique. Les conditions d'EPLAN sont publiées sur
sa [page juridique](https://www.eplan.com/gb-en/about-us/legal-information/terms-conditions/).
La présente étude n'importe aucune bibliothèque propriétaire EPLAN.

## 6. Architecture d'évolution

Le fichier `.qet` demeure la source du projet. Les symboles nécessaires doivent
rester incorporés à ce fichier. Les tables SQLite servent aux index et rapports,
sans devenir un stockage concurrent susceptible de diverger du dessin.
Les changements passent par les commandes annuler/rétablir existantes.

Relations à consolider progressivement :

```text
Projet -> Folio -> Représentation graphique
Appareil physique -> Fonctions -> Représentations sur un ou plusieurs folios
Appareil physique -> Article principal + Accessoires
Fonction -> Bornes -> Connexions -> Conducteurs / Câbles
Article -> Instantané des caractéristiques + Source + Version
Rapport -> Données du projet, jamais une seconde saisie manuelle
```

L'article commandé, la bobine dessinée et ses contacts ne doivent pas être
confondus. Le comptage de la nomenclature doit représenter les appareils
physiques et les accessoires réels, avec des règles explicites. Les propriétés
actuelles `manufacturer`, `manufacturer_reference`, `model`, `category`,
`voltage_rating` et `current_rating` constituent le point de départ ; les
valeurs existantes resteront lisibles lors des migrations.

Conserver un catalogue local versionné, des imports transactionnels, un index
de recherche et une séparation entre les données distribuées et les ajouts de
l'utilisateur. Commencer avec l'infrastructure CSV existante ; adopter un
catalogue SQLite plus structuré lorsque la gestion des variantes et des bornes
le demande. Aucune dépendance à un service cloud pour dessiner, sauvegarder
ou exporter un projet local.

## 7. Première version fonctionnelle : parcours et recette

Parcours cible : créer un projet « Armoire moteur », choisir le cartouche,
ajouter les folios, placer protections/commande/moteur, connecter les bornes,
attribuer les repères et articles, vérifier les incohérences, puis générer le
dossier PDF et la nomenclature.

L'interface proposée comporte un navigateur de projet à gauche, le folio au
centre, les propriétés à droite et une liste de diagnostics navigables en bas.
La bibliothèque doit filtrer par fonction, famille, fabricant et référence ;
l'aperçu montre le symbole et ses bornes avant insertion. Les commandes
fréquentes et leur raccourci doivent être visibles. Les thèmes clair/sombre
doivent garder un contraste utile à l'édition et un rendu d'impression stable.

Critères de réception de la V1 :

1. Projet de 12 folios avec alimentation, deux départs moteurs, commande,
   automate, E/S et bornier ; toutes les fonctions de dessin sont utilisables.
2. Enregistrement puis réouverture sans perte des appareils, connexions,
   articles, cartouches ou repères ; ouverture avec la collection externe
   indisponible pour vérifier l'incorporation des symboles.
3. Déplacement, rotation, duplication, suppression et annulation des appareils
   sans fils perdus ni numéros dupliqués silencieusement.
4. Bobine/contacts et reports inter-folios cohérents après renumérotation.
5. Détection vérifiée sur des défauts introduits volontairement : doublon de
   repère, référence inexistante et dépassement de raccordements autorisés.
6. Nomenclature contrôlée contre les appareils physiques et leurs accessoires ;
   listes de fils vérifiées aux deux extrémités.
7. PDF complet et lisible, références navigables lorsque le modèle le permet,
   CSV lisible dans un tableur, sans dépendance au poste de développement.
8. Démarrage, fermeture et récupération après interruption validés sur Windows.
9. Catalogue : trois fabricants par famille annoncée, références exactes,
   bornes contrôlées et sources consultables ; couverture incomplète affichée.
10. Mesures sur une machine de référence : recherche et manipulation fluides,
    ouverture et sauvegarde mesurées sur 25, 100 et 500 folios. Définir des
    seuils après les mesures initiales ; ne pas annoncer de performances
    équivalentes à EPLAN sans comparaison reproductible.

Les conventions de dessin seront vérifiées à partir des références normatives
appropriées : [IEC 60617 pour les symboles](https://webstore.iec.ch/en/publication/2723)
et [IEC 61082-1 pour la présentation des documents](https://webstore.iec.ch/en/publication/4469).
« Orienté IEC » ne signifie pas certification de l'application ou validation
automatique d'une installation. Les calculs de protections, sélectivité, SIL/PL
et conformité réglementaire ne font pas partie de cette V1 de schématique.

## 8. Ordre d'exécution

1. **Recherche et cadrage — cette livraison.** Branche, audit, comparaison,
   sélection des fabricants et critères de recette. Aucun changement des
   fonctions de dessin dans cette étape.
2. **Socle industriel utilisable.** Espace de travail, assistant projet,
   premiers modèles, bibliothèque et articles qualifiés, recette de bout en bout.
3. **Cohérence des appareils.** Modèle appareil/fonctions, catalogue structuré,
   contrôles, borniers avancés et E/S d'automates.
4. **Productivité.** Macros paramétrées, rapports, révisions et échanges métier.
5. **Extension de périmètre.** Implantation 2D/3D, collaboration et fabrication.

Chaque lot doit laisser l'application fonctionnelle, conserver la compatibilité
des projets et remplacer le même `build/qelectrotech.exe`. Les tests seront
choisis selon le changement : modèle/serialization/annulation/rapports pour les
données, parcours Windows pour l'interface. Les essais existants concernés
doivent être utilisés avant d'en ajouter. La branche `main` conserve le point
de départ ; les travaux de ce programme se poursuivent sur `EPLAN_DESIGN`.
