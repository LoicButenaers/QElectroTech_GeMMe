using System.IO;
using System.Text.Json;
using System.Windows;

namespace GeMMeElec;

public sealed class CircuitBlock
{
    public string Format { get; set; } = "GeMMeElecBlock";
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Circuit";
    public string Category { get; set; } = "Circuits";
    public string Tags { get; set; } = "";
    public int DetachedConnections { get; set; }
    public Project Project { get; set; } = new();
}
public static class CircuitBlocks
{
    public static void Validate(CircuitBlock block)
    {
        if(block.Format!="GeMMeElecBlock"||block.Version!=1||string.IsNullOrWhiteSpace(block.Name)||block.Name.Length>200||block.Category is null||block.Category.Length>100||block.Tags is null||block.Tags.Length>1000)throw new InvalidDataException("Bloc de circuit invalide.");
        ProjectStore.ValidateStructure(block.Project);
        if(block.Project.Sheets.Count!=1||block.Project.Sheets[0].BomPage is not null||block.Project.Sheets[0].TerminalPlanId.Length>0||block.Project.Sheets[0].CabinetPlanId.Length>0||block.Project.Sheets[0].Components.Count+block.Project.Sheets[0].Notes.Count+block.Project.Sheets[0].Graphics.Count==0)throw new InvalidDataException("Un bloc doit contenir des objets d’un seul schéma.");
    }
    public static Rect Bounds(Project p)
    {
        var s=p.Sheets[0];Rect bounds=Rect.Empty;
        foreach(var c in s.Components)bounds.Union(c.Bounds());
        foreach(var n in s.Notes)bounds.Union(FreeGraphics.NoteBounds(p,s,n));
        foreach(var g in s.Graphics)bounds.Union(g.Bounds());
        foreach(var point in s.Wires.SelectMany(w=>w.Route(s)).Concat(s.CableMarkers.SelectMany(m=>new[]{m.A.Point,m.B.Point})).Concat(s.TerminalMarkers.SelectMany(m=>new[]{m.A.Point,m.B.Point})))bounds.Union(point);
        return bounds;
    }
    internal static void Translate(Sheet s,double dx,double dy)
    {
        foreach(var c in s.Components){c.X+=dx;c.Y+=dy;}foreach(var n in s.Notes){n.X+=dx;n.Y+=dy;}
        foreach(var g in s.Graphics){g.X+=dx;g.Y+=dy;}
        foreach(var w in s.Wires)w.Waypoints=w.Waypoints.Select(p=>new Position(p.X+dx,p.Y+dy)).ToList();
        foreach(var m in s.CableMarkers){m.A=new(m.A.X+dx,m.A.Y+dy);m.B=new(m.B.X+dx,m.B.Y+dy);}
        foreach(var m in s.TerminalMarkers){m.A=new(m.A.X+dx,m.A.Y+dy);m.B=new(m.B.X+dx,m.B.Y+dy);}
    }
    public static CircuitBlock Extract(Project source,Sheet sheet,IEnumerable<string> selection,string name,string category="Circuits",string tags="")
    {
        var ids=selection.ToHashSet();var chosen=sheet.Components.Where(c=>ids.Contains(c.Id)).Select(c=>c.Id).ToHashSet();
        var s=DocumentModel.Copy(sheet);s.Id=Guid.NewGuid().ToString("N");s.Components.RemoveAll(c=>!chosen.Contains(c.Id));s.Wires.RemoveAll(w=>!chosen.Contains(w.From.ComponentId)||!chosen.Contains(w.To.ComponentId));s.Notes.RemoveAll(n=>!ids.Contains(n.Id));s.Graphics.RemoveAll(g=>!ids.Contains(g.Id));
        int detached=sheet.Wires.Count(w=>chosen.Contains(w.From.ComponentId)^chosen.Contains(w.To.ComponentId));
        foreach(var c in s.Components)
        {
            if(c.CoilId.Length>0&&!chosen.Contains(c.CoilId)){c.CoilId="";detached++;}
            if(c.LinkName.Length>0&&source.Sheets.SelectMany(sh=>sh.Components).Any(other=>other.LinkName==c.LinkName&&!chosen.Contains(other.Id))){c.LinkName="";c.LinkSource=false;detached++;}
        }
        var strips=DocumentModel.Copy(source.TerminalStrips);foreach(var strip in strips){strip.Terminals.RemoveAll(t=>!chosen.Contains(t.ComponentId));strip.Bridges.RemoveAll(b=>!chosen.Contains(b.FromComponent)||!chosen.Contains(b.ToComponent));}strips.RemoveAll(t=>t.Terminals.Count==0);
        s.TerminalMarkers.RemoveAll(m=>!strips.Any(t=>t.Id==m.StripId));var cableIds=s.Wires.Where(w=>w.CableId.Length>0).Select(w=>w.CableId).ToHashSet();s.CableMarkers.RemoveAll(m=>!cableIds.Contains(m.CableId));
        var p=new Project{Title=name,Sheets=[s],CustomSymbols=source.CustomSymbols.Where(pair=>s.Components.Any(c=>c.Symbol==pair.Key)).ToDictionary(pair=>pair.Key,pair=>DocumentModel.Copy(pair.Value)),TerminalStrips=strips,Cables=DocumentModel.Copy(source.Cables.Where(c=>cableIds.Contains(c.Id)).ToList()),Images=DocumentModel.Copy(source.Images.Where(pair=>s.Components.Any(c=>c.ImageId==pair.Key)||s.Graphics.Any(g=>g.ImageId==pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Value))};
        s.TitleBlockId="simple";s.Number="";p.NumberingRules.SheetId="";
        foreach(var c in s.Components)c.ResolvedDefinition=CustomSymbols.Resolve(p,c.Symbol);
        var bounds=Bounds(p);if(bounds.IsEmpty)throw new ArgumentException("Sélectionner les symboles ou notes du bloc.");Translate(s,40-bounds.Left,40-bounds.Top);bounds=Bounds(p);s.Width=Math.Max(1400,bounds.Right+80);s.Height=Math.Max(990,bounds.Bottom+160);
        var block=new CircuitBlock{Name=name,Category=category,Tags=tags,DetachedConnections=detached,Project=p};Validate(block);return block;
    }
    public static void Save(CircuitBlock block,string path)
    {
        Validate(block);if(!path.EndsWith(".gemblock",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Utiliser .gemblock.");var json=JsonSerializer.Serialize(block,ProjectStore.ProjectJson);if(System.Text.Encoding.UTF8.GetByteCount(json)>Project.MaxFileBytes)throw new InvalidDataException("Bloc trop volumineux.");ProjectStore.AtomicWrite(path,json);
    }
    public static CircuitBlock Load(string path)
    {
        if(new FileInfo(path).Length>Project.MaxFileBytes)throw new InvalidDataException("Bloc trop volumineux.");var block=JsonSerializer.Deserialize<CircuitBlock>(File.ReadAllText(path),ProjectStore.Json)??throw new InvalidDataException("Bloc absent.");Validate(block);return block;
    }
}
public sealed partial class Editor
{
    public List<string> InsertBlock(CircuitBlock block,Point anchor)
    {
        CircuitBlocks.Validate(block);var p=ProjectTemplates.Instantiate(block.Project,block.Name,true);var s=p.Sheets[0];var bounds=CircuitBlocks.Bounds(p);double dx=Snap(anchor.X)-bounds.Left,dy=Snap(anchor.Y)-bounds.Top;bounds.Offset(dx,dy);
        var placementArea=Sheet.Kind=="cover"?new Rect(0,0,Sheet.Width,Sheet.Height):SheetZones.Area(Project,Sheet);
        if(!PagePlacement.Editable(Sheet)||!double.IsFinite(anchor.X)||!double.IsFinite(anchor.Y)||!placementArea.Contains(bounds))throw new ArgumentException("Placer le bloc entièrement dans la zone de dessin d’un folio modifiable.");
        CircuitBlocks.Translate(s,dx,dy);List<string> ids=[];
        Change(()=>
        {
            foreach(var pair in p.CustomSymbols){var definition=CustomSymbols.Copy(pair.Value,pair.Value.Name);Project.CustomSymbols.Add(definition.Id,definition);foreach(var c in s.Components.Where(c=>c.Symbol==pair.Key))c.Symbol=definition.Id;}
            var imageIds=p.Images.ToDictionary(pair=>pair.Key,_=>Guid.NewGuid().ToString("N"));foreach(var pair in p.Images)Project.Images.Add(imageIds[pair.Key],pair.Value);
            string Unique(string name,IEnumerable<string> existing){var used=existing.ToHashSet(StringComparer.OrdinalIgnoreCase);if(!used.Contains(name))return name;for(int n=2;;n++)if(!used.Contains(name+"_"+n))return name+"_"+n;}
            foreach(var strip in p.TerminalStrips){strip.Name=Unique(strip.Name,Project.TerminalStrips.Select(t=>t.Name));Project.TerminalStrips.Add(strip);}
            foreach(var cable in p.Cables){cable.Name=Unique(cable.Name,Project.Cables.Select(c=>c.Name));Project.Cables.Add(cable);}
            var reports=s.Components.Where(c=>c.LinkName.Length>0).GroupBy(c=>c.LinkName).ToList();foreach(var group in reports){string name=Unique(group.Key,Project.Sheets.SelectMany(sh=>sh.Components).Select(c=>c.LinkName));foreach(var c in group)c.LinkName=name;}
            foreach(var c in s.Components)
            {
                c.ResolvedDefinition=CustomSymbols.Resolve(Project,c.Symbol);if(c.ImageId.Length>0)c.ImageId=imageIds[c.ImageId];
                c.Tag=NextTag(c.Definition.Prefix);c.TagLocked=c.TerminalNumberOnly;Sheet.Components.Add(c);ids.Add(c.Id);
            }
            foreach(var c in s.Components.Where(c=>c.CoilId.Length>0))c.Tag=s.Components.Single(root=>root.Id==c.CoilId).Tag;
            foreach(var strip in p.TerminalStrips)foreach(var t in strip.Terminals){var c=s.Components.Single(c=>c.Id==t.ComponentId);if(c.TerminalNumberOnly)c.Tag=strip.Name+":"+t.Number;}
            var numbers=new Dictionary<string,string>();foreach(var w in s.Wires){string key=w.Number;if(!numbers.TryGetValue(key,out string? number)){number="W"+NextWireNumber();numbers[key]=number;}w.Number=number;w.RoutingMode="manual";w.RoutingWarning="";Sheet.Wires.Add(w);ids.Add(w.Id);}
            foreach(var n in s.Notes){Sheet.Notes.Add(n);ids.Add(n.Id);}foreach(var g in s.Graphics){if(g.ImageId.Length>0)g.ImageId=imageIds[g.ImageId];Sheet.Graphics.Add(g);ids.Add(g.Id);}Sheet.CableMarkers.AddRange(s.CableMarkers);Sheet.TerminalMarkers.AddRange(s.TerminalMarkers);
        });return ids;
    }
}
