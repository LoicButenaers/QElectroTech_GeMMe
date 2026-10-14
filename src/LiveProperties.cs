using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    private bool editingProperty, updatingPropertyControls;
    private readonly Dictionary<TextBox,(Func<bool> Commit,Action Cancel,Action Sync)> dimensionInputs=[];
    internal bool CommitDimensionInputs(){foreach(var input in dimensionInputs.Values.ToList())if(!input.Commit())return false;return true;}
    private void ClearDimensionInputs(){foreach(var input in dimensionInputs.Values)input.Cancel();dimensionInputs.Clear();}
    private void DimensionText(TextBox box,Action<string> change,Func<string> currentValue)
    {
        bool dirty=false,active=true;
        bool Commit()
        {
            if(!active||!dirty)return true;bool success=false;LiveProperty(box,()=>{change(box.Text);success=true;});if(success){dirty=false;updatingPropertyControls=true;try{foreach(var input in dimensionInputs.Values.ToList())input.Sync();}finally{updatingPropertyControls=false;}}Editor.EndPropertyEdit();return success;
        }
        dimensionInputs[box]=(Commit,()=>{active=false;dirty=false;},()=>{if(active&&!dirty)box.Text=currentValue();});
        box.TextChanged+=(_,_)=>{if(!active||updatingPropertyControls)return;dirty=true;SetPropertyFeedback(box,null);status.Text="Dimension saisie · Entrée ou quitter le champ pour appliquer";};
        box.LostKeyboardFocus+=(_,_)=>Commit();box.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter){Commit();e.Handled=true;}else if(e.Key==Key.Escape){updatingPropertyControls=true;try{box.Text=currentValue();dirty=false;SetPropertyFeedback(box,null);}finally{updatingPropertyControls=false;}e.Handled=true;}};
    }
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
            if (key is "X" or "Y") continue; // PagePositionFields handles both numeric units and percentages.
            LiveText(box, value => Editor.SetComponentField(id,key,value));
        }
    }
}
