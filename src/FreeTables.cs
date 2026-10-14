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
        double dx=width/ColumnWidths.Sum(),dy=height/RowHeights.Sum();ColumnWidths=ColumnWidths.Select(w=>w*dx).ToList();RowHeights=RowHeights.Select(h=>h*dy).ToList();
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
    public void SetTableCell(string id,int row,int column,RichTextContent content)
    {
        RichText.Validate(content);Change(()=>{var graphic=Sheet.Graphics.Single(g=>g.Id==id&&g.Kind=="table");var table=graphic.Table!;if(row<0||row>=table.RowHeights.Count||column<0||column>=table.ColumnWidths.Count)throw new ArgumentException("Cellule hors du tableau.");table.Cells[row*table.ColumnWidths.Count+column]=DocumentModel.Copy(content);});
    }
}
