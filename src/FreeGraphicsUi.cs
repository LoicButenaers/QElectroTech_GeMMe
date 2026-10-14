using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace GeMMeElec;

public sealed partial class SheetCanvas
{
    private Point? graphicStart;
    private bool graphicPressed;
    private (string Id,string Data,double Width,double Height)? pendingImage;
    private (string Id,Point Anchor)? graphicResize;
    private readonly Dictionary<string,SheetGraphic> originalGraphics=[];
    private SheetGraphic? pendingGraphic;
    private DrawingGroup? pendingGraphicDrawing;
    public event Action<string>? GraphicEditRequested;
    public void PrepareGraphic(SheetGraphic graphic){pendingGraphic=DocumentModel.Copy(graphic);pendingGraphicDrawing=new();using(var dc=pendingGraphicDrawing.Open())Drawing.Render(dc,FreeGraphics.Scene(Editor.Project,pendingGraphic));pendingGraphicDrawing.Freeze();SetMode("graphicTemplate");}
    public void PrepareImage(string id,string data,double width,double height){pendingImage=(id,data,width,height);SetMode("graphicImage");}
    private void CancelGraphicGesture(){graphicStart=null;graphicPressed=false;graphicResize=null;}
    private bool GraphicClick(Point point)
    {
        if(Mode=="graphicTemplate"&&pendingGraphic is not null){var copy=DocumentModel.Copy(pendingGraphic);copy.Id=Guid.NewGuid().ToString("N");copy.X=Editor.Snap(point.X);copy.Y=Editor.Snap(point.Y);var created=Editor.PutGraphic(copy);pendingGraphic=null;SetMode("select");Select(created.Id);return true;}
        if(Mode=="graphicImage"&&pendingImage is { } image)
        {
            SheetGraphic? created=null;Editor.Change(()=>{Editor.Project.Images.TryAdd(image.Id,image.Data);created=new(){Kind="image",ImageId=image.Id,X=Editor.Snap(point.X),Y=Editor.Snap(point.Y),Width=image.Width,Height=image.Height};Editor.Sheet.Graphics.Add(created);});SetMode("select");Select(created!.Id);return true;
        }
        if(Mode is not ("graphicRect" or "graphicEllipse" or "graphicLine" or "graphicTextbox"))return false;
        if(graphicStart is null){graphicStart=new(Editor.Snap(point.X),Editor.Snap(point.Y));graphicPressed=true;Editor.InteractionInProgress=true;CaptureMouse();Status?.Invoke("Glisser ou cliquer le second point · Échap annule");}
        else CompleteGraphic(point);InvalidateVisual();return true;
    }
    private void CompleteGraphic(Point point)
    {
        if(graphicStart is not Point start)return;string kind=Mode=="graphicLine"?"line":Mode=="graphicEllipse"?"ellipse":Mode=="graphicTextbox"?"textbox":"rect";graphicStart=null;graphicPressed=false;Editor.InteractionInProgress=false;ReleaseMouseCapture();var g=Editor.AddGraphic(kind,start,point);SetMode("select");Select(g.Id);if(kind=="textbox")GraphicEditRequested?.Invoke(g.Id);
    }
    private bool GraphicRelease(Point point)
    {
        if(!graphicPressed||graphicStart is not Point start)return false;graphicPressed=false;
        if((point-start).Length*Zoom>3)CompleteGraphic(point);else ReleaseMouseCapture();return true;
    }
    private void RenderGraphicsOverlay(DrawingContext dc)
    {
        var pen=new Pen(Drawing.Brush("#007F86"),1.5/Zoom);
        foreach(var g in Editor.Sheet.Graphics.Where(g=>Selected.Contains(g.Id))){var r=g.Bounds();r.Inflate(4/Zoom,4/Zoom);dc.DrawRectangle(null,pen,r);if(Selected.Count==1)foreach(var point in GraphicHandles(g))dc.DrawRectangle(Brushes.White,pen,new(point.X-4/Zoom,point.Y-4/Zoom,8/Zoom,8/Zoom));}
        if(graphicStart is Point start){if(Mode=="graphicLine")dc.DrawLine(pen,start,pointer);else if(Mode=="graphicEllipse"){var r=new Rect(start,pointer);dc.DrawEllipse(null,pen,new(r.X+r.Width/2,r.Y+r.Height/2),r.Width/2,r.Height/2);}else dc.DrawRectangle(null,pen,new Rect(start,pointer));}
        if(Mode=="graphicImage"&&pendingImage is { } image&&IsMouseOver){dc.PushOpacity(.5);dc.DrawImage(ImageAssets.Decode(image.Data),new(Editor.Snap(pointer.X),Editor.Snap(pointer.Y),image.Width,image.Height));dc.Pop();}
        if(Mode=="graphicTemplate"&&pendingGraphic is SheetGraphic template&&IsMouseOver){dc.PushOpacity(.5);dc.PushTransform(new TranslateTransform(Editor.Snap(pointer.X)-template.X,Editor.Snap(pointer.Y)-template.Y));dc.DrawDrawing(pendingGraphicDrawing);dc.Pop();dc.Pop();}
    }
    private static Point[] GraphicHandles(SheetGraphic g)=>g.Kind=="line"?[new(g.X,g.Y),new(g.X+g.Width,g.Y+g.Height)]:[g.Bounds().TopLeft,g.Bounds().TopRight,g.Bounds().BottomRight,g.Bounds().BottomLeft];
    private bool BeginGraphicResize(Point point)
    {
        if(Mode!="select"||Selected.Count!=1)return false;var g=Editor.Sheet.Graphics.SingleOrDefault(g=>Selected.Contains(g.Id));if(g is null)return false;var handles=GraphicHandles(g);int hit=Array.FindIndex(handles,p=>(p-point).Length<7/Zoom);if(hit<0)return false;
        graphicResize=(g.Id,handles[g.Kind=="line"?1-hit:(hit+2)%4]);before=Editor.ExportSnapshot;Editor.InteractionInProgress=true;CaptureMouse();return true;
    }
    private void ResizeGraphic(Point point)
    {
        if(graphicResize is not { } resize)return;var g=Editor.Sheet.Graphics.Single(g=>g.Id==resize.Id);var p=new Point(Math.Clamp(Editor.Snap(point.X),0,Editor.Sheet.Width),Math.Clamp(Editor.Snap(point.Y),0,Editor.Sheet.Height));
        if(g.Kind=="line"){g.X=resize.Anchor.X;g.Y=resize.Anchor.Y;g.Width=p.X-g.X;g.Height=p.Y-g.Y;if(Math.Abs(g.Width)+Math.Abs(g.Height)<1)g.Width=1;}
        else{g.X=Math.Min(resize.Anchor.X,p.X);g.Y=Math.Min(resize.Anchor.Y,p.Y);double minWidth=g.Table is null?1:g.Width*8/g.Table.ColumnWidths.Min(),minHeight=g.Table is null?1:g.Height*8/g.Table.RowHeights.Min();FreeGraphics.SetSize(g,Math.Max(minWidth,Math.Abs(p.X-resize.Anchor.X)),Math.Max(minHeight,Math.Abs(p.Y-resize.Anchor.Y)));}
        retainedScene?.GraphicsChanged([g.Id]);InvalidateVisual();
    }
    private string? HitGraphic(Point point)
    {
        double tolerance=7/Zoom;foreach(var g in Editor.Sheet.Graphics.AsEnumerable().Reverse())
        {
            var r=g.Bounds();if(g.Kind=="line"){if(Distance(point,new(g.X,g.Y),new(g.X+g.Width,g.Y+g.Height))<tolerance)return g.Id;continue;}
            if(g.Kind=="ellipse"){double norm=Math.Sqrt(Math.Pow((point.X-r.X-r.Width/2)/(r.Width/2),2)+Math.Pow((point.Y-r.Y-r.Height/2)/(r.Height/2),2));if(g.Fill.Length>0&&norm<=1||Math.Abs(norm-1)*Math.Min(r.Width,r.Height)/2<tolerance)return g.Id;continue;}
            if(g.Kind is "image" or "textbox" or "table"||g.Fill.Length>0){if(r.Contains(point))return g.Id;}else{var outer=r;outer.Inflate(tolerance,tolerance);var inner=r;inner.Inflate(-Math.Min(tolerance,r.Width/2),-Math.Min(tolerance,r.Height/2));if(outer.Contains(point)&&!inner.Contains(point))return g.Id;}
        }return null;
    }
    internal bool GraphicClickForTest(Point point)=>GraphicClick(point);
    internal bool GraphicReleaseForTest(Point point)=>GraphicRelease(point);
    internal bool ResizeStartForTest(Point point)=>BeginGraphicResize(point);
    internal void ResizeForTest(Point point)=>ResizeGraphic(point);
}
public sealed partial class MainWindow
{
    private void AddCoverPage(){var title=Prompt("Ajouter une page de garde","Titre","Page de garde");if(title is null)return;Editor.AddCoverSheet(title);}
    private void GraphicImageDialog()
    {
        var file=new OpenFileDialog{Filter="Logo / image|*.png;*.jpg;*.jpeg;*.bmp"};if(file.ShowDialog(this)!=true)return;var asset=ImageAssets.Import(file.FileName);var bitmap=ImageAssets.Decode(asset.Data);double width=Math.Min(280,Editor.Sheet.Width-80),height=width*bitmap.PixelHeight/bitmap.PixelWidth;if(height>Editor.Sheet.Height-100){width*= (Editor.Sheet.Height-100)/height;height=Editor.Sheet.Height-100;}Canvas.PrepareImage(asset.Id,asset.Data,width,height);status.Text="Cliquer la position du logo / de l’image · Échap annule";
    }
    private void FreeTextDialog(Point point)
    {
        var dialog=DocumentWindow("Insérer du texte",600,530);var panel=new StackPanel{Margin=new(20)};dialog.Content=panel;var text=DocField(panel,"Texte · plusieurs lignes possibles","Texte",true);var size=DocField(panel,"Taille du texte",Editor.Sheet.Kind=="cover"?"32":"16");var color=DocField(panel,"Couleur #RRGGBB","#203348");var linked=new CheckBox{Content="Résoudre les champs du projet : {project.title}, {project.customer}…",Margin=new(0,12,0,12)};panel.Children.Add(linked);panel.Children.Add(DocButton("Insérer",dialog,()=>{var n=Editor.PutNote(new(){X=Editor.Snap(point.X),Y=Editor.Snap(point.Y),Text=text.Text,FontSize=ReadNumber(size.Text),Color=color.Text,ResolveFields=linked.IsChecked==true});Canvas.Select(n.Id);dialog.Close();}));ShowDocumentDialog(dialog);
    }
    private void GraphicProperties(SheetGraphic graphic)
    {
        properties.Children.Add(new TextBlock{Text=graphic.Kind=="table"?"Tableau":graphic.Kind=="textbox"?"Rectangle avec texte":graphic.Kind=="image"?"Logo / image":graphic.Kind=="line"?"Ligne":graphic.Kind=="ellipse"?"Ellipse":"Rectangle",FontSize=19,Margin=new(0,15,0,8)});properties.Children.Add(Hint("Déplacer avec la souris ; redimensionner par les poignées. Ctrl+Z annule."));
        if(graphic.Kind is "rect" or "textbox" or "table")PropertyGroup("Contenu",()=>{if(graphic.Kind=="table")PropertyContents.Children.Add(Hint($"{graphic.Table!.RowHeights.Count} lignes × {graphic.Table.ColumnWidths.Count} colonnes"));else PropertyContents.Children.Add(Hint(graphic.Content.Text));PropertyContents.Children.Add(Button(graphic.Kind=="table"?"Modifier le tableau…":"Écrire / formater le texte…",()=>EditGraphicContent(graphic.Id)));});
        PropertyGroup("Position et dimensions sur la page",()=>PagePositionFields(graphic.Id,"graphic",true));
        if(graphic.Kind=="table")PropertyGroup("Dimensions communes des cellules",()=>
        {
            var width=Field("tableColumnWidth","Largeur de toutes les colonnes · unités",graphic.Table!.ColumnWidths.Distinct().Count()==1?Number(graphic.Table.ColumnWidths[0]):"");var height=Field("tableRowHeight","Hauteur de toutes les lignes · unités",graphic.Table.RowHeights.Distinct().Count()==1?Number(graphic.Table.RowHeights[0]):"");
            string CommonSize(bool horizontal){var sizes=horizontal?Editor.Sheet.Graphics.Single(g=>g.Id==graphic.Id).Table!.ColumnWidths:Editor.Sheet.Graphics.Single(g=>g.Id==graphic.Id).Table!.RowHeights;return sizes.Distinct().Count()==1?Number(sizes[0]):"";}
            DimensionText(width,text=>Editor.SetTableCellSize(graphic.Id,columnWidth:ReadNumber(text)),()=>CommonSize(true));DimensionText(height,text=>Editor.SetTableCellSize(graphic.Id,rowHeight:ReadNumber(text)),()=>CommonSize(false));PropertyContents.Children.Add(Hint("Entrée ou quitter le champ pour appliquer à toutes les cellules. Pour les tailles individuelles : Modifier le tableau."));
        });
        if(graphic.Kind!="image")PropertyGroup("Aspect",()=>{
            foreach(var(key,label,value)in new[]{("Color","Couleur du trait",graphic.Color),("Fill","Fond · vide = transparent",graphic.Fill)}.Where(item=>graphic.Kind!="line"||item.Item1!="Fill")){var input=Field("graphic"+key,label,value);LiveText(input,text=>Editor.Change(()=>typeof(SheetGraphic).GetProperty(key)!.SetValue(Editor.Sheet.Graphics.Single(g=>g.Id==graphic.Id),text),graphic.Id+":"+key));}
            var style=PropertyChoice("Type de trait",graphic.LineStyle,[("solid","Continu"),("dashed","Tirets"),("dotted","Pointillés"),("dashdot","Mixte")]);style.SelectionChanged+=(_,_)=>LiveProperty(style,()=>Editor.Change(()=>Editor.Sheet.Graphics.Single(g=>g.Id==graphic.Id).LineStyle=(string)style.SelectedValue));
            var thickness=Field("graphicThickness","Épaisseur",Number(graphic.Thickness));LiveText(thickness,text=>Editor.Change(()=>Editor.Sheet.Graphics.Single(g=>g.Id==graphic.Id).Thickness=ReadNumber(text),graphic.Id+":thickness"));
        });
    }
    private void NoteStyleProperties(Annotation note)
    {
        PropertyGroup("Aspect du texte",()=>{
            TypographyProperties(note.Bold,note.Italic,(bold,italic)=>Editor.Change(()=>{var live=Editor.Sheet.Notes.Single(n=>n.Id==note.Id);var value=RichText.Note(live);RichText.Uniform(value,bold,italic);RichText.SetNote(live,value);}),"NoteText");PropertyContents.Children.Add(Button("Formater une partie du texte…",()=>EditNoteFormatting(note.Id)));
            foreach(var(key,label,value)in new[]{("FontSize","Taille",Number(note.FontSize)),("Color","Couleur #RRGGBB",note.Color),("Rotation","Angle",Number(note.Rotation))}){var input=Field("note"+key,label,value);LiveText(input,text=>Editor.Change(()=>typeof(Annotation).GetProperty(key)!.SetValue(Editor.Sheet.Notes.Single(n=>n.Id==note.Id),key=="Color"?text:ReadNumber(text)),note.Id+":"+key));}
            var align=PropertyChoice("Alignement",note.Align,[("left","Gauche"),("center","Centré"),("right","Droite")]);align.SelectionChanged+=(_,_)=>LiveProperty(align,()=>Editor.Change(()=>Editor.Sheet.Notes.Single(n=>n.Id==note.Id).Align=(string)align.SelectedValue));
            var linked=new CheckBox{Content="Texte lié aux champs du projet",IsChecked=note.ResolveFields,Margin=new(0,8,0,8)};PropertyContents.Children.Add(linked);RegisterPropertyControl(linked);linked.Click+=(_,_)=>LiveProperty(linked,()=>Editor.Change(()=>Editor.Sheet.Notes.Single(n=>n.Id==note.Id).ResolveFields=linked.IsChecked==true));PropertyContents.Children.Add(Hint("Champs : {project.title}, {project.customer}, {project.documentNumber}, {project.date}, {project.revision}, {project.author}."));
        });
    }
    private ComboBox PropertyChoice(string label,string value,(string Key,string Label)[] choices)
    {
        PropertyContents.Children.Add(Hint(label));var control=new ComboBox{SelectedValuePath="Tag",Margin=new(0,3,0,8)};foreach(var choice in choices)control.Items.Add(new ComboBoxItem{Content=choice.Label,Tag=choice.Key});control.SelectedValue=value;PropertyContents.Children.Add(control);RegisterPropertyControl(control);return control;
    }
}
