# GeMMeElec

- Dépôt de travail : C:\dev\QElectroTech_GeMMe, branche GeMMeElec.
- Application entièrement nouvelle, indépendante des sources QElectroTech.
- main et EPLAN_DESIGN restent conservées. Ne pas modifier upstream.
- Cible : schémas industriels, Belgique et Europe, interface en français.
- C# / WPF / .NET 10. Modèle partagé entre interface, MCP et exports.
- Les copies de symboles appartiennent au projet (`CustomSymbols`) ; ne pas muter la bibliothèque intégrée lors des imports ou de l’annulation.
- Imports : QET partiel avec rapport de pertes ; DXF graphique sans déduction de connectivité. Ne pas annoncer de conversion EPLAN native .elk/.zw1/.epj tant qu’elle n’est pas implémentée et validée.
- Compilation et dépendances locales dans build/ ; ne pas les suivre dans Git.
- Livrable : build/GeMMeElec.exe, remplacé à chaque publication.
- Catalogue : distinguer symboles fonctionnels, gammes et références commandables.
  Ne pas inventer de caractéristiques fabricant ni annoncer une exhaustivité non vérifiée.
- Tester sauvegarde, connexions, annulation et MCP après modification du modèle.
- Éviter le contrôle souris/clavier du PC sauf nécessité réelle. Préférer les tests hors écran et `Test-Mcp.ps1 -Headless` ; préserver toute fenêtre et tout projet utilisateur ouverts.
- À chaque évolution, vérifier la fluidité : éviter de reconstruire le schéma au survol, borner les caches et mesurer les traitements coûteux sur un jeu de données représentatif.
