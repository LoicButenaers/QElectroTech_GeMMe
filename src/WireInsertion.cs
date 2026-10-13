using System.Windows;

namespace GeMMeElec;

public sealed record WireCrossing(string WireId,Point Point,int Segment);
public static class WireIntersections
{
    public static List<WireCrossing> Across(Sheet sheet,Point a,Point b,IReadOnlyDictionary<string,List<Point>>? cachedRoutes=null)
    {
        if(!new[]{a.X,a.Y,b.X,b.Y}.All(double.IsFinite)||(b-a).Length<10)throw new ArgumentException("Tracer une ligne d’au moins 10 unités.");
        List<WireCrossing> result=[];var components=cachedRoutes is null?sheet.Components.ToDictionary(c=>c.Id):null;
        foreach(var wire in sheet.Wires){List<Point> hits=[];var path=cachedRoutes is null?wire.Route(sheet,components):cachedRoutes[wire.Id];for(int i=1;i<path.Count;i++)if(Cross(a,b,path[i-1],path[i]) is Point hit&&!hits.Any(x=>(x-hit).Length<.01)){hits.Add(hit);result.Add(new(wire.Id,hit,i));}}
        return result.OrderBy(x=>Vector.Multiply(x.Point-a,b-a)).ToList();
    }
    public static Point? Cross(Point a,Point b,Point c,Point d)
    {
        var u=b-a;var v=d-c;double determinant=u.X*v.Y-u.Y*v.X;if(Math.Abs(determinant)<.0001)return null;
        var q=c-a;double t=(q.X*v.Y-q.Y*v.X)/determinant,s=(q.X*u.Y-q.Y*u.X)/determinant;
        return t>=-.0001&&t<=1.0001&&s>=-.0001&&s<=1.0001?a+u*Math.Clamp(t,0,1):null;
    }
}
public sealed partial class Editor
{
    public Component InsertOnWire(string wireId,string symbol,Point location,string inputPort,string outputPort)
    {
        Component? created=null;Change(()=>created=InsertOnWireCore(wireId,symbol,location,inputPort,outputPort));return created!;
    }
    private Component InsertOnWireCore(string wireId,string symbol,Point location,string inputPort,string outputPort)
    {
        var wire=Sheet.Wires.Single(w=>w.Id==wireId);var route=wire.Route(Sheet);var hit=WirePoint(route,location);if(hit.Distance>20)throw new ArgumentException("Choisir une position sur le fil.");
        var definition=CustomSymbols.Resolve(Project,symbol);if(symbol=="junction"||symbol.StartsWith("folio_"))throw new ArgumentException("Choisir un appareil ou une borne.");
        var input=definition.Ports.SingleOrDefault(p=>p.Id==inputPort)??throw new ArgumentException("Port d’entrée absent.");var output=definition.Ports.SingleOrDefault(p=>p.Id==outputPort)??throw new ArgumentException("Port de sortie absent.");
        if(inputPort==outputPort)throw new ArgumentException("Choisir deux ports distincts.");var segment=route[hit.Segment]-route[hit.Segment-1];var gap=new Vector(output.X-input.X,output.Y-input.Y);if(gap.Length<.01)throw new ArgumentException("Les ports doivent être espacés.");
        double angle=(Math.Atan2(segment.Y,segment.X)-Math.Atan2(gap.Y,gap.X))*180/Math.PI;int rotation=(int)Math.Round(angle/90)*90;if(Math.Abs(angle-rotation)>.01)throw new ArgumentException("Les deux ports doivent être alignés sur le segment.");
        var c=new Component{Symbol=symbol,ResolvedDefinition=definition,Tag=NextTag(definition.Prefix),Description=definition.Name,Rotation=rotation};var midpoint=c.Transform((input.X+output.X)/2,(input.Y+output.Y)/2);c.X=hit.Point.X-midpoint.X;c.Y=hit.Point.Y-midpoint.Y;
        var entry=c.PortPoint(inputPort);var exit=c.PortPoint(outputPort);var direction=segment/segment.Length;
        if(Vector.Multiply(entry-route[hit.Segment-1],direction)<1||Vector.Multiply(route[hit.Segment]-exit,direction)<1)throw new ArgumentException("Le segment est trop court ou la position trop proche d’un coude.");
        Rect Envelope(Component value){if(value.Symbol=="terminal")return new Rect(value.X-(value.Rotation%180==0?8:25),value.Y-(value.Rotation%180==0?25:8),value.Rotation%180==0?16:50,value.Rotation%180==0?50:16);return value.Bounds();}
        if(!SheetZones.Area(Project,Sheet).Contains(c.Bounds())||Sheet.Components.Where(x=>!Junctions.Automatic(x)).Any(x=>{var overlap=Rect.Intersect(Envelope(x),Envelope(c));return !overlap.IsEmpty&&overlap.Width>.01&&overlap.Height>.01;}))throw new ArgumentException("Le symbole empiète sur un autre appareil ou sur le cadre.");
        c.RelayFunction=CoilContacts.Role(c);Sheet.Components.Add(c);
        bool passage=definition.InternalConnections.Any(p=>p.From==inputPort&&p.To==outputPort||p.From==outputPort&&p.To==inputPort);
        var second=DocumentModel.Copy(wire);second.Id=Guid.NewGuid().ToString("N");second.From=new(c.Id,outputPort);second.RoutingMode="manual";second.RoutingWarning="";
        if(!passage&&!wire.NumberIndependent)second.Number="W"+NextWireNumber();
        second.Waypoints=WireRouting.Simplify(new[]{exit}.Concat(route.Skip(hit.Segment))).Skip(1).SkipLast(1).Select(p=>new Position(p.X,p.Y)).ToList();
        wire.To=new(c.Id,inputPort);wire.RoutingMode="manual";wire.RoutingWarning="";wire.Waypoints=WireRouting.Simplify(route.Take(hit.Segment).Append(entry)).Skip(1).SkipLast(1).Select(p=>new Position(p.X,p.Y)).ToList();Sheet.Wires.Add(second);return c;
    }
    public TerminalStrip InsertTerminalLine(Point a,Point b,string name,int first=1,int level=1)
    {
        var crossings=WireIntersections.Across(Sheet,a,b);if(crossings.Count==0)throw new ArgumentException("La ligne ne traverse aucun conducteur.");if(crossings.GroupBy(x=>x.WireId).Any(g=>g.Count()>1))throw new ArgumentException("La ligne traverse plusieurs fois un même fil ; raccourcir la ligne.");if(first is <1 or >99999||level is <1 or >16)throw new ArgumentException("Numéro 1–99999 et niveau 1–16 requis.");
        var strip=Project.TerminalStrips.FirstOrDefault(t=>t.Name.Equals(name,StringComparison.OrdinalIgnoreCase));string id=strip?.Id??Guid.NewGuid().ToString("N");
        Change(()=>{strip=Project.TerminalStrips.FirstOrDefault(t=>t.Id==id);if(strip is null){strip=new(){Id=id,Name=name};Project.TerminalStrips.Add(strip);}for(int i=0;i<crossings.Count;i++){var hit=crossings[i];var c=InsertOnWireCore(hit.WireId,"terminal",hit.Point,"1","2");string number=(first+i).ToString();c.Tag=name+":"+number;c.TagLocked=true;c.TerminalNumberOnly=true;c.ShowPortLabels=false;c.TagPlacement=new(){Side="custom",Horizontal="left",Vertical="bottom",OffsetX=8,OffsetY=-4,FontSize=11};strip.Terminals.Add(new(){ComponentId=c.Id,Number=number,Level=level});}Sheet.TerminalMarkers.Add(new(){StripId=id,A=new(a.X,a.Y),B=new(b.X,b.Y)});});return strip!;
    }
}
