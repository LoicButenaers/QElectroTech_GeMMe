using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace GeMMeElec;

internal static class RichDocuments
{
    public static FlowDocument Create(RichTextContent content)
    {
        var document=new FlowDocument{PagePadding=new(6),FontFamily=new("Segoe UI"),FontSize=content.FontSize};var paragraph=new Paragraph{Margin=new(0)};
        foreach(var part in RichText.Effective(content.Text,content.Bold,content.Italic,content.Runs))paragraph.Inlines.Add(new Run(part.Text){FontWeight=part.Bold?FontWeights.Bold:FontWeights.Normal,FontStyle=part.Italic?FontStyles.Italic:FontStyles.Normal});document.Blocks.Add(paragraph);return document;
    }
    private static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks)
    {
        foreach(var block in blocks){if(block is Paragraph p)yield return p;else if(block is Section s)foreach(var child in Paragraphs(s.Blocks))yield return child;else if(block is System.Windows.Documents.List list)foreach(var item in list.ListItems)foreach(var child in Paragraphs(item.Blocks))yield return child;else if(block is System.Windows.Documents.Table table)foreach(var group in table.RowGroups)foreach(var row in group.Rows)foreach(var cell in row.Cells)foreach(var child in Paragraphs(cell.Blocks))yield return child;}
    }
    public static RichTextContent Read(FlowDocument document,RichTextContent style)
    {
        var value=DocumentModel.Copy(style);var runs=new List<RichRun>();
        void Inlines(InlineCollection collection){foreach(var item in collection){if(item is Run run)runs.Add(new(){Text=run.Text.Replace("\r\n","\n").Replace('\r','\n'),Bold=run.FontWeight>=FontWeights.SemiBold,Italic=run.FontStyle!=FontStyles.Normal});else if(item is LineBreak)runs.Add(new(){Text="\n",Bold=item.FontWeight>=FontWeights.SemiBold,Italic=item.FontStyle!=FontStyles.Normal});else if(item is Span span)Inlines(span.Inlines);}}
        bool first=true;foreach(var paragraph in Paragraphs(document.Blocks)){if(!first)runs.Add(new(){Text="\n"});first=false;Inlines(paragraph.Inlines);}
        value.Text=string.Concat(runs.Select(r=>r.Text));value.Runs=runs;RichText.Normalize(value);RichText.Validate(value);return value;
    }
}
public sealed partial class MainWindow
{
    internal void FormattedTextDialog(string title,RichTextContent source,Action<RichTextContent> accept,Window? owner=null)
    {
        var draft=DocumentModel.Copy(source);var dialog=DocumentWindow(title,790,690);if(owner is not null)dialog.Owner=owner;
        var root=new DockPanel{Margin=new(18)};dialog.Content=root;var bar=new WrapPanel();DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);
        var box=new RichTextBox{Document=RichDocuments.Create(draft),AcceptsTab=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=200,Name="RichTextEditor",FontFamily=new("Segoe UI")};UiTheme.Bind(box,Control.BackgroundProperty,"Ui.Surface");box.Foreground=Drawing.Brush(draft.Color);
        void Toggle(DependencyProperty property,object on,object off){var current=box.Selection.GetPropertyValue(property);box.Selection.ApplyPropertyValue(property,Equals(current,on)?off:on);box.Focus();}
        var bold=DocButton("Gras",dialog,()=>Toggle(TextElement.FontWeightProperty,FontWeights.Bold,FontWeights.Normal));bold.Name="RichBoldButton";bold.FontWeight=FontWeights.Bold;bold.ToolTip="Sélectionner un passage puis cliquer · Ctrl+B";bar.Children.Add(bold);
        var italic=DocButton("Italique",dialog,()=>Toggle(TextElement.FontStyleProperty,FontStyles.Italic,FontStyles.Normal));italic.Name="RichItalicButton";italic.FontStyle=FontStyles.Italic;italic.ToolTip="Sélectionner un passage puis cliquer · Ctrl+I";bar.Children.Add(italic);
        bar.Children.Add(DocButton("Tout sélectionner",dialog,()=>{box.Focus();box.SelectAll();}));
        var settings=new StackPanel();DockPanel.SetDock(settings,Dock.Bottom);root.Children.Add(settings);
        var font=DocField(settings,"Taille du texte",Number(draft.FontSize));font.Name="RichFontSize";var color=DocField(settings,"Couleur #RRGGBB",draft.Color);color.Name="RichTextColor";
        var align=new ComboBox{ItemsSource=new[]{"Gauche","Centré","Droite"},SelectedIndex=draft.Align=="left"?0:draft.Align=="center"?1:2,Name="RichAlignment",Margin=new(0,4,0,4)};settings.Children.Add(align);
        var vertical=new ComboBox{ItemsSource=new[]{"Haut","Milieu","Bas"},SelectedIndex=draft.Vertical=="top"?0:draft.Vertical=="center"?1:2,Name="RichVertical",Margin=new(0,4,0,4)};settings.Children.Add(vertical);
        settings.Children.Add(Hint("Sélectionner les mots à formater. Gras et italique peuvent être mélangés dans le même texte. Entrée ajoute une ligne."));
        var actions=new WrapPanel();settings.Children.Add(actions);actions.Children.Add(DocButton("Enregistrer le texte",dialog,()=>{draft.FontSize=ReadNumber(font.Text);draft.Color=color.Text;draft.Align=align.SelectedIndex==0?"left":align.SelectedIndex==1?"center":"right";draft.Vertical=vertical.SelectedIndex==0?"top":vertical.SelectedIndex==1?"center":"bottom";var result=RichDocuments.Read(box.Document,draft);accept(result);dialog.Close();}));actions.Children.Add(DocButton("Annuler",dialog,dialog.Close));
        font.TextChanged+=(_,_)=>{if(double.TryParse(font.Text.Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var size)&&size is >=4 and <=256)box.Document.FontSize=size;};color.TextChanged+=(_,_)=>{if(FreeGraphics.Color(color.Text))box.Foreground=Drawing.Brush(color.Text);};root.Children.Add(box);ShowDocumentDialog(dialog);
    }
    private void TypographyProperties(bool bold,bool italic,Action<bool?,bool?> update,string prefix)
    {
        var row=new WrapPanel();PropertyContents.Children.Add(row);var b=new CheckBox{Content="Gras",FontWeight=FontWeights.Bold,IsChecked=bold,Name=prefix+"Bold",Margin=new(0,6,16,6)};var i=new CheckBox{Content="Italique",FontStyle=FontStyles.Italic,IsChecked=italic,Name=prefix+"Italic",Margin=new(0,6,0,6)};row.Children.Add(b);row.Children.Add(i);RegisterPropertyControl(b);RegisterPropertyControl(i);
        b.Checked+=(_,_)=>LiveProperty(b,()=>update(true,null));b.Unchecked+=(_,_)=>LiveProperty(b,()=>update(false,null));i.Checked+=(_,_)=>LiveProperty(i,()=>update(null,true));i.Unchecked+=(_,_)=>LiveProperty(i,()=>update(null,false));
    }
    private void EditNoteFormatting(string id)
    {
        Canvas.CancelDrag();var note=Editor.Sheet.Notes.Single(n=>n.Id==id);FormattedTextDialog("Mise en forme du texte",RichText.Note(note),value=>Editor.Change(()=>RichText.SetNote(Editor.Sheet.Notes.Single(n=>n.Id==id),value)));
    }
    private void EditGraphicContent(string id)
    {
        Canvas.CancelDrag();var graphic=Editor.Sheet.Graphics.Single(g=>g.Id==id);if(graphic.Kind=="table"){TableDialog(id);return;}
        FormattedTextDialog("Texte du rectangle",graphic.Content,value=>Editor.Change(()=>{var live=Editor.Sheet.Graphics.Single(g=>g.Id==id);live.Kind="textbox";live.Content=DocumentModel.Copy(value);}));
    }
}
