using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    private bool editingProperty, updatingPropertyControls;
    internal TextBox PropertyField(string key) => fields[key];
    internal FrameworkElement PropertyPanel => properties;
    private void LiveProperty(Control control, Action change)
    {
        if (updatingPropertyControls) return;
        editingProperty = true;
        try { change(); SetPropertyFeedback(control, null); status.Text = "Propriété mise à jour · Ctrl+Z pour annuler · Ctrl+S pour enregistrer"; }
        catch (Exception ex) { SetPropertyFeedback(control, ex.Message); status.Text = ex.Message; }
        finally { editingProperty = false; }
    }
    private void LiveText(TextBox box, Action<string> change)
    {
        box.TextChanged += (_, _) => LiveProperty(box, () => { if (box.Text.Length > 2000) throw new ArgumentException("Texte limité à 2000 caractères."); change(box.Text); });
        box.LostKeyboardFocus += (_, _) => Editor.EndPropertyEdit();
    }
    private void BindComponentProperties(string id)
    {
        foreach (var (key, box) in fields)
        {
            LiveText(box, value => Editor.Change(() => {
                var c = Editor.Sheet.Components.Single(c => c.Id == id);
                if (key.StartsWith("port:")) c.PortLabels[key[5..]] = value;
                else switch (key) {
                    case "tag": c.Tag = value; break; case "description": c.Description = value; break; case "brand": c.Brand = value; break; case "range": c.Range = value; break; case "reference": c.Reference = value; break; case "rating": c.Rating = value; break;
                    case "X": c.X = ReadCoordinate(value, "X"); break; case "Y": c.Y = ReadCoordinate(value, "Y"); break;
                    case "rotation": if (!int.TryParse(value, out var angle) || angle % 90 != 0 || angle < -360 || angle > 360) throw new ArgumentException("Saisir un multiple de 90° entre −360 et 360."); c.Rotation = angle; break;
                }
            }, id + ":" + key));
        }
    }
}
