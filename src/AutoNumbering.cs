using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public sealed class NumberingRules
{
    public string Scope { get; set; } = "all";
    public string SheetId { get; set; } = "";
    public string ComponentPattern { get; set; } = "{prefix}{n}";
    public string WirePattern { get; set; } = "{prefix}{n}";
    public string WirePrefix { get; set; } = "W";
    public int Start { get; set; } = 1;
    public bool CounterPerFolio { get; set; }
    public void Validate(Project p)
    {
        if (Scope is not ("all" or "components" or "wires") || SheetId is null || SheetId.Length > 0 && !p.Sheets.Any(s=>s.Id==SheetId) || Start is < 0 or > 999999 || WirePrefix is null || WirePrefix.Length > 40) throw new ArgumentException("Numérotation : périmètre et compteur invalides.");
        foreach (string pattern in new[] { ComponentPattern, WirePattern }) if (pattern is null || pattern.Length is < 1 or > 120 || !Regex.IsMatch(pattern,@"\{n(?::0{1,6})?\}") || Regex.IsMatch(Regex.Replace(pattern,@"\{(?:prefix|folio|n(?::0{1,6})?)\}",""),@"[{}]")) throw new ArgumentException("Format : utiliser {prefix}, {folio}, {n} ou {n:000} ; un compteur est obligatoire.");
    }
}
public sealed record NumberingChange(string SheetId, string ObjectId, string Type, string Folio, string Before, string After, string Status);
public sealed record NumberingPlan(string SourceHash, NumberingRules Rules, List<NumberingChange> Changes)
{
    public bool CanApply => Changes.All(c=>c.Status!="Conflit");
}
public static class AutoNumbering
{
    public static string Hash(Project p) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectStore.Serialize(p))));
    public static NumberingPlan Preview(Project p, NumberingRules rules)
    {
        rules.Validate(p); var changes=new List<NumberingChange>(); var sheets=p.Sheets.Where(s=>rules.SheetId.Length==0||s.Id==rules.SheetId).ToList(); var counters=new Dictionary<string,int>();
        string Next(string pattern,string prefix,Sheet s) { string key=prefix+(rules.CounterPerFolio?":"+s.Id:""); int n=counters.GetValueOrDefault(key,rules.Start); counters[key]=n+1; return Regex.Replace(pattern,@"\{(prefix|folio|n(?::0{1,6})?)\}",m=>m.Groups[1].Value switch { "prefix"=>prefix,"folio"=>DocumentModel.PageNumber(p,s),"n"=>n.ToString(),var format=>n.ToString(format[2..],System.Globalization.CultureInfo.InvariantCulture) }); }
        if(rules.Scope is "all" or "components")
        {
            var all=p.Sheets.SelectMany(s=>s.Components.Select(c=>(Sheet:s,Component:c))).ToList();
            var roots=all.Where(x=>!Junctions.Automatic(x.Component)&&!CrossReferences.IsPoint(x.Component)&&x.Component.CoilId.Length==0).ToList(); var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var root in roots.Where(x=>x.Component.TagLocked||!sheets.Contains(x.Sheet)||all.Any(c=>c.Component.CoilId==x.Component.Id&&c.Component.TagLocked))) used.Add(root.Component.Tag);
            foreach(var root in roots.Where(x=>sheets.Contains(x.Sheet)))
            {
                var c=root.Component; bool locked=c.TagLocked||all.Any(x=>x.Component.CoilId==c.Id&&x.Component.TagLocked); string after=locked?c.Tag:Next(rules.ComponentPattern,c.Definition.Prefix,root.Sheet); string status=locked?"Verrouillé":used.Add(after)?"Prévu":"Conflit";
                changes.Add(new(root.Sheet.Id,c.Id,"Appareil",DocumentModel.PageNumber(p,root.Sheet),c.Tag,after,status));
            }
        }
        counters.Clear();
        if(rules.Scope is "all" or "wires")
        {
            var networks=new ConductorNetworks(p); var seen=new HashSet<string>(); var used=new HashSet<string>(p.Sheets.SelectMany(s=>s.Wires).Where(w=>w.NumberIndependent).Select(w=>w.Number),StringComparer.OrdinalIgnoreCase);
            var groups=new List<(Sheet Sheet,List<Wire> Net,List<Wire> Regular,bool Target)>();
            foreach(var s in p.Sheets) foreach(var wire in s.Wires) if(seen.Add(wire.Id))
            {
                var net=networks.Connected(wire.From).ToList(); foreach(var w in net)seen.Add(w.Id); var regular=net.Where(w=>!w.NumberIndependent).ToList(); bool target=sheets.Any(sheet=>sheet.Wires.Any(w=>regular.Any(r=>r.Id==w.Id)));
                groups.Add((s,net,regular,target));if(!target)foreach(var w in regular)used.Add(w.Number);
            }
            foreach(var(s,net,regular,target)in groups)
            {
                if(!target)continue;
                string after=regular.Count>0?Next(rules.WirePattern,rules.WirePrefix,s):""; string status=regular.Count>0&&(after.Length==0||!used.Add(after))?"Conflit":"Prévu";
                foreach(var sheet in p.Sheets) foreach(var w in sheet.Wires.Where(w=>net.Any(n=>n.Id==w.Id))) changes.Add(new(sheet.Id,w.Id,"Conducteur",DocumentModel.PageNumber(p,sheet),w.Number,w.NumberIndependent?w.Number:after,w.NumberIndependent?"Indépendant":status));
            }
        }
        return new(Hash(p),DocumentModel.Copy(rules),changes);
    }
}
public sealed partial class Editor
{
    public void ApplyNumbering(NumberingPlan plan)
    {
        if(plan.SourceHash!=AutoNumbering.Hash(Project))throw new InvalidOperationException("Le projet a changé. Actualiser l’aperçu de numérotation."); if(!plan.CanApply)throw new ArgumentException("Corriger les conflits avant d’appliquer.");
        Change(()=> { var components=Project.Sheets.SelectMany(s=>s.Components).ToDictionary(c=>c.Id);var wires=Project.Sheets.SelectMany(s=>s.Wires).ToDictionary(w=>w.Id); foreach(var row in plan.Changes.Where(c=>c.Status=="Prévu")) { if(row.Type=="Appareil")SetApparatusTag(components[row.ObjectId],row.After);else wires[row.ObjectId].Number=row.After; } Project.NumberingRules=DocumentModel.Copy(plan.Rules); });
    }
}
public sealed partial class MainWindow
{
    private void NumberingDialog()
    {
        Canvas.CancelDrag(); var dialog=DocumentWindow("Numérotation automatique · aperçu et verrous",1150,800);var root=new DockPanel{Margin=new(18)};dialog.Content=root;var controls=new StackPanel{Width=285,Margin=new(0,0,15,0)};DockPanel.SetDock(controls,Dock.Left);root.Children.Add(controls);var rules=DocumentModel.Copy(Editor.Project.NumberingRules);
        controls.Children.Add(Hint("Formats : {prefix}, {folio}, {n}, {n:000}. Les appareils bobine/contact partagent un repère. Les fils indépendants et repères verrouillés sont conservés."));
        var scope=new ComboBox{ItemsSource=new[]{new{Value="all",Text="Appareils et fils"},new{Value="components",Text="Appareils"},new{Value="wires",Text="Conducteurs"}},DisplayMemberPath="Text",SelectedValuePath="Value",SelectedValue=rules.Scope,Margin=new(0,6,0,8)};controls.Children.Add(scope);
        var componentPattern=DocField(controls,"Format des appareils",rules.ComponentPattern);var wirePattern=DocField(controls,"Format des fils",rules.WirePattern);var prefix=DocField(controls,"Préfixe des fils",rules.WirePrefix);var start=DocField(controls,"Premier compteur",rules.Start.ToString());var local=new CheckBox{Content="Folio courant uniquement",IsChecked=rules.SheetId.Length>0,Margin=new(0,10,0,5)};controls.Children.Add(local);var perSheet=new CheckBox{Content="Compteur par folio",IsChecked=rules.CounterPerFolio,Margin=new(0,5,0,10)};controls.Children.Add(perSheet);
        var area=new DockPanel();root.Children.Add(area);var info=Hint("");DockPanel.SetDock(info,Dock.Top);area.Children.Add(info);var grid=new DataGrid{IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,Name="NumberingPreview"};area.Children.Add(grid);grid.AutoGeneratingColumn+=(_,e)=>{if(e.PropertyName is "ObjectId" or "SheetId")e.Cancel=true;else e.Column.Header=e.PropertyName switch{"Before"=>"Avant","After"=>"Après","Status"=>"État",_=>e.PropertyName};};NumberingPlan? plan=null;
        void Preview(){if(!int.TryParse(start.Text,out int n))throw new ArgumentException("Compteur entier requis.");rules=new(){Scope=(string)scope.SelectedValue,SheetId=local.IsChecked==true?Editor.Sheet.Id:"",ComponentPattern=componentPattern.Text,WirePattern=wirePattern.Text,WirePrefix=prefix.Text,Start=n,CounterPerFolio=perSheet.IsChecked==true};plan=AutoNumbering.Preview(Editor.Project,rules);grid.ItemsSource=plan.Changes;info.Text=plan.CanApply?plan.Changes.Count+" ligne(s) · vérifier l’aperçu puis appliquer":"Conflits : modifier le format ou le compteur avant application";}
        controls.Children.Add(DocButton("Actualiser l’aperçu",dialog,Preview));controls.Children.Add(DocButton("Appliquer en une action",dialog,()=>{string previousRules=System.Text.Json.JsonSerializer.Serialize(plan?.Rules);Preview();if(previousRules!=System.Text.Json.JsonSerializer.Serialize(plan!.Rules)){info.Text="Aperçu actualisé. Vérifier les changements puis cliquer de nouveau sur Appliquer.";return;}Editor.ApplyNumbering(plan);dialog.Close();}));controls.Children.Add(DocButton("Fermer",dialog,dialog.Close));Preview();ShowDocumentDialog(dialog);
    }
}
