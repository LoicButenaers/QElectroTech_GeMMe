# GeMMe — étude approfondie Electric P8 et conception de l'interface

État de la recherche : **9 octobre 2026**. Référence locale : commit
`787e98a775b09e8296a9ecfa50f5aca32afe7810`, branche `EPLAN_DESIGN`.
Travail exclusivement dans le dépôt GeMMe. Cette livraison porte sur la
recherche, le cahier des charges et une maquette ; elle n'ajoute pas ces
fonctions à l'exécutable.

## 1. Objectif et méthode

L'objectif demandé reste la **parité fonctionnelle avec Electric P8**, avec
une interface indépendante optimisée pour les schémas industriels IEC.
Le premier dossier électrique utilisable est un jalon, pas une réduction de
l'objectif final. La fluidique, les révisions ou le travail en équipe restent
dans le programme même s'ils arrivent après le dessin électrique.

Le registre contient **253 exigences dans 26 domaines**, reliées à **65
références officielles**. Il couvre les familles de capacités repérées dans
l'aide, les descriptions de performances, les notes de version et les fiches
techniques d'échange. Ce nombre est celui de nos exigences, pas un nombre de
fonctions annoncé par EPLAN. Certaines exigences regroupent plusieurs
opérations étroitement liées.

Référence principale : aide de la plateforme 2027. La description commerciale
détaillée disponible et étudiée concerne P8 2026. Quelques pages spécialisées
2024/2025/2026 sont conservées avec leur version explicite ; elles ne prouvent
pas à elles seules la disponibilité dans toutes les éditions 2027. La grille
commerciale 2027 par licence n'a pas été établie dans cette étude.

Les critères de recette sont **nos propositions pour GeMMe**. Ils ne sont ni
des citations du manuel ni des essais déjà réussis. L'audit local repère des
points d'appui ; il ne prouve pas l'absence des fonctions marquées « non
démontré ». Il faudra détailler les règles de contrôle, propriétés, variantes
et interfaces à l'intérieur de chaque exigence avant sa réception. Cette
recherche ne peut pas certifier une liste exhaustive de tous les comportements
internes ou de chaque méthode API d'un logiciel propriétaire sans campagne
comparative sur une installation de référence.

Le [registre JSON](EPLAN_P8_REQUIREMENTS.json) constitue la donnée structurée :
identifiant stable, périmètre, sources, dépendances, lot, état, fichiers locaux
et recette. L'annexe ci-dessous en donne une lecture complète. Une fonction
n'est considérée terminée qu'après preuve de recette, pas après ajout de son
nom dans un menu.

## 2. Résultats qui changent le cadrage

**Le modèle métier passe avant l'habillage.** P8 permet de partir soit des
symboles, soit des appareils prévus. Le lien entre les fonctions et l'appareil
doit survivre aux différentes représentations. Pour GeMMe, le critère central
est simple : bobine, contacts et implantation d'un contacteur doivent donner
un seul appareil dans les quantités. [Appareils, S05](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/devicelistgui_k_start.htm).

**Les connexions sont des données calculées.** Une ligne visible n'est pas
une preuve de raccordement. Il faut connaître les cibles, potentiels, signaux,
ordres de câblage et état de recalcul. Les rapports doivent utiliser le même
graphe que le dessin. [Connexions, S07](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/connectionbrowsergui_k_start.htm).

