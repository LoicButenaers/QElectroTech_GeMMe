using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace GeMMeElec;

public sealed class PersonalEntry
{
    public string Format { get; set; } = "GeMMeElecLibrary";
    public int Version { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Favori";
    public string Category { get; set; } = "Favoris";
    public string Tags { get; set; } = "";
    public string BuiltinId { get; set; } = "";
    public SymbolDefinition? Definition { get; set; }
    public CircuitBlock? Block { get; set; }
    public string Kind => Block is null ? "Symbole" : "Circuit";
}
public static class PersonalLibrary
{
    public static string Folder => Path.Combine(Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION")=="1"?AppContext.BaseDirectory:Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GeMMeElec-library");
    public static void Validate(PersonalEntry entry)
    {
        if(entry.Format!="GeMMeElecLibrary"||entry.Version!=1||!Guid.TryParseExact(entry.Id,"N",out _)||string.IsNullOrWhiteSpace(entry.Name)||entry.Name.Length>200||entry.Category is null||entry.Category.Length>100||entry.Tags is null||entry.Tags.Length>1000||entry.BuiltinId is null)throw new InvalidDataException("Entrée de bibliothèque invalide.");
        if(new[]{entry.Block is not null,entry.Definition is not null,entry.BuiltinId.Length>0}.Count(x=>x)!=1)throw new InvalidDataException("Choisir un symbole ou un bloc.");
        if(entry.Block is not null)CircuitBlocks.Validate(entry.Block);if(entry.Definition is not null)CustomSymbols.Validate(entry.Definition);if(entry.BuiltinId.Length>0&&!Symbols.All.ContainsKey(entry.BuiltinId))throw new InvalidDataException("Symbole intégré absent.");
    }
    public static List<PersonalEntry> List(string? folder=null)
    {
        folder??=Folder;if(!Directory.Exists(folder))return[];List<PersonalEntry> entries=[];
        foreach(var path in Directory.EnumerateFiles(folder,"*.gemfavorite").Take(1001))
        {
            if(entries.Count>=1000)throw new InvalidDataException("Bibliothèque limitée à 1 000 entrées.");
            if(new FileInfo(path).Length>Project.MaxFileBytes)throw new InvalidDataException("Favori trop volumineux : "+Path.GetFileName(path));
            var entry=JsonSerializer.Deserialize<PersonalEntry>(File.ReadAllText(path),ProjectStore.Json)??throw new InvalidDataException("Favori absent.");Validate(entry);entries.Add(entry);
        }return entries.OrderBy(e=>e.Category).ThenBy(e=>e.Name).ToList();
    }
    public static void Save(PersonalEntry entry,string? folder=null)
    {
        Validate(entry);folder??=Folder;Directory.CreateDirectory(folder);string path=Path.Combine(folder,entry.Id+".gemfavorite");if(!File.Exists(path)&&Directory.EnumerateFiles(folder,"*.gemfavorite").Take(1000).Count()>=1000)throw new InvalidDataException("Maximum 1 000 favoris.");
        string json=JsonSerializer.Serialize(entry,ProjectStore.ProjectJson);if(System.Text.Encoding.UTF8.GetByteCount(json)>Project.MaxFileBytes)throw new InvalidDataException("Favori trop volumineux.");ProjectStore.AtomicWrite(path,json);
    }
    public static void Delete(string id,string? folder=null){if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Identifiant de favori invalide.");File.Delete(Path.Combine(folder??Folder,id+".gemfavorite"));}
}
public sealed partial class SheetCanvas
{
    private CircuitBlock? pendingBlock;
    private System.Windows.Media.DrawingGroup? blockPreview;
    private Rect? blockBounds;
    public CircuitBlock? PendingBlock { get => pendingBlock; set { pendingBlock=value;blockPreview=null;blockBounds=null; } }
    internal bool PlaceBlockForTest(Point point) => HandleBlockClick(point);
    internal System.Windows.Media.DrawingGroup BlockPreviewForTest() => BlockPreview();
    private System.Windows.Media.DrawingGroup BlockPreview()
    {
        if(blockPreview is not null)return blockPreview;
        var block=PendingBlock??throw new InvalidOperationException("Aucune copie à placer.");var p=block.Project;var s=p.Sheets[0];var drawing=new System.Windows.Media.DrawingGroup();
        using(var dc=drawing.Open())
        {
            Drawing.Render(dc,s.Graphics.Select(g=>FreeGraphics.Ink(p,g)));Drawing.Render(dc,s.Notes.Select(n=>FreeGraphics.NoteInk(p,s,n)));var refs=new CrossReferences(p);
            foreach(var w in s.Wires){var path=w.Route(s);for(int i=1;i<path.Count;i++)dc.DrawLine(new(Drawing.Brush(w.Color),w.Thickness),path[i-1],path[i]);}
            foreach(var c in s.Components)Drawing.Render(dc,Drawing.ComponentScene(p,c,refs));
        }
        drawing.Freeze();return blockPreview=drawing;
    }
    private bool HandleBlockClick(Point point)
    {
        if(Mode!="block"||PendingBlock is null)return false;var ids=Editor.InsertBlock(PendingBlock,point);SetMode("select");Selected.Clear();foreach(string id in ids)Selected.Add(id);SelectionChanged?.Invoke();InvalidateVisual();return true;
    }
    private void RenderBlock(System.Windows.Media.DrawingContext dc)
    {
        if(Mode!="block"||PendingBlock is null||!IsMouseOver)return;RenderBlockAt(dc,pointer);
    }
    internal void RenderBlockAt(System.Windows.Media.DrawingContext dc,Point point)
    {
        if(PendingBlock is null)return;var bounds=blockBounds??=CircuitBlocks.Bounds(PendingBlock.Project);var target=new Point(Editor.Snap(point.X),Editor.Snap(point.Y));var delta=target-bounds.TopLeft;
        dc.PushOpacity(.45);dc.PushTransform(new System.Windows.Media.TranslateTransform(delta.X,delta.Y));
        dc.DrawDrawing(BlockPreview());dc.Pop();dc.Pop();
    }
}
public sealed partial class MainWindow
{
    private List<PersonalEntry> personalEntries = [];
    internal void ReloadLibraryFromMcp()=>ReloadPersonalLibrary();
    internal List<string> PlaceLibraryFromMcp(string id,Point point)
    {
        var entry=PersonalLibrary.List().Single(e=>e.Id==id);if(entry.Block is not null)return Editor.InsertBlock(entry.Block,point);
        Component? component=null;
        Editor.Change(()=>{string symbol=entry.BuiltinId;if(entry.Definition is not null){var definition=CustomSymbols.Copy(entry.Definition,entry.Name);Editor.Project.CustomSymbols.Add(definition.Id,definition);symbol=definition.Id;}var d=CustomSymbols.Resolve(Editor.Project,symbol);component=new(){Symbol=symbol,ResolvedDefinition=d,X=Editor.Snap(point.X),Y=Editor.Snap(point.Y),Tag=Editor.NextTag(d.Prefix),Description=entry.Name};component.RelayFunction=CoilContacts.Role(component);Editor.Sheet.Components.Add(component);});return[component!.Id];
    }
    private void ReloadPersonalLibrary(){personalEntries=PersonalLibrary.List();if(libraryMode.SelectedIndex==4)LibraryCategories();}
    private void FavoriteSymbol(string symbol)
    {
        var definition=CustomSymbols.Resolve(Editor.Project,symbol);var name=Prompt("Ajouter un favori","Nom",definition.Name);if(name is null)return;
        var entry=new PersonalEntry{Name=name,Category=definition.Category};if(Symbols.All.ContainsKey(symbol))entry.BuiltinId=symbol;else entry.Definition=DocumentModel.Copy(definition);PersonalLibrary.Save(entry);ReloadPersonalLibrary();status.Text="Favori enregistré · "+name;
    }
    private CircuitBlock SelectionBlock(string name,string category="Circuits",string tags="")=>CircuitBlocks.Extract(Editor.Project,Editor.Sheet,Canvas.Selected,name,category,tags);
    private void SaveCircuitBlock()
    {
        var name=Prompt("Créer un bloc de circuit","Nom", "Circuit");if(name is null)return;var block=SelectionBlock(name);var path=new SaveFileDialog{Filter="Bloc GeMMeElec|*.gemblock",FileName="Circuit.gemblock"};if(path.ShowDialog(this)==true)CircuitBlocks.Save(block,path.FileName);
        PersonalLibrary.Save(new(){Name=name,Category=block.Category,Block=block});ReloadPersonalLibrary();status.Text=$"Bloc enregistré · {block.DetachedConnections} connexion(s) extérieure(s) détachée(s)";
    }
    private void ImportCircuitBlock()
    {
        var path=new OpenFileDialog{Filter="Bloc GeMMeElec|*.gemblock"};if(path.ShowDialog(this)!=true)return;var block=CircuitBlocks.Load(path.FileName);PersonalLibrary.Save(new(){Name=block.Name,Category=block.Category,Tags=block.Tags,Block=block});ReloadPersonalLibrary();PrepareBlock(block);
    }
    private void PrepareBlock(CircuitBlock block){Canvas.PendingBlock=block;Canvas.SetMode("block");status.Text="Cliquer le coin supérieur gauche du bloc · Échap annule";}
    private void SelectPersonalEntry(string id)
    {
        var entry=personalEntries.Single(e=>e.Id==id);if(entry.Block is not null){PrepareBlock(entry.Block);return;}
        string symbol=entry.BuiltinId;if(entry.Definition is not null)symbol=Editor.SaveSymbolCopy(entry.Definition).Id;
        Canvas.Symbol=symbol;Canvas.CatalogId=null;Canvas.SetMode("place");
    }
    private void PersonalLibraryItem(PersonalEntry entry)
    {
        var row=new StackPanel{Margin=new(8)};row.Children.Add(new TextBlock{Text=entry.Name,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});row.Children.Add(Hint(entry.Category+" · "+entry.Kind+" · "+entry.Tags));
        var item=new ListBoxItem{Content=row,Tag="personal:"+entry.Id};item.PreviewMouseLeftButtonDown+=(_,_)=>{if(item.IsSelected)Run(()=>SelectPersonalEntry(entry.Id));};library.Items.Add(item);
    }
    private void PersonalLibraryDialog()
    {
        ReloadPersonalLibrary();var dialog=DocumentWindow("Bibliothèque personnelle",950,680);var root=new DockPanel{Margin=new(20)};dialog.Content=root;
        var footer=new WrapPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);var hint=Hint("Favoris et blocs conservés localement. Modifier les catégories et mots-clés pour les retrouver dans le panneau Bibliothèque → Favoris et circuits.");DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
        var draft=DocumentModel.Copy(personalEntries);var grid=new DataGrid{ItemsSource=draft,AutoGenerateColumns=false,CanUserAddRows=false};foreach(var(label,path,width)in new[]{("Nom","Name",300d),("Catégorie","Category",220d),("Mots-clés","Tags",300d)})grid.Columns.Add(new DataGridTextColumn{Header=label,Binding=new System.Windows.Data.Binding(path),Width=width});root.Children.Add(grid);
        footer.Children.Add(DocButton("Enregistrer",dialog,()=>{grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);foreach(var entry in draft)PersonalLibrary.Validate(entry);foreach(var entry in draft)PersonalLibrary.Save(entry);ReloadPersonalLibrary();dialog.Close();}));
        footer.Children.Add(DocButton("Supprimer le favori choisi",dialog,()=>{if(grid.SelectedItem is PersonalEntry entry){PersonalLibrary.Delete(entry.Id);draft.Remove(entry);grid.ItemsSource=null;grid.ItemsSource=draft;ReloadPersonalLibrary();}}));
        footer.Children.Add(DocButton("Exporter le bloc choisi",dialog,()=>{if(grid.SelectedItem is not PersonalEntry{Block:not null} entry)throw new ArgumentException("Choisir un bloc de circuit.");var path=new SaveFileDialog{Filter="Bloc GeMMeElec|*.gemblock",FileName="Circuit.gemblock"};if(path.ShowDialog(dialog)==true)CircuitBlocks.Save(entry.Block,path.FileName);}));footer.Children.Add(DocButton("Fermer",dialog,dialog.Close));ShowDocumentDialog(dialog);
    }
}
