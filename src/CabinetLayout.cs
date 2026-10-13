using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GeMMeElec;

public sealed class CabinetLayout
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Armoire 1";
    public double Width { get; set; } = 600;
    public double Height { get; set; } = 800;
    public List<CabinetItem> Items { get; set; } = [];
}
public sealed class CabinetItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "device";
    public string ComponentId { get; set; } = "";
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 50;
    public double Height { get; set; } = 80;
    public int Rotation { get; set; }
    public string DimensionSource { get; set; } = "Saisie utilisateur";
    public Rect Bounds() => new(X,Y,Rotation%180==0?Width:Height,Rotation%180==0?Height:Width);
}
public sealed record CabinetFinding(string ItemId,string Message);
public static class Cabinets
{
    public static void Validate(Project p)
    {
        if(p.Cabinets is null||p.Cabinets.Count>100||p.Cabinets.Select(c=>c?.Id).Distinct().Count()!=p.Cabinets.Count)throw new InvalidDataException("Liste d’armoires invalide.");var components=p.Sheets.SelectMany(s=>s.Components).ToDictionary(c=>c.Id);
        foreach(var c in p.Cabinets)
        {
            if(c is null||string.IsNullOrWhiteSpace(c.Id)||c.Id.Length>100||string.IsNullOrWhiteSpace(c.Name)||c.Name.Length>200||!double.IsFinite(c.Width)||!double.IsFinite(c.Height)||c.Width is <50 or >10000||c.Height is <50 or >10000||c.Items is null||c.Items.Count>10000||c.Items.Select(i=>i?.Id).Distinct().Count()!=c.Items.Count)throw new InvalidDataException("Armoire : dimensions 50–10 000 mm et objets uniques requis.");
            foreach(var i in c.Items)if(i is null||string.IsNullOrWhiteSpace(i.Id)||i.Id.Length>100||i.Kind is not("device" or "rail" or "duct")||i.ComponentId is null||i.Kind=="device"&&!components.ContainsKey(i.ComponentId)||i.Kind!="device"&&i.ComponentId.Length>0||i.Name is null||i.Name.Length>200||i.DimensionSource is null||i.DimensionSource.Length is <1 or >2000||!new[]{i.X,i.Y,i.Width,i.Height}.All(double.IsFinite)||i.Width is <.1 or >10000||i.Height is <.1 or >10000||i.Rotation%90!=0||i.X<0||i.Y<0||i.Bounds().Right>c.Width||i.Bounds().Bottom>c.Height)throw new InvalidDataException("Implantation : appareil existant, dimensions connues et position dans l’armoire requis.");
        }
        foreach(var s in p.Sheets)if(s.CabinetPlanId.Length>0&&!p.Cabinets.Any(c=>c.Id==s.CabinetPlanId))throw new InvalidDataException("Plan d’armoire absent.");
    }
    public static List<CabinetFinding> Findings(Project p,CabinetLayout cabinet)
    {
        var result=new List<CabinetFinding>();var components=p.Sheets.SelectMany(s=>s.Components).ToDictionary(c=>c.Id);string Physical(CabinetItem i)=>components[i.ComponentId].CoilId.Length>0?components[i.ComponentId].CoilId:i.ComponentId;
        foreach(var group in cabinet.Items.Where(i=>i.Kind=="device").GroupBy(Physical).Where(g=>g.Count()>1))foreach(var i in group)result.Add(new(i.Id,"Appareil physique implanté plusieurs fois"));
        for(int a=0;a<cabinet.Items.Count;a++)for(int b=a+1;b<cabinet.Items.Count;b++){var x=cabinet.Items[a];var y=cabinet.Items[b];if(x.Kind=="rail"&&y.Kind!="duct"||y.Kind=="rail"&&x.Kind!="duct")continue;var overlap=Rect.Intersect(x.Bounds(),y.Bounds());if(!overlap.IsEmpty&&overlap.Width>.01&&overlap.Height>.01)result.Add(new(x.Id,"Chevauchement avec "+(y.Name.Length>0?y.Name:y.Kind)));}
        return result;
    }
    public static List<Ink> Scene(Project p,CabinetLayout cabinet)
    {
        var components=p.Sheets.SelectMany(s=>s.Components).ToDictionary(c=>c.Id);List<Ink> scene=[new("rect",0,0,cabinet.Width,cabinet.Height,Color:"#53677B")];
        foreach(var item in cabinet.Items){var r=item.Bounds();string color=item.Kind=="rail"?"#64748B":item.Kind=="duct"?"#8D7653":"#203348";scene.Add(new("rect",r.X,r.Y,r.Width,r.Height,Color:color));if(item.Kind=="rail")scene.Add(new("line",r.X,r.Y+r.Height/2,r.Right,r.Y+r.Height/2,Color:color));if(item.Kind=="duct")for(double y=r.Y+8;y<r.Bottom;y+=12)scene.Add(new("line",r.X,y,r.Right,y,Color:color));string text=item.Kind=="device"?components[item.ComponentId].Tag:item.Name;double font=Math.Clamp(Math.Min(r.Width/Math.Max(1,text.Length)*1.5,r.Height/3),3,14);scene.Add(new("text",r.X+3,r.Y+3,Text:text,Size:font,Color:color));}
        return scene;
    }
    private static List<string> Legend(Project p,CabinetLayout cabinet)
    {
        var components=p.Sheets.SelectMany(s=>s.Components).ToDictionary(c=>c.Id);List<string> lines=[];foreach(var item in cabinet.Items){string tag=item.Kind=="device"?components[item.ComponentId].Tag:item.Name;lines.AddRange(BomFolios.Wrap($"{tag} · {item.Width:0.##} × {item.Height:0.##} mm · {item.DimensionSource}",1200));lines.Add("");}return lines;
    }
    public static int PageCount(Project p,CabinetLayout cabinet)=>1+Math.Max(1,(Legend(p,cabinet).Count+41)/42);
    public static List<Ink> PlanScene(Project p,Sheet s)
    {
        var c=p.Cabinets.Single(c=>c.Id==s.CabinetPlanId);List<Ink> scene=[];scene.AddRange(SheetZones.Frame(p,s));scene.AddRange(DocumentModel.Scene(p,s));scene.Add(new("text",60,50,Text:$"Implantation {c.Name} · {c.Width:0.##} × {c.Height:0.##} mm",Size:22));
        if(s.CabinetPlanPage==0){double scale=Math.Min(1100/c.Width,650/c.Height);foreach(var ink in Scene(p,c)){bool line=ink.Kind=="line";scene.Add(ink with{X=60+ink.X*scale,Y=110+ink.Y*scale,A=line?60+ink.A*scale:ink.A*scale,B=line?110+ink.B*scale:ink.B*scale,Size=ink.Size*scale,Thickness=ink.Thickness*scale});}scene.Add(new("text",60,780,Text:"Dimensions et provenance des objets dans les folios suivants.",Size:11));}
        else {double y=120;foreach(var line in Legend(p,c).Skip((s.CabinetPlanPage-1)*42).Take(42)){scene.Add(new("text",60,y,Text:line,Size:11));y+=15;}}
        scene.Add(new("text",1150,80,Text:$"Page {s.CabinetPlanPage+1}/{PageCount(p,c)}",Size:11));return scene;
    }

}
public sealed class CabinetPreview(Project project, CabinetLayout draft):FrameworkElement
{
    public CabinetLayout Draft { get; private set; }=draft;
    public string? SelectedId { get; private set; }
    public event Action? Edited;
    private Point? drag;
    private Point origin;
    private double Scale=>Math.Max(.01,Math.Min((ActualWidth-40)/Draft.Width,(ActualHeight-40)/Draft.Height));
    public void Load(CabinetLayout value){Draft=value;SelectedId=null;InvalidateVisual();}
    private Point Model(Point p)=>new((p.X-20)/Scale,(p.Y-20)/Scale);
    protected override void OnRender(DrawingContext dc){dc.DrawRectangle(Brushes.White,null,new(0,0,ActualWidth,ActualHeight));if(!double.IsFinite(Draft.Width)||!double.IsFinite(Draft.Height)||Draft.Width<=0||Draft.Height<=0||Draft.Items.Any(i=>!new[]{i.X,i.Y,i.Width,i.Height}.All(double.IsFinite)||i.Width<=0||i.Height<=0)) {Drawing.Render(dc,[new("text",20,20,Text:"Corriger les dimensions pour afficher le plan.")]);return;}dc.PushTransform(new TranslateTransform(20,20));dc.PushTransform(new ScaleTransform(Scale,Scale));Drawing.Render(dc,Cabinets.Scene(project,Draft));if(SelectedId is string id&&Draft.Items.FirstOrDefault(i=>i.Id==id) is CabinetItem item){var r=item.Bounds();r.Inflate(2/Scale,2/Scale);dc.DrawRectangle(null,new Pen(Drawing.Brush("#007F86"),2/Scale),r);}dc.Pop();dc.Pop();}
    internal bool Begin(Point p){var item=Draft.Items.LastOrDefault(i=>i.Bounds().Contains(p));if(item is null)return false;SelectedId=item.Id;origin=new(item.X,item.Y);drag=p;InvalidateVisual();return true;}
    internal void Move(Point p){if(drag is not Point start||SelectedId is null)return;var item=Draft.Items.Single(i=>i.Id==SelectedId);var bounds=item.Bounds();if(bounds.Width>Draft.Width||bounds.Height>Draft.Height)return;var delta=p-start;item.X=Math.Clamp(Math.Round((origin.X+delta.X)/5)*5,0,Draft.Width-bounds.Width);item.Y=Math.Clamp(Math.Round((origin.Y+delta.Y)/5)*5,0,Draft.Height-bounds.Height);InvalidateVisual();Edited?.Invoke();}
    internal void End(bool cancel=false){if(cancel&&SelectedId is string id&&Draft.Items.FirstOrDefault(i=>i.Id==id) is CabinetItem item){item.X=origin.X;item.Y=origin.Y;}drag=null;ReleaseMouseCapture();InvalidateVisual();Edited?.Invoke();}
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){Focus();if(Begin(Model(e.GetPosition(this)))){CaptureMouse();e.Handled=true;}}
    protected override void OnMouseMove(MouseEventArgs e){if(drag is not null)Move(Model(e.GetPosition(this)));}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){if(drag is not null){Move(Model(e.GetPosition(this)));End();}}
    protected override void OnLostMouseCapture(MouseEventArgs e){if(drag is not null)End(true);}
    protected override void OnKeyDown(KeyEventArgs e){if(e.Key==Key.Escape&&drag is not null){End(true);e.Handled=true;}}
}
public sealed partial class Editor
{
    public CabinetLayout PutCabinet(CabinetLayout cabinet){Change(()=>{int at=Project.Cabinets.FindIndex(c=>c.Id==cabinet.Id);if(at>=0)Project.Cabinets[at]=DocumentModel.Copy(cabinet);else Project.Cabinets.Add(DocumentModel.Copy(cabinet));});return Project.Cabinets.Single(c=>c.Id==cabinet.Id);}
    public Sheet GenerateCabinetPlan(string id){var cabinet=Project.Cabinets.Single(c=>c.Id==id);List<Sheet> pages=[];Change(()=>{var previous=Project.Sheets.Where(s=>s.CabinetPlanId==id).ToList();int at=previous.Count>0?Project.Sheets.IndexOf(previous[0]):Project.Sheets.Count;Project.Sheets.RemoveAll(s=>s.CabinetPlanId==id);for(int i=0;i<Cabinets.PageCount(Project,cabinet);i++){var sheet=new Sheet{Id=i<previous.Count?previous[i].Id:Guid.NewGuid().ToString("N"),Title=i==0?"Implantation "+cabinet.Name:"Cotes "+cabinet.Name,CabinetPlanId=id,CabinetPlanPage=i,TitleBlockId="technical"};Project.Sheets.Insert(at+i,sheet);pages.Add(sheet);} });SelectSheet(pages[0].Id);return pages[0];}
}
public sealed partial class MainWindow
{
    private sealed record CabinetChoice(string Id,string Name);
    private void CabinetDialog()
    {
        Canvas.CancelDrag();var dialog=DocumentWindow("Implantation d’armoire · dimensions en millimètres",1380,900);var root=new DockPanel{Margin=new(18)};dialog.Content=root;var top=new WrapPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);var choose=new ComboBox{Width=200,DisplayMemberPath="Name",SelectedValuePath="Id",Margin=new(0,0,10,10)};top.Children.Add(choose);var name=new TextBox{Width=160,Padding=new(5),Margin=new(0,0,10,10)};top.Children.Add(name);var width=new TextBox{Width=80,Padding=new(5),ToolTip="Largeur armoire mm",Margin=new(0,0,10,10)};top.Children.Add(width);var height=new TextBox{Width=80,Padding=new(5),ToolTip="Hauteur armoire mm",Margin=new(0,0,10,10)};top.Children.Add(height);var footer=new WrapPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);var grid=new DataGrid{Width=520,AutoGenerateColumns=true,CanUserAddRows=false,Margin=new(0,0,12,0)};DockPanel.SetDock(grid,Dock.Left);root.Children.Add(grid);CabinetLayout draft=new();var preview=new CabinetPreview(Editor.Project,draft){Focusable=true};root.Children.Add(preview);
        grid.AutoGeneratingColumn+=(_,e)=>{if(e.PropertyName is "Id" or "ComponentId")e.Cancel=true;};preview.Edited+=()=>grid.Items.Refresh();
        void Display(){name.Text=draft.Name;width.Text=Number(draft.Width);height.Text=Number(draft.Height);grid.ItemsSource=null;grid.ItemsSource=draft.Items;preview.Load(draft);}
        void Read(){grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);draft.Name=name.Text;draft.Width=ReadNumber(width.Text);draft.Height=ReadNumber(height.Text);}
        void Choices(){choose.ItemsSource=Editor.Project.Cabinets.Select(c=>new CabinetChoice(c.Id,c.Name)).ToList();}
        choose.SelectionChanged+=(_,_)=>{if(choose.SelectedValue is string id){draft=DocumentModel.Copy(Editor.Project.Cabinets.Single(c=>c.Id==id));Display();}};
        top.Children.Add(DocButton("Nouvelle armoire",dialog,()=>{draft=new();choose.SelectedIndex=-1;Display();}));top.Children.Add(Hint("Vue de face · dimensions saisies ou sourcées · glisser les objets sur le plan"));
        footer.Children.Add(DocButton("Ajouter appareils sélectionnés",dialog,()=>{Read();var value=Prompt("Dimensions des appareils","Largeur ; hauteur en mm (valeurs connues)","");if(value is null)return;var values=value.Split(';');if(values.Length!=2)throw new ArgumentException("Saisir largeur ; hauteur.");double w=ReadNumber(values[0]),h=ReadNumber(values[1]);foreach(var c in Editor.Sheet.Components.Where(c=>Canvas.Selected.Contains(c.Id)&&!Junctions.Automatic(c)&&!CrossReferences.IsPoint(c)))if(!draft.Items.Any(i=>i.ComponentId==c.Id))draft.Items.Add(new(){ComponentId=c.Id,Name=c.Tag,Width=w,Height=h,DimensionSource="Saisie utilisateur"});Display();}));
        footer.Children.Add(DocButton("Ajouter depuis les dimensions de fiche",dialog,()=>{Read();var selected=Editor.Sheet.Components.Where(c=>Canvas.Selected.Contains(c.Id)).ToList();if(selected.Count==0)throw new ArgumentException("Sélectionner les appareils du schéma.");foreach(var c in selected){var f=ArticleFiches.For(c)??throw new ArgumentException(c.Tag+" : aucune fiche exacte associée.");if(f.Width is not double w||f.Height is not double h)throw new ArgumentException(c.Tag+" : dimensions manquantes dans la fiche ; les saisir explicitement.");if(!draft.Items.Any(i=>i.ComponentId==c.Id))draft.Items.Add(new(){ComponentId=c.Id,Name=c.Tag,Width=w,Height=h,DimensionSource=f.Source+" · "+f.Edition});}Display();}));
        foreach(var (kind,label) in new[]{("rail","Ajouter un rail"),("duct","Ajouter une goulotte")})footer.Children.Add(DocButton(label,dialog,()=>{Read();draft.Items.Add(new(){Kind=kind,Name=kind=="rail"?"Rail":"Goulotte",Width=Math.Min(draft.Width,300),Height=kind=="rail"?35:40,DimensionSource="Valeurs de dessin modifiables"});Display();}));
        footer.Children.Add(DocButton("Retirer l’objet",dialog,()=>{var item=grid.SelectedItem as CabinetItem??draft.Items.FirstOrDefault(i=>i.Id==preview.SelectedId);if(item is not null){draft.Items.Remove(item);Display();}}));
        footer.Children.Add(DocButton("Actualiser l’aperçu",dialog,()=>{Read();preview.Load(draft);}));footer.Children.Add(DocButton("Vérifier les chevauchements",dialog,()=>{Read();var findings=Cabinets.Findings(Editor.Project,draft);MessageBox.Show(dialog,findings.Count==0?"Aucun chevauchement ni appareil dupliqué.":string.Join("\n",findings.Select(f=>f.Message)),"Implantation");}));
        footer.Children.Add(DocButton("Enregistrer",dialog,()=>{Read();draft=DocumentModel.Copy(Editor.PutCabinet(draft));Choices();choose.SelectedValue=draft.Id;Display();}));footer.Children.Add(DocButton("Générer le folio",dialog,()=>{Read();Editor.PutCabinet(draft);Editor.GenerateCabinetPlan(draft.Id);dialog.Close();}));footer.Children.Add(DocButton("Supprimer l’armoire",dialog,()=>{Editor.Change(()=>{Editor.Project.Sheets.RemoveAll(s=>s.CabinetPlanId==draft.Id);Editor.Project.Cabinets.RemoveAll(c=>c.Id==draft.Id);});draft=new();Choices();Display();}));Choices();if(choose.Items.Count>0)choose.SelectedIndex=0;else Display();ShowDocumentDialog(dialog);
    }
}
