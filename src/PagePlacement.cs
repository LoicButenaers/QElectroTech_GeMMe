using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public static class PagePlacement
{
    public static bool Editable(Sheet sheet) => sheet.BomPage is null && sheet.TerminalPlanId.Length == 0 && sheet.CabinetPlanId.Length == 0;
    public static double Extent(Sheet sheet, string key) => key is "X" or "Width" ? sheet.Width : sheet.Height;
    public static double Display(Sheet sheet, string key, double value, bool percent) => percent ? value * 100 / Extent(sheet, key) : value;
    public static double Units(Sheet sheet, string key, double value, bool percent)
    {
        if (key is not ("X" or "Y" or "Width" or "Height") || !double.IsFinite(value)) throw new ArgumentException("Coordonnée invalide.");
        double units = percent ? value * Extent(sheet, key) / 100 : value;
        if (!double.IsFinite(units) || key is "X" or "Y" && (units < 0 || units > Extent(sheet, key))) throw new ArgumentException(percent ? "La position doit être comprise entre 0 et 100 %." : "La position doit rester dans la page.");
        return units;
    }
    public static double Coordinate(Sheet sheet, string id, string key)
    {
        if (sheet.Components.FirstOrDefault(c => c.Id == id) is Component c) return key == "X" ? c.X : c.Y;
        if (sheet.Notes.FirstOrDefault(n => n.Id == id) is Annotation n) return key == "X" ? n.X : n.Y;
        if (sheet.Graphics.FirstOrDefault(g => g.Id == id) is SheetGraphic g) return key switch { "X" => g.X, "Y" => g.Y, "Width" => g.Width, _ => g.Height };
        throw new ArgumentException("Élément introuvable.");
    }
    public static Rect Bounds(Project project, Sheet sheet, IEnumerable<string> selection)
    {
        var ids = selection.ToHashSet(); Rect result = Rect.Empty;
        foreach (var c in sheet.Components.Where(c => ids.Contains(c.Id) && !Junctions.Automatic(c))) result.Union(c.Bounds());
        foreach (var n in sheet.Notes.Where(n => ids.Contains(n.Id))) result.Union(FreeGraphics.NoteBounds(project, sheet, n));
        foreach (var g in sheet.Graphics.Where(g => ids.Contains(g.Id))) result.Union(g.Bounds());
        foreach (var w in sheet.Wires.Where(w => ids.Contains(w.From.ComponentId) && ids.Contains(w.To.ComponentId))) foreach (var point in w.Route(sheet)) result.Union(point);
        return result;
    }
}

public sealed partial class Editor
{
    public void SetElementCoordinate(string id, string key, double value, bool percent = false)
    {
        if (!PagePlacement.Editable(Sheet)) throw new ArgumentException("Ce folio est généré ; utiliser son gestionnaire.");
        double units = PagePlacement.Units(Sheet, key, value, percent);
        if (Sheet.Components.Any(c => c.Id == id))
        {
            if (key is not ("X" or "Y")) throw new ArgumentException("Seule la position du symbole peut être modifiée ici.");
            SetComponentField(id, key, units.ToString("R", CultureInfo.InvariantCulture)); return;
        }
        Change(() =>
        {
            if (Sheet.Graphics.FirstOrDefault(g => g.Id == id) is SheetGraphic g) {if(key is "Width" or "Height")FreeGraphics.SetSize(g,key=="Width"?units:g.Width,key=="Height"?units:g.Height);else typeof(SheetGraphic).GetProperty(key)!.SetValue(g, units);}
            else if (Sheet.Notes.FirstOrDefault(n => n.Id == id) is Annotation n && key is "X" or "Y") { if (key == "X") n.X = units; else n.Y = units; }
            else throw new ArgumentException("Élément ou coordonnée introuvable.");
        }, id + ":" + key);
    }
    public void AlignToPage(IEnumerable<string> selection, string operation)
    {
        if (!PagePlacement.Editable(Sheet)) throw new ArgumentException("Ce folio est généré ; utiliser son gestionnaire.");
        var ids = selection.ToHashSet(); var bounds = PagePlacement.Bounds(Project, Sheet, ids);
        if (bounds.IsEmpty) throw new ArgumentException("Sélectionner au moins un symbole, texte ou dessin libre.");
        if (bounds.Width > Sheet.Width || bounds.Height > Sheet.Height) throw new ArgumentException("La sélection est plus grande que la page.");
        var delta = operation switch
        {
            "left" => new Vector(-bounds.Left, 0), "right" => new Vector(Sheet.Width - bounds.Right, 0),
            "top" => new Vector(0, -bounds.Top), "bottom" => new Vector(0, Sheet.Height - bounds.Bottom),
            "centerX" => new Vector((Sheet.Width - bounds.Width) / 2 - bounds.Left, 0),
            "centerY" => new Vector(0, (Sheet.Height - bounds.Height) / 2 - bounds.Top),
            "center" => new Vector((Sheet.Width - bounds.Width) / 2 - bounds.Left, (Sheet.Height - bounds.Height) / 2 - bounds.Top),
            _ => throw new ArgumentException("Alignement inconnu.")
        };
        Change(() =>
        {
            foreach (var c in Sheet.Components.Where(c => ids.Contains(c.Id) && !Junctions.Automatic(c))) { c.X += delta.X; c.Y += delta.Y; }
            foreach (var n in Sheet.Notes.Where(n => ids.Contains(n.Id))) { n.X += delta.X; n.Y += delta.Y; }
            foreach (var g in Sheet.Graphics.Where(g => ids.Contains(g.Id))) { g.X += delta.X; g.Y += delta.Y; }
            foreach (var w in Sheet.Wires.Where(w => ids.Contains(w.From.ComponentId) && ids.Contains(w.To.ComponentId))) w.Waypoints = w.Waypoints.Select(p => new Position(p.X + delta.X, p.Y + delta.Y)).ToList();
        });
    }
}

