using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public sealed class TerminalStrip
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "X1";
    public string Location { get; set; } = "";
    public List<TerminalAssignment> Terminals { get; set; } = [];
    public List<TerminalBridge> Bridges { get; set; } = [];
}
public sealed class TerminalAssignment
{
    public string ComponentId { get; set; } = "";
    public string Number { get; set; } = "1";
    public int Level { get; set; } = 1;
    public string InternalPort { get; set; } = "1";
    public string ExternalPort { get; set; } = "2";
    public string Comment { get; set; } = "";
}
public sealed class TerminalBridge
{
    public string FromComponent { get; set; } = "";
    public string FromPort { get; set; } = "1";
    public string ToComponent { get; set; } = "";
    public string ToPort { get; set; } = "1";
}
public sealed record TerminalPlanRow(string Number,int Level,string Tag,string Folio,string Internal,string External,string Comment);
public static class TerminalStrips
{
    public static void Validate(Project p)
    {
        if(p.TerminalStrips is null||p.TerminalStrips.Count>200||p.TerminalStrips.Select(t=>t?.Id).Distinct().Count()!=p.TerminalStrips.Count)throw new InvalidDataException("Liste de borniers invalide.");
        var components=p.Sheets.SelectMany(s=>s.Components).ToDictionary(c=>c.Id); var assigned=new HashSet<string>();
        foreach(var strip in p.TerminalStrips)
        {
            if(strip is null||string.IsNullOrWhiteSpace(strip.Id)||strip.Id.Length>100||string.IsNullOrWhiteSpace(strip.Name)||strip.Name.Length>80||strip.Location is null||strip.Location.Length>2000||strip.Terminals is null||strip.Bridges is null||strip.Terminals.Count>5000||strip.Bridges.Count>5000)throw new InvalidDataException("Bornier : nom et contenu invalides.");
            var numbers=new HashSet<string>();
            foreach(var t in strip.Terminals)
            {
                if(t is null||!components.TryGetValue(t.ComponentId,out var c)||!assigned.Add(t.ComponentId+":"+string.Join(":",new[]{t.InternalPort,t.ExternalPort}.Order(StringComparer.Ordinal)))||t.Number is null||t.Number.Length is <1 or >80||t.Level is <1 or >16||!numbers.Add(t.Number+":"+t.Level)||t.Comment is null||t.Comment.Length>2000||t.InternalPort==t.ExternalPort||!c.Definition.InternalConnections.Any(pair=>pair.From==t.InternalPort&&pair.To==t.ExternalPort||pair.To==t.InternalPort&&pair.From==t.ExternalPort))throw new InvalidDataException("Bornes : affectation unique, numéro/niveau unique et deux ports avec continuité déclarée requis.");
            }
            foreach(var bridge in strip.Bridges) { if(bridge is null||bridge.FromComponent==bridge.ToComponent&&bridge.FromPort==bridge.ToPort||!strip.Terminals.Any(t=>t.ComponentId==bridge.FromComponent&&(t.InternalPort==bridge.FromPort||t.ExternalPort==bridge.FromPort))||!strip.Terminals.Any(t=>t.ComponentId==bridge.ToComponent&&(t.InternalPort==bridge.ToPort||t.ExternalPort==bridge.ToPort)))throw new InvalidDataException("Pontage : sélectionner des ports affectés distincts du même bornier."); }
        }
        foreach(var s in p.Sheets) if(s.TerminalPlanId.Length>0&&!p.TerminalStrips.Any(t=>t.Id==s.TerminalPlanId))throw new InvalidDataException("Plan de bornier absent.");
    }
    public static List<TerminalPlanRow> Rows(Project p,TerminalStrip strip)
    {
        var points=p.Sheets.SelectMany(s=>s.Components.Select(c=>(Sheet:s,Component:c))).ToDictionary(x=>x.Component.Id);var wires=p.Sheets.SelectMany(s=>s.Wires).ToList();
        string Ends(string id,string port)=>string.Join(" ; ",wires.Where(w=>w.From==new Endpoint(id,port)||w.To==new Endpoint(id,port)).Select(w=>{var end=w.From.ComponentId==id&&w.From.PortId==port?w.To:w.From;var c=points[end.ComponentId].Component;return $"{c.Tag}:{c.PortLabels.GetValueOrDefault(end.PortId,end.PortId)} [{w.Number}]";}));
        return strip.Terminals.Select(t=>{var point=points[t.ComponentId];return new TerminalPlanRow(t.Number,t.Level,point.Component.Tag,SheetZones.Address(p,point.Sheet,point.Component),Ends(t.ComponentId,t.InternalPort),Ends(t.ComponentId,t.ExternalPort),t.Comment);}).ToList();
    }
    private static readonly double[] Xs=[50,120,185,300,470,790,1110], Widths=[70,65,115,170,320,320,230];
    private static List<List<string[][]>> Pages(Project p,TerminalStrip strip)
    {
        List<List<string[][]>> pages=[[]];double used=0;
        void Add(string[] values) { var lines=values.Select((value,col)=>BomFolios.Wrap(value,Widths[col]-10).ToArray()).ToArray();int count=Math.Max(1,lines.Max(l=>l.Length));for(int offset=0;offset<count;offset+=12){int take=Math.Min(12,count-offset);double height=Math.Max(30,take*13+10);if(used+height>650){pages.Add([]);used=0;}pages[^1].Add(lines.Select(l=>l.Skip(offset).Take(take).ToArray()).ToArray());used+=height;} }
        foreach(var row in Rows(p,strip))Add([row.Number,row.Level.ToString(),row.Tag,row.Folio,row.Internal,row.External,row.Comment]);
        if(strip.Bridges.Count>0){pages.Add([]);used=0;foreach(var bridge in strip.Bridges){var a=strip.Terminals.First(t=>t.ComponentId==bridge.FromComponent&&(t.InternalPort==bridge.FromPort||t.ExternalPort==bridge.FromPort));var b=strip.Terminals.First(t=>t.ComponentId==bridge.ToComponent&&(t.InternalPort==bridge.ToPort||t.ExternalPort==bridge.ToPort));Add(["Pont",a.Level.ToString(),a.Number+" ↔ "+b.Number,"",a.Number+":"+bridge.FromPort,b.Number+":"+bridge.ToPort,""]);}}
        return pages;
    }
    public static int PageCount(Project p,TerminalStrip strip)=>Pages(p,strip).Count;
    public static List<Ink> Scene(Project p,Sheet sheet)
    {
        var strip=p.TerminalStrips.Single(t=>t.Id==sheet.TerminalPlanId);var pages=Pages(p,strip);List<Ink> scene=[new("text",60,50,Text:"Plan de bornier "+strip.Name+" · "+strip.Location,Size:22)];scene.AddRange(SheetZones.Frame(p,sheet));scene.AddRange(DocumentModel.Scene(p,sheet));
        string[] headers=["Borne","Niveau","Repère","Folio / zone","Interne","Externe","Commentaire"];for(int col=0;col<headers.Length;col++){scene.Add(new("rect",Xs[col],100,Widths[col],30));scene.Add(new("text",Xs[col]+5,108,Text:headers[col],Size:11));}
        double y=130;if(sheet.TerminalPlanPage<pages.Count)foreach(var row in pages[sheet.TerminalPlanPage]){double height=Math.Max(30,row.Max(l=>l.Length)*13+10);for(int col=0;col<headers.Length;col++){scene.Add(new("rect",Xs[col],y,Widths[col],height));scene.Add(new("text",Xs[col]+5,y+5,Text:string.Join("\n",row[col]),Size:10));}y+=height;}
        else scene.Add(new("text",60,150,Text:"Ce plan a changé. Régénérer ses folios depuis Projet → Borniers.",Size:14));
        scene.Add(new("text",1050,65,Text:$"Page {sheet.TerminalPlanPage+1}/{pages.Count}",Size:12));return scene;
    }

}
public sealed partial class Editor
{
    public TerminalStrip PutTerminalStrip(TerminalStrip strip)
    {
        Change(()=>{int at=Project.TerminalStrips.FindIndex(t=>t.Id==strip.Id);if(at>=0)Project.TerminalStrips[at]=DocumentModel.Copy(strip);else Project.TerminalStrips.Add(DocumentModel.Copy(strip));});return Project.TerminalStrips.Single(t=>t.Id==strip.Id);
    }
    public List<Sheet> GenerateTerminalPlan(string id)
    {
        var strip=Project.TerminalStrips.Single(t=>t.Id==id);List<Sheet> pages=[];Change(()=> { var existing=Project.Sheets.Where(s=>s.TerminalPlanId==id).ToList();int at=existing.Count>0?Project.Sheets.IndexOf(existing[0]):Project.Sheets.Count;Project.Sheets.RemoveAll(s=>s.TerminalPlanId==id);for(int i=0;i<TerminalStrips.PageCount(Project,strip);i++){var sheet=new Sheet{Id=i<existing.Count?existing[i].Id:Guid.NewGuid().ToString("N"),Title="Plan bornier "+strip.Name,TitleBlockId="technical",TerminalPlanId=id,TerminalPlanPage=i};Project.Sheets.Insert(at+i,sheet);pages.Add(sheet);} });SelectSheet(pages[0].Id);return pages;
    }
    internal void RemoveTerminalAssignments(HashSet<string> ids)
    {
        foreach(var cabinet in Project.Cabinets)cabinet.Items.RemoveAll(i=>ids.Contains(i.ComponentId));
        foreach(var strip in Project.TerminalStrips){strip.Terminals.RemoveAll(t=>ids.Contains(t.ComponentId));strip.Bridges.RemoveAll(b=>ids.Contains(b.FromComponent)||ids.Contains(b.ToComponent));}
    }
}
public sealed partial class MainWindow
{
    private sealed record TerminalChoice(string Id,string Name);
    private void TerminalStripsDialog()
    {
        Canvas.CancelDrag();var dialog=DocumentWindow("Borniers · affectations et plans de raccordement",1200,850);var root=new DockPanel{Margin=new(18)};dialog.Content=root;var top=new WrapPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);var choose=new ComboBox{Width=250,DisplayMemberPath="Name",SelectedValuePath="Id",Margin=new(0,0,10,10)};top.Children.Add(choose);var name=new TextBox{Width=120,Padding=new(5),Margin=new(0,0,10,10)};top.Children.Add(name);var location=new TextBox{Width=250,Padding=new(5),ToolTip="Localisation du bornier",Margin=new(0,0,10,10)};top.Children.Add(location);
        var bottom=new WrapPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);var tabs=new TabControl();root.Children.Add(tabs);var assignments=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false};var bridges=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false};
        var options=Editor.Project.Sheets.SelectMany(s=>s.Components.Where(c=>c.Definition.InternalConnections.Count>0).Select(c=>new TerminalChoice(c.Id,c.Tag+" · "+DocumentModel.PageNumber(Editor.Project,s)))).ToList();
        void ComponentColumn(DataGrid grid,string header,string path){grid.Columns.Add(new DataGridComboBoxColumn{Header=header,ItemsSource=options,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValueBinding=new System.Windows.Data.Binding(path),Width=170});}
        void Column(DataGrid grid,string header,string path,double width){grid.Columns.Add(new DataGridTextColumn{Header=header,Binding=new System.Windows.Data.Binding(path),Width=width});}
        ComponentColumn(assignments,"Symbole du schéma","ComponentId");foreach(var (header,path,width) in new[]{("Borne","Number",80d),("Niveau","Level",65d),("Port interne","InternalPort",100d),("Port externe","ExternalPort",100d),("Commentaire","Comment",250d)})Column(assignments,header,path,width);
        ComponentColumn(bridges,"De la borne","FromComponent");Column(bridges,"Port","FromPort",90);ComponentColumn(bridges,"Vers la borne","ToComponent");Column(bridges,"Port","ToPort",90);
        var preview=new DataGrid{AutoGenerateColumns=true,IsReadOnly=true,CanUserAddRows=false};tabs.Items.Add(new TabItem{Header="Bornes et ports",Content=assignments});tabs.Items.Add(new TabItem{Header="Pontages",Content=bridges});tabs.Items.Add(new TabItem{Header="Plan de raccordement",Content=preview});TerminalStrip draft=new();
        void Display(){name.Text=draft.Name;location.Text=draft.Location;assignments.ItemsSource=null;assignments.ItemsSource=draft.Terminals;bridges.ItemsSource=null;bridges.ItemsSource=draft.Bridges;}
        void Read(){assignments.CommitEdit(DataGridEditingUnit.Cell,true);assignments.CommitEdit(DataGridEditingUnit.Row,true);bridges.CommitEdit(DataGridEditingUnit.Cell,true);bridges.CommitEdit(DataGridEditingUnit.Row,true);draft.Name=name.Text;draft.Location=location.Text;}
        void ReloadChoices(){choose.ItemsSource=Editor.Project.TerminalStrips.Select(t=>new TerminalChoice(t.Id,t.Name)).ToList();}
        choose.SelectionChanged+=(_,_)=>{if(choose.SelectedValue is string id){draft=DocumentModel.Copy(Editor.Project.TerminalStrips.Single(t=>t.Id==id));Display();}};
        top.Children.Add(DocButton("Nouveau bornier",dialog,()=>{draft=new();choose.SelectedIndex=-1;Display();}));
        bottom.Children.Add(DocButton("Ajouter la sélection du schéma",dialog,()=>{Read();foreach(var c in Editor.Sheet.Components.Where(c=>Canvas.Selected.Contains(c.Id)&&c.Definition.InternalConnections.Count>0))if(!draft.Terminals.Any(t=>t.ComponentId==c.Id))draft.Terminals.Add(new(){ComponentId=c.Id,Number=(draft.Terminals.Count+1).ToString(),InternalPort=c.Definition.Ports[0].Id,ExternalPort=c.Definition.Ports[1].Id});Display();}));
        bottom.Children.Add(DocButton("Ajouter un niveau du symbole",dialog,()=>{Read();if(assignments.SelectedItem is not TerminalAssignment selected)throw new ArgumentException("Choisir une borne.");var component=Editor.Project.Sheets.SelectMany(s=>s.Components).Single(c=>c.Id==selected.ComponentId);var pair=component.Definition.InternalConnections.FirstOrDefault(pair=>!draft.Terminals.Any(t=>t.ComponentId==component.Id&&(t.InternalPort==pair.From&&t.ExternalPort==pair.To||t.InternalPort==pair.To&&t.ExternalPort==pair.From)))??throw new ArgumentException("Le symbole n’a pas d’autre étage électrique déclaré. Affecter un autre symbole de borne avec le même numéro et un niveau différent.");draft.Terminals.Add(new(){ComponentId=component.Id,Number=selected.Number,Level=selected.Level+1,InternalPort=pair.From,ExternalPort=pair.To});Display();}));
        bottom.Children.Add(DocButton("Retirer la borne choisie",dialog,()=>{if(assignments.SelectedItem is TerminalAssignment t){draft.Terminals.Remove(t);draft.Bridges.RemoveAll(b=>b.FromComponent==t.ComponentId||b.ToComponent==t.ComponentId);Display();}}));
        bottom.Children.Add(DocButton("Ajouter un pontage",dialog,()=>{if(draft.Terminals.Count<2)throw new ArgumentException("Affecter deux bornes d’abord.");draft.Bridges.Add(new(){FromComponent=draft.Terminals[0].ComponentId,FromPort=draft.Terminals[0].InternalPort,ToComponent=draft.Terminals[1].ComponentId,ToPort=draft.Terminals[1].InternalPort});Display();}));
        bottom.Children.Add(DocButton("Retirer le pontage choisi",dialog,()=>{if(bridges.SelectedItem is TerminalBridge b){draft.Bridges.Remove(b);Display();}}));
        bottom.Children.Add(DocButton("Enregistrer le bornier",dialog,()=>{Read();draft=DocumentModel.Copy(Editor.PutTerminalStrip(draft));ReloadChoices();choose.SelectedValue=draft.Id;preview.ItemsSource=TerminalStrips.Rows(Editor.Project,draft);}));
        bottom.Children.Add(DocButton("Générer les folios du plan",dialog,()=>{Read();Editor.PutTerminalStrip(draft);Editor.GenerateTerminalPlan(draft.Id);dialog.Close();}));
        bottom.Children.Add(DocButton("Supprimer le bornier",dialog,()=>{if(!Editor.Project.TerminalStrips.Any(t=>t.Id==draft.Id))return;Editor.Change(()=>{Editor.Project.Sheets.RemoveAll(s=>s.TerminalPlanId==draft.Id);Editor.Project.TerminalStrips.RemoveAll(t=>t.Id==draft.Id);});draft=new();ReloadChoices();Display();}));
        ReloadChoices();if(choose.Items.Count>0)choose.SelectedIndex=0;else Display();ShowDocumentDialog(dialog);
    }
}