**Fluid fait partie de P8 depuis 2026.** La cible complète comprend donc les
circuits fluidiques et leurs liens avec l'électricité. En 2027, la gestion
cloud des articles évolue vers la zone Parts data de Data management ; eStock
est indiqué comme obsolète. [Compatibilité des applications, S55](https://www.eplan.help/en-us/Infoportal/Content/SwReqs/Content/htm/SwReqs_k_cloud.htm).

**La topologie 2D est dans le périmètre.** Il faut distinguer le tracé d'un fil
sur le folio du trajet physique dont on calcule une longueur. L'implantation
2D et cette topologie sont traitées séparément de l'armoire 3D Pro Panel.
[Topologie, S33](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/cablinggui_k_start.htm),
[Implantation 2D, S34](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/panellayoutgui_k_start.htm).

**Les rapports ne se limitent pas à une nomenclature.** Le registre inclut les
plans de raccordement, vues de câbles, borniers, connecteurs, E/S, potentiels,
montage, topologie, révisions et paramètres. Les rapports de préplanification
énumérés dans l'aide commune appartiennent à une extension distincte.
[Types de rapports, S22](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/formgeneratorgui_k_auswertungstypen.htm).

**Une interface DXF/DWG ne transporte pas la logique électrique EPLAN.**
De même, notre import EDZ existant lit des données portables et produit un
symbole générique : il ne reconstitue pas les macros graphiques EPLAN. Une
compatibilité native `.elk`, `.zw1`, `.ema` ou un aller-retour sans perte ne
doivent pas être annoncés avant preuve. [Échanges DXF/DWG, S49](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/xdxfgui_k_start.htm),
[limites locales EDZ](../sources/import/edz/README.md).

**Les versions comptent.** Les notes 2027 mentionnent notamment des filtres
de navigateurs au niveau projet, des tags d'insertion et des évolutions PLC.
Le moniteur multi-utilisateur historique est retiré : cela ne supprime pas
le besoin de gérer les conflits d'édition. Le registre garde deux entrées
historiques pour éviter de construire une cible à partir d'un mélange de
versions. [Notes de version, S56](https://www.eplan.help/en-US/Infoportal/Content/htm/portal_rel_notes_platform.htm),
[évolutions 2027, S53](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/news_p_plattform_notes_2027.htm).

## 3. Écart avec le logiciel actuel

Sur les 253 lignes, **75 ont un point d'appui local à qualifier**, **169 ne
sont pas démontrées**, **7 concernent des extensions complémentaires** et
**2 sont historiques**. Ces catégories ne permettent pas de calculer un
pourcentage d'avancement : un éditeur vectoriel et un champ d'article n'ont
ni la même taille ni la même couverture fonctionnelle.

| Base examinée | Utilisation et limite constatée |
| --- | --- |
| `qetproject.cpp`, `diagram.cpp`, `qetdiagrameditor.cpp` | Conserver projets, scène, folios et commandes ; auditer les structures métier au-delà des libellés. |
| `properties/deviceinformation.h` | Les informations fabricant sont actuellement des champs, plusieurs grandeurs restent des textes. Ce n'est pas un catalogue de fonctions qualifiées. |
| `masterelement.cpp`, `crossrefitem.cpp`, `bomexport.cpp` | Réutiliser les liens maîtres/esclaves et exports ; vérifier la distinction appareil physique/représentation et les accessoires. |
| `conductorrouter.cpp`, `conductor.cpp` | Réutiliser les outils de dessin ; ajouter une représentation exploitable des réseaux, cibles et états calculés. |
| `TerminalStrip/` | Un vrai socle existe ; étages, positions physiques, réserves et accessoires demandent des recettes détaillées. |
| `wiringrules.h` | Limite de fils par borne et reports ; règles désactivables. Ce n'est pas un moteur complet de contrôles P8. |
| `partplctable.cpp`, `plclinkwidget.cpp` | Représentation et liens présents ; pas de preuve de couverture stations/racks/slots/AML. |
| `scripting/qetscriptapi.h` | Nombreuses commandes et exports réutilisables ; les exports relisent le fichier, donc sauvegarde nécessaire après édition. |
| `dxfexport.cpp` | Point d'appui DXF seulement ; l'état « socle » de l'exigence DXF/DWG n'annonce pas DWG disponible. |
| Navigateur GeMMe | Liste les représentations trouvées ; son compteur ne doit pas être présenté comme un nombre d'appareils physiques. |

Les chemins précis sont conservés dans le registre. Les essais Windows déjà
effectués figurent dans [EPLAN_DESIGN.md](EPLAN_DESIGN.md). Ils ne couvrent pas
les 253 nouvelles recettes. Aucune reconstruction de l'exécutable n'est
nécessaire pour cette livraison documentaire.

## 4. Architecture proposée pour atteindre la cible

Conserver C++/Qt et les outils QElectroTech opérationnels. Introduire le modèle
par migrations compatibles ; ne pas remplacer d'un coup le moteur de dessin.

| Entité GeMMe proposée | Responsabilité et invariant |
| --- | --- |
| Projet / structure / folio | Identités stables ; numéro et position ne sont pas des identifiants internes. |
| Appareil physique | Repère complet, localisation, fonctions, articles et accessoires ; quantité indépendante du nombre de symboles. |
| Fonction | Rôle électrique ou fluidique, raccordements et capacités disponibles. |
| Représentation | Symbole, coordonnées, visibilité et type de vue ; référence la fonction. |
| Connexion / réseau / potentiel / signal | Identités distinctes et parcours source-cible explicite ; une géométrie seule ne crée pas la relation métier. |
| Article versionné | Fabricant, référence exacte, fonctions, bornes, dimensions, accessoires, source et état de qualification. |
| Macro / option | Paramètres typés, variantes et transformations transactionnelles ; les options inactives sont exclues des quantités. |
| Diagnostic | Règle versionnée, gravité, objet, explication, état et révision de calcul. |
| Rapport | Requête et mise en page sur une révision connue des données ; régénération reproductible. |
| Révision / transaction | Ensemble atomique de changements avec auteur, date et motif ; annulation et comparaison cohérentes. |

Le `.qet` reste la source de vérité tant qu'une migration du format n'a pas
été spécifiée et testée. Une base SQLite dérivée accélère recherches et
rapports ; elle doit pouvoir être reconstruite. Le futur multi-utilisateur
nécessitera un vrai protocole de transactions et de conflits : partager le
fichier ou le cache sur un dossier réseau ne suffit pas.

Avant chaque migration : fixtures antérieures, sauvegarde/réouverture,
annuler/rétablir, copie entre projets, suppression et régénération des rapports.
Les champs inconnus doivent être préservés ou leur perte explicitement
signalée. Les importations doivent produire un bilan des objets importés,
ignorés et transformés.

## 5. Interface : priorité au travail de schématique

La [maquette interactive](GEMME_INTERFACE.html) s'ouvre localement dans un
navigateur. Elle illustre une proposition GeMMe avec le logo fourni. **Ce
n'est pas un éditeur électrique et elle n'enregistre aucun projet.** Les
appareils et diagnostics sont fictifs. Ses commandes permettent d'explorer
les espaces de travail, sélectionner des objets, filtrer, consulter leurs
propriétés, passer au diagnostic et changer de thème.

L'application native intègre désormais une barre compacte et trois espaces
qui utilisent le projet réel : Schéma, Appareils et Vérification. Le tableau
Appareils porte actuellement sur les représentations des symboles ; le contrôle
Vérification porte sur leurs champs fabricant et référence lorsqu'un repère
et au moins deux bornes sont présents, hors renvois et exclusions de nomenclature.
Les tables métier, les contrôles électriques et les fonctions
avancées décrits ci-dessous restent la cible, et ne sont pas validés par cette
première intégration. L'interface EPLAN documente déjà les espaces, panneaux détachables et
commandes contextuelles ; la disposition ci-dessous est notre conception.
[Interface de référence, S43](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/userinterface_k_hintergrund.htm).

| Espace | Zone centrale | Panneaux et actions prioritaires |
| --- | --- | --- |
| Schéma | Folio vectoriel, onglets et second folio facultatif | Projet/bibliothèque à gauche ; propriétés contextuelles à droite ; fil, symbole, renvoi, texte, numérotation accessibles directement. |
| Appareils | Tables synchronisées : appareils, borniers, câbles, connecteurs, E/S | Filtres enregistrés ; fonctions utilisées/libres ; article et accessoires ; aller au symbole. |
| Vérification | Diagnostics, comparaison de révisions et aperçu de publication | Erreurs groupées par cause ; clic vers l'objet ; profil de contrôle ; état des rapports et recette de livraison. |

La zone de dessin doit garder l'essentiel de l'écran. À 1440 × 900, viser une
barre d'actions de 44 px, un panneau gauche de 240 px et des propriétés de
280 px réductibles. À 125/150 % de mise à l'échelle, replier des groupes avant
de rogner le folio. Prévoir un mode plein dessin, un deuxième écran, la
restauration des panneaux hors écran et une interface entièrement au clavier.
Ces dimensions sont des points de départ à mesurer en usage.

### Menus et sous-menus cibles

Les menus constituent un accès complet et stable ; les raccourcis et la barre
contextuelle exposent les actions fréquentes. Aucun bouton de production ne
doit simuler une fonction absente.

| Menu | Groupes et sous-menus |
| --- | --- |
| Projet | Nouveau depuis modèle ; Ouvrir/récents ; Enregistrer ; Propriétés/structure ; Folios/créer/copier/numéroter ; Ressources ; Sauvegarde/restauration ; Fermer. |
| Insérer | Symboles ; Appareils ; Conducteurs/jonctions/potentiels/renvois ; Bornes/connecteurs/câbles ; E/S ; Macros/variantes ; Texte/images/cotes. |
| Modifier | Annuler/rétablir ; Couper/copier/coller ; Déplacer/aligner/tourner ; Propriétés ; Édition en table ; Rechercher/remplacer ; Numérotation ; Protection. |
| Appareils | Fonctions ; Articles/accessoires ; Borniers/étages/pontages ; Connecteurs/broches ; Câbles/âmes/blindages ; Automates/stations/E/S/bus ; Implantation ; Topologie. |
| Bibliothèques | Symboles ; Articles/fabricants ; Macros/projets de macros ; Formulaires/cartouches ; Modèles de projet ; Dictionnaires ; Synchronisation. |
| Vérifier | Projet/sélection ; Profils de règles ; Messages ; Données d'articles ; Comparaison ; Révisions/clôture ; Cahier client. |
| Publier | Rapports/modèles/actualisation ; Nomenclatures ; Plans de raccordement ; Étiquettes ; PDF/impression ; Images/DXF/DWG ; Échanges PLC ; Archives. |
| Outils | Options de machine ; Traduction ; Scripts/traitements ; Collaboration/sous-projets/droits ; Interfaces ; Préférences. |
| Affichage | Espaces ; Navigateurs ; Propriétés ; Diagnostics ; Second folio ; Grille/accrochage ; Zoom ; Thème ; Personnalisation. |
| Aide | Recherche de commandes ; Aide contextuelle ; Conventions du projet ; Diagnostic logiciel ; À propos/licences. |

### Comportements à réussir

1. **Insérer un départ moteur** : rechercher la fonction, prévisualiser ses
   raccordements, choisir une macro, renseigner ses paramètres, prévisualiser
   les nouveaux repères, placer et annuler en une action.
2. **Câbler** : voir les raccordements accrocheurs, le potentiel et les
   incompatibilités avant validation ; distinguer visuellement croisement et
   jonction ; déplacer un symbole sans détacher silencieusement ses fils.
3. **Changer un contacteur** : comparer les fonctions et bornes de l'ancien
   article et du remplaçant ; réaffecter les contacts ; vérifier l'effet sur
   nomenclature, implantation et schéma avant validation.
4. **Préparer un bornier** : table interne/borne/niveau/externe, ordre physique,
   accessoires et pontages ; aperçu du plan lié à la ligne sélectionnée.
5. **Affecter les E/S** : filtre par station et type ; plages d'adresses,
   signaux réservés et doublons visibles ; navigation vers tous les symboles.
6. **Livrer** : contrôler une révision, traiter les erreurs, recalculer les
   rapports puis publier un dossier portant la même révision partout.

Échap quitte le geste en cours avant de désélectionner ; zoom et déplacement
de vue ne détruisent pas le geste. Ctrl+Z conserve son sens dans tous les
espaces. Les raccourcis de production seront confrontés à ceux déjà employés
par QElectroTech avant attribution. La maquette utilise Ctrl+K pour sa recherche.
Le focus clavier, le contraste et les états ne dépendent pas uniquement de
la couleur. Les indications graphiques temporaires ne passent pas à l'impression.

## 6. Bibliothèque : trois fabricants par famille

Conserver les 16 familles et les 48 associations famille/fabricant documentées
dans [le cadrage initial](EPLAN_DESIGN.md). Elles constituent une sélection de
fournisseurs adaptés, pas un classement de parts de marché. La cible initiale
de 240 références qualifiées reste à produire ; les 8 839 fichiers de symboles
existants ne la remplacent pas.

Chaque article devra porter : fabricant, référence exacte et variante,
caractéristiques avec unités, fonctions et raccordements, accessoires,
documents et provenance datée, révision, conditions de redistribution et
état de qualification. Séparer « symbole générique », « article renseigné »
et « article contrôlé ». Afficher la couverture par famille et fabricant.
Ne pas distribuer de caractéristiques devinées ni de références de démonstration.
L'intégration d'un catalogue en ligne n'accorde pas automatiquement l'accès
à toutes les données et macros d'un autre fournisseur.

## 7. Lots, dépendances et portes de réception

| Lot | Résultat utilisable | Porte de réception |
| --- | --- | --- |
| L1 — atelier | Projet IEC, navigation, édition et premiers parcours de la nouvelle interface | Dossier de 12 folios créé, enregistré, réouvert et exporté ; usage clavier et mise à l'échelle vérifiés. |
| L2 — cohérence | Appareils/fonctions/articles, connexions, repérage, contrôles | Contacteur multi-vues compté une fois ; renumérotation et changement d'article sans perte de lien ; erreurs injectées détectées. |
| L3 — équipements | Borniers, connecteurs, câbles, E/S et échanges PLC | Bornier à étages, câble équipé et station automate validés par jeux d'essai indépendants. |
| L4 — productivité | Macros/variantes, données de base, rapports, langues, API et échanges | Deux variantes de machine génèrent des dossiers distincts, reproductibles et correctement traduits. |
| L5 — cycle de vie | Révisions, travail en équipe, implantation/topologie 2D, Fluid | Comparaison complète, conflit concurrent maîtrisé, longueur connue et appareil électrofluidique cohérent. |
| L6 — intégrations | Services partagés et extensions métier | Recettes par connecteur et produit, reprise sur erreur et traçabilité des versions. |

Des dépendances transversales imposent des tranches anticipées : PDF de base
et nomenclature dès L1, articles nécessaires aux borniers dès L2, journal de
transactions dès le modèle. Les lots indiquent la réception complète d'un
domaine, pas une interdiction d'en implémenter une partie plus tôt.

Mesurer ouverture, sauvegarde, recherche, déplacement, contrôle et rapports
sur 25/100/500 folios avec machine, mémoire, données et versions consignées.
Fixer les seuils après une mesure de référence ; aucun temps EPLAN n'a été
mesuré ici. Pour chaque ligne acceptée : preuve reproductible, résultat,
version du logiciel et limitations résiduelles. Aucun calendrier chiffré de
parité ne peut être déduit du seul nombre d'exigences.

## 8. Points à approfondir lors de l'implémentation

- Matrice des éditions/licences 2027 et fonctions réellement disponibles dans
  l'installation EPLAN choisie pour la comparaison.
- Détail des classes de messages, règles, types de fonctions et propriétés ;
  les familles ci-dessous n'en recopient pas chaque entrée de dictionnaire.
- Compatibilité exacte par version des fichiers natifs et connecteurs PLC ;
  les tests doivent mesurer les pertes et non seulement l'ouverture du fichier.
- Préplanification, 3D, usinage, câblage automatique et fabrication : détailler
  ces produits séparément sans les présenter comme le cœur de P8.
- Qualification fabricant, jeux de données de référence, licences des données
  et règles normatives applicables aux livrables Belgique/Europe.
- Évaluation de l'ergonomie avec un dossier réel : nombre de gestes, erreurs,
  récupération après erreur, lisibilité et temps de réalisation.

Les paragraphes précédents sont des choix d'architecture et de produit
GeMMe. Les annexes suivantes tracent les capacités observées dans les sources.

## Vérification de cette livraison

- Registre relu automatiquement : 253 identifiants uniques, sources et
  dépendances résolues, chemins locaux présents ; syntaxe JavaScript vérifiée.
- Dans le navigateur : filtre `KM1` donnant un appareil ; diagnostic Q1
  sélectionnant Q1 sur le schéma ; bibliothèque Bornier/WAGO ; recherche de
  commande KM1 ; bascule clair/sombre et surlignage ; plein dessin et retour ;
  aperçu de dossier ramenant aux diagnostics.
- Rendu observé à 1440 × 900 et 1100 × 760 ; adaptation du panneau étroit
  vérifiée par les dimensions du document (pas de débordement horizontal).
  Aucun message d'erreur JavaScript capturé pendant ces parcours.
- Ouverture de la recherche par bouton testée. Le raccourci Ctrl+K n'a pas
  pu être validé avec l'adaptateur de test du navigateur ; recette clavier
  complète et tests de mise à l'échelle Windows restent à faire.
- Tests réalisés via un serveur de prévisualisation local. Le navigateur de
  test interdit les URL `file:` ; l'ouverture directe du HTML n'y a donc pas
  été validée. La maquette n'utilise aucun service externe ni téléchargement.
- Aucune compilation C++ ni nouvelle recette électrique dans ce lot.
  L'exécutable du lot précédent reste le seul binaire applicatif.

## 9. Inventaire détaillé et recettes proposées

Les états concernent GeMMe, pas EPLAN. « Socle » peut ne couvrir qu’une partie de la ligne. Les recettes suivantes restent à exécuter.

### PRJ — Projets et structures

Lot L1. Dépendances : aucune.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| PRJ-01 | Créer depuis un projet type | Socle à qualifier | Créer deux dossiers clients sans partager leurs repères. | [S39](#s39) |
| PRJ-02 | Structurer fonctions et localisations | Non démontré | Deux appareils K1 dans des localisations différentes restent distincts. | [S02](#s02) |
| PRJ-03 | Configurer la structure par objet | Non démontré | Appliquer des conventions distinctes aux folios et aux câbles. | [S02](#s02) |
| PRJ-04 | Gérer les sous-identifiants | Non démontré | Copier une armoire imbriquée et vérifier tous les identifiants complets. | [S02](#s02) |
| PRJ-05 | Modifier une structure existante | Non démontré | Prévisualiser les collisions puis annuler sans perte de données. | [S02](#s02) |
| PRJ-06 | Administrer plusieurs projets | Non démontré | Retrouver un dossier déplacé et afficher ses propriétés. | [S39](#s39) |
| PRJ-07 | Modifier les propriétés en série | Non démontré | Changer le client sur dix dossiers sans modifier leurs circuits. | [S39](#s39) |
| PRJ-08 | Sauvegarder et restaurer les ressources | Socle à qualifier | Rouvrir une archive sur un poste sans bibliothèque externe. | [S48](#s48) |
| PRJ-09 | Archiver un état lisible | Socle à qualifier | Rouvrir le dossier livré et comparer les repères avec son PDF. | [S48](#s48) |
| PRJ-10 | Gérer les projets de base | Non démontré | Créer un modèle avec conventions et vérifier leur transmission. | [S40](#s40) |

### PAG — Folios et documents

Lot L1. Dépendances : PRJ.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| PAG-01 | Distinguer folios logiques et graphiques | Socle à qualifier | Une décoration ne doit pas ajouter un appareil à la nomenclature. | [S03](#s03) |
| PAG-02 | Schémas multifilaires | Socle à qualifier | Dessiner un départ moteur triphasé puis rouvrir ses connexions. | [S01](#s01) |
| PAG-03 | Schémas unifilaires cohérents | Non démontré | Représenter le même câble dans deux vues sans doubler les quantités. | [S01](#s01) |
| PAG-04 | Créer copier trier les folios | Socle à qualifier | Dupliquer trois folios et vérifier leurs liens internes. | [S03](#s03) |
| PAG-05 | Renuméroter les folios | Socle à qualifier | Insérer une page intermédiaire sans casser les renvois. | [S63](#s63) |
| PAG-06 | Gérer documents externes | Non démontré | Réouvrir un dossier livré avec les fiches techniques disponibles. | [S48](#s48) |
| PAG-07 | Cartouches et coordonnées | Socle à qualifier | Changer de format sans fausser les coordonnées des renvois. | [S24](#s24) |
| PAG-08 | Échelles et unités | Socle à qualifier | Une cote de 100 mm reste exacte après changement d'échelle. | [S04](#s04) |

### EDT — Édition graphique et schématique

Lot L1. Dépendances : PAG.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| EDT-01 | Grille et accrochages | Socle à qualifier | Insérer dix symboles au clavier sur la même grille. | [S04](#s04) |
| EDT-02 | Saisie de coordonnées | Socle à qualifier | Placer un raccordement à la coordonnée demandée. | [S04](#s04) |
| EDT-03 | Déplacer et transformer les objets | Socle à qualifier | Déplacer puis tourner un groupe sans perdre ses raccordements. | [S04](#s04) |
| EDT-04 | Copier coller entre projets | Socle à qualifier | La copie ne réutilise pas les identités internes de l'original. | [S04](#s04) |
| EDT-05 | Annuler rétablir une opération | Socle à qualifier | Annuler une insertion de circuit restitue exactement le projet initial. | [S04](#s04) |
| EDT-06 | Zoom pendant une action | Socle à qualifier | Changer le zoom au milieu d'un tracé et poursuivre au bon point. | [S04](#s04) |
| EDT-07 | Travailler avec plusieurs vues | Socle à qualifier | Sélectionner un appareil depuis deux folios sans incohérence. | [S04](#s04) |
| EDT-08 | Texte images et liens | Socle à qualifier | Imprimer accents images et liens sans déplacement de mise en page. | [S04](#s04) |
| EDT-09 | Propriétés visibles et textes groupés | Socle à qualifier | Déplacer la désignation sans modifier la valeur métier. | [S04](#s04) |
| EDT-10 | Cotation et annotations | Socle à qualifier | Réouvrir une page cotée et vérifier ses mesures. | [S04](#s04) |
| EDT-11 | Calques et présentation | Non démontré | Masquer les annotations de travail tout en conservant les connexions. | [S51](#s51) |
| EDT-12 | Rognage et ajustement des traits | Socle à qualifier | Ajuster une ligne graphique sans couper un conducteur logique. | [S04](#s04) |

### DEV — Appareils fonctions et représentations

Lot L2. Dépendances : PRJ, EDT.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| DEV-01 | Séparer appareil et représentation | Socle à qualifier | Une bobine et trois contacts donnent un seul contacteur physique. | [S05](#s05) |
| DEV-02 | Fonction principale et auxiliaires | Socle à qualifier | Changer l'article principal et actualiser toutes les fonctions associées. | [S05](#s05) |
| DEV-03 | Décrire les fonctions disponibles | Non démontré | Un article à deux contacts refuse une troisième affectation incompatible. | [S06](#s06) |
| DEV-04 | Concevoir depuis les symboles | Socle à qualifier | Dessiner un circuit puis lui affecter les articles compatibles. | [S05](#s05) |
| DEV-05 | Concevoir depuis les appareils | Non démontré | Préparer dix appareils et ne placer que cinq de leurs fonctions. | [S05](#s05) |
| DEV-06 | Conserver les fonctions non placées | Non démontré | La sauvegarde conserve les fonctions encore absentes du dessin. | [S05](#s05) |
| DEV-07 | Représenter un appareil plusieurs fois | Non démontré | Les vues unifilaire et multifilaire partagent les données communes. | [S05](#s05) |
| DEV-08 | Remplacer un appareil | Non démontré | Afficher les raccordements incompatibles avant validation. | [S05](#s05) |
| DEV-09 | Appareils imbriqués et modules | Non démontré | Déplacer un ensemble conserve l'appartenance des composants. | [S05](#s05) |
| DEV-10 | Protéger les appareils validés | Non démontré | Une édition groupée signale les objets protégés sans les modifier. | [S05](#s05) |
| DEV-11 | Synchroniser prévu et réalisé | Non démontré | Afficher les fonctions prévues non utilisées et les fonctions ajoutées. | [S05](#s05) |
| DEV-12 | Articles sans représentation | Non démontré | Ajouter un accessoire au dossier sans inventer de symbole électrique. | [S05](#s05) |

### CON — Connexions et potentiels

Lot L2. Dépendances : DEV.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| CON-01 | Connexions automatiques et aperçu | Socle à qualifier | Une borne alignée annonce la connexion avant placement. | [S07](#s07) |
| CON-02 | Jonctions avec ordre de câblage | Socle à qualifier | Un T conserve la paire de bornes réellement câblée. | [S08](#s08) |
| CON-03 | Croisement sans raccordement | Socle à qualifier | Deux traits croisés sans jonction appartiennent à deux réseaux. | [S08](#s08) |
| CON-04 | Points de définition de connexion | Non démontré | Une surcharge locale de section ne modifie pas tout le potentiel. | [S07](#s07) |
| CON-05 | Potentiels distincts des signaux | Non démontré | Deux signaux distincts peuvent utiliser la même catégorie de potentiel. | [S09](#s09) |
| CON-06 | Propagation des propriétés | Socle à qualifier | Changer le potentiel 24 V actualise uniquement ses connexions. | [S09](#s09) |
| CON-07 | Surlignage des réseaux | Non démontré | Suivre un signal sur trois folios sans changer les couleurs imprimées. | [S09](#s09) |
| CON-08 | Connexions non placées | Non démontré | Définir une liaison en table puis la placer sans la dupliquer. | [S07](#s07) |
| CON-09 | Ordre des cibles d'un réseau | Non démontré | Passer d'une chaîne à une étoile modifie la liste de fils. | [S07](#s07) |
| CON-10 | Unités sections couleurs | Socle à qualifier | Un aller-retour mm² et AWG n'efface pas la valeur d'origine. | [S07](#s07) |
| CON-11 | Connexions électriques et fluidiques | Non démontré | Un tuyau ne peut pas se connecter silencieusement à une borne électrique. | [S07](#s07) |
| CON-12 | Fraîcheur des connexions calculées | Non démontré | Un rapport indique si son graphe de connexions est périmé. | [S07](#s07) |
| CON-13 | Édition globale des connexions | Socle à qualifier | Modifier vingt fils puis annuler l'ensemble en une opération. | [S07](#s07) |

### NUM — Repérage et références croisées

Lot L2. Dépendances : DEV, CON.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| NUM-01 | Numéroter lors de l'insertion | Socle à qualifier | Insérer cinq appareils avec des repères distincts et prévisibles. | [S44](#s44) |
| NUM-02 | Configurer les règles de repérage | Socle à qualifier | Le modèle client produit le même repérage après réouverture. | [S44](#s44) |
| NUM-03 | Repérer les copies de macros | Socle à qualifier | Copier un départ conserve les liens entre sa bobine et ses contacts. | [S44](#s44) |
| NUM-04 | Renuméroter une sélection | Socle à qualifier | Les appareils hors sélection conservent leurs repères. | [S45](#s45) |
| NUM-05 | Réserver les repères non placés | Non démontré | Un appareil encore non dessiné empêche une collision de repère. | [S62](#s62) |
| NUM-06 | Numéroter fils et câbles | Socle à qualifier | Conserver les repères manuels lors d'une numérotation automatique. | [S07](#s07), [S13](#s13) |
| NUM-07 | Renvois en chaîne et étoile | Socle à qualifier | Contrôler quatre points de renvoi et signaler un point orphelin. | [S46](#s46) |
| NUM-08 | Trier les destinations de renvoi | Non démontré | Changer l'ordre de parcours sans modifier l'identité du signal. | [S46](#s46) |
| NUM-09 | Images de contacts configurables | Socle à qualifier | Les contacts NO et NC affichent leurs bonnes coordonnées. | [S47](#s47) |
| NUM-10 | Formats des références croisées | Socle à qualifier | Deux modèles client produisent des formats différents mais les mêmes cibles. | [S47](#s47) |

### TER — Borniers industriels

Lot L3. Dépendances : DEV, CON, NUM.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| TER-01 | Définir bornes et borniers | Socle à qualifier | Créer X1 avec douze bornes et conserver leurs données. | [S10](#s10) |
| TER-02 | Bornes principales et auxiliaires | Non démontré | Deux fonctions du même boîtier ne doublent pas la quantité physique. | [S10](#s10) |
| TER-03 | Bornes à plusieurs étages | Non démontré | Une borne à trois niveaux conserve son boîtier et ses trois potentiels. | [S11](#s11) |
| TER-04 | Assembler et séparer les étages | Non démontré | Séparer un niveau ne supprime ni son conducteur ni son article. | [S11](#s11) |
| TER-05 | Tri physique et tri logique | Socle à qualifier | Réordonner le bornier modifie son plan mais pas les destinations. | [S10](#s10) |
| TER-06 | Pontages manuels et automatiques | Socle à qualifier | Supprimer un pont isole le réseau attendu et met à jour les rapports. | [S10](#s10) |
| TER-07 | Accessoires et articles de bornier | Non démontré | Une butée et un séparateur apparaissent une seule fois dans la liste. | [S10](#s10) |
| TER-08 | Bornes distribuées | Non démontré | Deux représentations de la même borne partagent les mêmes cibles. | [S10](#s10) |
| TER-09 | Numérotation des bornes | Socle à qualifier | Renuméroter un sous-ensemble sans perdre les niveaux. | [S10](#s10) |
| TER-10 | Échanges de borniers | Non démontré | Exporter puis réimporter un bornier témoin sans perdre ses ponts. | [S10](#s10) |
| TER-11 | Destinations et potentiels | Socle à qualifier | Afficher simultanément les côtés interne et externe d'une borne. | [S10](#s10) |

### PLG — Connecteurs

Lot L3. Dépendances : DEV, CON, NUM.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| PLG-01 | Définir connecteurs et broches | Non démontré | Créer X20 à huit contacts et réouvrir ses huit affectations. | [S12](#s12) |
| PLG-02 | Accoupler mâle et femelle | Non démontré | Signaler une broche sans vis-à-vis dans le connecteur partenaire. | [S12](#s12) |
| PLG-03 | Connecteurs mixtes | Non démontré | Conserver deux inserts de genres différents dans un même boîtier. | [S12](#s12) |
| PLG-04 | Connecteurs hiérarchiques | Non démontré | Une broche garde son appartenance au sous-connecteur. | [S12](#s12) |
| PLG-05 | Ordre et repérage des broches | Non démontré | Réordonner les broches ne modifie pas leur identité électrique. | [S12](#s12) |
| PLG-06 | Articles par broche | Non démontré | Un contact à sertir figure dans la liste avec la bonne quantité. | [S12](#s12) |
| PLG-07 | Connecteurs modulaires | Non démontré | Remplacer un insert sans supprimer les autres inserts. | [S05](#s05), [S12](#s12) |

### CAB — Câbles et faisceaux 2D

Lot L3. Dépendances : CON, PLG, TER.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| CAB-01 | Définir câbles et conducteurs | Non démontré | Un câble cinq conducteurs conserve ses cinq affectations. | [S13](#s13) |
| CAB-02 | Affectation automatique des conducteurs | Non démontré | Associer quatre fils puis présenter le conducteur libre restant. | [S13](#s13) |
| CAB-03 | Affectation manuelle des conducteurs | Non démontré | Permuter deux âmes actualise les deux extrémités. | [S13](#s13) |
| CAB-04 | Réserves de câble | Non démontré | Les conducteurs non utilisés restent comptés et identifiables. | [S13](#s13) |
| CAB-05 | Blindages multiples | Non démontré | Deux blindages distincts conservent leurs raccordements respectifs. | [S13](#s13) |
| CAB-06 | Câbles hybrides | Non démontré | Une fibre et une âme cuivre gardent des types distincts. | [S13](#s13) |
| CAB-07 | Créer et compléter les câbles | Non démontré | Rassembler les liaisons sélectionnées sans doubler les connexions. | [S13](#s13) |
| CAB-08 | Source et destination déterministes | Non démontré | Inverser le câble conserve la correspondance broche à broche. | [S13](#s13) |
| CAB-09 | Cumuler les longueurs par article | Non démontré | Trois tronçons de même référence produisent la somme attendue. | [S13](#s13) |
| CAB-10 | Câbles préassemblés avec connecteurs | Non démontré | Un câble équipé conserve les affectations de ses deux fiches. | [S14](#s14) |

### PLC — Automates E/S et réseaux

Lot L3. Dépendances : DEV, CON, NUM.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| PLC-01 | Stations racks modules et slots | Non démontré | Deux automates conservent leurs racks et emplacements respectifs. | [S16](#s16) |
| PLC-02 | Vue centralisée des E/S | Socle à qualifier | Retrouver toutes les entrées TOR d'une station. | [S15](#s15) |
| PLC-03 | Représentations carte et folio | Socle à qualifier | Une E/S sur le synoptique et le folio possède une seule adresse. | [S15](#s15) |
| PLC-04 | Canaux et raccordements multiples | Non démontré | Un canal analogique conserve alimentation signal et commun. | [S15](#s15) |
| PLC-05 | Adressage automatique | Non démontré | Adresser deux cartes avec les réserves et pas configurés. | [S15](#s15) |
| PLC-06 | Adresses symboliques et listes | Non démontré | Importer une liste avec accents sans dédoubler les signaux. | [S15](#s15) |
| PLC-07 | Bus et réseaux physiques | Non démontré | Une passerelle reste rattachée à ses deux réseaux. | [S16](#s16) |
| PLC-08 | Descriptions fabricant | Non démontré | Vérifier les slots configurés contre le fichier de description. | [S17](#s17) |
| PLC-09 | Sous-appareils et modules configurables | Non démontré | Conserver les sous-modules lors du retour d'un échange. | [S17](#s17) |
| PLC-10 | Échange AutomationML | Non démontré | Importer exporter puis comparer appareils ports et adresses. | [S16](#s16) |
| PLC-11 | Échange TIA Portal versionné | Non démontré | Tester un jeu TIA 20 et un jeu TIA 21 avec le profil déclaré. | [S58](#s58) |
| PLC-12 | Génération de schémas E/S | Non démontré | Placer huit canaux depuis une liste sans ressaisie des adresses. | [S15](#s15) |

### ART — Catalogue et données fabricant

Lot L2. Dépendances : DEV.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| ART-01 | Références fabricant et fournisseur | Socle à qualifier | Retrouver une référence précise après export et réimport. | [S18](#s18) |
| ART-02 | Recherche et classement technique | Socle à qualifier | Filtrer un contacteur par tension bobine et nombre de contacts. | [S18](#s18) |
| ART-03 | Accessoires obligatoires et facultatifs | Non démontré | L'article principal propose ses accessoires avec quantités séparées. | [S18](#s18) |
| ART-04 | Assemblages et modules commerciaux | Non démontré | Décomposer un ensemble sans compter simultanément le parent et ses enfants. | [S18](#s18) |
| ART-05 | Articles multilingues et devises | Non démontré | Changer la langue ne modifie pas les identifiants de commande. | [S18](#s18) |
| ART-06 | Modèles de fonctions des articles | Non démontré | Une référence définit les raccordements réellement disponibles. | [S06](#s06) |
| ART-07 | Édition externe des articles | Non démontré | Un import de prix conserve les données techniques existantes. | [S18](#s18) |
| ART-08 | Base partagée d'articles | Non démontré | Deux postes lisent la même version d'un article validé. | [S18](#s18) |
| ART-09 | Articles obsolètes et remplaçants | Non démontré | Détecter un remplacement circulaire et proposer une référence valide. | [S53](#s53) |
| ART-10 | Identifiants Global Asset ID | Non démontré | Une copie reçoit une identité distincte selon les règles configurées. | [S54](#s54) |
| ART-11 | Propriétés techniques extensibles | Non démontré | Filtrer 1 kV et 1000 V comme la même grandeur. | [S31](#s31) |
| ART-12 | Import des données portables EDZ | Socle à qualifier | Afficher explicitement ce qui est importé et ce qui est perdu. | [S31](#s31) |

### MAC — Macros et configuration

Lot L4. Dépendances : DEV, ART, NUM.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| MAC-01 | Macros de circuits et folios | Socle à qualifier | Réutiliser un départ moteur en conservant ses relations internes. | [S19](#s19) |
| MAC-02 | Projets de macros et génération | Non démontré | Régénérer une bibliothèque depuis son projet source. | [S19](#s19) |
| MAC-03 | Variantes de représentation | Non démontré | Choisir une variante sans changer l'identité fonctionnelle du circuit. | [S19](#s19) |
| MAC-04 | Paramètres et jeux de valeurs | Non démontré | Changer la puissance du moteur actualise les propriétés liées. | [S20](#s20) |
| MAC-05 | Variables sur plusieurs objets | Non démontré | Un jeu de valeurs modifie appareil texte et propriétés du folio. | [S20](#s20) |
| MAC-06 | Remplacement paramétré de symboles | Non démontré | Un changement de variante conserve les raccordements compatibles. | [S20](#s20) |
| MAC-07 | Groupes protégés | Non démontré | Déplacer une macro protégée sans altérer sa structure interne. | [S19](#s19) |
| MAC-08 | Mise à jour des macros | Non démontré | Présenter les différences avant d'écraser une adaptation locale. | [S19](#s19) |
| MAC-09 | Options de machine activables | Non démontré | Désactiver une option la retire des rapports sans effacer son modèle. | [S21](#s21) |
| MAC-10 | Sections et paramètres d'option | Non démontré | Deux options indépendantes conservent leurs valeurs propres. | [S21](#s21) |

### RPT — Rapports et dossier de fabrication

Lot L4. Dépendances : DEV, CON, ART.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| RPT-01 | Nomenclature détaillée et cumulée | Socle à qualifier | Un relais représenté trois fois apparaît comme un seul appareil physique. | [S22](#s22) |
| RPT-02 | Liste des repères d'appareils | Socle à qualifier | Chaque repère exporté ouvre l'appareil correspondant. | [S22](#s22) |
| RPT-03 | Plans et implantation des borniers | Socle à qualifier | Vérifier les niveaux ponts et accessoires contre le bornier témoin. | [S22](#s22) |
| RPT-04 | Plans de connecteurs et broches | Non démontré | Comparer le plan avec les huit affectations du connecteur témoin. | [S22](#s22) |
| RPT-05 | Plans et affectations de câbles | Non démontré | Comparer chaque âme aux deux bornes réellement raccordées. | [S22](#s22) |
| RPT-06 | Schémas de raccordement des appareils | Non démontré | La vue appareil restitue toutes ses destinations. | [S22](#s22) |
| RPT-07 | Listes des connexions | Socle à qualifier | La liste contient les deux extrémités de chaque fil. | [S22](#s22) |
| RPT-08 | Vues des potentiels | Non démontré | Un potentiel sur trois folios apparaît une seule fois avec ses cibles. | [S22](#s22) |
| RPT-09 | Rapports cartes et adresses API | Non démontré | Les réserves et canaux utilisés sont distingués. | [S22](#s22) |
| RPT-10 | Sommaire couverture et structures | Socle à qualifier | La pagination correspond au dossier PDF final. | [S22](#s22) |
| RPT-11 | Listes fabricants et fournisseurs | Non démontré | Chaque article renvoie au fournisseur choisi. | [S22](#s22) |
| RPT-12 | Légendes d'armoire et montage | Non démontré | L'implantation et sa légende partagent les mêmes repères. | [S22](#s22) |
| RPT-13 | Listes et plans de topologie | Non démontré | La longueur rapportée égale celle du parcours retenu. | [S22](#s22) |
| RPT-14 | Options paramètres et révisions | Non démontré | Le rapport distingue les options actives des options disponibles. | [S22](#s22) |
| RPT-15 | Documentation des données de base | Non démontré | Lister les symboles cartouches et formulaires réellement utilisés. | [S22](#s22) |
| RPT-16 | Diagramme de séquence fonctionnelle | Non démontré | Le document généré correspond aux étapes du scénario témoin. | [S22](#s22) |
| RPT-17 | Rapports incorporés ou sur folios | Non démontré | Actualiser une légende locale sans dupliquer ses lignes. | [S23](#s23) |
| RPT-18 | Modèles filtres tris et blocs | Non démontré | Deux mises à jour successives donnent les mêmes lignes et pages. | [S23](#s23), [S24](#s24) |
| RPT-19 | Étiquetage de fabrication | Non démontré | Les repères exportés correspondent aux appareils et fils retenus. | [S01](#s01) |
| RPT-20 | Listes des assemblages et modules | Non démontré | Vérifier le choix détaillé ou regroupé sur un ensemble composé. | [S22](#s22) |
| RPT-21 | Vues générales câbles borniers connecteurs | Non démontré | Comparer les trois listes aux objets présents dans le dossier témoin. | [S22](#s22) |
| RPT-22 | Légendes de découpes | Non démontré | Associer chaque entrée à un objet de fabrication identifié. | [S22](#s22) |

### MDT — Symboles formulaires et données de base

Lot L4. Dépendances : ART, MAC.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| MDT-01 | Éditer les bibliothèques de symboles | Socle à qualifier | Créer un symbole à six raccordements et l'incorporer au projet. | [S40](#s40) |
| MDT-02 | Variantes graphiques de symboles | Non démontré | La rotation de la variante conserve les numéros des raccordements. | [S42](#s42) |
| MDT-03 | Éditer les cartouches | Socle à qualifier | Changer le logo et vérifier tous les formats de folio. | [S24](#s24) |
| MDT-04 | Éditer les formulaires de rapports | Non démontré | Un tableau de 80 lignes se poursuit sans perdre son en-tête. | [S24](#s24) |
| MDT-05 | Organiser les données par client | Non démontré | Deux clients peuvent utiliser deux versions d'un même modèle. | [S40](#s40) |
| MDT-06 | Comparer versions des données | Non démontré | Montrer les modèles plus récents avant synchronisation. | [S41](#s41) |
| MDT-07 | Synchroniser les ressources du projet | Socle à qualifier | Mettre à jour un symbole sélectionné sans toucher aux autres. | [S41](#s41) |
| MDT-08 | Ressources incorporées transportables | Socle à qualifier | Ouvrir un projet dont la bibliothèque d'origine est absente. | [S40](#s40) |

### DAT — Propriétés et édition en masse

Lot L4. Dépendances : DEV, ART.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| DAT-01 | Propriétés personnalisées typées | Non démontré | Refuser un texte dans une grandeur numérique configurée. | [S31](#s31) |
| DAT-02 | Propriétés transmises avec le projet | Non démontré | Le destinataire retrouve valeurs et définitions sans import séparé. | [S31](#s31) |
| DAT-03 | Propriétés composées indirectes | Non démontré | Afficher la localisation de la cible et l'actualiser après déplacement. | [S32](#s32) |
| DAT-04 | Calculs de propriétés | Non démontré | Un calcul configuré donne un résultat reproductible avec unités. | [S64](#s64) |
| DAT-05 | Recherche et remplacement | Socle à qualifier | Remplacer un texte sur deux folios sans altérer les repères voisins. | [S04](#s04) |
| DAT-06 | Édition externe aller-retour | Non démontré | Réimporter une table modifiée sans changer les lignes intactes. | [S50](#s50) |
| DAT-07 | Création d'objets depuis une table | Non démontré | Importer dix fonctions avec identités stables et rapport d'erreurs. | [S50](#s50) |
| DAT-08 | Édition tabulaire des propriétés | Non démontré | Modifier une colonne sur cent objets et annuler ensemble. | [S56](#s56) |

### LNG — Langues et conventions

Lot L4. Dépendances : DAT, MDT.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| LNG-01 | Dictionnaires métier multilingues | Non démontré | Deux projets utilisent des lexiques client différents. | [S29](#s29) |
| LNG-02 | Traduction à la saisie ou par lot | Non démontré | Traduire le dossier français vers néerlandais sans changer les repères. | [S30](#s30) |
| LNG-03 | Corrections manuelles de traduction | Non démontré | Conserver une traduction validée lors d'un nouveau traitement. | [S30](#s30) |
| LNG-04 | Liste des termes manquants | Non démontré | Exporter trois termes puis réimporter leurs traductions. | [S29](#s29) |
| LNG-05 | Termes exclus et Unicode | Non démontré | Un code produit reste inchangé dans les sorties multilingues. | [S29](#s29) |
| LNG-06 | Changement de convention de dessin | Non démontré | Convertir le projet témoin puis contrôler symboles cartouches et renvois. | [S01](#s01) |

### QA — Contrôles et vérification

Lot L2. Dépendances : DEV, CON.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| QA-01 | Contrôles interactifs et par lot | Socle à qualifier | Introduire un défaut connu et le retrouver dans les deux modes. | [S25](#s25) |
| QA-02 | Profils de règles et sévérités | Non démontré | Deux profils classent différemment le même défaut identifié. | [S25](#s25) |
| QA-03 | Contrôler une sélection ou le projet | Non démontré | Les défauts hors sélection ne disparaissent pas de l'historique. | [S25](#s25) |
| QA-04 | Diagnostics navigables | Non démontré | Activer un diagnostic sélectionne exactement son objet. | [S25](#s25) |
| QA-05 | Contrôles des articles | Non démontré | Repérer une fonction incompatible avec l'article affecté. | [S25](#s25) |
| QA-06 | Messages explicatifs et corrections | Non démontré | Chaque défaut expose sa cause et une action vérifiable. | [S25](#s25) |
| QA-07 | Vérification du cahier client | Non démontré | Un projet incomplet échoue avec une liste de critères non satisfaits. | [S01](#s01) |
| QA-08 | Limites de raccordement | Socle à qualifier | Une borne limitée à deux fils signale le troisième. | [S25](#s25) |

### REV — Révisions et livraison

Lot L5. Dépendances : QA, RPT.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| REV-01 | Créer une référence figée | Non démontré | La référence reste identique après modification du projet courant. | [S26](#s26) |
| REV-02 | Suivre les changements | Non démontré | Ajouter déplacer et supprimer un appareil produit trois événements. | [S26](#s26) |
| REV-03 | Comparer des propriétés choisies | Non démontré | Ignorer une couleur et signaler un changement de référence fabricant. | [S26](#s26) |
| REV-04 | Marqueurs de modification | Non démontré | Chaque marqueur ouvre l'objet et le changement associés. | [S26](#s26) |
| REV-05 | Marqueurs des suppressions | Non démontré | Un composant supprimé conserve ses anciennes données consultables. | [S28](#s28) |
| REV-06 | Clôturer folios et projets | Non démontré | Une page modifiée repasse en brouillon après clôture. | [S27](#s27) |
| REV-07 | Indices motifs auteurs et dates | Non démontré | Retrouver la raison du changement dans le dossier livré. | [S27](#s27) |
| REV-08 | Rapports des révisions | Non démontré | Le rapport distingue révision du projet et indice du folio. | [S26](#s26) |

### TEAM — Travail en équipe et droits

Lot L5. Dépendances : REV, PRJ.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| TEAM-01 | Édition simultanée et conflits | Non démontré | Deux postes modifiant le même objet obtiennent un conflit explicite. | [S01](#s01) |
| TEAM-02 | Sections de travail attribuées | Non démontré | Chaque utilisateur retrouve les folios de son périmètre. | [S37](#s37) |
| TEAM-03 | Accès temporaire au projet complet | Non démontré | Une mise à jour globale ne modifie pas les droits persistants. | [S37](#s37) |
| TEAM-04 | Extraire et réintégrer un sous-projet | Non démontré | Rendre un sous-projet modifié sans écraser les autres sections. | [S38](#s38) |
| TEAM-05 | Sous-projets hors réseau | Non démontré | Rouvrir un lot autonome avec ses symboles et références disponibles. | [S38](#s38) |
| TEAM-06 | Rôles et droits par commande | Non démontré | Un utilisateur lecteur ne peut pas modifier les données par une autre vue. | [S01](#s01) |
| TEAM-07 | Utilisateurs et groupes d'entreprise | Non démontré | Un changement de groupe modifie les droits à la reconnexion. | [S01](#s01) |
| TEAM-08 | Commentaires avec état et historique | Non démontré | Passer une remarque à traitée conserve son auteur et son origine. | [S01](#s01) |
| TEAM-09 | Ancien moniteur multi-utilisateur | Historique | Conserver la trace du retrait 2027 et ne pas le présenter comme fonction actuelle. | [S53](#s53) |

### LAY — Implantation et topologie 2D

Lot L5. Dépendances : DEV, CAB.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| LAY-01 | Plaques et implantation 2D | Non démontré | Placer les appareils d'une armoire selon leurs dimensions. | [S34](#s34) |
| LAY-02 | Suivi des appareils à placer | Non démontré | Une seconde représentation ne crée pas un deuxième appareil physique. | [S01](#s01) |
| LAY-03 | Légende d'implantation | Non démontré | La légende suit le remplacement d'un appareil. | [S34](#s34) |
| LAY-04 | Réseau de parcours 2D | Non démontré | Créer trois chemins et rattacher leurs extrémités aux bons équipements. | [S33](#s33) |
| LAY-05 | Routage selon filtres de parcours | Non démontré | Un câble interdit dans un chemin utilise une autre route. | [S33](#s33) |
| LAY-06 | Longueurs et différences de hauteur | Non démontré | Ajouter une élévation connue augmente la longueur calculée. | [S33](#s33) |
| LAY-07 | Topologie sans placement | Non démontré | Les objets préparés en table peuvent ensuite être placés. | [S33](#s33) |
| LAY-08 | Rapports de câbles routés | Non démontré | Comparer la liste des câbles traversant un chemin à son contenu. | [S33](#s33) |

### FLU — Fluid et équipements hybrides

Lot L5. Dépendances : CON, DEV, RPT.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| FLU-01 | Hydraulique pneumatique et autres fluides | Non démontré | Distinguer deux circuits de métiers différents dans le même projet. | [S35](#s35) |
| FLU-02 | Repérage des appareils Fluid | Non démontré | Le profil choisi produit des repères reproductibles. | [S35](#s35) |
| FLU-03 | Tuyaux tubes et caractéristiques | Non démontré | Un diamètre de tube ne devient pas une section électrique. | [S35](#s35) |
| FLU-04 | Groupes et blocs de dispositifs | Non démontré | Conserver les composants internes d'un bloc de distributeurs. | [S35](#s35) |
| FLU-05 | Références électrique vers Fluid | Non démontré | Une électrovanne relie sa bobine au bon distributeur. | [S60](#s60) |
| FLU-06 | Vues fonctionnelles Fluid | Non démontré | L'aperçu et le schéma détaillé partagent les mêmes identités. | [S60](#s60) |
| FLU-07 | Listes de conduites et contrôles | Non démontré | Une conduite orpheline est signalée avant génération du dossier. | [S35](#s35) |
| FLU-08 | Configurateur de flexibles | Non démontré | Produire un code depuis les règles choisies avec ses paramètres conservés. | [S36](#s36) |

### IO — Échanges et publication

Lot L4. Dépendances : RPT, QA.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| IO-01 | PDF de projet et sélection | Socle à qualifier | Exporter uniquement les folios sélectionnés dans le bon ordre. | [S52](#s52) |
| IO-02 | PDF navigable | Socle à qualifier | Suivre un renvoi et revenir au bon appareil. | [S65](#s65) |
| IO-03 | Exports graphiques | Socle à qualifier | Comparer le cadrage de la page à l'image produite. | [S52](#s52) |
| IO-04 | Exports DXF et DWG | Socle à qualifier | Documenter les pertes logiques et vérifier la géométrie dans le lecteur cible. | [S49](#s49) |
| IO-05 | Imports DXF et DWG | Non démontré | Importer un dessin sans attribuer une logique électrique aux traits. | [S49](#s49) |
| IO-06 | Archives avec données de base | Socle à qualifier | Restaurer le dossier sur un poste vierge et contrôler les ressources. | [S48](#s48) |
| IO-07 | Impression et sorties par profils | Socle à qualifier | Deux sorties successives avec le même profil ont la même pagination. | [S52](#s52) |
| IO-08 | Interopérabilité native EPLAN | Non démontré | Définir une matrice de formats et pertes avant toute promesse d'aller-retour. | [S52](#s52) |
| IO-09 | PDF/A et documents liés | Non démontré | Valider le PDF avec un outil indépendant et signaler les documents exclus. | [S65](#s65) |
| IO-10 | Signets langues et propriétés PDF | Non démontré | Le lecteur retrouve chaque folio et affiche les textes dans la langue choisie. | [S65](#s65) |

### AUTO — API scripts et traitement automatique

Lot L4. Dépendances : DEV, QA, RPT.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| AUTO-01 | Actions exécutables en ligne de commande | Socle à qualifier | Un export sans interface renvoie un code d'échec si le projet manque. | [S51](#s51) |
| AUTO-02 | Scripts et paramètres | Socle à qualifier | Un script reçoit un projet explicite et rapporte ses erreurs. | [S51](#s51) |
| AUTO-03 | Traitements enchaînés | Socle à qualifier | Exécuter contrôle puis rapports puis export avec un journal unique. | [S51](#s51) |
| AUTO-04 | Commandes personnalisées | Non démontré | La commande ajoutée expose les mêmes préconditions que la commande native. | [S59](#s59) |
| AUTO-05 | API du modèle métier | Socle à qualifier | Créer un appareil avec deux fonctions dans une transaction annulable. | [S59](#s59) |
| AUTO-06 | Extensions externes | Non démontré | Une extension incompatible refuse le chargement sans corrompre le projet. | [S01](#s01) |

### UX — Ergonomie et navigation

Lot L1. Dépendances : EDT.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| UX-01 | Espaces de travail mémorisés | Socle à qualifier | Retrouver disposition zoom et folio après redémarrage. | [S43](#s43) |
| UX-02 | Panneaux détachables | Socle à qualifier | Passer de deux écrans à un conserve tous les panneaux accessibles. | [S43](#s43) |
| UX-03 | Commandes contextuelles | Socle à qualifier | Sans appareil sélectionné les actions nécessitant un appareil expliquent leur état. | [S43](#s43) |
| UX-04 | Recherche de commandes | Non démontré | Trouver une commande par son nom et son synonyme français. | [S43](#s43) |
| UX-05 | Accès rapides et raccourcis | Socle à qualifier | Utiliser les commandes fréquentes sans ouvrir de menus successifs. | [S43](#s43) |
| UX-06 | Thèmes clair sombre | Non démontré | Les couleurs de travail n'altèrent pas le document imprimé. | [S43](#s43) |
| UX-07 | Filtres et schémas du projet | Non démontré | Deux utilisateurs retrouvent le filtre fourni avec le projet. | [S56](#s56) |
| UX-08 | Insertion avec tags et aperçus | Non démontré | Retrouver un symbole par fonction puis vérifier ses raccordements. | [S56](#s56) |
| UX-09 | Navigation graphique vers données | Socle à qualifier | Sélectionner le même objet depuis un tableau et son symbole. | [S43](#s43) |
| UX-10 | État des calculs et diagnostics | Non démontré | Afficher explicitement que les rapports doivent être recalculés. | [S43](#s43) |

### CLD — Services connectés associés

Lot L6. Dépendances : ART, REV, AUTO.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| CLD-01 | Catalogue fabricant en ligne | Non démontré | Importer des données autorisées avec source date et version. | [S01](#s01) |
| CLD-02 | Gestion cloud des données projet | Non démontré | Deux versions publiées restent consultables séparément. | [S01](#s01) |
| CLD-03 | Gestion cloud des articles | Non démontré | Un article mis à jour conserve son historique et ses dépendances. | [S55](#s55) |
| CLD-04 | Bibliothèques et génération configurée | Non démontré | Générer deux variantes à partir des mêmes paramètres de modèle. | [S01](#s01) |
| CLD-05 | Consultation et annotations partagées | Non démontré | Une remarque distante cible un objet stable du projet publié. | [S01](#s01) |
| CLD-06 | Synchronisation cloud des données de base | Non démontré | Comparer puis appliquer une mise à jour de bibliothèque. | [S61](#s61) |
| CLD-07 | Assistant intégré et commandes guidées | Non démontré | Chaque modification suggérée reste explicable révisable et annulable. | [S53](#s53) |
| CLD-08 | Ancien service eStock | Historique | Tracer son remplacement 2027 sans supprimer le besoin de catalogue partagé. | [S55](#s55) |

### EXT — Produits complémentaires et intégrations

Lot L6. Dépendances : LAY, IO, AUTO.

| ID | Capacité cible | État GeMMe | Recette proposée | Sources |
| --- | --- | --- | --- | --- |
| EXT-01 | Armoires 3D et préparation fabrication | Extension | Définir une recette indépendante pour implantation collisions et fabrication. | [S57](#s57) |
| EXT-02 | Préplanification fonctionnelle et procédés | Extension | Relier un objet de conception aux fonctions détaillées sans doublon. | [S57](#s57) |
| EXT-03 | Faisceaux et câblage machine 3D | Extension | Distinguer longueur physique et représentation schématique. | [S57](#s57) |
| EXT-04 | Configuration industrielle étendue | Extension | Une configuration produit un dossier traçable et reproductible. | [S57](#s57) |
| EXT-05 | ERP et synchronisation des articles | Extension | Un échange conserve identités quantités et sens de synchronisation. | [S01](#s01) |
| EXT-06 | PDM PLM et documents | Extension | Deux versions du dossier restent reliées à la bonne révision produit. | [S01](#s01) |
| EXT-07 | Production et dossier numérique | Extension | Les retours d'atelier citent la version exacte du dossier exécuté. | [S57](#s57) |

## 10. Références officielles consultées

Consultation le 9 octobre 2026. Les numéros servent à la traçabilité du registre. La version de chaque source reste explicite ; une source ancienne ne suffit pas à qualifier la parité 2027.

<a id="s01"></a>
- **S01 — 2026** : [Performances P8](https://www.eplan.com/content/dam/eplan/corporate/performance-descriptions/2026/en/performance-description-eplan-electric-p8.pdf).

<a id="s02"></a>
- **S02 — 2027** : [Structure du projet](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/projectstructure_h_prjstrukturdefinieren.htm).

<a id="s03"></a>
- **S03 — 2027** : [Types de folios](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/pagebrowsergui_k_seitentypen.htm).

<a id="s04"></a>
- **S04 — 2027** : [Éditeur graphique](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/gededitgui_k_start.htm).

<a id="s05"></a>
- **S05 — 2027** : [Appareils](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/devicelistgui_k_start.htm).

<a id="s06"></a>
- **S06 — 2027** : [Définitions des appareils](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/devicelistgui_h_geraetedefinitionenanlegen.htm).

<a id="s07"></a>
- **S07 — 2027** : [Connexions](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/connectionbrowsergui_k_start.htm).

<a id="s08"></a>
- **S08 — 2027** : [Symboles de connexion](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/egedgui_k_start.htm).

<a id="s09"></a>
- **S09 — 2027** : [Potentiels et signaux](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/potentialbrowsergui_k_start.htm).

<a id="s10"></a>
- **S10 — 2027** : [Borniers](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/terminalgui_k_start.htm).

<a id="s11"></a>
- **S11 — 2027** : [Bornes à étages](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/terminalgui_h_mehrstockklemmenarbeit.htm).

<a id="s12"></a>
- **S12 — 2027** : [Connecteurs](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/plugsgui_k_start.htm).

<a id="s13"></a>
- **S13 — 2027** : [Câbles](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/cablegui_k_start.htm).

<a id="s14"></a>
- **S14 — 2026** : [Câbles préfabriqués](https://eplan.help/techtipps/en-us/Kabel/TechTip-Usecase-prefabricated-cables.pdf).

<a id="s15"></a>
- **S15 — 2027** : [Automates](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/plcgui_k_start.htm).

<a id="s16"></a>
- **S16 — 2027** : [Échanges automates](https://eplan.help/techtipps/en-us/SPS/TechTip-PLC-data-exchange.pdf).

<a id="s17"></a>
- **S17 — 2027** : [Descriptions fabricant des automates](https://eplan.help/techtipps/en-us/SPS/TechTip-Configuring-PLC-devices-with-device-description-files.pdf).

<a id="s18"></a>
- **S18 — 2027** : [Gestion des articles](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/articlesgui_k_start.htm).

<a id="s19"></a>
- **S19 — 2027** : [Macros](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/macrosgui_k_start.htm).

<a id="s20"></a>
- **S20 — 2027** : [Objets paramétrés](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/macrosgui_d_platzhalterobjekt.htm).

<a id="s21"></a>
- **S21 — 2027** : [Options de projet](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/projectoptionsgui_k_start.htm).

<a id="s22"></a>
- **S22 — 2027** : [Types de rapports](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/formgeneratorgui_k_auswertungstypen.htm).

<a id="s23"></a>
- **S23 — 2027** : [Génération de rapports](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/formgeneratorgui_k_start.htm).

<a id="s24"></a>
- **S24 — 2027** : [Formulaires et cartouches](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/formeditorgui_k_start.htm).

<a id="s25"></a>
- **S25 — 2027** : [Gestion des messages](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/msgmanagementgui_k_start.htm).

<a id="s26"></a>
- **S26 — 2027** : [Révisions](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/revisionmgtgui_k_start.htm).

<a id="s27"></a>
- **S27 — 2027** : [Clôture des folios](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/revisionmgtgui_h_seitenabschliessen.htm).

<a id="s28"></a>
- **S28 — 2027** : [Marqueurs de suppression](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/revisionmgtgui_h_loeschzeichenbearbeiten.htm).

<a id="s29"></a>
- **S29 — 2027** : [Dictionnaires](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/translatedbgui_k_start.htm).

<a id="s30"></a>
- **S30 — 2027** : [Traduction](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/translategui_k_start.htm).

<a id="s31"></a>
- **S31 — 2027** : [Propriétés personnalisées](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/userdefproperties_k_prinzip.htm).

<a id="s32"></a>
- **S32 — 2024** : [Propriétés composées](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2024/Content/htm/blockproperties_k_prinzip.htm).

<a id="s33"></a>
- **S33 — 2027** : [Topologie et routage 2D](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/cablinggui_k_start.htm).

<a id="s34"></a>
- **S34 — 2027** : [Implantation 2D](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/panellayoutgui_k_start.htm).

<a id="s35"></a>
- **S35 — 2026** : [Fluid](https://www.eplan.help/en-US/Infoportal/Content/Plattform/2026/Content/htm/ftechnic_k_start.htm).

<a id="s36"></a>
- **S36 — 2027** : [Configurateur de flexibles](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/fluidhoseconfiggui_k_start.htm).

<a id="s37"></a>
- **S37 — 2027** : [Sections de travail](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/workingsection_h_bereichedefinieren.htm).

<a id="s38"></a>
- **S38 — 2027** : [Sous-projets](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/subprojectservicesgui_k_start.htm).

<a id="s39"></a>
- **S39 — 2027** : [Gestion des projets](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/prjmanagementgui_k_start.htm).

<a id="s40"></a>
- **S40 — 2027** : [Données de base](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/xmasterdatasettingsgui_k_start.htm).

<a id="s41"></a>
- **S41 — 2027** : [Synchronisation des données de base](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/modaldialogsdb_d_stammdatenabgleich.htm).

<a id="s42"></a>
- **S42 — 2027** : [Variantes de symboles](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/symboleditorgui_h_mitsymbolvariantenarbeiten.htm).

<a id="s43"></a>
- **S43 — 2027** : [Interface utilisateur](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/userinterface_k_hintergrund.htm).

<a id="s44"></a>
- **S44 — 2027** : [Numérotation à l'insertion](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/numerationgui_k_start.htm).

<a id="s45"></a>
- **S45 — 2027** : [Renumérotation](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/offlinenumerationgui_k_start.htm).

<a id="s46"></a>
- **S46 — 2027** : [Renvois inter-folios](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/interruptionpointgui_k_darstellungabbruchstellen.htm).

<a id="s47"></a>
- **S47 — 2027** : [Références et images de contacts](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/xessettingsgui_d_einstellungenprojektqvwallgemein.htm).

<a id="s48"></a>
- **S48 — 2027** : [Archives](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/bakbackupdlggui_k_prinzip.htm).

<a id="s49"></a>
- **S49 — 2027** : [Interface DXF/DWG](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/xdxfgui_k_start.htm).

<a id="s50"></a>
- **S50 — 2027** : [Édition externe](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/pxfinterface_k_start.htm).

<a id="s51"></a>
- **S51 — 2027** : [Actions et automatisation](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/availableactions_k_start.htm).

<a id="s52"></a>
- **S52 — 2027** : [API des exports](https://www.eplan.help/en-US/infoportal/content/api/2027/export.html).

<a id="s53"></a>
- **S53 — 2027** : [Évolutions 2027](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/news_p_plattform_notes_2027.htm).

<a id="s54"></a>
- **S54 — 2027** : [Propriétés des articles 2027](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/news_p_artikelverwaltung_eigenschaften_555495.htm).

<a id="s55"></a>
- **S55 — 2027** : [Applications cloud](https://www.eplan.help/en-us/Infoportal/Content/SwReqs/Content/htm/SwReqs_k_cloud.htm).

<a id="s56"></a>
- **S56 — 2027 et antérieures** : [Notes de version](https://www.eplan.help/en-US/Infoportal/Content/htm/portal_rel_notes_platform.htm).

<a id="s57"></a>
- **S57 — consulté le 2026-10-09** : [Portefeuille EPLAN](https://www.eplan.com/us-en/products/).

<a id="s58"></a>
- **S58 — 2027** : [Échanges TIA Portal](https://www.eplan.help/techtipps/en-US/SPS/TechTip-PLC-data-exchange-with-SIEMENS-STEP-7-TIA-Portal.pdf).

<a id="s59"></a>
- **S59 — 2027** : [API actions](https://www.eplan.help/en-US/Infoportal/content/api/2027/Actions.html).

<a id="s60"></a>
- **S60 — 2027** : [Folios Fluid](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/ftechnic_k_seitentyp_fluid.htm).

<a id="s61"></a>
- **S61 — 2027** : [Synchronisation cloud des données de base](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/modaldialogsdb_d_stammdatenabgleichcloud.htm).

<a id="s62"></a>
- **S62 — 2027** : [Fonctions et numérotation](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/numerationgui_k_verhaltenfunktionen.htm).

<a id="s63"></a>
- **S63 — 2026** : [API de renumérotation](https://eplan.help/en-us/Infoportal/Content/api/2026/renumber.html).

<a id="s64"></a>
- **S64 — 2027** : [Calculs de propriétés](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2027/Content/htm/blockproperties_k_berechnung.htm).

<a id="s65"></a>
- **S65 — 2025** : [Paramètres PDF](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2025/Content/htm/pdfexportgui_r_allgemein.htm).
