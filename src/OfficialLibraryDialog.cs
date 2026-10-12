using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    private void SelectLibrary(string id)
    {
        var e = Catalog.Entries.FirstOrDefault(c => c.Id == id);
        if(e is {CanPlace:false}) { Canvas.SetMode("select"); Canvas.CatalogId=null; status.Text=e.Status+" · double-cliquez pour consulter la source"; return; }
        Canvas.Symbol=e?.Symbol??id; Canvas.CatalogId=e?.Id; Canvas.SetMode("place");
    }
    private void OfficialLibraryItem(CatalogEntry entry)
    {
        var row=new DockPanel {Margin=new(3,8,3,8)};
        row.Children.Add(new Image {Source=ImageAssets.Decode(Convert.ToBase64String(Catalog.ReadChecked(entry.Asset,entry.AssetSha256))),Width=56,Height=54,Stretch=Stretch.Uniform,Margin=new(0,0,9,0)});
        var text=new StackPanel(); row.Children.Add(text);
        text.Children.Add(new TextBlock {Text=entry.Reference,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
        text.Children.Add(new TextBlock {Text=entry.Brand+" · "+entry.Range,FontSize=11,TextWrapping=TextWrapping.Wrap});
        text.Children.Add(new TextBlock {Text=entry.CanPlace?"Bornes documentées":"Aperçu uniquement",FontSize=11,Foreground=Drawing.Brush("#61758A"),TextWrapping=TextWrapping.Wrap});
        var item=new ListBoxItem {Tag=entry.Id,Content=row,ToolTip=entry.Status+"\n"+entry.Note,HorizontalContentAlignment=HorizontalAlignment.Stretch};
        var menu=new ContextMenu(); menu.Items.Add(QuickAction("Aperçu, bornes et provenance…",()=>OfficialLibraryDetails(entry)));
        if(entry.CanPlace) {menu.Items.Add(QuickAction("Placer sur le folio",()=>SelectLibrary(entry.Id)));menu.Items.Add(QuickAction("Modifier une copie non officielle…",()=>EditSymbol(Symbols.All[entry.Symbol],null,"La copie est personnelle et ne conserve pas le statut du dessin officiel.")));}
        item.ContextMenu=menu; item.MouseDoubleClick+=(_,e)=>{e.Handled=true;Run(()=>OfficialLibraryDetails(entry));};
        item.PreviewMouseLeftButtonDown+=(_,_)=>{if(item.IsSelected)SelectLibrary(entry.Id);};library.Items.Add(item);
    }
    internal void OfficialLibraryDetails(CatalogEntry entry)
    {
        var dialog=DocumentWindow(entry.Display,940,790); var root=new DockPanel{Margin=new(20),Background=Brushes.White};dialog.Content=root;
        var actions=new WrapPanel{Margin=new(0,12,0,0)};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        void Web(string url)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
        actions.Children.Add(DocButton("Page officielle",dialog,()=>Web(entry.Source)));
        actions.Children.Add(DocButton("Dessin / document en ligne",dialog,()=>Web(entry.DrawingSource)));
        if(entry.Document.Length>0) actions.Children.Add(DocButton("Enregistrer le document source…",dialog,()=>{var picker=new Microsoft.Win32.SaveFileDialog{FileName=entry.Document,Filter="Document PDF|*.pdf"};if(picker.ShowDialog(dialog)==true)File.WriteAllBytes(picker.FileName,Catalog.ReadChecked(entry.Document,entry.DocumentSha256));}));
        if(entry.CanPlace)actions.Children.Add(DocButton("Placer",dialog,()=>{SelectLibrary(entry.Id);dialog.Close();}));
        actions.Children.Add(DocButton("Fermer",dialog,dialog.Close));
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(scroll);var body=new StackPanel();scroll.Content=body;
        void Text(string value,double size=13)=>body.Children.Add(new TextBlock{Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new(0,5,0,5)});
        Text(entry.Status,18);Text(entry.Edition);Text(entry.Note);
        body.Children.Add(new Border{BorderBrush=Drawing.Brush("#D5DFE9"),BorderThickness=new(1),Padding=new(18),Margin=new(0,10,0,10),Background=Brushes.White,Child=new Image{Source=ImageAssets.Decode(Convert.ToBase64String(Catalog.ReadChecked(entry.Asset,entry.AssetSha256))),Height=220,Stretch=Stretch.Uniform}});
        Text("Contrôlé le "+entry.CheckedOn+" · "+entry.Method);
        Text(entry.CanPlace ? "Bornes : "+string.Join(" · ",entry.Ports.Select(p=>p.Id)) : "La consultation de cette image ne fournit pas une bibliothèque EPLAN native utilisable pour raccorder des conducteurs.");
        Text("Source : "+entry.DrawingSource,11);Text("SHA-256 du dessin : "+entry.AssetSha256,11);
        Text("Le contrôle d’intégrité garantit la conservation du fichier intégré ; il ne constitue pas une certification par EPLAN ou le fabricant.",11);
        ShowDocumentDialog(dialog);
    }
}
