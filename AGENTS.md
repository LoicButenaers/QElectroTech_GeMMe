# GeMMeElec

- Dépôt de travail : C:\dev\QElectroTech_GeMMe, branche GeMMeElec.
- Application entièrement nouvelle, indépendante des sources QElectroTech.
- main et EPLAN_DESIGN restent conservées. Ne pas modifier upstream.
- Cible : schémas industriels, Belgique et Europe, interface en français.
- C# / WPF / .NET 10. Modèle partagé entre interface, MCP et exports.
- Compilation et dépendances locales dans build/ ; ne pas les suivre dans Git.
- Livrable : build/GeMMeElec.exe, remplacé à chaque publication.
- Catalogue : distinguer symboles fonctionnels, gammes et références commandables.
  Ne pas inventer de caractéristiques fabricant ni annoncer une exhaustivité non vérifiée.
- Tester sauvegarde, connexions, annulation et MCP après modification du modèle.
