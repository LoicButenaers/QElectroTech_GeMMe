"""Collect the verified 0.19 executable's tests and isolated benchmark reports."""
from pathlib import Path
import hashlib
import json
import shutil
import statistics

root = Path(__file__).resolve().parents[1]
stage = root / "build/update-0.19"

def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

app = read(stage / "self-test.json")
mcp = read(root / "build/mcp-test.json")
benchmarks = [read(stage / f"benchmark-{n}.json") for n in (100, 200)]
assert app["passed"] and app["failures"] == 0 and len(app["results"]) == 150
assert mcp["passed"] and len(mcp["results"]) == 135
assert all(item["passed"] and item["version"] == "0.19" for item in benchmarks)
executable_hash = hashlib.sha256((stage / "GeMMeElec.exe").read_bytes()).hexdigest().upper()
user = Path(r"C:\Users\pravi\Documents\LaserSieve_GH.gemelec")
original_hash = hashlib.sha256(user.read_bytes()).hexdigest().upper()
assert original_hash == "E94D84AB7148318D8F627371FA7734913E0157151C4EC4886A26F5DA2088C89C"
report = {
    "version": "0.19", "projectFormat": 8, "applicationTests": len(app["results"]),
    "mcpChecksIncludingUserCopy": len(mcp["results"]), "mcpTools": 87,
    "executableSha256": executable_hash, "originalUserDocumentSha256": original_hash,
    "benchmarks": benchmarks, "routing": read(stage / "self-test-routing-measure.json"),
    "renderer": "WPF hors écran, logiciel; mesures locales en processus séparés",
}
(root / "docs/MESURES_0.19.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
rows = []
for item in benchmarks:
    sample = item["samples"][0]
    rows.append(f'| {sample["components"]:,} | {sample["propertyEditMs"][0]:.0f} ms | '
                f'{statistics.median(sample["propertyEditMs"][1:]):.0f} ms | '
                f'{sample["undoMs"]:.0f} ms | {sample["redoMs"]:.0f} ms | '
                f'{sample["pdfMs"] / 1000:.2f} s |'.replace(",", " "))
summary = f"""# Vérification de la livraison 0.19

Les améliorations 19, 21, 24, 25, 26, 27, 28 et les deux gestes de la vidéo avec section mm² sont réalisés. [Guide des commandes](LIVRAISON_0.19.md).

- Compilation et publication Windows x64 : aucune erreur, aucun avertissement.
- **150 tests applicatifs** passent sur l’exécutable publié, dont continuités distinctes, scissions dans quatre directions, annulation/rétablissement, refus atomiques, vues graphiques paginées, copies de circuits, favoris, lecture et onglets indépendants.
- **135 contrôles MCP** passent sur le même exécutable, avec **87 outils**. Ils incluent la migration, l’enregistrement et la relecture d’une copie du projet utilisateur, et la vérification de l’original.
- Rendus hors écran des nouveaux gestionnaires, onglets, bornier et ligne de câble inspectés. Les documents PDF et PNG d’aperçu sont produits par le moteur de dessin du logiciel.
- Format projet 8 ; migration des versions 1 à 7. Les versions antérieures de l’application ne lisent pas le format 8.

| Composants | Première saisie | Saisies répétées, médiane | Annuler | Rétablir | PDF |
| --- | --- | --- | --- | --- | --- |
{chr(10).join(rows)}

Les charges contiennent 100/200 folios et 4 900/9 800 fils. Les mesures proviennent de deux processus isolés sur ce poste. Elles ne mesurent pas une cadence GPU ni une latence de souris. Les mutations globales et les exports peuvent prendre davantage de temps que la saisie ciblée.

Routage : {report['routing']['components']} composants et {report['routing']['wires']} fils, {report['routing']['milliseconds']} ms sur le cas de charge testé. Ce résultat ne garantit pas ce délai pour toutes les dispositions ; la recherche reste bornée et signale ses échecs.

SHA256 de l’exécutable vérifié : `{executable_hash}`.

SHA256 du projet utilisateur conservé : `{original_hash}`.

Le paquet préparé est `build/update-0.19/GeMMeElec.exe`. La copie vers `build/GeMMeElec.exe` doit utiliser ce même binaire après fermeture de l’application par l’utilisateur si Windows le verrouille.

Reproduire : lancer l’exécutable préparé avec `--self-test`, puis exécuter `Test-Mcp.ps1 -Headless -ExecutablePath <chemin absolu> -RoundTripProjectPath <projet utilisateur>` ; lancer séparément `--benchmark --folios=100` et `--benchmark --folios=200`, puis `tools/release_019_report.py`. Les essais utilisent leurs fichiers et sessions de test séparés de l’application de travail.
"""
(root / "docs/VERIFICATION_0.19.md").write_text(summary, encoding="utf-8")
for source, destination in (
    ("self-test-video.png", "output/images/GeMMeElec_0.19_bornes_cable.png"),
    ("self-test-terminal-graphic.png", "output/images/GeMMeElec_0.19_bornier_graphique.png"),
    ("self-test-video.pdf", "output/pdf/GeMMeElec_0.19_bornes_cable.pdf"),
):
    target = root / destination
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(stage / source, target)
print(json.dumps({"tests": len(app["results"]), "mcp": len(mcp["results"]), "sha256": executable_hash}))
