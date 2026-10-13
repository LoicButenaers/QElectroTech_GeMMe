# Repérage et renvois interfolios

## Références étudiées

Documentation officielle EPLAN consultée le 12 octobre 2026. Les points d’interruption permettent de poursuivre une connexion ailleurs dans le projet ; leur nom commun identifie les points associés. GeMMeElec reprend ce principe avec ses propres symboles et son modèle de connexions. [EPLAN : interruption points](https://eplan.help/en-us/Infoportal/Content/Plattform/2.9/Content/htm/interruptionpointgui_k_start.htm).

EPLAN distingue notamment les références en étoile, d’une source vers plusieurs points, et en chaîne, par paires 1–2, 3–4, etc. Ce sont les deux modes retenus ici. [EPLAN : types de références croisées](https://eplan.help/fr-fr/infoportal/content/plattform/2022/content/htm/xessettingsgui_k_querverweisarten.htm).

Le repérage de page par lignes et colonnes d’un cadre permet de retrouver les éléments et les destinations. Le cadre GeMMeElec est configuré par défaut comme l’image fournie : 20 colonnes et dix lignes A–J. [EPLAN : lignes et colonnes des cadres](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2.9/Content/htm/formeditorgui_h_normblaetterpfadeaktualisieren.htm).

Cette version met en œuvre ces comportements précis, sans annoncer l’ensemble des réglages et fonctions d’EPLAN.

## Repérer un appareil

Le menu **Folio → Repérage lignes / colonnes…** règle de 1 à 40 colonnes et de 1 à 26 lignes. La case dédiée applique le réglage à tous les folios de schéma. Les chiffres sont en haut, les lettres à gauche ; le cadre est imprimé dans le PDF et exporté en SVG. Il est indépendant de la grille de placement, réservée à l’éditeur.

L’adresse utilise le numéro du folio et la zone contenant le centre du symbole : `3 / C7` ou `CMD-02 / D10` si le numéro est libre. Les limites de zones vont de gauche à droite et de haut en bas ; un centre hors de la zone de dessin donne `hors cadre`. La hauteur disponible tient compte du cartouche. Les folios BOM ne portent pas ce cadre.

La colonne **Folios / zones** de la nomenclature associe chaque repère à son emplacement : `K1 : 3 / C7`. Lorsqu’une ligne regroupe plusieurs appareils identiques, chaque repère conserve son adresse. Le déplacement, le numéro du folio et son ordre sont pris en compte au recalcul de la BOM. Pour les pages BOM déjà présentes dans le projet, cliquer **Ajouter / actualiser les folios BOM** après modification.

## Relier deux folios

1. Dans **Symboles industriels → 10 · Renvois interfolios**, poser **Folio suivant · départ de renvoi** sur la première page. Raccorder le conducteur à sa borne.
2. Poser **Folio précédent · arrivée de renvoi** sur l’autre page et raccorder sa borne au conducteur qui poursuit le circuit. La touche R oriente la flèche.
3. Clic droit sur une flèche → **Lier à un autre report…** : choisir la contrepartie dans la liste (folio, zone, titre et nom), saisir un nom commun, par exemple `COMMANDE_M1`, et cliquer **Lier les reports**. Le nom est appliqué aux deux extrémités dans une seule opération annulable. Un nom utilisé par une autre paire est refusé. Pour les groupes avancés, les propriétés permettent toujours de saisir le même **Nom commun de la liaison** sur chaque point, avec application immédiate.
4. Les deux symboles sont des triangles évidés noirs dirigés vers la droite : le départ a sa borne à gauche de la base, l’arrivée à droite de la pointe. La bibliothèque et le plan partagent leur géométrie. Nom et destination sont noirs : `DI1.4 / 2 / D9`. Double-cliquer la flèche ou son texte dans n’importe quel mode, ou utiliser le clic droit **Aller au report lié**, pour rejoindre cette destination. Un double-clic sur un report non lié ouvre le choix de contrepartie. Les longues listes de destinations passent sur plusieurs lignes. Les anciens réglages de position des textes et les raccordements restent conservés.

Les noms sont comparés sans tenir compte de la casse ni des espaces de début/fin. Le lien peut rejoindre n’importe quel folio ; il ne dépend pas de la proximité des pages. Une flèche dupliquée reçoit un nom vide pour éviter une connexion involontaire. Le déplacement et la réorganisation des folios recalculent les adresses sans changer les noms.

## Chaîne et étoile

**Chaîne** trie les points par ordre explicite, puis par ordre des folios, position Y et position X. Les points sont associés par paires 1–2, 3–4, etc. Pour stabiliser une association dans un groupe comportant plusieurs paires, donner des ordres explicites aux points. Un dernier point isolé reste non lié.

**Étoile** relie une source à toutes les autres flèches portant ce nom. Cocher **Point source de l’étoile** sur la source souhaitée ; les autres points du groupe perdent cette coche. Sans source explicite, le premier point du tri est utilisé. La source affiche toutes les destinations ; chacune affiche la source. Les listes longues se répartissent sur plusieurs lignes. Le mode choisi est commun à tous les points de même nom.

**Projet → Navigateur des renvois…** affiche et filtre les noms, types, positions, ordres et destinations. Double-cliquer une ligne rejoint son symbole. Une flèche ayant plusieurs destinations ouvre ce navigateur au double-clic. **Vérifier** signale les renvois isolés ou incompatibles ; leur libellé est `Non lié`.

## Modèle et limites

Les flèches font partie du graphe de connexions : le MCP `connection_trace` peut suivre un fil d’un folio à l’autre. Il ne traverse pas l’intérieur d’un appareil et ne simule pas les contacts, tensions ou courants. Les renvois sont exclus de la BOM, y compris lorsque les symboles auxiliaires sont inclus.

Le lien porte sur les bornes des flèches ; il ne synchronise pas automatiquement le numéro ou la couleur des différents segments de conducteur. Les références croisées entre une bobine et ses contacts constituent une fonction distincte, encore absente. La navigation est disponible dans l’éditeur ; le PDF affiche les références sous forme de texte.

Les projets antérieurs restent lisibles avec un cadre 20 × 10 par défaut. L’enregistrement, l’annulation et les modifications MCP utilisent le même modèle que l’interface.
