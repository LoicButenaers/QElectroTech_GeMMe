using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public sealed class ArticleFiche
{
    public string Id { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Name { get; set; } = "";
    public string Family { get; set; } = "";
    public string Source { get; set; } = "";
    public string CheckedOn { get; set; } = "";
    public string Edition { get; set; } = "";
    public string Document { get; set; } = "";
    public string DocumentSha256 { get; set; } = "";
    public double? Width { get; set; }
    public double? Height { get; set; }
    public double? Depth { get; set; }
    public Dictionary<string,string> Facts { get; set; } = [];
    public string Note { get; set; } = "";
    public override string ToString() => Brand+" · "+Reference+" · "+Name;
}
public static class ArticleFiches
{
    public static IReadOnlyList<ArticleFiche> All { get; } = Load();
    private static IReadOnlyList<ArticleFiche> Load()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("article-fiches.json")!;var rows=JsonSerializer.Deserialize<List<ArticleFiche>>(stream,ProjectStore.Json)!;
        if(rows.Select(r=>r.Id).Distinct().Count()!=rows.Count)throw new InvalidDataException("Fiches article dupliquées.");
        foreach(var r in rows){if(string.IsNullOrWhiteSpace(r.Id)||string.IsNullOrWhiteSpace(r.Brand)||string.IsNullOrWhiteSpace(r.Reference)||string.IsNullOrWhiteSpace(r.Edition)||!DateOnly.TryParseExact(r.CheckedOn,"yyyy-MM-dd",out _)||!Uri.TryCreate(r.Source,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!new[]{r.Width,r.Height,r.Depth}.All(n=>n is null||double.IsFinite(n.Value)&&n.Value>0))throw new InvalidDataException("Provenance ou dimensions de fiche incomplètes.");if(r.Document.Length>0)ReadDocument(r);}
        return rows.AsReadOnly();
    }
    public static byte[] ReadDocument(ArticleFiche fiche){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("datasheet."+fiche.Document)??throw new InvalidDataException("Document fabricant absent.");using var buffer=new MemoryStream();stream.CopyTo(buffer);byte[] bytes=buffer.ToArray();if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(fiche.DocumentSha256,StringComparison.OrdinalIgnoreCase)||!System.Text.Encoding.ASCII.GetString(bytes.Take(5).ToArray()).StartsWith("%PDF-"))throw new InvalidDataException("Document fabricant altéré.");return bytes;}
    public static IEnumerable<ArticleFiche> Search(string query)=>All.Where(r=>Symbols.SearchKey(r.Brand+" "+r.Reference+" "+r.Name+" "+r.Family).Contains(Symbols.SearchKey(query)));
    public static ArticleFiche? For(Component c)=>All.FirstOrDefault(f=>f.Reference.Equals(c.Reference,StringComparison.OrdinalIgnoreCase)&&f.Brand.Equals(c.Brand,StringComparison.OrdinalIgnoreCase));
}
public sealed partial class Editor
{
    public void AssignArticleFiche(string componentId,string ficheId)
    {
        var c=Project.Sheets.SelectMany(s=>s.Components).Single(c=>c.Id==componentId);if(c.CoilId.Length>0)c=Project.Sheets.SelectMany(s=>s.Components).Single(x=>x.Id==c.CoilId);var fiche=ArticleFiches.All.Single(f=>f.Id==ficheId);
        if(c.Symbol.StartsWith("official-")&&!c.Reference.Equals(fiche.Reference,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Ce dessin officiel représente une autre référence. Choisir un symbole fonctionnel ou le dessin adapté.");
        Change(()=>{c.Brand=fiche.Brand;c.Reference=fiche.Reference;c.Range=fiche.Name;c.Source=fiche.Source;c.Rating=string.Join(" · ",fiche.Facts.Take(3).Select(f=>f.Value));});
    }
}
public sealed partial class MainWindow
{
    private void ArticleFichesDialog()
    {
        Canvas.CancelDrag();var dialog=DocumentWindow("Fiches fabricants · références et documents hors ligne",1120,780);var root=new DockPanel{Margin=new(18)};dialog.Content=root;var input=new TextBox{Padding=new(8),Margin=new(0,0,0,10),ToolTip="Fabricant, référence ou famille"};DockPanel.SetDock(input,Dock.Top);root.Children.Add(input);var footer=new WrapPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);var list=new ListBox{Width=320,Margin=new(0,0,15,0)};DockPanel.SetDock(list,Dock.Left);root.Children.Add(list);var details=new TextBlock{TextWrapping=TextWrapping.Wrap,FontSize=13};root.Children.Add(new ScrollViewer{Content=details,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        void Filter(){list.ItemsSource=ArticleFiches.Search(input.Text).ToList();if(list.Items.Count>0)list.SelectedIndex=0;}
        list.SelectionChanged+=(_,_)=>{if(list.SelectedItem is ArticleFiche f)details.Text=$"{f.Brand} · {f.Reference}\n{f.Name}\n\n"+string.Join("\n",f.Facts.Select(v=>v.Key+" : "+v.Value))+$"\n\nDimensions de la source : {f.Width?.ToString("0.##")??"à renseigner"} × {f.Height?.ToString("0.##")??"à renseigner"} × {f.Depth?.ToString("0.##")??"à renseigner"} mm\n\nSource : {f.Source}\nÉdition : {f.Edition}\nVérifié le : {f.CheckedOn}\n\n{f.Note}";};
        footer.Children.Add(DocButton("Affecter l’article au composant sélectionné",dialog,()=>{if(list.SelectedItem is not ArticleFiche f)return;var c=Editor.Sheet.Components.SingleOrDefault(c=>Canvas.Selected.Contains(c.Id))??throw new ArgumentException("Sélectionner un seul composant sur le schéma.");Editor.AssignArticleFiche(c.Id,f.Id);}));
        footer.Children.Add(DocButton("Ouvrir le document enregistré",dialog,()=>{if(list.SelectedItem is not ArticleFiche f)return;if(f.Document.Length==0)throw new ArgumentException("Les caractéristiques sont disponibles hors ligne ; le PDF original n’a pas pu être téléchargé. Le lien source est indiqué dans la fiche.");var folder=Path.Combine(AppContext.BaseDirectory,"article-documents");Directory.CreateDirectory(folder);var path=Path.Combine(folder,f.Document);File.WriteAllBytes(path,ArticleFiches.ReadDocument(f));System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});}));
        footer.Children.Add(DocButton("Fermer",dialog,dialog.Close));input.TextChanged+=(_,_)=>Filter();Filter();ShowDocumentDialog(dialog);
    }
}
