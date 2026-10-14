using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace GeMMeElec;

internal sealed class TableCellDraft(RichTextContent value)
{
    public RichTextContent Value {get;}=value;
    public string Text {get=>Value.Text;set{if(Value.Text==value)return;Value.Text=value;Value.Runs.Clear();}}
}
internal sealed class TableRowDraft {public List<TableCellDraft> Cells {get;set;}=[];public double Height {get;set;}}
internal sealed class FreeGraphicPreview:FrameworkElement
{
    private Project? project;private SheetGraphic? graphic;
    private const double Scale=.75;
    public void Load(Project p,SheetGraphic g){project=p;graphic=g;InvalidateMeasure();InvalidateVisual();}
    protected override Size MeasureOverride(Size available)=>graphic is null?new(16,16):new(graphic.Width*Scale+16,graphic.Height*Scale+16);
    protected override void OnRender(DrawingContext dc){dc.DrawRectangle(Brushes.White,null,new(0,0,ActualWidth,ActualHeight));if(project is null||graphic is null)return;dc.PushTransform(new TranslateTransform(8,8));dc.PushTransform(new ScaleTransform(Scale,Scale));dc.PushTransform(new TranslateTransform(-graphic.X,-graphic.Y));Drawing.Render(dc,FreeGraphics.Scene(project,graphic));dc.Pop();dc.Pop();dc.Pop();}
}
public sealed partial class MainWindow
{
    private void NewTableDialog()=>TableDialog(null);
    internal void TableDialog(string? id)
    {
        Canvas.CancelDrag();var draft=id is null?new SheetGraphic{Kind="table",Width=360,Height=120,Table=new()}:DocumentModel.Copy(Editor.Sheet.Graphics.Single(g=>g.Id==id&&g.Kind=="table"));
        var dialog=DocumentWindow(id is null?"Créer un tableau":"Modifier le tableau",1150,850);var root=new DockPanel{Margin=new(16)};dialog.Content=root;
        var left=new StackPanel{Width=245};var scroll=new ScrollViewer{Content=left,Width=265,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new(0,0,12,0)};DockPanel.SetDock(scroll,Dock.Left);root.Children.Add(scroll);
        var rows=DocField(left,"Nombre de lignes · 1 à 100",draft.Table!.RowHeights.Count.ToString());rows.Name="TableRows";var columns=DocField(left,"Nombre de colonnes · 1 à 40",draft.Table.ColumnWidths.Count.ToString());columns.Name="TableColumns";
        string Common(List<double> sizes)=>sizes.Distinct().Count()==1?Number(sizes[0]):"";
        var defaultWidth=DocField(left,"Largeur de toutes les colonnes · unités",Common(draft.Table.ColumnWidths));defaultWidth.Name="TableCommonColumnWidth";var defaultHeight=DocField(left,"Hauteur de toutes les lignes · unités",Common(draft.Table.RowHeights));defaultHeight.Name="TableCommonRowHeight";
        var widths=DocField(left,"Largeurs : valeur unique ou liste (;)",string.Join(" ; ",draft.Table.ColumnWidths.Select(Number)),true);widths.Name="TableColumnWidths";var heights=DocField(left,"Hauteurs : valeur unique ou liste (;)",string.Join(" ; ",draft.Table.RowHeights.Select(Number)),true);heights.Name="TableRowHeights";
        var font=DocField(left,"Taille commune des textes","16");font.Name="TableCommonFontSize";var preview=new FreeGraphicPreview{Margin=new(0,0,0,4)};var previewScroll=new ScrollViewer{Content=preview,Height=185,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var center=new DockPanel();root.Children.Add(center);DockPanel.SetDock(previewScroll,Dock.Top);center.Children.Add(previewScroll);var sizeLabel=Hint("");sizeLabel.Name="TableSizeLabel";DockPanel.SetDock(sizeLabel,Dock.Top);center.Children.Add(sizeLabel);
        var grid=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,CanUserSortColumns=false,CanUserReorderColumns=false,EnableColumnVirtualization=true,EnableRowVirtualization=true,MinColumnWidth=8,SelectionUnit=DataGridSelectionUnit.Cell,SelectionMode=DataGridSelectionMode.Single,Name="FreeTableGrid"};List<TableRowDraft> displayed=[];bool syncing=false;
        var rowHeaderStyle=new Style(typeof(DataGridRowHeader),dialog.TryFindResource(typeof(DataGridRowHeader)) as Style);rowHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty,new DynamicResourceExtension("Ui.Surface")));rowHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty,new DynamicResourceExtension("Ui.Text")));rowHeaderStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty,30.0));grid.RowHeaderStyle=rowHeaderStyle;
        var rowStyle=new Style(typeof(DataGridRow),dialog.TryFindResource(typeof(DataGridRow)) as Style);rowStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty,new Binding("Height")));grid.RowStyle=rowStyle;
        var watchedWidths=new List<(DataGridColumn Column,EventHandler Handler)>();var widthDescriptor=DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty,typeof(DataGridColumn));
        void Unwatch(){foreach(var pair in watchedWidths)widthDescriptor.RemoveValueChanged(pair.Column,pair.Handler);watchedWidths.Clear();}
        void Sizes(){draft.Width=draft.Table.ColumnWidths.Sum();draft.Height=draft.Table.RowHeights.Sum();sizeLabel.Text=$"Taille totale : {Number(draft.Width)} × {Number(draft.Height)} unités · aperçu à 75 %";preview.Load(Editor.Project,draft);}
        void SyncFields(){syncing=true;try{widths.Text=string.Join(" ; ",draft.Table.ColumnWidths.Select(Number));heights.Text=string.Join(" ; ",draft.Table.RowHeights.Select(Number));defaultWidth.Text=Common(draft.Table.ColumnWidths);defaultHeight.Text=Common(draft.Table.RowHeights);}finally{syncing=false;}}
        List<double> Axis(TextBox input,int count){var values=input.Text.Split([';','\r','\n'],StringSplitOptions.RemoveEmptyEntries).Select(ReadNumber).ToList();if(values.Count==1)values=Enumerable.Repeat(values[0],count).ToList();if(values.Count!=count)throw new ArgumentException("Saisir une valeur commune ou une valeur par colonne/ligne.");return values;}
        void Flush(){if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Corriger la cellule en cours.");}
        void Rebuild(){Unwatch();syncing=true;try{grid.Columns.Clear();displayed=[];for(int r=0;r<draft.Table.RowHeights.Count;r++)displayed.Add(new(){Height=draft.Table.RowHeights[r],Cells=draft.Table.Cells.Skip(r*draft.Table.ColumnWidths.Count).Take(draft.Table.ColumnWidths.Count).Select(c=>new TableCellDraft(c)).ToList()});for(int c=0;c<draft.Table.ColumnWidths.Count;c++){int index=c;var column=new DataGridTextColumn{Header="Colonne "+(c+1),Binding=new Binding($"Cells[{c}].Text"){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus},Width=draft.Table.ColumnWidths[c]};grid.Columns.Add(column);EventHandler handler=(_,_)=>{if(syncing||!column.Width.IsAbsolute)return;double value=column.Width.Value;if(!double.IsFinite(value)||value<8)return;draft.Table.ColumnWidths[index]=value;SyncFields();Sizes();};widthDescriptor.AddValueChanged(column,handler);watchedWidths.Add((column,handler));}grid.ItemsSource=displayed;}finally{syncing=false;}Sizes();}
        void ReadDimensions(){var w=Axis(widths,draft.Table.ColumnWidths.Count);var h=Axis(heights,draft.Table.RowHeights.Count);var proposed=DocumentModel.Copy(draft.Table);proposed.ColumnWidths=w;proposed.RowHeights=h;proposed.Validate();draft.Table=proposed;Sizes();SyncFields();}
        void EditCell(){Flush();if(grid.CurrentCell.Item is not TableRowDraft row||grid.CurrentCell.Column is null)throw new ArgumentException("Choisir une cellule du tableau.");int index=displayed.IndexOf(row)*draft.Table.ColumnWidths.Count+grid.CurrentCell.Column.DisplayIndex;FormattedTextDialog("Texte de la cellule",draft.Table.Cells[index],value=>{draft.Table.Cells[index]=value;Rebuild();},dialog);}
        left.Children.Add(DocButton("Appliquer le nombre de lignes/colonnes",dialog,()=>{Flush();if(!int.TryParse(rows.Text,out int r)||!int.TryParse(columns.Text,out int c))throw new ArgumentException("Saisir deux nombres entiers.");ReadDimensions();draft.Table.Reconfigure(r,c,defaultWidth.Text.Length==0?120:ReadNumber(defaultWidth.Text),defaultHeight.Text.Length==0?40:ReadNumber(defaultHeight.Text));SyncFields();Rebuild();}));
        left.Children.Add(DocButton("Appliquer les largeurs et hauteurs",dialog,()=>{Flush();ReadDimensions();Rebuild();}));left.Children.Add(DocButton("Appliquer la taille à toutes les cellules",dialog,()=>{double size=ReadNumber(font.Text);if(size is <4 or >256)throw new ArgumentException("Taille 4 à 256.");foreach(var cell in draft.Table.Cells)cell.FontSize=size;preview.Load(Editor.Project,draft);}));
        left.Children.Add(Hint("Les cellules conservées gardent leur contenu. Réduire la grille retire les lignes/colonnes en fin de tableau. L’enregistrement est annulable avec Ctrl+Z."));
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);center.Children.Add(footer);footer.Children.Add(Hint("Saisir directement dans une case. Pour formater un passage, choisir la case puis cliquer sur Éditer la cellule."));var buttons=new WrapPanel();footer.Children.Add(buttons);
        buttons.Children.Add(DocButton("Éditer la cellule (texte, gras, italique)",dialog,EditCell));buttons.Children.Add(DocButton(id is null?"Placer le tableau":"Enregistrer le tableau",dialog,()=>{Flush();ReadDimensions();draft.Table.Validate();if(draft.Width>Editor.Sheet.Width||draft.Height>Editor.Sheet.Height)throw new ArgumentException("Le tableau dépasse la page ; réduire les largeurs ou hauteurs.");if(id is null){Canvas.PrepareGraphic(draft);status.Text="Cliquer pour placer le tableau · Échap annule";}else Editor.PutGraphic(draft);dialog.Close();}));buttons.Children.Add(DocButton("Annuler",dialog,dialog.Close));
        defaultWidth.TextChanged+=(_,_)=>{if(syncing||defaultWidth.Text.Length==0)return;syncing=true;try{widths.Text=string.Join(" ; ",Enumerable.Repeat(defaultWidth.Text,draft.Table.ColumnWidths.Count));}finally{syncing=false;}};defaultHeight.TextChanged+=(_,_)=>{if(syncing||defaultHeight.Text.Length==0)return;syncing=true;try{heights.Text=string.Join(" ; ",Enumerable.Repeat(defaultHeight.Text,draft.Table.RowHeights.Count));}finally{syncing=false;}};
        widths.TextChanged+=(_,_)=>{if(syncing)return;syncing=true;try{defaultWidth.Text="";}finally{syncing=false;}};heights.TextChanged+=(_,_)=>{if(syncing)return;syncing=true;try{defaultHeight.Text="";}finally{syncing=false;}};
        grid.LoadingRow+=(_,e)=>e.Row.Header=(e.Row.GetIndex()+1).ToString();grid.CellEditEnding+=(_,_)=>dialog.Dispatcher.BeginInvoke(()=>preview.Load(Editor.Project,draft));dialog.Closed+=(_,_)=>Unwatch();center.Children.Add(grid);Rebuild();ShowDocumentDialog(dialog);
    }
}
