using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace GeMMeElec;

public sealed class RichRun
{
    public string Text { get; set; } = "";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
}
public sealed class RichTextContent
{
    public string Text { get; set; } = "";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public List<RichRun> Runs { get; set; } = [];
    public double FontSize { get; set; } = 16;
    public string Color { get; set; } = "#203348";
    public string Align { get; set; } = "left";
    public string Vertical { get; set; } = "center";
}
public static class RichText
{
    public static List<RichRun> Effective(string text, bool bold, bool italic, IReadOnlyList<RichRun>? runs) => runs is { Count: > 0 } ? runs.ToList() : [new() { Text=text, Bold=bold, Italic=italic }];
    public static void Validate(string text, IReadOnlyList<RichRun>? runs)
    {
        if(text is null || text.Length>2000 || runs is null || runs.Count>2000 || runs.Any(r=>r is null || r.Text is null || r.Text.Length>2000) || runs.Count>0 && string.Concat(runs.Select(r=>r.Text))!=text) throw new InvalidDataException("Texte formaté invalide : maximum 2 000 caractères, passages correspondant au texte.");
    }
    public static void Validate(RichTextContent? value)
    {
        if(value is null || !double.IsFinite(value.FontSize) || value.FontSize is <4 or >256 || !FreeGraphics.Color(value.Color) || value.Align is not ("left" or "center" or "right") || value.Vertical is not ("top" or "center" or "bottom")) throw new InvalidDataException("Style du texte formaté invalide.");
        Validate(value.Text,value.Runs);
    }
    public static void Normalize(RichTextContent value)
    {
        var runs=new List<RichRun>();foreach(var run in value.Runs.Where(r=>r.Text.Length>0))
        {
            if(runs.LastOrDefault() is RichRun last && last.Bold==run.Bold && last.Italic==run.Italic)last.Text+=run.Text;
            else runs.Add(DocumentModel.Copy(run));
        }
        value.Runs=runs;
        if(runs.Count==1){value.Bold=runs[0].Bold;value.Italic=runs[0].Italic;value.Runs.Clear();}
    }
    public static RichTextContent Note(Annotation n)=>new(){Text=n.Text,Bold=n.Bold,Italic=n.Italic,Runs=DocumentModel.Copy(n.Runs),FontSize=n.FontSize,Color=n.Color,Align=n.Align,Vertical="top"};
    public static void SetNote(Annotation n,RichTextContent value){Validate(value);n.Text=value.Text;n.Bold=value.Bold;n.Italic=value.Italic;n.Runs=DocumentModel.Copy(value.Runs);n.FontSize=value.FontSize;n.Color=value.Color;n.Align=value.Align;}
    public static void Uniform(RichTextContent value,bool? bold=null,bool? italic=null)
    {
        if(bold is bool b){value.Bold=b;foreach(var r in value.Runs)r.Bold=b;}
        if(italic is bool i){value.Italic=i;foreach(var r in value.Runs)r.Italic=i;}
        Normalize(value);
    }
    public static List<RichRun> ResolveRuns(string text,IReadOnlyList<RichRun> runs,IReadOnlyDictionary<string,string> values)
    {
        var result=new List<RichRun>();var offsets=new List<(int Start,RichRun Run)>();int offset=0;foreach(var r in runs){offsets.Add((offset,r));offset+=r.Text.Length;}
        void Slice(int start,int length){foreach(var item in offsets){int left=Math.Max(start,item.Start),right=Math.Min(start+length,item.Start+item.Run.Text.Length);if(right>left)result.Add(new(){Text=text[left..right],Bold=item.Run.Bold,Italic=item.Run.Italic});}}
        int position=0;foreach(Match token in Regex.Matches(text,@"\{([^{}]+)\}")){Slice(position,token.Index-position);var style=offsets.Last(o=>o.Start<=token.Index).Run;result.Add(new(){Text=values.GetValueOrDefault(token.Groups[1].Value,token.Value),Bold=style.Bold,Italic=style.Italic});position=token.Index+token.Length;}Slice(position,text.Length-position);return result;
    }
    // One layout supplies screen and vector exports, including mixed styles and wrapping.
    private static List<List<RichRun>> Lines(string text,bool bold,bool italic,IReadOnlyList<RichRun>? runs,double size,double width,bool wrap)
    {
        List<List<RichRun>> lines=[[]];double used=0;
        void NewLine(){lines.Add([]);used=0;}
        void Append(string value,RichRun style)
        {
            if(value.Length==0)return;var line=lines[^1];if(line.LastOrDefault() is RichRun last&&last.Bold==style.Bold&&last.Italic==style.Italic){double previous=Drawing.Measure(last.Text,size,last.Bold,last.Italic).Width;last.Text+=value;used+=Drawing.Measure(last.Text,size,last.Bold,last.Italic).Width-previous;}else{line.Add(new(){Text=value,Bold=style.Bold,Italic=style.Italic});used+=Drawing.Measure(value,size,style.Bold,style.Italic).Width;}
        }
        foreach(var run in Effective(text,bold,italic,runs))foreach(Match token in Regex.Matches(run.Text.Replace("\r\n","\n").Replace('\r','\n'),@"\n|[^\S\n]+|[^\s]+"))
        {
            string word=token.Value;if(word=="\n"){NewLine();continue;}double length=Drawing.Measure(word,size,run.Bold,run.Italic).Width;
            if(wrap&&used>0&&used+length>width)NewLine();
            if(wrap&&length>width)
            {
                foreach(var rune in word.EnumerateRunes()){string ch=rune.ToString();if(used>0&&used+Drawing.Measure(ch,size,run.Bold,run.Italic).Width>width)NewLine();Append(ch,run);}
            }
            else Append(word,run);
        }
        return lines;
    }
    public static IEnumerable<Ink> Unwrapped(Ink ink)
    {
        var lines=Lines(ink.Text,ink.Bold,ink.Italic,ink.Runs,ink.Size,double.MaxValue,false);double angle=ink.Rotation*Math.PI/180;
        double step=Effective(ink.Text,ink.Bold,ink.Italic,ink.Runs).Select(r=>Drawing.Measure("Mg",ink.Size,r.Bold,r.Italic).Height).DefaultIfEmpty(Drawing.Measure("Mg",ink.Size).Height).Max();
        for(int row=0;row<lines.Count;row++){double offset=0;foreach(var run in lines[row]){yield return ink with{Text=run.Text,X=ink.X+Math.Cos(angle)*offset-Math.Sin(angle)*row*step,Y=ink.Y+Math.Sin(angle)*offset+Math.Cos(angle)*row*step,Bold=run.Bold,Italic=run.Italic,Runs=null};offset+=Drawing.Measure(run.Text,ink.Size,run.Bold,run.Italic).Width;}}
    }
    public static List<Ink> Layout(RichTextContent content,Rect area,double padding=6)
    {
        if(content.Text.Length==0)return [];
        double width=Math.Max(1,area.Width-2*padding),height=Math.Max(1,area.Height-2*padding);var lines=Lines(content.Text,content.Bold,content.Italic,content.Runs,content.FontSize,width,true);
        double step=Effective(content.Text,content.Bold,content.Italic,content.Runs).Select(r=>Drawing.Measure("Mg",content.FontSize,r.Bold,r.Italic).Height).DefaultIfEmpty(Drawing.Measure("Mg",content.FontSize).Height).Max();
        double top=area.Y+padding+(content.Vertical=="top"?0:content.Vertical=="bottom"?Math.Max(0,height-lines.Count*step):Math.Max(0,(height-lines.Count*step)/2));
        List<Ink> result=[new("clipStart",area.X,area.Y,area.Width,area.Height)];
        for(int row=0;row<lines.Count;row++)
        {
            double lineWidth=lines[row].Sum(r=>Drawing.Measure(r.Text,content.FontSize,r.Bold,r.Italic).Width);double x=area.X+padding+(content.Align=="center"?(width-lineWidth)/2:content.Align=="right"?width-lineWidth:0);
            foreach(var run in lines[row]){result.Add(new("text",x,top+row*step,Text:run.Text,Size:content.FontSize,Color:content.Color,Bold:run.Bold,Italic:run.Italic));x+=Drawing.Measure(run.Text,content.FontSize,run.Bold,run.Italic).Width;}
        }
        result.Add(new("clipEnd",0,0));return result;
    }
}
public sealed partial class Editor
{
    public void SetTextStyle(string id,string textKey,bool? bold,bool? italic)
    {
        if(bold is null&&italic is null)throw new ArgumentException("Choisir gras ou italique.");
        Change(()=>
        {
            if(Sheet.Notes.FirstOrDefault(n=>n.Id==id) is Annotation note){var value=RichText.Note(note);RichText.Uniform(value,bold,italic);RichText.SetNote(note,value);}
            else if(Sheet.Components.FirstOrDefault(c=>c.Id==id) is Component c){var placement=DocumentModel.Copy(ComponentTexts.Get(c,textKey.Length==0?"tag":textKey).Placement);if(bold is bool b)placement.Bold=b;if(italic is bool i)placement.Italic=i;ComponentTexts.SetPlacement(c,textKey.Length==0?"tag":textKey,placement);}
            else if(Sheet.Wires.FirstOrDefault(w=>w.Id==id) is Wire w){if(bold is bool b)w.NumberPlacement.Bold=b;if(italic is bool i)w.NumberPlacement.Italic=i;}
            else if(Sheet.Graphics.FirstOrDefault(g=>g.Id==id) is SheetGraphic g&&g.Kind is "textbox" or "table"){if(g.Table is null)RichText.Uniform(g.Content,bold,italic);else foreach(var cell in g.Table.Cells)RichText.Uniform(cell,bold,italic);}
            else throw new ArgumentException("Texte introuvable.");
        });
    }
}
