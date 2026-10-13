using System.IO;
using System.Windows;

namespace GeMMeElec;

public sealed class CableDefinition
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="W1";
    public string Type {get;set;}="";
    public double? SectionMm2 {get;set;}
    public string Comment {get;set;}="";
}
public sealed class CableMarker
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string CableId {get;set;}="";
    public Position A {get;set;}=new(0,0);
    public Position B {get;set;}=new(100,0);
}
public static class CableDefinitions
{
    public static bool Section(double? value)=>value is null||double.IsFinite(value.Value)&&value>0&&value<=1000;
    public static void Validate(Project p)
    {
        if(p.Cables is null||p.Cables.Count>2000||p.Cables.Any(c=>c is null||string.IsNullOrWhiteSpace(c.Id)||c.Id.Length>100||string.IsNullOrWhiteSpace(c.Name)||c.Name.Length>100||c.Type is null||c.Type.Length>200||c.Comment is null||c.Comment.Length>2000||!Section(c.SectionMm2))||p.Cables.Select(c=>c.Id).Distinct().Count()!=p.Cables.Count||p.Cables.Select(c=>c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=p.Cables.Count)throw new InvalidDataException("Câbles : repères uniques et section positive jusqu’à 1 000 mm².");
        var ids=p.Cables.Select(c=>c.Id).ToHashSet();foreach(var s in p.Sheets){if(s.CableMarkers is null||s.CableMarkers.Count>2000||s.CableMarkers.Select(m=>m.Id).Distinct().Count()!=s.CableMarkers.Count)throw new InvalidDataException("Marquages de câble invalides.");foreach(var m in s.CableMarkers)if(m is null||string.IsNullOrWhiteSpace(m.Id)||!ids.Contains(m.CableId)||m.A is null||m.B is null||!new[]{m.A.X,m.A.Y,m.B.X,m.B.Y}.All(double.IsFinite)||!new Rect(0,0,s.Width,s.Height).Contains(m.A.Point)||!new Rect(0,0,s.Width,s.Height).Contains(m.B.Point)||(m.B.Point-m.A.Point).Length<10)throw new InvalidDataException("Position du marquage de câble invalide.");foreach(var w in s.Wires)if(w.CableId is null||w.CableCore is null||w.CableCore.Length>100||w.CableId.Length>0&&(!ids.Contains(w.CableId)||string.IsNullOrWhiteSpace(w.CableCore))||w.CableId.Length==0&&w.CableCore.Length>0||!Section(w.SectionMm2))throw new InvalidDataException("Affectation de câble ou section de fil invalide.");}
    }
    public static List<Ink> Scene(Project p,Sheet s)
    {
        List<Ink> result=[];foreach(var marker in s.CableMarkers){var cable=p.Cables.Single(c=>c.Id==marker.CableId);var hits=WireIntersections.Across(s,marker.A.Point,marker.B.Point).Where(h=>s.Wires.Single(w=>w.Id==h.WireId).CableId==cable.Id).GroupBy(h=>(h.Point.X,h.Point.Y)).Select(g=>g.First()).ToList();int cores=p.Sheets.SelectMany(s=>s.Wires).Where(w=>w.CableId==cable.Id).Select(w=>w.CableCore).Distinct().Count();string section=cable.SectionMm2 is double size?$" · {cores} × {size.ToString("0.###",System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))} mm²":"";result.Add(new("line",marker.A.X,marker.A.Y,marker.B.X,marker.B.Y,Thickness:1));result.Add(new("text",Math.Min(marker.A.X,marker.B.X),Math.Min(marker.A.Y,marker.B.Y)-26,Text:cable.Name+section,Size:14));foreach(var hit in hits){result.Add(new("line",hit.Point.X-6,hit.Point.Y-6,hit.Point.X+6,hit.Point.Y+6,Color:"#C62828"));var wire=s.Wires.Single(w=>w.Id==hit.WireId);result.Add(new("text",hit.Point.X+8,hit.Point.Y+8,Text:wire.CableCore,Size:10));}}return result;
    }
}
public sealed partial class Editor
{
    public CableDefinition PutCable(CableDefinition cable){Change(()=>{int index=Project.Cables.FindIndex(c=>c.Id==cable.Id);if(index<0)Project.Cables.Add(DocumentModel.Copy(cable));else Project.Cables[index]=DocumentModel.Copy(cable);});return Project.Cables.Single(c=>c.Id==cable.Id);}
    public CableDefinition MarkCableLine(Point a,Point b,string name,double? sectionMm2=null,string type="")
    {
        var hits=WireIntersections.Across(Sheet,a,b);if(hits.Count==0)throw new ArgumentException("La ligne ne traverse aucun fil.");if(hits.GroupBy(x=>x.WireId).Any(g=>g.Count()>1))throw new ArgumentException("La ligne traverse plusieurs fois le même fil.");var cable=Project.Cables.FirstOrDefault(c=>c.Name.Equals(name,StringComparison.OrdinalIgnoreCase));string id=cable?.Id??Guid.NewGuid().ToString("N");
        Change(()=>{cable=Project.Cables.FirstOrDefault(c=>c.Id==id);if(cable is null){cable=new(){Id=id,Name=name};Project.Cables.Add(cable);}cable.SectionMm2=sectionMm2;cable.Type=type;int next=1;var used=Project.Sheets.SelectMany(s=>s.Wires).Where(w=>w.CableId==id).Select(w=>w.CableCore).ToHashSet();foreach(var hit in hits){var w=Sheet.Wires.Single(w=>w.Id==hit.WireId);if(w.CableId==id)continue;if(w.CableId.Length>0)throw new ArgumentException("Un fil appartient déjà à un autre câble ; le détacher d’abord.");while(used.Contains(next.ToString()))next++;w.CableId=id;w.CableCore=next.ToString();used.Add(w.CableCore);}Sheet.CableMarkers.Add(new(){CableId=id,A=new(a.X,a.Y),B=new(b.X,b.Y)});});return cable!;
    }
    public void DeleteCable(string id){Change(()=>{Project.Cables.RemoveAll(c=>c.Id==id);foreach(var s in Project.Sheets){s.CableMarkers.RemoveAll(m=>m.CableId==id);foreach(var w in s.Wires.Where(w=>w.CableId==id)){w.CableId="";w.CableCore="";}}});}
}
