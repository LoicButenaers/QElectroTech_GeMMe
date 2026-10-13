using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace GeMMeElec;

public static class ProjectTemplates
{
    public sealed class Palette { public List<PaletteColor> Colors {get;set;}=[]; public string WireColor {get;set;}="#243B53"; public void Validate(){new EditorPreferences{Colors=Colors,WireColor=WireColor}.Validate();} }
    public static Project Instantiate(Project source,string name,bool includeObjects)
    {
        var p=ProjectStore.Deserialize(ProjectStore.Serialize(source));p.Id=Guid.NewGuid().ToString("N");p.Title=name;p.Date="";
        var sheets=p.Sheets.ToDictionary(s=>s.Id,_=>Guid.NewGuid().ToString("N"));var components=p.Sheets.SelectMany(s=>s.Components).ToDictionary(c=>c.Id,_=>Guid.NewGuid().ToString("N"));var strips=p.TerminalStrips.ToDictionary(t=>t.Id,_=>Guid.NewGuid().ToString("N"));var cabinets=p.Cabinets.ToDictionary(c=>c.Id,_=>Guid.NewGuid().ToString("N"));
        foreach(var s in p.Sheets){s.Id=sheets[s.Id];foreach(var c in s.Components){c.Id=components[c.Id];if(c.CoilId.Length>0)c.CoilId=components[c.CoilId];}foreach(var w in s.Wires){w.Id=Guid.NewGuid().ToString("N");w.From=new(components[w.From.ComponentId],w.From.PortId);w.To=new(components[w.To.ComponentId],w.To.PortId);}foreach(var n in s.Notes)n.Id=Guid.NewGuid().ToString("N");if(s.TerminalPlanId.Length>0)s.TerminalPlanId=strips[s.TerminalPlanId];if(s.CabinetPlanId.Length>0)s.CabinetPlanId=cabinets[s.CabinetPlanId];}
        foreach(var strip in p.TerminalStrips){strip.Id=strips[strip.Id];foreach(var t in strip.Terminals)t.ComponentId=components[t.ComponentId];foreach(var b in strip.Bridges){b.FromComponent=components[b.FromComponent];b.ToComponent=components[b.ToComponent];}}
        foreach(var cabinet in p.Cabinets){cabinet.Id=cabinets[cabinet.Id];foreach(var i in cabinet.Items){i.Id=Guid.NewGuid().ToString("N");if(i.ComponentId.Length>0)i.ComponentId=components[i.ComponentId];}}
        if(!includeObjects){p.Sheets.RemoveAll(s=>s.BomPage is not null||s.TerminalPlanId.Length>0||s.CabinetPlanId.Length>0);if(p.Sheets.Count==0)p.Sheets.Add(new());foreach(var s in p.Sheets){s.Components.Clear();s.Wires.Clear();s.Notes.Clear();s.Number="";}p.TerminalStrips.Clear();p.Cabinets.Clear();p.Customer="";p.DocumentNumber="";}
        p.NumberingRules.SheetId="";ProjectStore.ValidateStructure(p);return p;
    }
    public static void Save(Project project,string path,bool includeObjects,EditorPreferences? preferences=null)
    {
        if(!path.EndsWith(".gemtemplate",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Utiliser .gemtemplate.");var template=Instantiate(project,project.Title,includeObjects);Palette? palette=preferences is null?null:new(){Colors=preferences.Colors.ToList(),WireColor=preferences.WireColor};palette?.Validate();var json=JsonSerializer.Serialize(new{format="GeMMeElecTemplate",version=1,includeObjects,project=template,palette},new JsonSerializerOptions(ProjectStore.Json){WriteIndented=false});if(System.Text.Encoding.UTF8.GetByteCount(json)>Project.MaxFileBytes)throw new InvalidDataException("Gabarit trop volumineux.");ProjectStore.AtomicWrite(path,json);
    }
    public static (Project Project,bool IncludeObjects,Palette? Palette) Load(string path)
    {
        if(new FileInfo(path).Length>Project.MaxFileBytes)throw new InvalidDataException("Gabarit trop volumineux.");using var document=JsonDocument.Parse(File.ReadAllText(path));var root=document.RootElement;if(root.GetProperty("format").GetString()!="GeMMeElecTemplate"||root.GetProperty("version").GetInt32()!=1)throw new InvalidDataException("Format de gabarit non pris en charge.");bool include=root.GetProperty("includeObjects").GetBoolean();var p=ProjectStore.Deserialize(root.GetProperty("project").GetRawText());var palette=root.TryGetProperty("palette",out var value)&&value.ValueKind==JsonValueKind.Object?value.Deserialize<Palette>(ProjectStore.Json):null;palette?.Validate();return(p,include,palette);
    }
}
public sealed partial class MainWindow
{
    internal EditorPreferences TemplatePreferences => preferences;
    internal void ApplyTemplatePalette(ProjectTemplates.Palette? palette) {if(palette is null)return;palette.Validate();var draft=DocumentModel.Copy(preferences);draft.Colors=palette.Colors.ToList();draft.WireColor=palette.WireColor;draft.Save();preferences.Colors=draft.Colors;preferences.WireColor=draft.WireColor;Editor.CurrentWireColor=draft.WireColor;}
    private void SaveProjectTemplate()
    {
        Canvas.CancelDrag();var dialog=DocumentWindow("Enregistrer un gabarit de projet",560,410);var panel=new StackPanel{Margin=new(20)};dialog.Content=panel;panel.Children.Add(Hint("Conserve cartouches, images, symboles personnels, repérage, présentation et organisation des folios. Par défaut les schémas sont vides."));var include=new CheckBox{Content="Inclure les appareils, fils, bornes et implantations",Margin=new(0,15,0,15)};panel.Children.Add(include);var colors=new CheckBox{Content="Inclure la palette et la couleur des prochains fils",IsChecked=true,Margin=new(0,0,0,15)};panel.Children.Add(colors);panel.Children.Add(DocButton("Choisir le fichier et enregistrer",dialog,()=>{var save=new SaveFileDialog{Filter="Gabarit GeMMeElec|*.gemtemplate",FileName="Gabarit.gemtemplate"};if(save.ShowDialog(dialog)==true){ProjectTemplates.Save(Editor.Project,save.FileName,include.IsChecked==true,colors.IsChecked==true?preferences:null);dialog.Close();}}));ShowDocumentDialog(dialog);
    }
    private void NewFromProjectTemplate()
    {
        Canvas.CancelDrag();var open=new OpenFileDialog{Filter="Gabarit GeMMeElec|*.gemtemplate"};if(open.ShowDialog(this)!=true)return;var template=ProjectTemplates.Load(open.FileName);var name=Prompt("Nouveau projet depuis le gabarit","Nom du projet",template.Project.Title);if(name is null||!CanReplace())return;var project=ProjectTemplates.Instantiate(template.Project,name,template.IncludeObjects);ApplyTemplatePalette(template.Palette);Editor.ImportProject(project);
    }
}
