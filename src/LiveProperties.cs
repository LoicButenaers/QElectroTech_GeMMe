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
        try { change(); updateCoilInspector?.Invoke(); SetPropertyFeedback(control, null); status.Text = "Propriété mise à jour · Ctrl+Z pour annuler · Ctrl+S pour enregistrer"; }
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
            LiveText(box, value => Editor.SetComponentField(id,key,value));
        }
    }
}
