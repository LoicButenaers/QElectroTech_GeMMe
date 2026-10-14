using System.Windows;
using System.Windows.Media;

namespace GeMMeElec;

// One active-folio cache. Immutable drawings keep the painter's order; previews
// replace only affected drawings, while committed edits discard the whole cache.
internal sealed class CanvasScene
{
    private readonly Project project;
    private readonly Sheet sheet;
    private readonly ReadingOptions reading;
    private CrossReferences references;
    private readonly Dictionary<string,Component> components;
    private readonly Dictionary<string,Wire> wires;
    private readonly Dictionary<string,Annotation> notes;
    private readonly Dictionary<string,int> imageSlots=[],componentSlots=[],wireSlots=[],noteSlots=[];
    private readonly Dictionary<int,List<Ink>> inks=[];
    private readonly HashSet<string> dirtyComponents=[],dirtyWires=[],dirtyNotes=[];
    private int dotSlot,markerSlot;
    private bool geometryDirty,referencesDirty;
    public DrawingGroup Drawing {get;}=new();
    public Dictionary<string,List<Point>> Routes {get;}=[];
    public int RecordBuilds {get;private set;}
    public CanvasScene(Project project,Sheet sheet,ReadingOptions reading)
    {
        this.project=project;this.sheet=sheet;this.reading=reading;references=new(project);
        components=sheet.Components.ToDictionary(c=>c.Id);wires=sheet.Wires.ToDictionary(w=>w.Id);notes=sheet.Notes.ToDictionary(n=>n.Id);
        if(sheet.BomPage is not null||sheet.TerminalPlanId.Length>0||sheet.CabinetPlanId.Length>0){Add(GeMMeElec.Drawing.Scene(project,sheet,reading));return;}
        List<Ink> frame=[new("rect",10,10,sheet.Width-40,sheet.Height-40,Color:"#53677B")];frame.AddRange(DocumentModel.Scene(project,sheet));frame.AddRange(SheetZones.Frame(project,sheet));Add(frame);
        foreach(var c in sheet.Components)imageSlots[c.Id]=Add(Images(c));
        foreach(var w in sheet.Wires){Routes[w.Id]=w.Route(sheet,components);wireSlots[w.Id]=Add(WireScene(w));}
        foreach(var c in sheet.Components)componentSlots[c.Id]=Add(GeMMeElec.Drawing.ComponentScene(project,c,references,false,reading));
        dotSlot=Add(Dots());foreach(var n in sheet.Notes)noteSlots[n.Id]=Add(NoteScene(n));markerSlot=Add(Markers());
    }
    private List<Ink> Images(Component c)=>c.Definition.Shapes.Where(s=>s.Kind=="image").Select(s=>{var p=c.Transform(s.X,s.Y);return new Ink("image",p.X,p.Y,s.A,s.B,s.Text,Rotation:c.Rotation+s.Rotation);}).ToList();
    private List<Ink> WireScene(Wire w)
    {
        var path=Routes[w.Id];List<Ink> result=[];for(int i=1;i<path.Count;i++)result.Add(new("line",path[i-1].X,path[i-1].Y,path[i].X,path[i].Y,Color:w.Color,Thickness:w.Thickness,LineStyle:w.LineStyle));
        if(reading.WireNumbers&&GeMMeElec.Drawing.WireLabel(w,path) is Ink label)result.Add(reading.Scale(label));return result;
    }
    private List<Ink> NoteScene(Annotation n)=>reading.Notes?[new("text",n.X,n.Y,Text:n.Text,Size:16*reading.TextScale)]:[];
    private List<Ink> Dots()=>Junctions.Dots(sheet,Routes).Select(dot=>new Ink("dot",dot.Point.X,dot.Point.Y,4,Color:dot.Color)).ToList();
    private List<Ink> Markers()
    {
        var result=CableDefinitions.Scene(project,sheet);foreach(var marker in sheet.TerminalMarkers)result.Add(new("text",Math.Max(32,Math.Min(marker.A.X,marker.B.X)-40),Math.Min(marker.A.Y,marker.B.Y)-15,Text:project.TerminalStrips.Single(t=>t.Id==marker.StripId).Name,Size:14));return result;
    }
    private DrawingGroup Record(List<Ink> values){RecordBuilds++;var drawing=new DrawingGroup();using(var dc=drawing.Open())GeMMeElec.Drawing.Render(dc,values);drawing.Freeze();return drawing;}
    private int Add(List<Ink> values){int at=Drawing.Children.Count;inks[at]=values;Drawing.Children.Add(Record(values));return at;}
    private void Replace(int at,List<Ink> values){inks[at]=values;Drawing.Children[at]=Record(values);}
    public void TextChanged(string id,bool wire){if(wire)dirtyWires.Add(id);else dirtyComponents.Add(id);}
    public void GeometryChanged(IEnumerable<string> componentIds,IEnumerable<string> noteIds,IEnumerable<string>? wireIds=null)
    {
        var changed=componentIds.ToHashSet();dirtyComponents.UnionWith(changed);dirtyNotes.UnionWith(noteIds);geometryDirty=true;
        foreach(var w in wires.Values)if(changed.Contains(w.From.ComponentId)||changed.Contains(w.To.ComponentId))dirtyWires.Add(w.Id);if(wireIds is not null)dirtyWires.UnionWith(wireIds);
        var linked=changed.Select(id=>components[id]).Where(c=>CrossReferences.IsPoint(c)||CoilContacts.IsCoil(c)||CoilContacts.IsContact(c)).ToList();
        if(linked.Count>0){referencesDirty=true;var names=linked.Where(CrossReferences.IsPoint).Select(c=>c.LinkName).Where(n=>n.Length>0).ToHashSet(StringComparer.OrdinalIgnoreCase);var roots=linked.Select(c=>c.CoilId.Length>0?c.CoilId:c.Id).ToHashSet();foreach(var c in components.Values)if(names.Contains(c.LinkName)||roots.Contains(c.Id)||roots.Contains(c.CoilId))dirtyComponents.Add(c.Id);}
    }
    public void Update()
    {
        if(referencesDirty){references=new(project);referencesDirty=false;}
        foreach(string id in dirtyWires){var w=wires[id];if(geometryDirty)Routes[id]=w.Route(sheet,components);Replace(wireSlots[id],WireScene(w));}
        foreach(string id in dirtyComponents){var c=components[id];if(geometryDirty)Replace(imageSlots[id],Images(c));Replace(componentSlots[id],GeMMeElec.Drawing.ComponentScene(project,c,references,false,reading));}
        foreach(string id in dirtyNotes)Replace(noteSlots[id],NoteScene(notes[id]));
        if(geometryDirty){Replace(dotSlot,Dots());if(sheet.CableMarkers.Count>0)Replace(markerSlot,Markers());}
        dirtyComponents.Clear();dirtyWires.Clear();dirtyNotes.Clear();geometryDirty=false;
    }
    internal List<Ink> SceneForTest(){Update();return Enumerable.Range(0,Drawing.Children.Count).SelectMany(at=>inks[at]).ToList();}
}
