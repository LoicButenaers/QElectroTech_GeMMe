# Vérification de la livraison 0.19

Les améliorations 19, 21, 24, 25, 26, 27, 28 et les deux gestes de la vidéo avec section mm² sont réalisés. [Guide des commandes](LIVRAISON_0.19.md).

- Compilation et publication Windows x64 : aucune erreur, aucun avertissement.
- **150 tests applicatifs** passent sur l’exécutable publié, dont continuités distinctes, scissions dans quatre directions, annulation/rétablissement, refus atomiques, vues graphiques paginées, copies de circuits, favoris, lecture et onglets indépendants.
- **135 contrôles MCP** passent sur le même exécutable, avec **87 outils**. Ils incluent la migration, l’enregistrement et la relecture d’une copie du projet utilisateur, et la vérification de l’original.
- Rendus hors écran des nouveaux gestionnaires, onglets, bornier et ligne de câble inspectés. Les documents PDF et PNG d’aperçu sont produits par le moteur de dessin du logiciel.
- Format projet 8 ; migration des versions 1 à 7. Les versions antérieures de l’application ne lisent pas le format 8.

| Composants | Première saisie | Saisies répétées, médiane | Annuler | Rétablir | PDF |
| --- | --- | --- | --- | --- | --- |
| 5 000 | 41 ms | 10 ms | 39 ms | 22 ms | 2.10 s |
| 10 000 | 65 ms | 21 ms | 51 ms | 37 ms | 4.98 s |

Les charges contiennent 100/200 folios et 4 900/9 800 fils. Les mesures proviennent de deux processus isolés sur ce poste. Elles ne mesurent pas une cadence GPU ni une latence de souris. Les mutations globales et les exports peuvent prendre davantage de temps que la saisie ciblée.

Routage : 104 composants et 4 fils, 18 ms sur le cas de charge testé. Ce résultat ne garantit pas ce délai pour toutes les dispositions ; la recherche reste bornée et signale ses échecs.

SHA256 de l’exécutable vérifié : `C4FF0954EA4274BC36711F64C8920A320CB0058E88A9D00CF56E98D99EF960A2`.

SHA256 du projet utilisateur conservé : `E94D84AB7148318D8F627371FA7734913E0157151C4EC4886A26F5DA2088C89C`.

Le paquet préparé est `build/update-0.19/GeMMeElec.exe`. La copie vers `build/GeMMeElec.exe` doit utiliser ce même binaire après fermeture de l’application par l’utilisateur si Windows le verrouille.

Reproduire : lancer l’exécutable préparé avec `--self-test`, puis exécuter `Test-Mcp.ps1 -Headless -ExecutablePath <chemin absolu> -RoundTripProjectPath <projet utilisateur>` ; lancer séparément `--benchmark --folios=100` et `--benchmark --folios=200`, puis `tools/release_019_report.py`. Les essais utilisent leurs fichiers et sessions de test séparés de l’application de travail.
