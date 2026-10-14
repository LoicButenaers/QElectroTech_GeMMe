using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace GeMMeElec;

public sealed class RoutingObstacles
{
    public Rect Area {get;}
    public List<Rect> Blocks {get;}=[];
    public Dictionary<string,List<Point>> OtherRoutes {get;}
    private readonly Project routingProject;
    private readonly Dictionary<(int,int),List<Rect>> obstacleCells=[];
    private readonly Dictionary<(int,int),List<(string Id,Point A,Point B)>> wireCells=[];
    private static IEnumerable<(int,int)> Cells(Rect r){for(int x=(int)Math.Floor(r.Left/100);x<=(int)Math.Floor(r.Right/100);x++)for(int y=(int)Math.Floor(r.Top/100);y<=(int)Math.Floor(r.Bottom/100);y++)yield return(x,y);}
    public RoutingObstacles(Project project,Sheet sheet)
    {
        routingProject=project;
        Area=SheetZones.Area(project,sheet);var refs=new CrossReferences(project);var components=sheet.Components.ToDictionary(c=>c.Id);OtherRoutes=sheet.Wires.ToDictionary(w=>w.Id,w=>w.Route(sheet,components));
        foreach(var c in sheet.Components){if(!Junctions.Automatic(c)){var bounds=c.Bounds();bounds.Inflate(7,7);Blocks.Add(bounds);}foreach(var ink in Drawing.ComponentScene(project,c,refs).Where(i=>i.Kind=="text")){var bounds=Drawing.TextBounds(ink);bounds.Inflate(4,4);Blocks.Add(bounds);}}
        foreach(var n in sheet.Notes){var bounds=FreeGraphics.NoteBounds(project,sheet,n);bounds.Inflate(4,4);Blocks.Add(bounds);}
        foreach(var block in Blocks)foreach(var cell in Cells(block)){if(!obstacleCells.TryGetValue(cell,out var list))obstacleCells[cell]=list=[];list.Add(block);}
        foreach(var pair in OtherRoutes)for(int i=1;i<pair.Value.Count;i++){var a=pair.Value[i-1];var b=pair.Value[i];foreach(var cell in Cells(new Rect(a,b))){if(!wireCells.TryGetValue(cell,out var list))wireCells[cell]=list=[];list.Add((pair.Key,a,b));}}
    }
    public bool Clear(Point a,Point b)
    {
        if(!Area.Contains(a)||!Area.Contains(b))return false;
        foreach(var r in Cells(new Rect(a,b)).SelectMany(cell=>obstacleCells.GetValueOrDefault(cell)??[]))if(Intersects(a,b,r))return false;return true;
    }
    internal static bool Intersects(Point a,Point b,Rect r)=>Math.Abs(a.Y-b.Y)<.001? a.Y>r.Top+.01&&a.Y<r.Bottom-.01&&Math.Max(a.X,b.X)>r.Left+.01&&Math.Min(a.X,b.X)<r.Right-.01 : a.X>r.Left+.01&&a.X<r.Right-.01&&Math.Max(a.Y,b.Y)>r.Top+.01&&Math.Min(a.Y,b.Y)<r.Bottom-.01;
    private int Crossings(Point a,Point b,string id,Point start,Point end)=>Cells(new Rect(a,b)).SelectMany(cell=>wireCells.GetValueOrDefault(cell)??[]).Distinct().Count(segment=>segment.Id!=id&&WireIntersections.Cross(a,b,segment.A,segment.B) is Point hit&&(hit-start).Length>2&&(hit-end).Length>2);
    public List<Point> Route(Sheet sheet,Wire wire)
    {
        var from=sheet.Components.Single(c=>c.Id==wire.From.ComponentId);var to=sheet.Components.Single(c=>c.Id==wire.To.ComponentId);var a=from.PortPoint(wire.From.PortId);var b=to.PortPoint(wire.To.PortId);
        Point Escape(Component c,Point port){var r=c.Bounds();var sides=new[]{(Math.Abs(port.X-r.Left),new Vector(-1,0)),(Math.Abs(port.X-r.Right),new Vector(1,0)),(Math.Abs(port.Y-r.Top),new Vector(0,-1)),(Math.Abs(port.Y-r.Bottom),new Vector(0,1))};var nearest=sides.MinBy(x=>x.Item1);return port+nearest.Item2*(nearest.Item1+20);}
        var start=Junctions.Automatic(from)?a:Escape(from,a);var end=Junctions.Automatic(to)?b:Escape(to,b);
        if(!Area.Contains(start)||!Area.Contains(end))throw new ArgumentException("Une borne est trop proche du cadre pour sortir du symbole.");
        // Coordinate compression limits the search; every edge is still checked
        // against all obstacles, including those outside the candidate corridor.
        var corridor=new Rect(start,end);corridor.Inflate(80,80);double Distance(Rect r)=>Math.Max(0,Math.Max(corridor.Left-r.Right,r.Left-corridor.Right))+Math.Max(0,Math.Max(corridor.Top-r.Bottom,r.Top-corridor.Bottom));
        var nearby=Blocks.OrderBy(Distance).Take(80).ToList();double[] xs=nearby.SelectMany(r=>new[]{r.Left-1,r.Right+1}).Concat(new[]{start.X,end.X,Area.Left+2,Area.Right-2}).Where(x=>x>=Area.Left&&x<=Area.Right).Distinct().Order().ToArray();double[] ys=nearby.SelectMany(r=>new[]{r.Top-1,r.Bottom+1}).Concat(new[]{start.Y,end.Y,Area.Top+2,Area.Bottom-2}).Where(y=>y>=Area.Top&&y<=Area.Bottom).Distinct().Order().ToArray();
        int columns=xs.Length;int Node(Point p)=>Array.IndexOf(ys,p.Y)*columns+Array.IndexOf(xs,p.X);Point Position(int node)=>new(xs[node%columns],ys[node/columns]);int first=Node(start),last=Node(end);var queue=new PriorityQueue<int,double>();var cost=new Dictionary<int,double>();var previous=new Dictionary<int,int>();int initial=first*3;cost[initial]=0;queue.Enqueue(initial,0);int found=-1,expanded=0;
        while(queue.TryDequeue(out int state,out double score)&&expanded++<12000){int node=state/3,oldDirection=state%3;var pos=Position(node);if(node==last){found=state;break;}int x=node%columns,y=node/columns;
            foreach(var(nx,ny,direction)in new[]{(x-1,y,1),(x+1,y,1),(x,y-1,2),(x,y+1,2)}){if(nx<0||ny<0||nx>=xs.Length||ny>=ys.Length)continue;int nextNode=ny*columns+nx;var next=Position(nextNode);if(!Clear(pos,next))continue;int crossing=Crossings(pos,next,wire.Id,a,b);double candidate=cost[state]+(next-pos).Length+(oldDirection!=0&&oldDirection!=direction?35:0)+crossing*45;int nextState=nextNode*3+direction;if(cost.TryGetValue(nextState,out double known)&&known<=candidate)continue;cost[nextState]=candidate;previous[nextState]=state;queue.Enqueue(nextState,candidate+Math.Abs(next.X-end.X)+Math.Abs(next.Y-end.Y));}}
        if(found<0)throw new ArgumentException("Aucun trajet libre trouvé dans la recherche bornée ; déplacer les objets ou conserver un parcours manuel.");List<Point> path=[];for(int state=found;;state=previous[state]){path.Add(Position(state/3));if(state==initial)break;}path.Reverse();var route=WireRouting.Simplify(new[]{a}.Concat(path).Append(b));
        // Stubs cross their own envelope; other symbols and annotations remain obstacles.
        foreach(var c in sheet.Components.Where(c=>!Junctions.Automatic(c))){var r=c.Bounds();r.Inflate(7,7);if(c.Id!=from.Id&&Intersects(a,start,r)||c.Id!=to.Id&&Intersects(b,end,r))throw new ArgumentException("Une sortie de borne traverse un autre appareil.");}
        var references=new CrossReferences(routingProject);
        foreach(var c in sheet.Components.Where(c=>c.Id!=from.Id&&c.Id!=to.Id))foreach(var text in Drawing.ComponentScene(routingProject,c,references).Where(i=>i.Kind=="text")){var r=Drawing.TextBounds(text);r.Inflate(4,4);if(Intersects(a,start,r)||Intersects(b,end,r))throw new ArgumentException("Un texte recouvre une sortie de borne.");}
        foreach(var n in sheet.Notes){var r=FreeGraphics.NoteBounds(routingProject,sheet,n);r.Inflate(4,4);if(Intersects(a,start,r)||Intersects(b,end,r))throw new ArgumentException("Une note recouvre une sortie de borne.");}
        if(route.Count>102)throw new ArgumentException("Le parcours dépasse 100 coudes ; simplifier le dessin.");return route;
    }
}
public sealed partial class Editor
{
    private readonly Dictionary<string,string> routingSignatures=[];
    private static string RoutingSignature(Project p,Sheet s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{p.Id,p.ZoneRows,p.ZoneColumns,p.Presentation,TitleBlock=p.TitleBlocks.Single(t=>t.Id==s.TitleBlockId),s.UseProjectZones,SheetRows=s.ZoneRows,SheetColumns=s.ZoneColumns,s.Width,s.Height,s.TitleBlockId,s.Components,s.Notes,Wires=s.Wires.Select(w=>new{w.Id,w.From,w.To,w.RoutingMode})},ProjectStore.ProjectJson))));
    public int RouteWires(IEnumerable<string> ids)
    {
        var chosen=ids.ToHashSet();var wires=Sheet.Wires.Where(w=>chosen.Contains(w.Id)).ToList();if(wires.Count!=chosen.Count||wires.Count==0)throw new ArgumentException("Choisir au moins un fil du folio.");int count=0;
        Change(()=>{var router=new RoutingObstacles(Project,Sheet);foreach(var w in wires){var path=router.Route(Sheet,w);w.Waypoints=path.Skip(1).SkipLast(1).Select(p=>new Position(p.X,p.Y)).ToList();w.RoutingMode="smart";w.RoutingWarning="";router.OtherRoutes[w.Id]=path;count++;}routingSignatures[Sheet.Id]=RoutingSignature(Project,Sheet);});return count;
    }
    private void RefreshSmartRoutes(Sheet? target=null)
    {
        foreach(var s in Project.Sheets.Where(s=>(target is null||s==target)&&s.Wires.Any(w=>w.RoutingMode=="smart"))){string signature=RoutingSignature(Project,s);if(routingSignatures.GetValueOrDefault(s.Id)==signature)continue;var router=new RoutingObstacles(Project,s);foreach(var w in s.Wires.Where(w=>w.RoutingMode=="smart")){try{var path=router.Route(s,w);w.Waypoints=path.Skip(1).SkipLast(1).Select(p=>new Position(p.X,p.Y)).ToList();w.RoutingWarning="";router.OtherRoutes[w.Id]=path;}catch(ArgumentException ex){w.RoutingWarning=ex.Message;}}routingSignatures[s.Id]=signature;}
        foreach(string id in routingSignatures.Keys.Where(id=>!Project.Sheets.Any(s=>s.Id==id)).ToList())routingSignatures.Remove(id);
    }
}
