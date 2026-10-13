using System.IO;
using System.Text.Json;

namespace GeMMeElec;

// Only this closed set of scalar fields uses local transactions. Topology,
// symbol definitions and arbitrary actions still require full validation.
public sealed partial class Editor
{
    private SheetStateCache? sheetStateCache;
    private SheetStateCache StateCache => sheetStateCache ??= new(currentState,Project);
    public void SetComponentField(string id,string key,string value)
    {
        if(InteractionInProgress)throw new InvalidOperationException("Terminer le déplacement en cours.");
        if(value is null||value.Length>2000)throw new ArgumentException("Texte limité à 2000 caractères.");
        var located=Project.Sheets.SelectMany(s=>s.Components.Select(c=>(Sheet:s,Component:c))).FirstOrDefault(x=>x.Component.Id==id);
        var c=located.Component??throw new ArgumentException("Composant introuvable.");
        double coordinate=0;int angle=0;
        if(key is "X" or "Y") { if(!double.TryParse(value.Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out coordinate)||!double.IsFinite(coordinate)||coordinate<0||coordinate>(key=="X"?located.Sheet.Width:located.Sheet.Height))throw new ArgumentException("Coordonnée hors du folio."); }
        else if(key=="rotation") { if(!int.TryParse(value,out angle)||angle%90!=0||angle is < -360 or >360)throw new ArgumentException("Saisir un multiple de 90° entre −360 et 360."); }
        else if(key.StartsWith("port:")) { if(!c.Definition.Ports.Any(p=>p.Id==key[5..]))throw new ArgumentException("Borne inconnue."); }
        else if(key is not ("tag" or "description" or "brand" or "range" or "reference" or "rating"))throw new ArgumentException("Champ non pris en charge.");
        string root=c.CoilId.Length>0?c.CoilId:c.Id;
        var affected=key=="tag"?Project.Sheets.Where(s=>s.Components.Any(x=>x.Id==root||x.CoilId==root)).ToList():[located.Sheet];
        var ids=affected.Select(s=>s.Id).ToArray();var cache=StateCache;string before=cache.Bundle(ids);string historyKey="local:"+id+":"+key;
        switch(key){case "tag":SetApparatusTag(c,value);break;case "description":c.Description=value;break;case "brand":c.Brand=value;break;case "range":c.Range=value;break;case "reference":c.Reference=value;break;case "rating":c.Rating=value;break;case "X":c.X=coordinate;break;case "Y":c.Y=coordinate;break;case "rotation":c.Rotation=angle;break;default:c.PortLabels[key[5..]]=value;break;}
        cache.Update(affected);string after=cache.Bundle(ids);
        if(before!=after){if(propertyHistoryKey!=historyKey||undo.Count==0||DateTime.UtcNow-propertyHistoryTime>TimeSpan.FromSeconds(2))undo.Add(HistorySnapshot.CaptureSheets(before,ids));redo.Clear();TrimHistory();propertyHistoryKey=historyKey;propertyHistoryTime=DateTime.UtcNow;}
        currentState=cache.Compose();Changed?.Invoke();
    }
    private void RestoreSheets(List<HistorySnapshot> source,List<HistorySnapshot> destination)
    {
        var entry=source[^1];var ids=entry.SheetIds!;var restored=JsonSerializer.Deserialize<List<Sheet>>(entry.Restore(),ProjectStore.Json)??throw new InvalidDataException("Historique vide.");
        if(!restored.Select(s=>s.Id).SequenceEqual(ids)||ids.Any(id=>!Project.Sheets.Any(s=>s.Id==id)))throw new InvalidDataException("Historique incohérent ; le projet courant est conservé.");
        foreach(var sheet in restored)foreach(var c in sheet.Components)c.ResolvedDefinition=CustomSymbols.Resolve(Project,c.Symbol);
        var cache=StateCache;var outgoing=HistorySnapshot.CaptureSheets(cache.Bundle(ids),ids);
        foreach(var sheet in restored)Project.Sheets[Project.Sheets.FindIndex(s=>s.Id==sheet.Id)]=sheet;
        cache.Update(restored);currentState=cache.Compose();destination.Add(outgoing);source.RemoveAt(source.Count-1);TrimHistory();RepairSelection();Changed?.Invoke();
    }
    private sealed class SheetStateCache
    {
        private readonly string prefix;
        private readonly string[] order;
        private readonly Dictionary<string,string> sheets;
        public SheetStateCache(string state,Project p)
        {
            // This is our already validated, canonical serializer output, not an
            // external JSON parser. Splitting raw folios avoids a second full DOM.
            const string marker="\"sheets\":[";int start=state.LastIndexOf(marker,StringComparison.Ordinal);
            if(start<0||!state.EndsWith("]}",StringComparison.Ordinal))throw new InvalidOperationException("Ordre de sérialisation inattendu.");
            start+=marker.Length;prefix=state[..(start-1)];order=p.Sheets.Select(s=>s.Id).ToArray();sheets=[];int at=start;
            foreach(string id in order){if(at>=state.Length||state[at]!='{')throw new InvalidOperationException("Cache de folios incohérent.");int begin=at,depth=0;bool quoted=false,escape=false;do{char ch=state[at++];if(quoted){if(escape)escape=false;else if(ch=='\\')escape=true;else if(ch=='\"')quoted=false;}else if(ch=='\"')quoted=true;else if(ch is '{' or '[')depth++;else if(ch is '}' or ']')depth--;}while(depth>0&&at<state.Length);sheets.Add(id,state[begin..at]);if(at<state.Length&&state[at]==',')at++;}
            if(at!=state.Length-2)throw new InvalidOperationException("Cache de folios incomplet.");
        }
        public string Bundle(string[] ids)=>"["+string.Join(",",ids.Select(id=>sheets[id]))+"]";
        public void Update(IEnumerable<Sheet> changed){foreach(var sheet in changed)sheets[sheet.Id]=JsonSerializer.Serialize(sheet,ProjectStore.ProjectJson);}
        public string Compose()=>string.Create(prefix.Length+3+order.Length-1+order.Sum(id=>sheets[id].Length),this,static(span,cache)=>{int at=0;cache.prefix.AsSpan().CopyTo(span);at+=cache.prefix.Length;span[at++]='[';for(int i=0;i<cache.order.Length;i++){if(i>0)span[at++]=',';string sheet=cache.sheets[cache.order[i]];sheet.AsSpan().CopyTo(span[at..]);at+=sheet.Length;}span[at++]=']';span[at]='}';});
    }
}
