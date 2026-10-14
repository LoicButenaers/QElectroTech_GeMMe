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
    public void PrepareImage(string id,string data,double width,double height){pendingImage=(id,data,width,height);SetMode("graphicImage");}
    private void CancelGraphicGesture(){graphicStart=null;graphicPressed=false;graphicResize=null;}
    private bool GraphicClick(Point point)
    {
        if(Mode=="graphicImage"&&pendingImage is { } image)
        {
            SheetGraphic? created=null;Editor.Change(()=>{Editor.Project.Images.TryAdd(image.Id,image.Data);created=new(){Kind="image",ImageId=image.Id,X=Editor.Snap(point.X),Y=Editor.Snap(point.Y),Width=image.Width,Height=image.Height};Editor.Sheet.Graphics.Add(created);});SetMode("select");Select(created!.Id);return true;
        }
        if(Mode is not ("graphicRect" or "graphicEllipse" or "graphicLine"))return false;
        if(graphicStart is null){graphicStart=new(Editor.Snap(point.X),Editor.Snap(point.Y));graphicPressed=true;Editor.InteractionInProgress=true;CaptureMouse();Status?.Invoke("Glisser ou cliquer le second point · Échap annule");}
        else CompleteGraphic(point);InvalidateVisual();return true;
    }
    private void CompleteGraphic(Point point)
    {
        if(graphicStart is not Point start)return;string kind=Mode=="graphicLine"?"line":Mode=="graphicEllipse"?"ellipse":"rect";graphicStart=null;graphicPressed=false;Editor.InteractionInProgress=false;ReleaseMouseCapture();var g=Editor.AddGraphic(kind,start,point);SetMode("select");Select(g.Id);
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
        else{g.X=Math.Min(resize.Anchor.X,p.X);g.Y=Math.Min(resize.Anchor.Y,p.Y);g.Width=Math.Max(1,Math.Abs(p.X-resize.Anchor.X));g.Height=Math.Max(1,Math.Abs(p.Y-resize.Anchor.Y));}
        retainedScene?.GraphicsChanged([g.Id]);InvalidateVisual();
    }
    private string? HitGraphic(Point point)
    {
        double tolerance=7/Zoom;foreach(var g in Editor.Sheet.Graphics.AsEnumerable().Reverse())
        {
            var r=g.Bounds();if(g.Kind=="line"){if(Distance(point,new(g.X,g.Y),new(g.X+g.Width,g.Y+g.Height))<tolerance)return g.Id;continue;}
            if(g.Kind=="ellipse"){double norm=Math.Sqrt(Math.Pow((point.X-r.X-r.Width/2)/(r.Width/2),2)+Math.Pow((point.Y-r.Y-r.Height/2)/(r.Height/2),2));if(g.Fill.Length>0&&norm<=1||Math.Abs(norm-1)*Math.Min(r.Width,r.Height)/2<tolerance)return g.Id;continue;}
            if(g.Kind=="image"||g.Fill.Length>0){if(r.Contains(point))return g.Id;}else{var outer=r;outer.Inflate(tolerance,tolerance);var inner=r;inner.Inflate(-Math.Min(tolerance,r.Width/2),-Math.Min(tolerance,r.Height/2));if(outer.Contains(point)&&!inner.Contains(point))return g.Id;}
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
        properties.Children.Add(new TextBlock{Text=graphic.Kind=="image"?"Logo / image":graphic.Kind=="line"?"Ligne":graphic.Kind=="ellipse"?"Ellipse":"Rectangle",FontSize=19,Margin=new(0,15,0,8)});properties.Children.Add(Hint("Déplacer avec la souris ; redimensionner par les poignées. Ctrl+Z annule."));
        PropertyGroup("Position et dimensions sur la page",()=>PagePositionFields(graphic.Id,"graphic",true));
        if(graphic.Kind!="image")PropertyGroup("Aspect",()=>{
            foreach(var(key,label,value)in new[]{("Color","Couleur du trait",graphic.Color),("Fill","Fond · vide = transparent",graphic.Fill)}.Where(item=>graphic.Kind!="line"||item.Item1!="Fill")){var input=Field("graphic"+key,label,value);LiveText(input,text=>Editor.Change(()=>typeof(SheetGraphic).GetProperty(key)!.SetValue(Editor.Sheet.Graphics.Single(g=>g.Id==graphic.Id),text),graphic.Id+":"+key));}
            var style=PropertyChoice("Type de trait",graphic.LineStyle,[("solid","Continu"),("dashed","Tirets"),("dotted","Pointillés"),("dashdot","Mixte")]);style.SelectionChanged+=(_,_)=>LiveProperty(style,()=>Editor.Change(()=>Editor.Sheet.Graphics.Single(g=>g.Id==graphic.Id).LineStyle=(string)style.SelectedValue));
            var thickness=Field("graphicThickness","Épaisseur",Number(graphic.Thickness));LiveText(thickness,text=>Editor.Change(()=>Editor.Sheet.Graphics.Single(g=>g.Id==graphic.Id).Thickness=ReadNumber(text),graphic.Id+":thickness"));
        });
    }
    private void NoteStyleProperties(Annotation note)
    {
        PropertyGroup("Aspect du texte",()=>{
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
