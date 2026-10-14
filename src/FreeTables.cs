using System.IO;

namespace GeMMeElec;

public sealed class FreeTable
{
    public List<double> ColumnWidths { get; set; } = [120,120,120];
    public List<double> RowHeights { get; set; } = [40,40,40];
    public List<RichTextContent> Cells { get; set; } = Enumerable.Range(0,9).Select(_=>new RichTextContent{Align="center"}).ToList();
    public static FreeTable Create(int rows,int columns,double columnWidth,double rowHeight)
    {
        if(rows is <1 or >100 || columns is <1 or >40 || rows*columns>2000)throw new ArgumentException("Tableau : 1 à 100 lignes, 1 à 40 colonnes, maximum 2 000 cellules.");
        var table=new FreeTable{ColumnWidths=Enumerable.Repeat(columnWidth,columns).ToList(),RowHeights=Enumerable.Repeat(rowHeight,rows).ToList(),Cells=Enumerable.Range(0,rows*columns).Select(_=>new RichTextContent{Align="center"}).ToList()};table.Validate();return table;
    }
    public void Validate()
    {
        if(ColumnWidths is null||RowHeights is null||Cells is null||RowHeights.Count is <1 or >100||ColumnWidths.Count is <1 or >40||Cells.Count!=RowHeights.Count*ColumnWidths.Count||Cells.Count>2000||!ColumnWidths.Concat(RowHeights).All(v=>double.IsFinite(v)&&v>=8&&v<=20000))throw new InvalidDataException("Dimensions du tableau invalides : chaque largeur/hauteur entre 8 et 20 000 unités.");
        foreach(var cell in Cells)RichText.Validate(cell);
    }
    public void Resize(double width,double height)
    {
        double minimumWidth=ColumnWidths.Sum()*8/ColumnWidths.Min(),minimumHeight=RowHeights.Sum()*8/RowHeights.Min();
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<minimumWidth-1e-8||height<minimumHeight-1e-8)throw new ArgumentException($"Chaque cellule doit mesurer au moins 8 unités. Taille totale minimale pour ce tableau : {minimumWidth:0.###} × {minimumHeight:0.###}.");
        double dx=width/ColumnWidths.Sum(),dy=height/RowHeights.Sum();ColumnWidths=ColumnWidths.Select(w=>Math.Max(8,w*dx)).ToList();RowHeights=RowHeights.Select(h=>Math.Max(8,h*dy)).ToList();
    }
    public void Reconfigure(int rows,int columns,double defaultWidth,double defaultHeight)
    {
        var other=Create(rows,columns,defaultWidth,defaultHeight);int oldColumns=ColumnWidths.Count;
        for(int c=0;c<Math.Min(columns,oldColumns);c++)other.ColumnWidths[c]=ColumnWidths[c];for(int r=0;r<Math.Min(rows,RowHeights.Count);r++)other.RowHeights[r]=RowHeights[r];
        for(int r=0;r<Math.Min(rows,RowHeights.Count);r++)for(int c=0;c<Math.Min(columns,oldColumns);c++)other.Cells[r*columns+c]=DocumentModel.Copy(Cells[r*oldColumns+c]);
        ColumnWidths=other.ColumnWidths;RowHeights=other.RowHeights;Cells=other.Cells;
    }
}
public sealed partial class Editor
{
    public void SetTableCellSize(string id,double? columnWidth=null,double? rowHeight=null)
    {
        Change(()=>{var graphic=Sheet.Graphics.Single(g=>g.Id==id&&g.Kind=="table");var table=graphic.Table!;if(columnWidth is double w)table.ColumnWidths=Enumerable.Repeat(w,table.ColumnWidths.Count).ToList();if(rowHeight is double h)table.RowHeights=Enumerable.Repeat(h,table.RowHeights.Count).ToList();table.Validate();graphic.Width=table.ColumnWidths.Sum();graphic.Height=table.RowHeights.Sum();});
    }
    public void SetTableCell(string id,int row,int column,RichTextContent content)
    {
        RichText.Validate(content);Change(()=>{var graphic=Sheet.Graphics.Single(g=>g.Id==id&&g.Kind=="table");var table=graphic.Table!;if(row<0||row>=table.RowHeights.Count||column<0||column>=table.ColumnWidths.Count)throw new ArgumentException("Cellule hors du tableau.");table.Cells[row*table.ColumnWidths.Count+column]=DocumentModel.Copy(content);});
    }
}
