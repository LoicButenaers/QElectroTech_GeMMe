using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

public sealed class BomColumn
{
    public string Key { get; set; } = "description";
    public string Label { get; set; } = "Description";
    public bool Enabled { get; set; } = true;
}
public sealed class BomOptions
{
    public List<BomColumn> Columns { get; set; } = BomReport.Fields.Select(x=>new BomColumn {Key=x.Key,Label=x.Value,Enabled=x.Key is not("symbol" or "catalog" or "source")}).ToList();
    public bool GroupArticles { get; set; } = true;
    public bool IncludeAuxiliary { get; set; }
    public string SheetId { get; set; } = "";
    public string Brand { get; set; } = "";
    public void Validate()
    {
        if(Columns is null || Columns.Count==0 || Columns.Count>BomReport.Fields.Count || Columns.Any(c=>c is null || !BomReport.Fields.ContainsKey(c.Key)||string.IsNullOrWhiteSpace(c.Label)||c.Label.Length>100)||Columns.Select(c=>c.Key).Distinct().Count()!=Columns.Count||!Columns.Any(c=>c.Enabled)||SheetId is null||Brand is null||Brand.Length>2000) throw new InvalidDataException("Configuration de nomenclature invalide ; choisir au moins une colonne.");
    }
}
public sealed record BomRow(Dictionary<string,string> Values,string ImageId,string Symbol);
public static class BomReport
{
    public static readonly Dictionary<string,string> Fields=new() { ["quantity"]="Quantité",["image"]="Image",["tags"]="Repères",["pages"]="Folios / zones",["description"]="Description",["brand"]="Fabricant",["range"]="Gamme",["reference"]="Référence",["rating"]="Caractéristiques",["symbol"]="Symbole",["catalog"]="ID bibliothèque",["source"]="Documentation" };
    public static List<BomRow> Rows(Project p,BomOptions options)
    {
        options.Validate();
        if(options.SheetId.Length>0&&!p.Sheets.Any(s=>s.Id==options.SheetId)) throw new InvalidDataException("Folio de nomenclature introuvable ; choisir Tous les folios.");
        var items=p.Sheets.Where(s=>s.BomPage is null&&(options.SheetId.Length==0||s.Id==options.SheetId)).SelectMany(s=>s.Components.Where(c=>!CrossReferences.IsPoint(c)&&!Junctions.Automatic(c)&&c.Definition.Ports.Count>0&&(options.IncludeAuxiliary||c.Symbol is not("junction" or "earth" or "source"))&&(options.Brand.Length==0||c.Brand.Equals(options.Brand,StringComparison.OrdinalIgnoreCase))).Select(c=>(s,c)));
        var groups=items.GroupBy(x=>new { Identity=options.GroupArticles?"":x.c.Id, x.c.Symbol,x.c.Brand,x.c.Range,x.c.Reference,x.c.Description,x.c.Rating,x.c.ImageId,x.c.CatalogId,x.c.Source });
        return groups.Select(g=>new BomRow(new() { ["quantity"]=g.Count().ToString(CultureInfo.InvariantCulture),["tags"]=string.Join(", ",g.Select(x=>x.c.Tag)),["pages"]=string.Join(", ",g.Select(x=>x.c.Tag+" : "+SheetZones.Address(p,x.s,x.c))),["description"]=g.Key.Description,["brand"]=g.Key.Brand,["range"]=g.Key.Range,["reference"]=g.Key.Reference,["rating"]=g.Key.Rating,["symbol"]=g.Key.Symbol,["catalog"]=g.Key.CatalogId,["source"]=g.Key.Source,["image"]=g.Key.ImageId.Length>0?"Image intégrée "+g.Key.ImageId:"Symbole : "+g.Key.Symbol },g.Key.ImageId,g.Key.Symbol)).ToList();
    }
    public static string Csv(Project p,BomOptions o)
    {
        string Cell(string v) { if(v.TrimStart().StartsWith('=')||v.TrimStart().StartsWith('+')||v.TrimStart().StartsWith('-')||v.TrimStart().StartsWith('@'))v="'"+v;return "\""+v.Replace("\"","\"\"")+"\""; }
        var cols=o.Columns.Where(c=>c.Enabled).ToArray();var b=new StringBuilder();b.AppendLine(string.Join(';',cols.Select(c=>Cell(c.Label))));
        foreach(var row in Rows(p,o))b.AppendLine(string.Join(';',cols.Select(c=>Cell(row.Values[c.Key]))));return b.ToString();
    }
    public static ImageSource Picture(Project p,BomRow row)
    {
        if(row.ImageId.Length>0)return ImageAssets.Decode(p.Images[row.ImageId]);
        var d=CustomSymbols.Resolve(p, row.Symbol);var drawing=new DrawingGroup();using(var dc=drawing.Open()) { double scale=Math.Min(70/d.Width,70/d.Height);dc.PushTransform(new TranslateTransform(40,40));dc.PushTransform(new ScaleTransform(scale,scale));Drawing.Render(dc,d.Shapes.Select(s=>new Ink(s.Kind,s.X,s.Y,s.A,s.B,s.Text,Size:s.A,Rotation:s.Rotation))); }
        drawing.Freeze();var image=new DrawingImage(drawing);image.Freeze();return image;
    }
    private static string SymbolSvg(Project p, string symbol)
    {
        var d=CustomSymbols.Resolve(p, symbol);string N(double x)=>x.ToString("0.###",CultureInfo.InvariantCulture);var b=new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='{N(-d.Width/2-8)} {N(-d.Height/2-8)} {N(d.Width+16)} {N(d.Height+16)}' width='72' height='72'><g fill='none' stroke='#203348' stroke-width='2'>");
        foreach(var s in d.Shapes)
        {
            if(s.Rotation!=0)b.Append($"<g transform='rotate({N(s.Rotation)} {N(s.X)} {N(s.Y)})'>");
            if(s.Kind=="line")b.Append($"<line x1='{N(s.X)}' y1='{N(s.Y)}' x2='{N(s.A)}' y2='{N(s.B)}'/>");
            else if(s.Kind=="rect")b.Append($"<rect x='{N(s.X)}' y='{N(s.Y)}' width='{N(s.A)}' height='{N(s.B)}'/>");
            else if(s.Kind is "circle" or "dot")b.Append($"<circle cx='{N(s.X)}' cy='{N(s.Y)}' r='{N(s.A)}' fill='{(s.Kind=="dot"?"#203348":"white")}'/>");
            else if(s.Kind=="text")b.Append($"<text x='{N(s.X)}' y='{N(s.Y+s.A)}' stroke='none' fill='#203348' font-size='{N(s.A)}'>{WebUtility.HtmlEncode(s.Text)}</text>");
            else if(s.Kind=="image")b.Append($"<image x='{N(s.X)}' y='{N(s.Y)}' width='{N(s.A)}' height='{N(s.B)}' href='data:{(s.Text.StartsWith("iVBOR")?"image/png":"image/jpeg")};base64,{s.Text}'/>");
            if(s.Rotation!=0)b.Append("</g>");
        }
        return b.Append("</g></svg>").ToString();
    }
    public static string Html(Project p,BomOptions o)
    {
        string H(string x)=>WebUtility.HtmlEncode(x);var cols=o.Columns.Where(c=>c.Enabled).ToArray();
        var b=new StringBuilder("<!doctype html><html lang='fr'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Nomenclature</title><style>body{font:14px Segoe UI,Arial;margin:32px;color:#203348}table{border-collapse:collapse;width:100%}th,td{border:1px solid #cbd5e1;padding:8px;text-align:left;white-space:pre-wrap;overflow-wrap:anywhere}th{background:#edf3f8}img,svg{max-width:80px;max-height:80px}tr{break-inside:avoid}thead{display:table-header-group}@media print{body{margin:0;font-size:10px} @page{size:A3 landscape;margin:12mm}}</style>");
        b.Append($"<h1>Nomenclature — {H(p.Title)}</h1><p>{H(p.Customer)} · Document {H(p.DocumentNumber)} · Révision {H(p.Revision)}</p><p>Images : photo ajoutée au composant ou dessin du symbole utilisé.</p><table><thead><tr>");foreach(var col in cols)b.Append($"<th>{H(col.Label)}</th>");b.Append("</tr></thead><tbody>");
        var images=new Dictionary<string,string>();
        foreach(var row in Rows(p,o)) { b.Append("<tr>");foreach(var col in cols) { b.Append("<td>");if(col.Key=="image") { string key=row.ImageId.Length>0?row.ImageId:row.Symbol;if(!images.TryGetValue(key,out var html)) { html=row.ImageId.Length>0?$"<img alt='Image du composant' src='data:image/jpeg;base64,{p.Images[row.ImageId]}'>":SymbolSvg(p, row.Symbol);images[key]=html; }b.Append(html); }else b.Append(H(row.Values[col.Key]));b.Append("</td>"); }b.Append("</tr>"); }
        return b.Append("</tbody></table></html>").ToString();
    }
}
