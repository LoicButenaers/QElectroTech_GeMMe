using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

public sealed class TitleBlockTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nouveau cartouche";
    public double Height { get; set; } = 130;
    public List<TitleBlockCell> Cells { get; set; } = [];
}
// Coordinates are percentages of the cartouche, making templates portable between folios.
public sealed class TitleBlockCell
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 25;
    public double Height { get; set; } = 50;
    public string Text { get; set; } = "Texte";
    public double FontSize { get; set; } = 12;
    public string Align { get; set; } = "left";
    public string Vertical { get; set; } = "center";
    public bool Border { get; set; } = true;
    public string ImageId { get; set; } = "";
}
public static class TitleBlocksDefaults
{
    public static List<TitleBlockTemplate> Create() => [
        new() { Id = "simple", Name = "Simple", Height = 60, Cells = [
            new() { X=0, Y=0, Width=70, Height=100, Text="{project.title}\n{sheet.title}" },
            new() { X=70, Y=0, Width=30, Height=100, Text="Révision {sheet.revision} · {sheet.author}\nFolio {sheet.number} / {sheet.total}" }] },
        Technical()
    ];
    public static TitleBlockTemplate Technical()
    {
        var t = new TitleBlockTemplate { Id="technical", Name="Technique · révisions", Height=140 };
        void Cell(double x, double y, double w, double h, string text, double font=11, string align="center") => t.Cells.Add(new() { X=x,Y=y,Width=w,Height=h,Text=text,FontSize=font,Align=align });
        Cell(0,0,13,100,"LOGO / SOCIÉTÉ\n{project.customer}");
        Cell(13,0,14,33.333,"DESSINÉ PAR\n{sheet.author}"); Cell(13,33.333,14,33.334,"VÉRIFIÉ PAR\n{sheet.checkedBy}"); Cell(13,66.667,14,33.333,"DATE\n{sheet.date}");
        double[] xs=[27,32,40,54], ws=[5,8,14,6]; string[] heads=["IND.","DATE","MODIFICATION","PAR"];
        for(int i=0;i<4;i++) { for(int r=0;r<3;r++) Cell(xs[i],r*25,ws[i],25,"",9); Cell(xs[i],75,ws[i],25,heads[i],8); }
        Cell(60,0,31,70,"{project.title}\n{sheet.title}",17); Cell(60,70,31,30,"Document : {project.documentNumber}\n{sheet.location} · {sheet.function}",10);
        Cell(91,0,9,70,"FOLIO\n{sheet.number}/{sheet.total}",14); Cell(91,70,9,30,"RÉV. {sheet.revision}",10);
        return t;
    }
}
public static class DocumentModel
{
    public static readonly string[] Fields = ["project.title","project.author","project.revision","project.customer","project.documentNumber","project.date","sheet.title","sheet.number","sheet.index","sheet.total","sheet.author","sheet.checkedBy","sheet.approvedBy","sheet.date","sheet.revision","sheet.location","sheet.function"];
    private static readonly Regex Tokens = new(@"\{([^{}]+)\}", RegexOptions.Compiled);
    public static string PageNumber(Project p, Sheet s) => s.Number.Length > 0 ? s.Number : (p.Sheets.IndexOf(s)+1).ToString(CultureInfo.InvariantCulture);
    public static Dictionary<string,string> Values(Project p, Sheet s)
    {
        string F(string a,string b) => a.Length > 0 ? a : b;
        var d = new Dictionary<string,string> { ["project.title"]=p.Title,["project.author"]=p.Author,["project.revision"]=p.Revision,["project.customer"]=p.Customer,["project.documentNumber"]=p.DocumentNumber,["project.date"]=p.Date,
            ["sheet.title"]=s.Title,["sheet.number"]=PageNumber(p,s),["sheet.index"]=(p.Sheets.IndexOf(s)+1).ToString(),["sheet.total"]=p.Sheets.Count.ToString(),["sheet.author"]=F(s.Author,p.Author),["sheet.checkedBy"]=s.CheckedBy,["sheet.approvedBy"]=s.ApprovedBy,["sheet.date"]=F(s.Date,p.Date),["sheet.revision"]=F(s.Revision,p.Revision),["sheet.location"]=s.Location,["sheet.function"]=s.Function };
        foreach(var pair in p.CustomFields) d["project.custom."+pair.Key]=pair.Value;
        foreach(var pair in s.CustomFields) d["sheet.custom."+pair.Key]=pair.Value;
        return d;
    }
    public static string Resolve(string text, IReadOnlyDictionary<string,string> values) => Tokens.Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
    public static TitleBlockTemplate? Template(Project p,Sheet s) => s.BomPage is null ? p.TitleBlocks.FirstOrDefault(t=>t.Id==s.TitleBlockId) : null;
    public static double DrawingBottom(Project p,Sheet s) => s.Height-30-(Template(p,s)?.Height??0);
    public static List<Ink> Scene(Project p,Sheet s,TitleBlockTemplate? template=null)
    {
        var t=template??Template(p,s); List<Ink> result=[]; if(t is null) return result;
        double width=s.Width-60, top=s.Height-30-t.Height; var values=Values(p,s);
        foreach(var cell in t.Cells)
        {
            double x=30+width*cell.X/100,y=top+t.Height*cell.Y/100,w=width*cell.Width/100,h=t.Height*cell.Height/100;
            if(cell.Border) result.Add(new("rect",x,y,w,h));
            if(cell.ImageId.Length>0 && p.Images.TryGetValue(cell.ImageId,out var data))
            {
                var image=ImageAssets.Decode(data); double scale=Math.Min(Math.Max(1,w-8)/image.PixelWidth,Math.Max(1,h-8)/image.PixelHeight);
                double iw=image.PixelWidth*scale, ih=image.PixelHeight*scale; result.Add(new("image",x+(w-iw)/2,y+(h-ih)/2,iw,ih,Text:data)); continue;
            }
            var lines=Resolve(cell.Text,values).Replace("\r","").Split('\n');
            double size=cell.FontSize; var widths=lines.Select(line=>Drawing.Measure(line,size).Width).ToArray();
            double factor=Math.Min(1,Math.Min(Math.Max(1,w-8)/Math.Max(1,widths.DefaultIfEmpty(0).Max()),Math.Max(1,h-6)/(lines.Length*size*1.3)));
            size*=factor; double total=lines.Length*size*1.3; double ty=y+(cell.Vertical=="top"?3:cell.Vertical=="bottom"?h-total-3:(h-total)/2);
            for(int i=0;i<lines.Length;i++) { double tw=widths[i]*factor; double tx=x+(cell.Align=="left"?4:cell.Align=="right"?w-tw-4:(w-tw)/2); result.Add(new("text",tx,ty+i*size*1.3,Text:lines[i],Size:size)); }
        }
        return result;
    }
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,ProjectStore.Json),ProjectStore.Json)!;
    public static void Validate(Project p)
    {
        void Text(string? v) { if(v is null || v.Length>2000) throw new InvalidDataException("Champ documentaire invalide (maximum 2 000 caractères)."); }
        void Custom(Dictionary<string,string>? d) { if(d is null || d.Count>100) throw new InvalidDataException("Champs personnalisés invalides."); foreach(var item in d) { if(!Regex.IsMatch(item.Key,@"^[\p{L}\p{N}_-]{1,80}$")) throw new InvalidDataException("Clé personnalisée : lettres, chiffres, _ ou - uniquement."); Text(item.Value); } }
        Text(p.Customer);Text(p.DocumentNumber);Text(p.Date);Custom(p.CustomFields);
        if(p.Images is null || p.Images.Count>200 || p.Images.Values.Sum(x=>(long)(x?.Length??0))>12_000_000) throw new InvalidDataException("Images : maximum 200 images / 12 Mo encodés par projet.");
        foreach(var pair in p.Images) { if(pair.Key.Length>100 || string.IsNullOrWhiteSpace(pair.Key)) throw new InvalidDataException("Identifiant image invalide."); ImageAssets.Validate(pair.Value); }
        void Image(string id) { if(id is null || (id.Length>0&&!p.Images.ContainsKey(id))) throw new InvalidDataException("Image absente du projet."); }
        if(p.TitleBlocks is null || p.TitleBlocks.Count>100 || p.TitleBlocks.Select(t=>t?.Id).Distinct().Count()!=p.TitleBlocks.Count) throw new InvalidDataException("Cartouches invalides ou dupliqués.");
        foreach(var t in p.TitleBlocks)
        {
            if(t is null || string.IsNullOrWhiteSpace(t.Id)) throw new InvalidDataException("Identifiant de cartouche absent."); Text(t.Id); Text(t.Name);
            if(!double.IsFinite(t.Height)||t.Height<20||t.Height>500||t.Cells is null||t.Cells.Count>300) throw new InvalidDataException("Cartouche : hauteur 20 à 500, maximum 300 cellules.");
            foreach(var c in t.Cells) { if(c is null || !new[]{c.X,c.Y,c.Width,c.Height,c.FontSize}.All(double.IsFinite)||c.X<0||c.Y<0||c.Width<=0||c.Height<=0||c.X+c.Width>100.001||c.Y+c.Height>100.001||c.FontSize<4||c.FontSize>72||c.Align is not("left" or "center" or "right")||c.Vertical is not("top" or "center" or "bottom")) throw new InvalidDataException("Cellule invalide : limites 0–100 %, taille 4–72."); Text(c.Text); Image(c.ImageId); }
        }
        if(p.Sheets is null) throw new InvalidDataException("Folios absents.");
        foreach(var s in p.Sheets)
        {
            if(s is null) throw new InvalidDataException("Folio absent.");
            foreach(var v in new[]{s.Number,s.Author,s.CheckedBy,s.ApprovedBy,s.Date,s.Revision,s.Location,s.Function,s.TitleBlockId}) Text(v); Custom(s.CustomFields);
            if(s.TitleBlockId.Length>0 && Template(p,s) is null) throw new InvalidDataException("Cartouche introuvable.");
            if((Template(p,s)?.Height??0)>s.Height-100) throw new InvalidDataException("Le cartouche est trop haut pour ce folio.");
            if(s.Components is not null) foreach(var c in s.Components) { if(c is null) throw new InvalidDataException("Composant absent."); Image(c.ImageId); }
        }
        if(p.BomOptions is null) throw new InvalidDataException("Configuration BOM absente."); p.BomOptions.Validate();
    }
}
public static class ImageAssets
{
    // Bounded caches; encoded assets are deduplicated within a project and decoded only once.
    [ThreadStatic] private static Dictionary<string,BitmapSource>? cache;
    [ThreadStatic] private static HashSet<string>? validated;
    private static Dictionary<string,BitmapSource> Cache => cache ??= [];
    private static HashSet<string> Validated => validated ??= [];
    public static BitmapSource Decode(string data)
    {
        if(Cache.TryGetValue(data,out var cached)) return cached;
        if(data is null || data.Length>1_000_000) throw new InvalidDataException("Image trop volumineuse.");
        byte[] bytes=Convert.FromBase64String(data);
        if(bytes.Length<8 || !(bytes[0]==255 && bytes[1]==216 || bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))) throw new InvalidDataException("Image interne JPEG ou PNG attendue.");
        using var stream=new MemoryStream(bytes); var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad); var image=decoder.Frames[0];
        if(image.PixelWidth>1024||image.PixelHeight>1024) throw new InvalidDataException("Image interne : 1024 pixels maximum."); image.Freeze();
        if(Cache.Count>=64) Cache.Clear(); Cache[data]=image; return image;
    }
    public static void Validate(string data) {if(Validated.Contains(data))return;Decode(data);if(Validated.Count>=512)Validated.Clear();Validated.Add(data);}
    public static (string Id,string Data) Import(string path)
    {
        if(new FileInfo(path).Length>20_000_000) throw new InvalidDataException("Choisir une image de moins de 20 Mo.");
        using var stream=File.OpenRead(path); var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.DecodePixelWidth=512; image.StreamSource=stream; image.EndInit(); image.Freeze();
        double scale=Math.Min(1,512.0/Math.Max(image.PixelWidth,image.PixelHeight)); int w=Math.Max(1,(int)(image.PixelWidth*scale)),h=Math.Max(1,(int)(image.PixelHeight*scale));
        var visual=new DrawingVisual(); using(var dc=visual.RenderOpen()) { dc.DrawRectangle(Brushes.White,null,new(0,0,w,h));dc.DrawImage(image,new(0,0,w,h)); }
        var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder=new JpegBitmapEncoder { QualityLevel=90 }; encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output=new MemoryStream();encoder.Save(output);var bytes=output.ToArray();
        return (Convert.ToHexString(SHA256.HashData(bytes)),Convert.ToBase64String(bytes));
    }
}
