using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;

namespace GeMMeElec;

public sealed record ProjectImportResult(Project Project, List<string> Warnings)
{
    public string Report => $"{Project.Title}\n{Project.Sheets.Count} folio(s), {Project.Sheets.Sum(s=>s.Components.Count)} élément(s), {Project.Sheets.Sum(s=>s.Wires.Count)} conducteur(s).\n\n" + string.Join("\n", Warnings);
}
public static class ProjectImport
{
    internal static string A(XElement e,string name,string fallback="")=>(string?)e.Attribute(name)??fallback;
    internal static double N(XElement e,string name,double fallback=0)=>double.TryParse(A(e,name),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)?n:fallback;
    private static XDocument Xml(string path)
    {
        if(new FileInfo(path).Length>20_000_000)throw new InvalidDataException("Projet source limité à 20 Mo.");
        using var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20_000_000});return XDocument.Load(reader);
    }
    public static ProjectImportResult Qet(string path)
    {
        var doc=Xml(path);var root=doc.Root;if(root?.Name.LocalName!="project")throw new InvalidDataException("Le fichier ne contient pas un projet QElectroTech.");
        var p=new Project{Title=A(root,"title",Path.GetFileNameWithoutExtension(path)),Sheets=[]};var counts=new Dictionary<string,int>();
        void Warn(string reason){counts[reason]=counts.GetValueOrDefault(reason)+1;}
        var definitions=new Dictionary<string,XElement>();
        foreach(var def in root.Descendants("definition"))
        {
            var parent=def.Parent;if(parent?.Name!="element")continue;
            string key=string.Join('/',parent.Ancestors("category").Reverse().Select(c=>A(c,"name")).Append(A(parent,"name")));definitions[key]=def;
        }
        foreach(var diagram in root.Elements("diagram"))
        {
            var s=new Sheet{UseProjectZones=false,Title=A(diagram,"title","Folio importé"),Author=A(diagram,"author"),Date=A(diagram,"date"),ZoneColumns=Math.Clamp((int)N(diagram,"cols",20),1,40),ZoneRows=Math.Clamp((int)N(diagram,"rows",10),1,26)};p.Sheets.Add(s);
            string folio=A(diagram,"folio");if(!folio.Contains('%'))s.Number=folio;
            var terminals=new Dictionary<string,Endpoint>();
            int i=0;
            foreach(var element in diagram.Element("elements")?.Elements("element")??[])
            {
                i++;string type=A(element,"type");string key=type.StartsWith("embed://")?type[8..]:type;
                definitions.TryGetValue(key,out var def);var parts=new List<Shape>();
                if(def is not null)foreach(var shape in def.Element("description")?.Elements()??[])ParseShape(shape,parts,Warn);
                else {parts.Add(new("rect",-20,-20,40,40));parts.Add(new("text",-15,-7,12,Text:"?"));Warn("Symbole absent de la collection embarquée : bloc de remplacement");}
                var ports=new List<Port>();var sourcePorts=(element.Element("terminals")?.Elements("terminal")??[]).ToArray();
                for(int n=0;n<sourcePorts.Length;n++)ports.Add(new((n+1).ToString(),N(sourcePorts[n],"x"),N(sourcePorts[n],"y")));
                var names=def?.Element("names")?.Elements("name");string name=names?.FirstOrDefault(x=>A(x,"lang")=="fr")?.Value??names?.FirstOrDefault()?.Value??Path.GetFileName(key);
                var texts=(element.Element("inputs")?.Elements("input")??[]).Select(x=>new Shape("text",N(x,"userx",N(x,"x")),N(x,"usery",N(x,"y"))-N(x,"size",9),N(x,"size",9),Text:A(x,"text"))).ToList();
                foreach(var text in element.Element("dynamic_texts")?.Elements("dynamic_elmt_text")??[])texts.Add(new("text",N(text,"x"),N(text,"y"),10,Text:text.Element("text")?.Value??""){Rotation=N(text,"rotation")});
                var infos=element.Element("elementInformations")?.Elements("elementInformation")??[];
                string Info(string field)=>infos.FirstOrDefault(x=>A(x,"name")==field)?.Value??"";
                string tag=Info("label");if(tag.Length==0)tag=texts.Select(t=>t.Text).FirstOrDefault(t=>Regex.IsMatch(t,@"^[-+=]?[A-Za-z]{1,4}\d[\w.\-]*$"))??"IMP"+i;
                var c=new Component{Symbol="custom-"+Guid.NewGuid().ToString("N"),ShowPortLabels=false,Tag=tag,Description=name,X=N(element,"x"),Y=N(element,"y"),Rotation=(int)N(element,"orientation")*90,Brand=Info("manufacturer"),Reference=Info("manufacturer-reference"),TagPlacement=new(){Visible=false},DetailPlacement=new(){Visible=false}};
                if(A(element,"mirror") is "true" or "1"){Warn("Élément miroir : vérifier l’orientation et les bornes");}
                // QET stores these terminal coordinates in element-local coordinates.
                for(int n=0;n<sourcePorts.Length;n++){var id=A(sourcePorts[n],"id");if(!terminals.TryAdd(id,new(c.Id,ports[n].Id)))throw new InvalidDataException("Identifiant de borne QET dupliqué.");string label=A(sourcePorts[n],"number");if(label.Length>0&&label!="_")c.PortLabels[ports[n].Id]=label;}
                parts.AddRange(texts.Where(t=>t.Text.Length>0&&t.Text!="_"));
                double w=Math.Max(N(def??element,"width",40),parts.Select(x=>Math.Abs(x.X)*2+20).DefaultIfEmpty(40).Max()),h=Math.Max(N(def??element,"height",40),parts.Select(x=>Math.Abs(x.Y)*2+20).DefaultIfEmpty(40).Max());
                var definition=new SymbolDefinition(c.Symbol,name.Length>0?name:"Symbole importé","U",w,h,ports,parts){Category="Import QElectroTech"};p.CustomSymbols.Add(c.Symbol,definition);c.ResolvedDefinition=definition;s.Components.Add(c);
            }
            foreach(var input in diagram.Element("inputs")?.Elements("input")??[])
            {
                string text=A(input,"text");if(text.Contains("<html",StringComparison.OrdinalIgnoreCase)){text=Regex.Replace(text,@"<style[\s\S]*?</style>","");text=WebUtility.HtmlDecode(Regex.Replace(text,@"<[^>]*>"," ")).Trim();Warn("Texte enrichi converti en texte simple");}
                if(text.Length>0)s.Notes.Add(new(){X=N(input,"x"),Y=N(input,"y"),Text=text.Length>2000?text[..2000]:text});
            }
            foreach(var conductor in diagram.Element("conductors")?.Elements("conductor")??[])
            {
                if(!terminals.TryGetValue(A(conductor,"terminal1"),out var a)||!terminals.TryGetValue(A(conductor,"terminal2"),out var b)){Warn("Conducteur omis : borne source ou destination absente");continue;}
                if(a==b||s.Wires.Any(w=>w.From==a&&w.To==b||w.From==b&&w.To==a)){Warn("Connexion dupliquée ou vers elle-même omise");continue;}
                var origin=s.Components.Single(c=>c.Id==a.ComponentId).PortPoint(a.PortId);List<Position> stops=[];var pos=origin;
                foreach(var seg in conductor.Elements("segment")){double length=N(seg,"length");pos=A(seg,"orientation")=="horizontal"?new(pos.X+length,pos.Y):new(pos.X,pos.Y+length);stops.Add(new(pos.X,pos.Y));}
                if(stops.Count>0)stops.RemoveAt(stops.Count-1);
                string color=A(conductor,"conductor_color",A(conductor,"color","#243B53"));if(!Regex.IsMatch(color,"^#[0-9A-Fa-f]{6}$"))color="#243B53";
                s.Wires.Add(new(){From=a,To=b,Number=A(conductor,"num"),Color=color,Waypoints=stops});
            }
            if(diagram.Elements().Any(x=>x.Name.LocalName is "images" or "shapes"))Warn("Images et dessins libres de folio non repris");
            Fit(s,N(diagram,"cols",20)*N(diagram,"colsize",60),N(diagram,"rows",10)*N(diagram,"rowsize",80));
        }
        if(p.Sheets.Count==0)throw new InvalidDataException("Aucun folio dans le projet QElectroTech.");
        p.CustomFields["import_source"]=Path.GetFileName(path);p.CustomFields["import_format"]="QElectroTech";
        var warnings=counts.Select(x=>$"{x.Key} : {x.Value}").ToList();warnings.Add("Cartouches remplacés par le modèle GeMMeElec. Styles, textes et symboles complexes peuvent être simplifiés. Les reports QET ne sont pas convertis en liaisons interfolios ; vérifier les raccordements et les références avant utilisation.");
        ProjectStore.ValidateStructure(p);return new(p,warnings);
    }
    private static void Fit(Sheet s,double width,double height)
    {
        double minX=s.Components.Select(c=>c.Bounds().Left).Concat(s.Notes.Select(n=>n.X)).Concat(s.Wires.SelectMany(w=>w.Waypoints.Select(v=>v.X))).DefaultIfEmpty(0).Min();
        double minY=s.Components.Select(c=>c.Bounds().Top).Concat(s.Notes.Select(n=>n.Y)).Concat(s.Wires.SelectMany(w=>w.Waypoints.Select(v=>v.Y))).DefaultIfEmpty(0).Min();
        double dx=50-Math.Min(0,minX),dy=50-Math.Min(0,minY);
        foreach(var c in s.Components){c.X+=dx;c.Y+=dy;}foreach(var n in s.Notes){n.X+=dx;n.Y+=dy;}foreach(var w in s.Wires)w.Waypoints=w.Waypoints.Select(v=>new Position(v.X+dx,v.Y+dy)).ToList();
        s.Width=Math.Max(300,Math.Max(width+dx+50,s.Components.Select(c=>c.Bounds().Right).Concat(s.Notes.Select(n=>n.X+100)).Concat(s.Wires.SelectMany(w=>w.Waypoints.Select(v=>v.X))).DefaultIfEmpty(0).Max()+50));
        s.Height=Math.Max(300,Math.Max(height+dy+130,s.Components.Select(c=>c.Bounds().Bottom).Concat(s.Notes.Select(n=>n.Y+30)).Concat(s.Wires.SelectMany(w=>w.Waypoints.Select(v=>v.Y))).DefaultIfEmpty(0).Max()+130));
    }
    private static void ParseShape(XElement e,List<Shape> shapes,Action<string> warn)
    {
        double x=N(e,"x"),y=N(e,"y"),w=N(e,"width"),h=N(e,"height");
        switch(e.Name.LocalName)
        {
            case "line":shapes.Add(new("line",N(e,"x1"),N(e,"y1"),N(e,"x2"),N(e,"y2")));break;
            case "rect":shapes.Add(new("rect",x,y,w,h));break;
            case "circle":double r=N(e,"diameter")/2;shapes.Add(new("circle",x+r,y+r,r));break;
            case "ellipse":case "arc":
                double start=N(e,"start"),sweep=e.Name=="ellipse"?360:N(e,"angle",360);var prev=new Point(x+w/2+w/2*Math.Cos(start*Math.PI/180),y+h/2-h/2*Math.Sin(start*Math.PI/180));
                for(int i=1;i<=48;i++){double a=(start+sweep*i/48)*Math.PI/180;var next=new Point(x+w/2+w/2*Math.Cos(a),y+h/2-h/2*Math.Sin(a));shapes.Add(new("line",prev.X,prev.Y,next.X,next.Y));prev=next;}break;
            case "polygon":
                List<Point> pts=[];for(int i=1;e.Attribute("x"+i)is not null;i++)pts.Add(new(N(e,"x"+i),N(e,"y"+i)));if(A(e,"closed","true")!="false"&&pts.Count>1)pts.Add(pts[0]);for(int i=1;i<pts.Count;i++)shapes.Add(new("line",pts[i-1].X,pts[i-1].Y,pts[i].X,pts[i].Y));break;
            case "text":shapes.Add(new("text",x,y-N(e,"size",9),N(e,"size",9),Text:A(e,"text")){Rotation=N(e,"rotation")});break;
            case "terminal":case "input":break;
            default:warn("Primitive QET omise : "+e.Name.LocalName);break;
        }
    }
    public static ProjectImportResult DxfPages(IEnumerable<string> paths)
    {
        var p=new Project{Title="Projet importé DXF",Sheets=[]};List<string>warnings=["Import graphique : les dessins sont conservés comme éléments vectoriels éditables, sans déduire les appareils, réseaux, références croisées ou articles de BOM. Les formats EPLAN natifs .elk, .zw1 et .epj ne sont pas pris en charge."];
        foreach(var path in paths)
        {
            if(p.Sheets.Count>=Project.MaxSheets)throw new InvalidDataException("Maximum 200 folios.");var result=DxfImport.Read(path);var d=result.Symbol;
            double scale=Math.Min(1,Math.Min(1260/d.Width,800/d.Height));d=DxfImport.Scale(d,scale);CustomSymbols.Validate(d);p.CustomSymbols.Add(d.Id,d);
            var s=new Sheet{Title=Path.GetFileNameWithoutExtension(path),TitleBlockId=""};s.Components.Add(new(){Symbol=d.Id,ResolvedDefinition=d,X=700,Y=450,Tag="DXF"+(p.Sheets.Count+1),TagPlacement=new(){Visible=false},DetailPlacement=new(){Visible=false}});p.Sheets.Add(s);
            warnings.Add(Path.GetFileName(path)+" : échelle "+scale.ToString("0.###",CultureInfo.InvariantCulture)+", unités "+result.Units);warnings.AddRange(result.Warnings);
        }
        ProjectStore.ValidateStructure(p);return new(p,warnings.Distinct().ToList());
    }
}
public sealed partial class MainWindow
{
    private async void ImportDrawingProject(bool dxf)
    {
        Canvas.CancelDrag();var picker=new OpenFileDialog{Filter=dxf?"Folios DXF|*.dxf":"Projet QElectroTech|*.qet",Multiselect=dxf,Title=dxf?"Choisir les folios DXF exportés depuis EPLAN ou une CAO":"Convertir un projet QElectroTech"};if(picker.ShowDialog(this)!=true)return;
        try
        {
            IsEnabled=false;status.Text="Conversion du projet…";var result=await Task.Run(()=>dxf?ProjectImport.DxfPages(picker.FileNames):ProjectImport.Qet(picker.FileName));IsEnabled=true;
            var dialog=DocumentWindow("Rapport de conversion",800,600);var root=new DockPanel{Margin=new(18)};dialog.Content=root;var actions=new WrapPanel();DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
            root.Children.Add(new TextBox{Text=result.Report,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            actions.Children.Add(DocButton("Ouvrir le projet converti",dialog,()=>{if(!CanReplace())return;Editor.ImportProject(result.Project);libraryMode.SelectedIndex=2;Filter();dialog.Close();Canvas.Fit();}));actions.Children.Add(DocButton("Annuler",dialog,dialog.Close));ShowDocumentDialog(dialog);
        }catch(Exception ex){MessageBox.Show(this,ex.Message,"Conversion du projet");}finally{IsEnabled=true;status.Text="Prêt";}
    }
}
