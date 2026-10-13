using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GeMMeElec;

public sealed partial class SheetCanvas
{
    private Point? bandStart;
    internal bool BandClickForTest(Point point)=>HandleBandClick(point);
    public string? InsertionWireId {get;set;}
    public string InsertionSymbol {get;set;}="terminal";
    public string InsertionInput {get;set;}="1";
    public string InsertionOutput {get;set;}="2";
    public event Action<Point,Point,string>? BandRequested;
    private void CancelBand(){bandStart=null;}
    private bool HandleBandClick(Point location)
    {
        if(Mode=="insertOnWire"){if(InsertionWireId is null)throw new ArgumentException("Sélectionner un fil.");var c=Editor.InsertOnWire(InsertionWireId,InsertionSymbol,location,InsertionInput,InsertionOutput);SetMode("select");Select(c.Id);return true;}
        if(Mode is not ("terminalLine" or "cableLine"))return false;
        if(bandStart is null){bandStart=new(Editor.Snap(location.X),Editor.Snap(location.Y));Editor.InteractionInProgress=true;Status?.Invoke("Premier point fixé · cliquer le second point · Échap annule");}
        else{var start=bandStart.Value;bandStart=null;Editor.InteractionInProgress=false;string mode=Mode;SetMode("select");BandRequested?.Invoke(start,new(Editor.Snap(location.X),Editor.Snap(location.Y)),mode);}InvalidateVisual();return true;
    }
    private void RenderBand(DrawingContext dc)
    {
        if(bandStart is not Point a)return;var b=new Point(Editor.Snap(pointer.X),Editor.Snap(pointer.Y));dc.DrawLine(new Pen(Drawing.Brush("#007F86"),2/Zoom),a,b);
        if((b-a).Length<10)return;if(routeCache is null){var index=Editor.Sheet.Components.ToDictionary(c=>c.Id);routeCache=Editor.Sheet.Wires.ToDictionary(w=>w.Id,w=>w.Route(Editor.Sheet,index));}foreach(var hit in WireIntersections.Across(Editor.Sheet,a,b,routeCache))dc.DrawEllipse(Brushes.White,new Pen(Drawing.Brush("#007F86"),1.5/Zoom),hit.Point,6/Zoom,6/Zoom);
    }
}
public sealed partial class MainWindow
{
    private void BandDialog(Point a,Point b,string mode)
    {
        var hits=WireIntersections.Across(Editor.Sheet,a,b);if(hits.Count==0)throw new ArgumentException("La ligne ne traverse aucun fil.");bool terminal=mode=="terminalLine";
        var dialog=DocumentWindow(terminal?"Bornier en travers des fils":"Définition du câble et section",660,560);var panel=new StackPanel{Margin=new(20)};dialog.Content=panel;panel.Children.Add(Hint(hits.Count+" intersection(s) · "+(terminal?"Chaque borne conserve la continuité et le numéro du fil.":"La ligne regroupe les conducteurs sans créer de liaison électrique.")));
        var name=DocField(panel,terminal?"Repère du bornier":"Repère du câble",terminal?"X"+(Editor.Project.TerminalStrips.Count+1):"W"+(Editor.Project.Cables.Count+1));var first=DocField(panel,terminal?"Premier numéro de borne":"Section commune en mm² · vide si inconnue",terminal?"1":"");var level=DocField(panel,terminal?"Niveau 1 à 16":"Type de câble · facultatif",terminal?"1":"");
        var grid=new DataGrid{IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,Height=150,ItemsSource=hits.Select(h=>new{Fil=Editor.Sheet.Wires.Single(w=>w.Id==h.WireId).Number,X=h.Point.X,Y=h.Point.Y}).ToList(),Margin=new(0,10,0,10)};panel.Children.Add(grid);
        panel.Children.Add(DocButton("Créer en une action",dialog,()=>{if(terminal){if(!int.TryParse(first.Text,out int n)||!int.TryParse(level.Text,out int l))throw new ArgumentException("Numéro et niveau entiers requis.");Editor.InsertTerminalLine(a,b,name.Text,n,l);}else Editor.MarkCableLine(a,b,name.Text,first.Text.Trim().Length==0?null:ReadNumber(first.Text),level.Text);dialog.Close();}));ShowDocumentDialog(dialog);
    }
    private void InsertSymbolOnWireDialog()
    {
        var wire=Editor.Sheet.Wires.FirstOrDefault(w=>Canvas.Selected.Contains(w.Id))??throw new ArgumentException("Sélectionner un fil.");var dialog=DocumentWindow("Insérer un symbole sur le fil",580,450);var panel=new StackPanel{Margin=new(20)};dialog.Content=panel;
        panel.Children.Add(Hint("Choisir les deux ports raccordés, puis cliquer la position sur le fil. Une borne de passage conserve son numéro ; un appareil crée un nouveau potentiel à sa sortie."));
        var symbols=new ComboBox{ItemsSource=Symbols.All.Values.Concat(Editor.Project.CustomSymbols.Values).Where(d=>d.Ports.Count>=2&&d.Id!="junction").OrderBy(d=>d.Name).ToList(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue="terminal",Margin=new(0,10,0,10)};panel.Children.Add(symbols);panel.Children.Add(new TextBlock{Text="Port d’entrée"});var input=new ComboBox{Margin=new(0,5,0,10)};panel.Children.Add(input);panel.Children.Add(new TextBlock{Text="Port de sortie"});var output=new ComboBox{Margin=new(0,5,0,10)};panel.Children.Add(output);
        void Ports(){if(symbols.SelectedValue is not string id)return;var d=CustomSymbols.Resolve(Editor.Project,id);input.ItemsSource=output.ItemsSource=d.Ports.Select(p=>p.Id).ToList();input.SelectedIndex=0;output.SelectedIndex=1;}symbols.SelectionChanged+=(_,_)=>Ports();Ports();
        panel.Children.Add(DocButton("Cliquer l’emplacement sur le fil",dialog,()=>{if(symbols.SelectedValue is not string id||input.SelectedItem is not string a||output.SelectedItem is not string b||a==b)throw new ArgumentException("Deux ports distincts requis.");Canvas.InsertionWireId=wire.Id;Canvas.InsertionSymbol=id;Canvas.InsertionInput=a;Canvas.InsertionOutput=b;Canvas.SetMode("insertOnWire");dialog.Close();}));ShowDocumentDialog(dialog);
    }
    private void SmartRouteSelection(){var ids=Editor.Sheet.Wires.Where(w=>Canvas.Selected.Contains(w.Id)).Select(w=>w.Id).ToList();if(ids.Count==0)throw new ArgumentException("Sélectionner les fils à router.");int count=Editor.RouteWires(ids);status.Text=count+" fil(s) routé(s) · Ctrl+Z annule";}
    private void CablesDialog()
    {
        var dialog=DocumentWindow("Câbles sections et conducteurs",1100,700);var root=new DockPanel{Margin=new(20)};dialog.Content=root;var footer=new WrapPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);var hint=Hint("La section est une donnée saisie, sans dimensionnement automatique. La ligne de câble se trace depuis Insérer → Ligne de câble.");DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);var draft=DocumentModel.Copy(Editor.Project.Cables);var grid=new DataGrid{ItemsSource=draft,AutoGenerateColumns=false,CanUserAddRows=false};root.Children.Add(grid);
        foreach(var(label,path,width)in new[]{("Repère","Name",160d),("Type","Type",250d),("Section mm²","SectionMm2",130d),("Commentaire","Comment",400d)})grid.Columns.Add(new DataGridTextColumn{Header=label,Binding=new System.Windows.Data.Binding(path),Width=width});
        footer.Children.Add(DocButton("Enregistrer",dialog,()=>{grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);Editor.Change(()=>Editor.Project.Cables=DocumentModel.Copy(draft));dialog.Close();}));footer.Children.Add(DocButton("Supprimer le câble choisi",dialog,()=>{if(grid.SelectedItem is CableDefinition cable){Editor.DeleteCable(cable.Id);draft=DocumentModel.Copy(Editor.Project.Cables);grid.ItemsSource=draft;}}));footer.Children.Add(DocButton("Fermer",dialog,dialog.Close));ShowDocumentDialog(dialog);
    }
}
