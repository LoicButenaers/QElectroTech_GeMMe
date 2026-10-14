using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace GeMMeElec;

internal sealed class TableCellDraft(RichTextContent value)
{
    public RichTextContent Value {get;}=value;
    public string Text {get=>Value.Text;set{if(Value.Text==value)return;Value.Text=value;Value.Runs.Clear();}}
}
internal sealed class TableRowDraft {public List<TableCellDraft> Cells {get;set;}=[];}
internal sealed class FreeGraphicPreview:FrameworkElement
{
    private Project? project;private SheetGraphic? graphic;
    public void Load(Project p,SheetGraphic g){project=p;graphic=g;InvalidateVisual();}
    protected override void OnRender(DrawingContext dc){dc.DrawRectangle(Brushes.White,null,new(0,0,ActualWidth,ActualHeight));if(project is null||graphic is null)return;double scale=Math.Max(.001,Math.Min((ActualWidth-16)/graphic.Width,(ActualHeight-16)/graphic.Height));dc.PushTransform(new TranslateTransform(8,8));dc.PushTransform(new ScaleTransform(scale,scale));dc.PushTransform(new TranslateTransform(-graphic.X,-graphic.Y));Drawing.Render(dc,FreeGraphics.Scene(project,graphic));dc.Pop();dc.Pop();dc.Pop();}
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
        var defaultWidth=DocField(left,"Largeur des nouvelles colonnes","120");var defaultHeight=DocField(left,"Hauteur des nouvelles lignes","40");
        var widths=DocField(left,"Largeurs des colonnes · séparées par ;",string.Join(" ; ",draft.Table.ColumnWidths.Select(Number)),true);widths.Name="TableColumnWidths";var heights=DocField(left,"Hauteurs des lignes · séparées par ;",string.Join(" ; ",draft.Table.RowHeights.Select(Number)),true);heights.Name="TableRowHeights";
        var font=DocField(left,"Taille commune des textes","16");font.Name="TableCommonFontSize";var preview=new FreeGraphicPreview{Height=175,Margin=new(0,0,0,10)};var center=new DockPanel();root.Children.Add(center);DockPanel.SetDock(preview,Dock.Top);center.Children.Add(preview);
        var grid=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,EnableColumnVirtualization=true,EnableRowVirtualization=true,SelectionUnit=DataGridSelectionUnit.Cell,SelectionMode=DataGridSelectionMode.Single,Name="FreeTableGrid"};List<TableRowDraft> displayed=[];
        void Flush(){if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Corriger la cellule en cours.");}
        void Rebuild(){grid.Columns.Clear();displayed=[];for(int r=0;r<draft.Table.RowHeights.Count;r++)displayed.Add(new(){Cells=draft.Table.Cells.Skip(r*draft.Table.ColumnWidths.Count).Take(draft.Table.ColumnWidths.Count).Select(c=>new TableCellDraft(c)).ToList()});for(int c=0;c<draft.Table.ColumnWidths.Count;c++)grid.Columns.Add(new DataGridTextColumn{Header="Colonne "+(c+1),Binding=new Binding($"Cells[{c}].Text"){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus},Width=Math.Clamp(draft.Table.ColumnWidths[c],90,300)});grid.ItemsSource=displayed;preview.Load(Editor.Project,draft);}
        void ReadDimensions(){var w=widths.Text.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(ReadNumber).ToList();var h=heights.Text.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(ReadNumber).ToList();if(w.Count!=draft.Table.ColumnWidths.Count||h.Count!=draft.Table.RowHeights.Count)throw new ArgumentException("Saisir une largeur par colonne et une hauteur par ligne.");var proposed=DocumentModel.Copy(draft.Table);proposed.ColumnWidths=w;proposed.RowHeights=h;proposed.Validate();draft.Table=proposed;draft.Width=w.Sum();draft.Height=h.Sum();}
        void EditCell(){Flush();if(grid.CurrentCell.Item is not TableRowDraft row||grid.CurrentCell.Column is null)throw new ArgumentException("Choisir une cellule du tableau.");int index=displayed.IndexOf(row)*draft.Table.ColumnWidths.Count+grid.CurrentCell.Column.DisplayIndex;FormattedTextDialog("Texte de la cellule",draft.Table.Cells[index],value=>{draft.Table.Cells[index]=value;Rebuild();},dialog);}
        left.Children.Add(DocButton("Appliquer le nombre de lignes/colonnes",dialog,()=>{Flush();if(!int.TryParse(rows.Text,out int r)||!int.TryParse(columns.Text,out int c))throw new ArgumentException("Saisir deux nombres entiers.");draft.Table.Reconfigure(r,c,ReadNumber(defaultWidth.Text),ReadNumber(defaultHeight.Text));draft.Width=draft.Table.ColumnWidths.Sum();draft.Height=draft.Table.RowHeights.Sum();widths.Text=string.Join(" ; ",draft.Table.ColumnWidths.Select(Number));heights.Text=string.Join(" ; ",draft.Table.RowHeights.Select(Number));Rebuild();}));
        left.Children.Add(DocButton("Appliquer les largeurs et hauteurs",dialog,()=>{Flush();ReadDimensions();Rebuild();}));left.Children.Add(DocButton("Appliquer la taille à toutes les cellules",dialog,()=>{double size=ReadNumber(font.Text);if(size is <4 or >256)throw new ArgumentException("Taille 4 à 256.");foreach(var cell in draft.Table.Cells)cell.FontSize=size;preview.Load(Editor.Project,draft);}));
        left.Children.Add(Hint("Les cellules conservées gardent leur contenu. Réduire la grille retire les lignes/colonnes en fin de tableau. L’enregistrement est annulable avec Ctrl+Z."));
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);center.Children.Add(footer);footer.Children.Add(Hint("Saisir directement dans une case. Pour formater un passage, choisir la case puis cliquer sur Éditer la cellule."));var buttons=new WrapPanel();footer.Children.Add(buttons);
        buttons.Children.Add(DocButton("Éditer la cellule (texte, gras, italique)",dialog,EditCell));buttons.Children.Add(DocButton(id is null?"Placer le tableau":"Enregistrer le tableau",dialog,()=>{Flush();ReadDimensions();draft.Table.Validate();if(draft.Width>Editor.Sheet.Width||draft.Height>Editor.Sheet.Height)throw new ArgumentException("Le tableau dépasse la page ; réduire les largeurs ou hauteurs.");if(id is null){Canvas.PrepareGraphic(draft);status.Text="Cliquer pour placer le tableau · Échap annule";}else Editor.PutGraphic(draft);dialog.Close();}));buttons.Children.Add(DocButton("Annuler",dialog,dialog.Close));
        grid.LoadingRow+=(_,e)=>e.Row.Header=(e.Row.GetIndex()+1).ToString();grid.CellEditEnding+=(_,_)=>dialog.Dispatcher.BeginInvoke(()=>preview.Load(Editor.Project,draft));center.Children.Add(grid);Rebuild();ShowDocumentDialog(dialog);
    }
}
