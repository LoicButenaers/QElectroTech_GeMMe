# Bibliothèques documentées — version 0.8

Le catalogue a été reconstruit le 12 octobre 2026. Les 75 associations génériques famille / fabricant / gamme ne sont plus proposées. Elles restent dans l’historique Git. Les composants d’anciens projets gardent leurs symboles et propriétés ; aucun ancien ID n’est réaffecté à un autre appareil.

## GeMMe fabricant · dessins officiels

| Fabricant | Référence exacte | Dessin et bornes | Source conservée |
|---|---|---|---|
| Schneider Electric | LC1D09BD | 3 NO principaux + auxiliaires NO 13–14 et NC 21–22 + bobine A1–A2, 12 bornes | [Fiche officielle, page 6](https://iportal.se.com/Contents/docs/SQD-LC1D09BD.PDF) |
| ABB | 1SBL137001R1301 / AF09-30-01-13 | 3 NO principaux + auxiliaire NC 21–22 + bobine A1–A2, 10 bornes | [1SBC101402D0201 du 24/03/2011, pages 1 et 8](https://library.e.abb.com/public/084359a07eda807bc12578610035133d/1SBC101402D0201.pdf) |
| Eaton | 276705 / DILM9-10(24VDC) | 3 NO principaux + auxiliaire NO 13–14 + bobine A1–A2, 10 bornes | [CA034001EN décembre 2017, pages 1/24–1/25](https://ecat.eaton.com/flip-cat/MOTCONT1_EN/blaetterkatalog/pdf/complete.pdf), [fiche produit](https://www.eaton.com/gb/en-gb/skuPage.276705.html) |

Les dessins sont des images extraites de ces sources, sans reconstruction manuelle des traits. Pour Schneider, le PNG correspond à l’image Im6 de la page 6, à sa résolution native de 159 × 95 pixels ; cette source est peu définie. ABB et Eaton sont des rendus PNG sans perte de zones des PDF à 600 dpi. Ces images ne sont donc pas des symboles vectoriels natifs. Les points de connexion ont été relevés manuellement sur les extrémités visibles, puis testés en déplacement, rotation, sauvegarde et export. Ils ne constituent pas une extraction de la logique d’un fichier EPLAN.

Les documents sont historiques : aucune disponibilité commerciale actuelle, certification électrique, dernière révision universelle ni exhaustivité n’est annoncée. Le dessin Eaton est celui des contacts ; sa varistance interne mentionnée au catalogue n’est pas figurée et n’a pas été ajoutée.

## EPLAN · aperçus officiels

Quatre illustrations précédemment présentées sont conservées **octet pour octet**, dans leurs couleurs publiées :

- QLTR2_1 // 1590 et XTR3_3 // 1701, IEC_symbol, variante A, [EPLAN Platform 2026](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2026/Content/htm/news_p_stammdaten_start_2026.htm).
- PVG // 1689 et LSW1C // 1691, IEC_symbol, variante A, [EPLAN Platform 2025](https://www.eplan.help/en-us/Infoportal/Content/Plattform/2025/Content/htm/news_p_stammdaten_start_2025.htm).

**Consultation uniquement.** Ces illustrations ne contiennent pas les propriétés électriques, les variantes complètes ou la bibliothèque native. EPLAN précise lui-même que des graphiques identiques peuvent correspondre à des fonctions différentes. Le placement et le raccordement sont donc refusés aussi bien dans l’interface que via MCP. Pour intégrer des symboles EPLAN raccordables à l’identique, il manque leurs fichiers natifs ou des exports électriques documentés et une conversion validée ; un DXF graphique seul ne suffit pas.

## Utilisation

Dans le panneau Bibliothèque, choisir « GeMMe fabricant · dessins officiels » ou « EPLAN · aperçus officiels ». Un clic sur un fabricant prépare le placement. Un double-clic ou le menu contextuel ouvre le dessin, le bornage, la référence, l’édition, la source et la méthode de contrôle. Le document source peut être enregistré depuis la fiche, sans réseau. « Modifier une copie non officielle » crée un symbole personnel dans le projet ; l’original reste intact.

Les dessins PNG sont conservés sans recompression JPEG dans les exports PDF/SVG et dans les images de nomenclature. Les conducteurs passent au-dessus du fond blanc des dessins pour que les bornes internes au recadrage restent visiblement raccordées. Les copies peuvent déplacer leurs bornes ou ajouter du dessin, mais le contenu d’une image ne devient pas éditable trait par trait.

## Traçabilité et contrôles

`library/catalog.json` est le manifeste : URLs de page et de dessin, édition, date de contrôle, méthode d’extraction, référence exacte, état raccordable, bornes, dimensions et SHA-256. `library/official/` contient les PNG et les PDF sources (extrait de deux pages pour Eaton ; empreinte du PDF complet dans le manifeste).

La vérification SHA-256 porte sur **l’intégrité des fichiers conservés**, pas sur leur certification ni sur l’adéquation à une installation. Les sources ont été consultées visuellement pour vérifier le bon dessin et la bonne variante. L’application contrôle les empreintes au chargement ; aucun réseau n’est nécessaire au fonctionnement. Les décodages d’images restent mis en cache et bornés.

Les illustrations et documents restent attribués à leurs éditeurs respectifs. Leur présence ne signifie pas une approbation de GeMMeElec par EPLAN, Schneider Electric, ABB ou Eaton. Les 92 symboles industriels génériques sont des dessins GeMMeElec séparés.

La couverture actuelle est **3 références fabricants dans une famille et 4 aperçus EPLAN**. Ce n’est pas une bibliothèque équivalente à l’intégralité d’EPLAN ni un catalogue de trois fabricants par famille. Aucune entrée sans dessin vérifiable n’a été ajoutée pour gonfler les nombres.
