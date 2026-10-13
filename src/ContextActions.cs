using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    private MenuItem QuickAction(string label, Action action)
    {
        var item = new MenuItem { Header = label }; item.Click += (_, _) => Run(action); return item;
    }
    internal ContextMenu SchematicMenu()
    {
        var menu = new ContextMenu();
        var components = Editor.Sheet.Components.Where(c => Canvas.Selected.Contains(c.Id)).ToList();
        var wires = Editor.Sheet.Wires.Where(w => Canvas.Selected.Contains(w.Id)).ToList();
        if (Canvas.Selected.Count > 0)
        {
            menu.Items.Add(QuickAction("Propriétés", () => { workspace?.Show("properties"); Inspect(); }));
            menu.Items.Add(QuickAction("Cadrer la sélection",()=>Canvas.ZoomSelection()));
            if(components.Count>0)menu.Items.Add(QuickAction("Créer un bloc de circuit…",SaveCircuitBlock));
            if (components.Count > 0) menu.Items.Add(QuickAction("Rotation 90°    R", Rotate));
            if (components.Count > 0 || Editor.Sheet.Notes.Any(n => Canvas.Selected.Contains(n.Id))) menu.Items.Add(QuickAction("Dupliquer    Ctrl+D", Duplicate));
            if (components.Count == 1 && Canvas.Selected.Count == 1)
            {
                var c = components[0];
                menu.Items.Add(QuickAction("Position des textes…", () => TextPositionDialog(c.Id)));
                if (CrossReferences.IsPoint(c))
                {
                    menu.Items.Add(QuickAction("Renommer le report…", () => { var name = Prompt("Nom du report", "Nom affiché (la liaison est conservée)", ReferenceDisplay.Name(c)); if (name is not null) Editor.RenameReference(c.Id, name); }));
                    menu.Items.Add(QuickAction("Lier à un autre report…", () => LinkReferenceDialog(c.Id)));
                    if (new CrossReferences(Editor.Project).Targets(c.Id).Count > 0) menu.Items.Add(QuickAction("Aller au report lié", () => FollowReference(c.Id)));
                }
                else if (c.Symbol != "junction") menu.Items.Add(QuickAction("Modifier le symbole en copie…", SymbolEditor));
                if(c.Symbol!="junction")menu.Items.Add(QuickAction("Ajouter le symbole aux favoris…",()=>FavoriteSymbol(c.Symbol)));
            }
            if (wires.Count > 0)
            {
                menu.Items.Add(QuickAction("Surligner / parcourir le circuit…", CircuitDialog));menu.Items.Add(QuickAction("Routage intelligent",SmartRouteSelection));if(wires.Count==1)menu.Items.Add(QuickAction("Insérer un symbole sur le fil…",InsertSymbolOnWireDialog));
                menu.Items.Add(QuickAction("Effacer le surlignage", Canvas.ClearCircuitHighlight));
                menu.Items.Add(QuickAction("Parcours automatique", () => Editor.Change(() => wires.ForEach(w => {w.Waypoints.Clear();w.RoutingMode="manual";w.RoutingWarning="";}))));
                menu.Items.Add(QuickAction("Couleur des conducteurs…", () => PaletteDialog(wires[0].Color, color => Editor.Change(() => wires.ForEach(w => w.Color = color)))));
            }
            menu.Items.Add(new Separator()); menu.Items.Add(QuickAction("Supprimer    Suppr", Delete));
        }
        else
        {
            if (Editor.Sheet.BomPage is null)
            {
                menu.Items.Add(QuickAction("Conducteur    W", () => Canvas.SetMode("wire")));
                menu.Items.Add(QuickAction("Texte    T", () => Canvas.SetMode("text")));
                menu.Items.Add(QuickAction("Tout sélectionner", () => Canvas.SelectRectangle(new Rect(0, 0, Editor.Sheet.Width, Editor.Sheet.Height), false)));
            }
            menu.Items.Add(QuickAction("Propriétés du folio…", SheetInfo));
            menu.Items.Add(QuickAction("Ajuster le folio    F", Canvas.Fit));
        }
        return menu;
    }
    private void ShowSchematicMenu()
    {
        var menu = SchematicMenu(); menu.PlacementTarget = Canvas; menu.Placement = PlacementMode.MousePoint; menu.IsOpen = true;
    }
}