public sealed partial class MainWindow
{
    private bool percentCoordinates;
    private void PagePositionFields(string id, string prefix, bool dimensions = false)
    {
        var mode = PropertyChoice("Saisie des coordonnées", percentCoordinates ? "percent" : "units", [("units", "Valeurs numériques · unités de dessin"), ("percent", "Pourcentage de la page · %")]); mode.Name = "PageCoordinateMode";
        PropertyContents.Children.Add(Hint("Origine : coin supérieur gauche de la page. X = largeur, Y = hauteur.\nLe symbole utilise son centre ; le texte son ancrage ; la forme son premier coin. Les valeurs en % sont converties en coordonnées."));
        var inputs = new Dictionary<string, TextBox>();
        foreach (string key in dimensions ? new[] { "X", "Y", "Width", "Height" } : new[] { "X", "Y" })
        {
            string label = key switch { "Width" => "Largeur", "Height" => "Hauteur", _ => "Position " + key };
            var box = Field(prefix + key, label, Number(PagePlacement.Display(Editor.Sheet, key, PagePlacement.Coordinate(Editor.Sheet, id, key), percentCoordinates)), "Saisir une valeur dans l’unité choisie ci-dessus. Virgule ou point accepté.");
            inputs[key] = box; LiveText(box, text => Editor.SetElementCoordinate(id, key, ReadNumber(text), percentCoordinates));
        }
        mode.SelectionChanged += (_, _) =>
        {
            Editor.EndPropertyEdit(); percentCoordinates = (string?)mode.SelectedValue == "percent";
            updatingPropertyControls = true;
            try { foreach (var pair in inputs) { pair.Value.Text = Number(PagePlacement.Display(Editor.Sheet, pair.Key, PagePlacement.Coordinate(Editor.Sheet, id, pair.Key), percentCoordinates)); SetPropertyFeedback(pair.Value, null); } }
            finally { updatingPropertyControls = false; }
        };
        PageAlignmentButtons();
    }
    private void PageAlignmentButtons()
    {
        PropertyContents.Children.Add(Hint("Aligner la sélection sur la page entière"));
        var buttons = new WrapPanel(); PropertyContents.Children.Add(buttons);
        foreach (var (operation, label) in new[] { ("left", "Gauche"), ("centerX", "Centre X"), ("right", "Droite"), ("top", "Haut"), ("centerY", "Centre Y"), ("bottom", "Bas"), ("center", "Centre de la page") })
        {
            var button = Button(label, () => { Canvas.CancelDrag(); Editor.AlignToPage(Canvas.Selected, operation); }); button.Tag = "pageAlign:" + operation; button.Margin = new(2); buttons.Children.Add(button);
        }
    }
}
