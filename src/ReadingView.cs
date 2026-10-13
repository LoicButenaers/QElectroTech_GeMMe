using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public sealed class ReadingOptions
{
    public double TextScale { get; set; } = 1;
    public bool Details { get; set; } = true;
    public bool Ports { get; set; } = true;
    public bool WireNumbers { get; set; } = true;
    public bool Notes { get; set; } = true;
    public void Validate() { if (!double.IsFinite(TextScale) || TextScale is < .5 or > 2) throw new ArgumentException("Taille de lecture : 50 à 200 %."); }
    public Ink? Text(string key, Ink? ink) => ink is null || !Details && key is "detail" or "description" or "coilReferences" || !Ports && key.StartsWith("port:") ? null : Scale(ink);
    public Ink Scale(Ink ink) => ink with { Size = ink.Size * TextScale };
    public Ink? TextInk(Project p,Component c,CrossReferences refs,string key)
    {
        var ink=Text(key,GeMMeElec.ComponentTexts.Ink(p,c,refs,key));
        if(ink is not null&&key=="detail"&&CrossReferences.IsPoint(c)&&c.ReferenceDestinationInline&&GeMMeElec.ComponentTexts.Ink(p,c,refs,"tag") is Ink name)ink=ink with{Y=ink.Y+Drawing.TextBounds(Scale(name)).Bottom-Drawing.TextBounds(name).Bottom};
        return ink;
    }
    public IEnumerable<Ink> ComponentTexts(Project p, Component c, CrossReferences refs)
    {
        foreach (var text in GeMMeElec.ComponentTexts.List(c)) if (TextInk(p,c,refs,text.Key) is Ink ink)yield return ink;
    }
}
public sealed partial class SheetCanvas
{
    public ReadingOptions Reading { get; private set; } = new();
    public void SetReading(ReadingOptions options) { options.Validate(); Reading=DocumentModel.Copy(options); sceneCache=null; InvalidateVisual(); }
    internal List<Ink> ReadingScene() => Drawing.Scene(Editor.Project, Editor.Sheet, Reading);
    public void ZoomSelection()
    {
        Rect bounds = Rect.Empty; var refs = new CrossReferences(Editor.Project);
        foreach(var c in Editor.Sheet.Components.Where(c=>Selected.Contains(c.Id))) { bounds.Union(c.Bounds()); foreach(var ink in Reading.ComponentTexts(Editor.Project,c,refs)) bounds.Union(Drawing.TextBounds(ink)); }
        foreach(var n in Editor.Sheet.Notes.Where(n=>Selected.Contains(n.Id))) bounds.Union(new Rect(new Point(n.X,n.Y),Drawing.Measure(n.Text,16*Reading.TextScale)));
        foreach(var w in Editor.Sheet.Wires.Where(w=>Selected.Contains(w.Id))) foreach(var point in w.Route(Editor.Sheet)) bounds.Union(point);
        if(bounds.IsEmpty)throw new ArgumentException("Sélectionner les objets à cadrer."); bounds.Inflate(30,30);
        Zoom=Math.Clamp(Math.Min(Math.Max(10,ActualWidth-40)/bounds.Width,Math.Max(10,ActualHeight-40)/bounds.Height),.08,4);
        offset=new((ActualWidth-bounds.Width*Zoom)/2-bounds.Left*Zoom,(ActualHeight-bounds.Height*Zoom)/2-bounds.Top*Zoom);InvalidateVisual();
    }
}
public sealed partial class MainWindow
{
    private void ReadingDialog()
    {
        var dialog=DocumentWindow("Lecture du folio",500,440);var panel=new StackPanel{Margin=new(22)};dialog.Content=panel;
        panel.Children.Add(Hint("Ces réglages concernent la vue de cet onglet. Les textes du document et les exports conservent leurs réglages."));
        panel.Children.Add(new TextBlock{Text="Taille des textes à l’écran",Margin=new(0,14,0,8)});
        var scale=new Slider{Minimum=.5,Maximum=2,Value=Canvas.Reading.TextScale,TickFrequency=.25,IsSnapToTickEnabled=true};panel.Children.Add(scale);
        CheckBox Check(string name,bool value){var check=new CheckBox{Content=name,IsChecked=value,Margin=new(0,10,0,0)};panel.Children.Add(check);return check;}
        var details=Check("Caractéristiques, descriptions et renvois",Canvas.Reading.Details);var ports=Check("Numéros de bornes",Canvas.Reading.Ports);var numbers=Check("Numéros de conducteurs",Canvas.Reading.WireNumbers);var notes=Check("Notes",Canvas.Reading.Notes);
        void Update()=>Canvas.SetReading(new(){TextScale=scale.Value,Details=details.IsChecked==true,Ports=ports.IsChecked==true,WireNumbers=numbers.IsChecked==true,Notes=notes.IsChecked==true});
        scale.ValueChanged+=(_,_)=>Update();foreach(var check in new[]{details,ports,numbers,notes})check.Click+=(_,_)=>Update();
        panel.Children.Add(DocButton("Réinitialiser",dialog,()=>{scale.Value=1;foreach(var check in new[]{details,ports,numbers,notes})check.IsChecked=true;Update();}));panel.Children.Add(DocButton("Fermer",dialog,dialog.Close));ShowDocumentDialog(dialog);
    }
}
