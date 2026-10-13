using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public sealed class WireNumberPlacement
{
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double FontSize { get; set; }
    public string Color { get; set; } = "";
    public double? Rotation { get; set; }
    public bool Visible { get; set; } = true;
    public void Validate()
    {
        if (!double.IsFinite(OffsetX) || !double.IsFinite(OffsetY) || Math.Abs(OffsetX) > 10000 || Math.Abs(OffsetY) > 10000 || !double.IsFinite(FontSize) || FontSize != 0 && (FontSize < 4 || FontSize > 200) || Rotation is double rotation && (!double.IsFinite(rotation) || Math.Abs(rotation) > 360) || Color is null || Color.Length > 0 && !EditorPreferences.IsColor(Color)) throw new ArgumentException("Numéro du fil : taille 4–200, décalages ±10 000, rotation ±360 et couleur #RRGGBB.");
    }
}

public sealed partial class MainWindow
{
    private void WireLabelInspector(Wire wire)
    {
        PropertyGroup("Position du numéro", () => {
            PropertyContents.Children.Add(Hint("Maj + glisser directement sur le numéro du fil. Rotation vide : suivre le trajet ; taille 0 : automatique."));
            var placement = wire.NumberPlacement; var visible = new CheckBox { Content = "Afficher le numéro", IsChecked = placement.Visible, Name = "WireNumberVisible", Margin = new(0, 4, 0, 4) }; PropertyContents.Children.Add(visible); RegisterPropertyControl(visible);
            foreach (var (key, label, value) in new[] { ("wireNumberX", "Décalage horizontal", Number(placement.OffsetX)), ("wireNumberY", "Décalage vertical", Number(placement.OffsetY)), ("wireNumberSize", "Taille du numéro", Number(placement.FontSize)), ("wireNumberRotation", "Rotation · vide = automatique", placement.Rotation is double angle ? Number(angle) : ""), ("wireNumberColor", "Couleur · vide = couleur du fil", placement.Color) })
            {
                var input = Field(key, label, value); LiveText(input, text => Editor.Change(() => { var live = Editor.Sheet.Wires.Single(w => w.Id == wire.Id).NumberPlacement; switch (key) { case "wireNumberX": live.OffsetX = ReadNumber(text); break; case "wireNumberY": live.OffsetY = ReadNumber(text); break; case "wireNumberSize": live.FontSize = ReadNumber(text); break; case "wireNumberRotation": live.Rotation = string.IsNullOrWhiteSpace(text) ? null : ReadNumber(text); break; case "wireNumberColor": live.Color = text; break; } }, wire.Id + ":" + key));
            }
            void VisibilityChanged() => LiveProperty(visible, () => Editor.Change(() => Editor.Sheet.Wires.Single(w => w.Id == wire.Id).NumberPlacement.Visible = visible.IsChecked == true));
            visible.Checked += (_, _) => VisibilityChanged(); visible.Unchecked += (_, _) => VisibilityChanged();
            PropertyContents.Children.Add(Button("Rétablir la position automatique", () => Editor.Change(() => Editor.Sheet.Wires.Single(w => w.Id == wire.Id).NumberPlacement = new())));
            PropertyContents.Children.Add(Button("Surligner / parcourir le circuit", CircuitDialog));
        }, false);
    }
}
