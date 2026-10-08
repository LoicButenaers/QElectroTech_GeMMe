# Règles du projet GeMMe

- Travailler uniquement dans ce dépôt pour le logiciel : `C:\dev\QElectroTech_GeMMe`.
- Toujours travailler sur la branche `main`. Ne pas créer de branche ni de worktree.
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
