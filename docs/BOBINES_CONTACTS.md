# Références croisées entre bobines et contacts

Livraison 0.13, 13 octobre 2026. Il s’agit d’associer les représentations d’un même appareil ; cette association n’ajoute aucune liaison électrique entre les circuits.

## Parcours

1. Placer une **Bobine de relais / contacteur** depuis la bibliothèque, puis renseigner son repère.
2. Placer des **Contacts normalement ouverts / fermés**, un inverseur, un contact temporisé ou une fonction de puissance du contacteur, sur les folios voulus.
3. Sélectionner un contact. Dans **Propriétés → Bobine et contacts**, choisir la bobine par son repère et son adresse de folio/zone. La liaison et le repère partagé s’appliquent immédiatement.
4. Le contact affiche l’adresse de la bobine. La bobine affiche les fonctions, libellés de bornes et adresses de ses contacts. Ces textes se recalculent après déplacement, rotation, changement de numéro et réorganisation des folios.
5. Utiliser les boutons de destination dans l’inspecteur, le double-clic sur la référence calculée, ou **Projet → Bobines et contacts…**. Dans le ruban : onglet **Projet → Bobines / contacts**. Le navigateur se filtre par repère, fonction ou emplacement ; double-cliquer une ligne pour la localiser.
6. **Ctrl+S** enregistre le projet. L’association et les placements se retrouvent à la réouverture ; **Ctrl+Z / Ctrl+Y** annule ou rétablit les changements.

Choisir **Aucune bobine** détache le contact en conservant son repère, ses bornes et ses conducteurs. Renommer le repère depuis la bobine ou un contact associé renomme toutes les fonctions du même appareil. Les libellés de bornes restent propres à chaque fonction ; leurs IDs de connexion ne changent pas.

## Symboles et textes

Fonctions génériques reconnues : bobine simple, bobines temporisées ON/OFF, bobine bistable ; contacts NO, NF, temporisé, inverseur et contacteurs de puissance génériques. Les bobines de solénoïdes et les boutons-poussoirs ne sont pas associés automatiquement aux contacts de relais.

Une copie du symbole d’une fonction conserve son rôle lors du remplacement du composant. Pour un ancien symbole personnel ou importé, ouvrir **Définition du symbole → Fonction déclarée** et choisir explicitement Bobine, NO, NF, Inverseur ou Puissance. Il faut au moins deux bornes définies. Cette déclaration ne fournit aucune caractéristique ni certification fabricant ; les libellés et le bornage doivent venir de la définition vérifiée ou d’une saisie explicite.

Dans **Position des textes**, choisir **Références bobine / contacts** pour déplacer ou régler taille, couleur, rotation et visibilité. Le contenu reste calculé en lecture seule. L’ajustement intelligent individuel ou global inclut ce texte. Les références sont incluses dans la scène vectorielle commune et les exports PDF/SVG ; le thème de l’interface ne les recolore pas.

## Édition et vérifications

- Supprimer une bobine ou son folio détache les contacts restants, sans supprimer leurs fils. L’annulation restaure les associations.
- Dupliquer ensemble une bobine et ses contacts crée une nouvelle bobine et rattache les copies à celle-ci. Dupliquer un contact seul conserve son association ; vérifier ses libellés de bornes.
- **Vérifier** signale les fonctions sans bobine et les répétitions d’un même type de contact avec les mêmes libellés de bornes. Le logiciel ne renumérote pas les bornes et ne déduit pas les contacts disponibles chez un fabricant.
- Le repère partagé d’un appareil n’est plus signalé comme un doublon. Deux appareils distincts portant le même repère restent signalés.
- Par défaut, la nomenclature compte l’article porté par la bobine une seule fois. Ses emplacements comprennent les fonctions associées ; le filtre par folio retrouve aussi l’article depuis un folio contenant seulement ses contacts. **Inclure les symboles auxiliaires** permet de consulter séparément les représentations.

## Format et MCP

Le format `.gemelec` passe à la **version 2** : `coilId`, `relayFunction`, `coilReferencePlacement` dans les composants. Les fichiers version 1 sont migrés en mémoire, avec associations vides ; aucun lien n’est inventé en comparant les repères. Le fichier source n’est pas réécrit à l’ouverture. Le prochain enregistrement explicite produit la version 2, avec JSON compact. Les versions antérieures de l’application doivent rejeter ce format plutôt que perdre silencieusement les associations.

`coil_contact_assign` reçoit `contactId` et `coilId` (vide pour détacher). `coil_contacts_list` retourne les fonctions du projet avec rôle, bornes, association et adresses. `component_update` accepte `relayFunction` (`coil`, `no`, `nc`, `changeover`, `power`, ou vide) pour une déclaration explicite. `component_text_list` expose la clé `coilReferences` : contenu calculé, style éditable avec `component_text_update`. Les changements passent par l’historique commun de l’interface et du MCP.

Cette étape ne simule pas l’état des contacts, ne calcule pas la capacité d’un appareil et ne garantit pas un placement automatique de toutes les références sans conflit. Les propriétés et l’ajustement des textes permettent de corriger les positions.
