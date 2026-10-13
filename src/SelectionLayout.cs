using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public sealed partial class Editor
{
    public void AlignSelection(IEnumerable<string> selected, string operation)
    {
        var ids = selected.ToHashSet(); var objects = Sheet.Components.Where(c => ids.Contains(c.Id) && !Junctions.Automatic(c)).Select(c => (c.Id, X: c.X, Y: c.Y)).Concat(Sheet.Notes.Where(n => ids.Contains(n.Id)).Select(n => (n.Id, X: n.X, Y: n.Y))).ToList();
        if (objects.Count < 2 || operation.StartsWith("distribute") && objects.Count < 3) throw new ArgumentException("Sélectionner au moins deux objets, ou trois pour répartir.");
        if (operation is not ("left" or "right" or "top" or "bottom" or "centerX" or "centerY" or "distributeX" or "distributeY")) throw new ArgumentException("Alignement inconnu.");
        bool horizontal = operation is "left" or "right" or "centerX" or "distributeX"; var order = objects.OrderBy(o => horizontal ? o.X : o.Y).ThenBy(o => o.Id).ToList();
        double min = horizontal ? order[0].X : order[0].Y, max = horizontal ? order[^1].X : order[^1].Y;
        var targets = order.Select((o, i) => (o.Id, Value: operation.StartsWith("distribute") ? min + (max - min) * i / (order.Count - 1) : operation is "left" or "top" ? min : operation is "right" or "bottom" ? max : (min + max) / 2)).ToDictionary(o => o.Id, o => o.Value);
        Change(() => { foreach (var c in Sheet.Components.Where(c => targets.ContainsKey(c.Id))) { if (horizontal) c.X = targets[c.Id]; else c.Y = targets[c.Id]; } foreach (var n in Sheet.Notes.Where(n => targets.ContainsKey(n.Id))) { if (horizontal) n.X = targets[n.Id]; else n.Y = targets[n.Id]; } });
    }
}

public sealed partial class MainWindow
{
    private void AlignmentDialog()
    {
        Canvas.CancelDrag(); var dialog = DocumentWindow("Aligner et répartir la sélection", 560, 430); var panel = new StackPanel { Margin = new(20) }; dialog.Content = panel;
        panel.Children.Add(Hint("Les centres des symboles et les origines des textes sont alignés. Les bornes et les conducteurs suivent ; chaque bouton produit une action annulable."));
        foreach (var (operation, text) in new[] { ("left", "Aligner sur le centre le plus à gauche"), ("right", "Aligner sur le centre le plus à droite"), ("top", "Aligner en haut"), ("bottom", "Aligner en bas"), ("centerX", "Centrer horizontalement"), ("centerY", "Centrer verticalement"), ("distributeX", "Répartir horizontalement"), ("distributeY", "Répartir verticalement") }) panel.Children.Add(DocButton(text, dialog, () => Editor.AlignSelection(Canvas.Selected, operation)));
        ShowDocumentDialog(dialog);
    }
}
