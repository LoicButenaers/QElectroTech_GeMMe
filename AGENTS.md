# Règles du projet GeMMe

- Travailler uniquement dans ce dépôt pour le logiciel : `C:\dev\QElectroTech_GeMMe`.
- Pour le chantier demandé le 9 octobre 2026, travailler sur `EPLAN_DESIGN`.
  Cette demande explicite remplace la consigne précédente de travail sur `main`.
  Ne pas créer d'autre branche ni de worktree sans nouvelle demande.
- Cible : schémas industriels IEC pour la Belgique et l'Europe. Les fabricants
  sont choisis par famille de composants. Suivre le cadrage dans
  `docs/EPLAN_DESIGN.md` et l'étude approfondie `docs/EPLAN_P8_RESEARCH.md`.
  Le registre `docs/EPLAN_P8_REQUIREMENTS.json` porte les exigences et recettes.
  Distinguer les fonctions existantes, les maquettes et les objectifs. La cible
  complète P8 est conservée ; les lots indiquent l'ordre de réception.
- Ne créer que les dossiers nécessaires. Réutiliser les dossiers existants.
- Conserver une seule version de l'exécutable QElectroTech. Utiliser
  `build/qelectrotech.exe` pour les compilations Windows Release et remplacer
  ce fichier à chaque compilation ; ne pas créer de copies versionnées.
- Placer les fichiers de compilation et les DLL nécessaires dans `build/`.
  Ne pas ajouter les binaires générés ou une chaîne de compilation au suivi Git.
- Conserver les licences et les mentions d'origine de QElectroTech et de ses
  dépendances. Le dépôt `upstream` sert de référence ; `origin` est le dépôt GeMMe.
- Vérifier les modifications avec les contrôles adaptés et signaler clairement
  lorsqu'une compilation ou un lancement n'a pas pu être effectué.
