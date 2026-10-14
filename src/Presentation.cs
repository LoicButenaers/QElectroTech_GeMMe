using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public sealed class ProjectPresentation
{
    public bool AllTextBold {get;set;}
    public bool AllTextItalic {get;set;}
    public string Name { get; set; } = "Technique";
    public double TagSize { get; set; } = 16;
    public double DetailSize { get; set; } = 10;
    public double DescriptionSize { get; set; } = 12;
    public double WireNumberSize { get; set; } = 10;
    public double WireThickness { get; set; } = 1.6;
    public string SymbolColor { get; set; } = "#203348";
    public string SecondaryColor { get; set; } = "#64748B";
    public string WireColor { get; set; } = "#243B53";
    public bool PreserveIndividual { get; set; } = true;
    public void Validate(){if(Name is null||Name.Length is <1 or >100||!new[]{TagSize,DetailSize,DescriptionSize,WireNumberSize}.All(n=>double.IsFinite(n)&&n>=4&&n<=200)||!double.IsFinite(WireThickness)||WireThickness is <.25 or >10||!new[]{SymbolColor,SecondaryColor,WireColor}.All(EditorPreferences.IsColor))throw new InvalidDataException("Présentation : tailles 4–200, épaisseur 0,25–10 et couleurs #RRGGBB.");}
    public static ProjectPresentation Preset(string name)=>name switch{"Noir"=>new(){Name="Noir",SymbolColor="#000000",SecondaryColor="#000000",WireColor="#000000"},"Lecture agrandie"=>new(){Name="Lecture agrandie",TagSize=20,DetailSize=13,DescriptionSize=15,WireNumberSize=13,WireThickness=2},_=>new()};
}
public static class Presentation
{
    public static LabelPlacement TextPlacement(Project p,LabelPlacement value){if(!p.Presentation.AllTextBold&&!p.Presentation.AllTextItalic)return value;var copy=DocumentModel.Copy(value);copy.Bold|=p.Presentation.AllTextBold;copy.Italic|=p.Presentation.AllTextItalic;return copy;}
    public static RichTextContent TextContent(Project p,RichTextContent value){if(!p.Presentation.AllTextBold&&!p.Presentation.AllTextItalic)return value;var copy=DocumentModel.Copy(value);RichText.Uniform(copy,p.Presentation.AllTextBold?true:null,p.Presentation.AllTextItalic?true:null);return copy;}
    public static Ink TextInk(Project p,Ink value)
    {
        if(value.Kind!="text"||!p.Presentation.AllTextBold&&!p.Presentation.AllTextItalic)return value;bool bold=value.Bold||p.Presentation.AllTextBold,italic=value.Italic||p.Presentation.AllTextItalic;
        var runs=value.Runs;if(runs is {Count:>0}&&runs.Any(r=>p.Presentation.AllTextBold&&!r.Bold||p.Presentation.AllTextItalic&&!r.Italic))runs=runs.Select(r=>new RichRun{Text=r.Text,Bold=r.Bold||p.Presentation.AllTextBold,Italic=r.Italic||p.Presentation.AllTextItalic}).ToList();
        return value.Bold==bold&&value.Italic==italic&&ReferenceEquals(runs,value.Runs)?value:value with{Bold=bold,Italic=italic,Runs=runs};
    }
    public static List<Ink> TextScene(Project p,List<Ink> scene)=>!p.Presentation.AllTextBold&&!p.Presentation.AllTextItalic?scene:scene.Select(i=>TextInk(p,i)).ToList();
    public static void Apply(Project p,ProjectPresentation style)
    {
        style.Validate();var old=p.Presentation;
        void Label(LabelPlacement placement,double size,string color,double previousSize,string previousColor){if(!style.PreserveIndividual||placement.FontSize==0||placement.FontSize==previousSize)placement.FontSize=size;if(!style.PreserveIndividual||placement.Color.Length==0||placement.Color==previousColor)placement.Color=color;}
        foreach(var sheet in p.Sheets){foreach(var c in sheet.Components){if(Junctions.Automatic(c)||CrossReferences.IsPoint(c))continue;Label(c.TagPlacement,style.TagSize,style.SymbolColor,old.TagSize,old.SymbolColor);Label(c.DetailPlacement,style.DetailSize,style.SecondaryColor,old.DetailSize,old.SecondaryColor);Label(c.DescriptionPlacement,style.DescriptionSize,style.SecondaryColor,old.DescriptionSize,old.SecondaryColor);}foreach(var w in sheet.Wires){if(style.PreserveIndividual&&w.NumberIndependent)continue;if(!style.PreserveIndividual||w.Thickness==old.WireThickness)w.Thickness=style.WireThickness;if(!style.PreserveIndividual||w.Color.Equals(old.WireColor,StringComparison.OrdinalIgnoreCase))w.Color=style.WireColor;if(!style.PreserveIndividual||w.NumberPlacement.FontSize==0||w.NumberPlacement.FontSize==old.WireNumberSize)w.NumberPlacement.FontSize=style.WireNumberSize;}}
        p.Presentation=DocumentModel.Copy(style);
    }
}
public sealed class DocumentPreview:FrameworkElement
{
    private Project project=new();private string sheetId="";
    public void Load(Project p,string id){project=p;sheetId=id;InvalidateVisual();}
    protected override void OnRender(System.Windows.Media.DrawingContext dc){var s=project.Sheets.FirstOrDefault(s=>s.Id==sheetId)??project.Sheets[0];dc.DrawRectangle(System.Windows.Media.Brushes.White,null,new(0,0,ActualWidth,ActualHeight));double scale=Math.Max(.01,Math.Min((ActualWidth-20)/s.Width,(ActualHeight-20)/s.Height));dc.PushTransform(new System.Windows.Media.TranslateTransform(10,10));dc.PushTransform(new System.Windows.Media.ScaleTransform(scale,scale));Drawing.Render(dc,Drawing.Scene(project,s));dc.Pop();dc.Pop();}
}
public sealed partial class MainWindow
{
    private void PresentationDialog()
    {
        Canvas.CancelDrag();var dialog=DocumentWindow("Présentation du projet · aperçu avant application",1200,780);var root=new DockPanel{Margin=new(18)};dialog.Content=root;var controls=new StackPanel{Width=270};var scroll=new ScrollViewer{Content=controls,Width=290,Margin=new(0,0,15,0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};DockPanel.SetDock(scroll,Dock.Left);root.Children.Add(scroll);var preset=new ComboBox{ItemsSource=new[]{"Technique","Noir","Lecture agrandie"},SelectedIndex=0,Margin=new(0,0,0,10)};controls.Children.Add(preset);var style=DocumentModel.Copy(Editor.Project.Presentation);var tag=DocField(controls,"Taille des repères",Number(style.TagSize));var detail=DocField(controls,"Taille des caractéristiques",Number(style.DetailSize));var description=DocField(controls,"Taille des descriptions",Number(style.DescriptionSize));var number=DocField(controls,"Taille des numéros de fil",Number(style.WireNumberSize));var thickness=DocField(controls,"Épaisseur commune",Number(style.WireThickness));var symbolColor=DocField(controls,"Couleur des symboles / repères",style.SymbolColor);var secondary=DocField(controls,"Couleur des détails",style.SecondaryColor);var wireColor=DocField(controls,"Couleur commune des fils",style.WireColor);var preserve=new CheckBox{Content="Conserver les réglages individuels",IsChecked=style.PreserveIndividual,Margin=new(0,8,0,10)};controls.Children.Add(preserve);var allBold=new CheckBox{Content="Gras pour tous les textes",Name="AllProjectTextBold",IsChecked=style.AllTextBold,Margin=new(0,4,0,4)};var allItalic=new CheckBox{Content="Italique pour tous les textes",Name="AllProjectTextItalic",IsChecked=style.AllTextItalic,Margin=new(0,4,0,8)};controls.Children.Add(allBold);controls.Children.Add(allItalic);var preview=new DocumentPreview();root.Children.Add(preview);string? shownStyle=null;
        void Read(){style.TagSize=ReadNumber(tag.Text);style.DetailSize=ReadNumber(detail.Text);style.DescriptionSize=ReadNumber(description.Text);style.WireNumberSize=ReadNumber(number.Text);style.WireThickness=ReadNumber(thickness.Text);style.SymbolColor=symbolColor.Text;style.SecondaryColor=secondary.Text;style.WireColor=wireColor.Text;style.PreserveIndividual=preserve.IsChecked==true;style.AllTextBold=allBold.IsChecked==true;style.AllTextItalic=allItalic.IsChecked==true;style.Validate();}
        void RefreshPreview(){Read();var copy=ProjectStore.Deserialize(Editor.ExportSnapshot);Presentation.Apply(copy,style);preview.Load(copy,Editor.Sheet.Id);shownStyle=System.Text.Json.JsonSerializer.Serialize(style);}
        preset.SelectionChanged+=(_,_)=>{style=ProjectPresentation.Preset((string)preset.SelectedItem);tag.Text=Number(style.TagSize);detail.Text=Number(style.DetailSize);description.Text=Number(style.DescriptionSize);number.Text=Number(style.WireNumberSize);thickness.Text=Number(style.WireThickness);symbolColor.Text=style.SymbolColor;secondary.Text=style.SecondaryColor;wireColor.Text=style.WireColor;RefreshPreview();};
        var notice=Hint("Les positions, bornes et trajets sont conservés. Les images officielles ne sont pas recolorées.");controls.Children.Add(DocButton("Actualiser l’aperçu",dialog,RefreshPreview));controls.Children.Add(DocButton("Appliquer au projet",dialog,()=>{Read();if(shownStyle!=System.Text.Json.JsonSerializer.Serialize(style)){RefreshPreview();notice.Text="Aperçu actualisé. Vérifier puis cliquer de nouveau sur Appliquer.";return;}Editor.Change(()=>Presentation.Apply(Editor.Project,style));dialog.Close();}));controls.Children.Add(notice);RefreshPreview();ShowDocumentDialog(dialog);
    }
}
