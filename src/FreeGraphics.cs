using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace GeMMeElec;

public sealed class SheetGraphic
{
    public RichTextContent Content {get;set;}=new();
    public FreeTable? Table {get;set;}
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Kind {get;set;}="rect";
    public double X {get;set;}
    public double Y {get;set;}
    public double Width {get;set;}=160;
    public double Height {get;set;}=80;
    public string Color {get;set;}="#203348";
    public string Fill {get;set;}="";
    public double Thickness {get;set;}=1.6;
    public string LineStyle {get;set;}="solid";
    public string ImageId {get;set;}="";
    public Rect Bounds()=>new(new Point(Math.Min(X,X+Width),Math.Min(Y,Y+Height)),new Point(Math.Max(X,X+Width),Math.Max(Y,Y+Height)));
}
public static class FreeGraphics
{
    public static bool Color(string? color,bool optional=false)=>optional&&color==""||color is not null&&Regex.IsMatch(color,"^#[0-9A-Fa-f]{6}$");
    public static void Validate(Sheet s,Project p)
    {
        if(s.Kind is not ("drawing" or "cover")||s.Graphics is null||s.Graphics.Count>5000)throw new InvalidDataException("Type ou dessins libres du folio invalides.");
        if(s.Kind=="cover"&&(s.BomPage is not null||s.TerminalPlanId.Length>0||s.CabinetPlanId.Length>0))throw new InvalidDataException("Un plan généré ne peut pas être une page de garde.");
        if(s.Graphics.Count>0&&(s.BomPage is not null||s.TerminalPlanId.Length>0||s.CabinetPlanId.Length>0))throw new InvalidDataException("Modifier les plans générés depuis leur gestionnaire.");
        foreach(var g in s.Graphics)
        {
            if(g is null||g.Kind is not ("rect" or "ellipse" or "line" or "image" or "textbox" or "table")||!new[]{g.X,g.Y,g.Width,g.Height,g.Thickness}.All(double.IsFinite)||!new Rect(0,0,s.Width,s.Height).Contains(g.Bounds()))throw new InvalidDataException("Dimensions du dessin libre invalides.");
            if(g.Kind=="line"?Math.Abs(g.Width)+Math.Abs(g.Height)<1:g.Width<1||g.Height<1)throw new InvalidDataException("Le dessin libre doit avoir une taille positive.");
            if(!Color(g.Color)||!Color(g.Fill,true)||g.Thickness is <.25 or >10||g.LineStyle is not ("solid" or "dashed" or "dotted" or "dashdot")||g.ImageId is null||g.Kind=="image"&&!p.Images.ContainsKey(g.ImageId)||g.Kind!="image"&&g.ImageId.Length>0)throw new InvalidDataException("Style ou image du dessin libre invalide.");
            RichText.Validate(g.Content);if(g.Kind=="table"){if(g.Table is null)throw new InvalidDataException("Tableau absent.");g.Table.Validate();if(Math.Abs(g.Width-g.Table.ColumnWidths.Sum())>.001||Math.Abs(g.Height-g.Table.RowHeights.Sum())>.001)throw new InvalidDataException("Dimensions du tableau incohérentes.");}else if(g.Table is not null)throw new InvalidDataException("Grille sur un élément autre que tableau.");
        }
        if(s.Graphics.Sum(g=>g.Table?.Cells.Count??0)>20000)throw new InvalidDataException("Maximum 20 000 cellules de tableau par folio.");
    }
    public static void SetSize(SheetGraphic graphic,double width,double height){if(graphic.Table is not null)graphic.Table.Resize(width,height);graphic.Width=width;graphic.Height=height;}
    public static List<Ink> Scene(Project p,SheetGraphic g)
    {
        if(g.Kind is not ("textbox" or "table"))return [Ink(p,g)];
        List<Ink> result=[new("rect",g.X,g.Y,g.Width,g.Height,Color:g.Color,Thickness:g.Thickness,LineStyle:g.LineStyle,Fill:g.Fill)];
        if(g.Kind=="textbox"){result.AddRange(RichText.Layout(Presentation.TextContent(p,g.Content),g.Bounds()));return result;}
        var table=g.Table!;double y=g.Y;
        for(int r=0;r<table.RowHeights.Count;r++)
        {
            double x=g.X;for(int c=0;c<table.ColumnWidths.Count;c++){result.AddRange(RichText.Layout(Presentation.TextContent(p,table.Cells[r*table.ColumnWidths.Count+c]),new(x,y,table.ColumnWidths[c],table.RowHeights[r])));x+=table.ColumnWidths[c];}
            y+=table.RowHeights[r];if(r+1<table.RowHeights.Count)result.Add(new("line",g.X,y,g.X+g.Width,y,Color:g.Color,Thickness:g.Thickness,LineStyle:g.LineStyle));
        }
        double columnX=g.X;for(int c=0;c+1<table.ColumnWidths.Count;c++){columnX+=table.ColumnWidths[c];result.Add(new("line",columnX,g.Y,columnX,g.Y+g.Height,Color:g.Color,Thickness:g.Thickness,LineStyle:g.LineStyle));}return result;
    }
    public static Ink Ink(Project p,SheetGraphic g)=>g.Kind=="image"?new("image",g.X,g.Y,g.Width,g.Height,Text:p.Images[g.ImageId]):g.Kind=="line"?new("line",g.X,g.Y,g.X+g.Width,g.Y+g.Height,Color:g.Color,Thickness:g.Thickness,LineStyle:g.LineStyle):new(g.Kind,g.X,g.Y,g.Width,g.Height,Color:g.Color,Thickness:g.Thickness,LineStyle:g.LineStyle,Fill:g.Fill);
    public static Ink NoteInk(Project p,Sheet s,Annotation n,double scale=1)
    {
        if(p.Presentation.AllTextBold||p.Presentation.AllTextItalic){n=DocumentModel.Copy(n);var displayed=Presentation.TextContent(p,RichText.Note(n));RichText.SetNote(n,displayed);}
        string text=n.ResolveFields?DocumentModel.Resolve(n.Text,DocumentModel.Values(p,s)):n.Text;IReadOnlyList<RichRun>? runs=n.Runs;
        if(n.ResolveFields&&n.Runs.Count>0){runs=RichText.ResolveRuns(n.Text,n.Runs,DocumentModel.Values(p,s));text=string.Concat(runs.Select(r=>r.Text));}
        double size=n.FontSize*scale;double width=Drawing.Measure(text,size,n.Bold,n.Italic,runs).Width;
        return new("text",n.X-(n.Align=="center"?width/2:n.Align=="right"?width:0),n.Y,Text:text,Color:n.Color,Size:size,Rotation:n.Rotation,Bold:n.Bold,Italic:n.Italic,Runs:runs is {Count:>0}?runs:null);
    }
    public static Rect NoteBounds(Project p,Sheet s,Annotation n,double scale=1)=>Drawing.TextBounds(NoteInk(p,s,n,scale));
    public static void ValidateNote(Annotation n)
    {
        RichText.Validate(n.Text,n.Runs);
        if(!double.IsFinite(n.FontSize)||n.FontSize is <4 or >256||!double.IsFinite(n.Rotation)||n.Rotation is <-360 or >360||!Color(n.Color)||n.Align is not ("left" or "center" or "right"))throw new InvalidDataException("Style du texte invalide : taille 4 à 256, angle −360 à 360 et couleur #RRGGBB.");
    }
    public static void SetKind(Sheet s,string kind)
    {
        if(kind is not ("drawing" or "cover"))throw new ArgumentException("Choisir Schéma ou Page de garde.");if(s.BomPage is not null||s.TerminalPlanId.Length>0||s.CabinetPlanId.Length>0)throw new ArgumentException("Ce folio est généré ; conserver son type.");
        if(s.Kind!=kind){s.Kind=kind;if(kind=="cover")s.TitleBlockId="";else if(s.TitleBlockId.Length==0)s.TitleBlockId="simple";}
    }
}
public sealed partial class Editor
{
    public Sheet AddCoverSheet(string title)
    {
        var sheet=new Sheet{Title=title,Kind="cover",TitleBlockId=""};Change(()=>Project.Sheets.Insert(0,sheet));SelectSheet(sheet.Id);return sheet;
    }
    public void SetSheetKind(string kind)=>Change(()=>FreeGraphics.SetKind(Sheet,kind));
    public SheetGraphic PutGraphic(SheetGraphic graphic)
    {
        Change(()=>{int at=Sheet.Graphics.FindIndex(g=>g.Id==graphic.Id);if(at<0)Sheet.Graphics.Add(DocumentModel.Copy(graphic));else Sheet.Graphics[at]=DocumentModel.Copy(graphic);});return Sheet.Graphics.Single(g=>g.Id==graphic.Id);
    }
    public SheetGraphic AddGraphic(string kind,Point a,Point b)
    {
        var graphic=new SheetGraphic{Kind=kind};if(kind=="line"){graphic.X=Snap(a.X);graphic.Y=Snap(a.Y);graphic.Width=Snap(b.X)-graphic.X;graphic.Height=Snap(b.Y)-graphic.Y;}else{graphic.X=Snap(Math.Min(a.X,b.X));graphic.Y=Snap(Math.Min(a.Y,b.Y));graphic.Width=Math.Abs(Snap(b.X)-Snap(a.X));graphic.Height=Math.Abs(Snap(b.Y)-Snap(a.Y));}return PutGraphic(graphic);
    }
    public Annotation PutNote(Annotation note){Change(()=>{int at=Sheet.Notes.FindIndex(n=>n.Id==note.Id);if(at<0)Sheet.Notes.Add(DocumentModel.Copy(note));else Sheet.Notes[at]=DocumentModel.Copy(note);});return Sheet.Notes.Single(n=>n.Id==note.Id);}
}
