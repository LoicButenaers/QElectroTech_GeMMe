using System.IO;

namespace GeMMeElec;

public sealed class TerminalMarker
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string StripId {get;set;}="";
    public Position A {get;set;}=new(0,0);
    public Position B {get;set;}=new(100,0);
}
public static class TerminalGraphics
{
    public static void Validate(Project p)
    {
        foreach(var s in p.Sheets){if(s.TerminalPlanStyle is not ("table" or "graphic")||s.TerminalMarkers is null||s.TerminalMarkers.Count>2000||s.TerminalMarkers.Select(m=>m.Id).Distinct().Count()!=s.TerminalMarkers.Count)throw new InvalidDataException("Présentation ou marquage du bornier invalide.");foreach(var m in s.TerminalMarkers)if(m is null||string.IsNullOrWhiteSpace(m.Id)||!p.TerminalStrips.Any(t=>t.Id==m.StripId)||m.A is null||m.B is null||!new[]{m.A.X,m.A.Y,m.B.X,m.B.Y}.All(double.IsFinite)||m.A.X<0||m.A.Y<0||m.B.X<0||m.B.Y<0||m.A.X>s.Width||m.B.X>s.Width||m.A.Y>s.Height||m.B.Y>s.Height)throw new InvalidDataException("Marquage de bornier absent ou hors folio.");}
    }
    private static List<(List<string> Numbers,List<int> Levels)> Pages(TerminalStrip strip)
    {
        var numbers=strip.Terminals.Select(t=>t.Number).Distinct().Chunk(12).Select(c=>c.ToList()).ToList();var levels=strip.Terminals.Select(t=>t.Level).Distinct().Order().Chunk(4).Select(c=>c.ToList()).ToList();return numbers.SelectMany(n=>levels.Select(l=>(n,l))).DefaultIfEmpty((new List<string>(),new List<int>())).ToList();
    }
    public static int PageCount(TerminalStrip strip)=>Pages(strip).Count;
    public static List<Ink> Scene(Project p,Sheet sheet)
    {
        var strip=p.TerminalStrips.Single(t=>t.Id==sheet.TerminalPlanId);var pages=Pages(strip);List<Ink> inks=[new("text",60,50,Text:"Vue graphique du bornier "+strip.Name,Size:22),new("text",60,83,Text:"Raccordements et commentaires complets dans les pages de tableau suivantes.",Size:12)];inks.AddRange(SheetZones.Frame(p,sheet));inks.AddRange(DocumentModel.Scene(p,sheet));if(sheet.TerminalPlanPage>=pages.Count){inks.Add(new("text",60,140,Text:"Régénérer les plans du bornier."));return inks;}var page=pages[sheet.TerminalPlanPage];var rows=TerminalStrips.Rows(p,strip);var portPoints=new Dictionary<(string,string),(double X,double Y)>();
        for(int col=0;col<page.Numbers.Count;col++){double x=130+col*98;string number=page.Numbers[col];inks.Add(new("text",x-10,120,Text:number,Size:16));for(int row=0;row<page.Levels.Count;row++){int level=page.Levels[row];double y=210+row*160;if(col==0)inks.Add(new("text",45,y-5,Text:"Niv. "+level,Size:12));var terminal=strip.Terminals.FirstOrDefault(t=>t.Number==number&&t.Level==level);if(terminal is null)continue;inks.Add(new("rect",x-27,y-30,54,60));inks.Add(new("line",x,y-50,x,y+50));inks.Add(new("circle",x,y-30,4));inks.Add(new("circle",x,y+30,4));portPoints[(terminal.ComponentId,terminal.InternalPort)]=(x,y-30);portPoints[(terminal.ComponentId,terminal.ExternalPort)]=(x,y+30);inks.Add(new("text",x+8,y-43,Text:terminal.InternalPort,Size:9));inks.Add(new("text",x+8,y+27,Text:terminal.ExternalPort,Size:9));var detail=rows.Single(t=>t.Number==number&&t.Level==level);string Short(string value)=>value.Length<=16?value:value[..15]+"…";inks.Add(new("text",x-45,y-74,Text:Short(detail.Internal),Size:9));inks.Add(new("text",x-45,y+57,Text:Short(detail.External),Size:9));inks.Add(new("text",x-45,y-16,Text:Short(detail.Folio),Size:8));}}
        int lane=0;foreach(var bridge in strip.Bridges){bool a=portPoints.TryGetValue((bridge.FromComponent,bridge.FromPort),out var from),b=portPoints.TryGetValue((bridge.ToComponent,bridge.ToPort),out var to);if(a&&b){double y=Math.Min(from.Y,to.Y)-12-(lane++%4)*7;inks.Add(new("line",from.X,from.Y,from.X,y,Color:"#087344",Thickness:2));inks.Add(new("line",from.X,y,to.X,y,Color:"#087344",Thickness:2));inks.Add(new("line",to.X,y,to.X,to.Y,Color:"#087344",Thickness:2));}else if(a||b){var at=a?from:to;inks.Add(new("text",at.X+8,at.Y-12,Text:"↗ pont hors page",Size:8,Color:"#087344"));}}
        inks.Add(new("text",60,790,Text:"Vue "+(sheet.TerminalPlanPage+1)+" / "+pages.Count+" · "+strip.Location,Size:12));return inks;
    }
}
