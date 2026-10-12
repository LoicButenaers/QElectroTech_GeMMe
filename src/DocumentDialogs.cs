using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    internal Action<Window>? DocumentTestProbe;
    internal void TestDocumentDialog(string kind) {if(kind=="titleblock")TitleBlockEditor();else if(kind=="bom")BomDialog();else if(kind=="zones")ZonesDialog();else if(kind=="references")ReferenceNavigator();else if(kind=="grid")GridDialog();else if(kind=="palette")PaletteDialog();else MetadataDialog(kind=="sheet");}
    private void ShowDocumentDialog(Window dialog) {if(DocumentTestProbe is null)dialog.ShowDialog();else {try{DocumentTestProbe(dialog);}finally{dialog.Close();}}}
    private Window DocumentWindow(string title,double width=1050,double height=720) => new() { Owner=DocumentTestProbe is null?this:null,Title=title,Width=Math.Min(width,SystemParameters.WorkArea.Width*.92),Height=Math.Min(height,SystemParameters.WorkArea.Height*.9),WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brushes.White,FontFamily=FontFamily,FontSize=13 };
    private static TextBlock Hint(string text) => new() { Text=text,TextWrapping=TextWrapping.Wrap,Foreground=Drawing.Brush("#61758A"),Margin=new(4,8,4,8) };
    private Button DocButton(string text,Window owner,Action action) {var b=new Button{Content=text,Padding=new(10,7,10,7),Margin=new(3)};b.Click+=(_,_)=>{try{action();}catch(Exception ex){if(DocumentTestProbe is not null)throw;MessageBox.Show(owner,ex.Message,"GeMMeElec",MessageBoxButton.OK,MessageBoxImage.Information);}};return b;}
    private static TextBox DocField(Panel panel,string label,string value,bool multiline=false)
    {panel.Children.Add(new TextBlock{Text=label,Margin=new(0,9,0,3)});var input=new TextBox{Text=value,Padding=new(7),AcceptsReturn=multiline,MinHeight=multiline?80:30,VerticalScrollBarVisibility=multiline?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled};panel.Children.Add(input);return input;}
    private static Dictionary<string,string> ParseCustom(string text)
    {
        var result=new Dictionary<string,string>();foreach(var line in text.Split('\n',StringSplitOptions.RemoveEmptyEntries)) {var index=line.IndexOf('=');if(index<1)throw new ArgumentException("Champ personnalisé : nom=valeur, un par ligne.");if(!result.TryAdd(line[..index].Trim(),line[(index+1)..].Trim()))throw new ArgumentException("Nom personnalisé dupliqué.");}return result;
    }
    private void SheetInfo()=>MetadataDialog(true);
    private void MovePage(int delta)
    { Canvas.CancelDrag();var sheet=Editor.Sheet;int from=Editor.Project.Sheets.IndexOf(sheet),to=from+delta;if(to<0||to>=Editor.Project.Sheets.Count)return;Editor.Change(()=>{Editor.Project.Sheets.RemoveAt(from);Editor.Project.Sheets.Insert(to,sheet);}); }
    private void MetadataDialog(bool sheetMode)
    {
        Canvas.CancelDrag();var dialog=DocumentWindow(sheetMode?"Propriétés du folio":"Informations du projet",680,850);var root=new DockPanel{Margin=new(18)};dialog.Content=root;var bottom=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        var panel=new StackPanel();root.Children.Add(new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});object target=sheetMode?Editor.Sheet:Editor.Project;var inputs=new Dictionary<string,TextBox>();
        (string Key,string Label)[] spec=sheetMode?[("Title","Titre"),("Number","Numéro de page · vide = ordre automatique"),("Author","Auteur · vide = auteur du projet"),("CheckedBy","Vérifié par"),("ApprovedBy","Approuvé par"),("Date","Date · vide = date du projet"),("Revision","Révision · vide = révision du projet"),("Location","Localisation"),("Function","Fonction")]:[("Title","Titre du projet"),("Author","Auteur"),("Revision","Révision"),("Customer","Client / société"),("DocumentNumber","Numéro de document"),("Date","Date")];
        foreach(var entry in spec)inputs[entry.Key]=DocField(panel,entry.Label,(string)target.GetType().GetProperty(entry.Key)!.GetValue(target)!);
        ComboBox? templates=null;
        if(sheetMode) {panel.Children.Add(Hint("Cartouche affecté à ce folio"));templates=new ComboBox{ItemsSource=new[]{new KeyValuePair<string,string>("","Sans cartouche")}.Concat(Editor.Project.TitleBlocks.Select(t=>new KeyValuePair<string,string>(t.Id,t.Name))),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=Editor.Sheet.TitleBlockId,Padding=new(6)};templates.IsEnabled=Editor.Sheet.BomPage is null;panel.Children.Add(templates);}
        var custom=DocField(panel,"Champs personnalisés · nom=valeur, un par ligne",string.Join("\n",(sheetMode?Editor.Sheet.CustomFields:Editor.Project.CustomFields).Select(x=>x.Key+"="+x.Value)),true);
        panel.Children.Add(Hint("Dans le cartouche : {sheet.title}, {sheet.number}, {sheet.author}, {project.title}…\nChamps personnalisés : {sheet.custom.nom} ou {project.custom.nom}. Le total des folios et leur ordre sont calculés automatiquement."));
        bottom.Children.Add(DocButton("Enregistrer",dialog,()=>{var values=inputs.ToDictionary(x=>x.Key,x=>x.Value.Text);var extra=ParseCustom(custom.Text);var template=(string?)templates?.SelectedValue??"";Editor.Change(()=>{var live=sheetMode?(object)Editor.Sheet:Editor.Project;foreach(var pair in values)live.GetType().GetProperty(pair.Key)!.SetValue(live,pair.Value);if(sheetMode){Editor.Sheet.CustomFields=extra;Editor.Sheet.TitleBlockId=template;}else Editor.Project.CustomFields=extra;});dialog.Close();}));bottom.Children.Add(DocButton("Annuler",dialog,dialog.Close));ShowDocumentDialog(dialog);
    }
    private (string Id,string Data)? ChooseImage(Window owner)
    {var picker=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp",Title="Image intégrée au projet (redimensionnée à 512 pixels)"};return picker.ShowDialog(owner)==true?ImageAssets.Import(picker.FileName):null;}
    private void ComponentImage(string id)
    {Canvas.CancelDrag();var image=ChooseImage(this);if(image is null)return;Editor.Change(()=>{Editor.Project.Images[image.Value.Id]=image.Value.Data;Editor.Sheet.Components.Single(c=>c.Id==id).ImageId=image.Value.Id;});}
    private void TitleBlockEditor()
    {
        Canvas.CancelDrag();var draft=DocumentModel.Copy(Editor.Project);var sheet=draft.Sheets.Single(s=>s.Id==Editor.Sheet.Id);var dialog=DocumentWindow("Cartouches · modèles et champs liés",1250,850);var root=new DockPanel{Margin=new(16)};dialog.Content=root;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);var bar=new WrapPanel();top.Children.Add(bar);
        var models=new ComboBox{Width=240,Margin=new(3),DisplayMemberPath="Name"};bar.Children.Add(models);
        var name=new TextBox{Width=230,Margin=new(3),ToolTip="Nom du modèle"};bar.Children.Add(name);bar.Children.Add(new TextBlock{Text="Hauteur :",VerticalAlignment=VerticalAlignment.Center});var height=new TextBox{Width=65,Margin=new(3)};bar.Children.Add(height);
        var preview=new TitleBlockPreview{Height=200,Margin=new(0,8,0,8)};top.Children.Add(preview);
        top.Children.Add(Hint("Chaque ligne définit une cellule, fusionnée en ajustant sa largeur/hauteur. X, Y, largeur et hauteur : 0 à 100 % du cartouche. Le texte peut contenir plusieurs lignes (\u21b5) et des champs {…}. Les images remplacent le texte de leur cellule."));
        var bindings=new ComboBox{Width=220,ItemsSource=DocumentModel.Fields.Concat(draft.CustomFields.Keys.Select(k=>"project.custom."+k)).Concat(sheet.CustomFields.Keys.Select(k=>"sheet.custom."+k)),SelectedIndex=0};var toolsBar=new WrapPanel();top.Children.Add(toolsBar);toolsBar.Children.Add(bindings);
        var grid=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,EnableRowVirtualization=true,EnableColumnVirtualization=true,SelectionMode=DataGridSelectionMode.Single};
        void Col(string title,string path,double width) => grid.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(path){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus},Width=width});
        Col("X %","X",55);Col("Y %","Y",55);Col("L %","Width",55);Col("H %","Height",55);Col("Texte / champs liés","Text",330);Col("Taille","FontSize",55);
        var multiline=new Style(typeof(TextBox));multiline.Setters.Add(new Setter(TextBox.AcceptsReturnProperty,true));multiline.Setters.Add(new Setter(TextBox.TextWrappingProperty,TextWrapping.Wrap));multiline.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,55.0));((DataGridTextColumn)grid.Columns[4]).EditingElementStyle=multiline;
        grid.Columns.Add(new DataGridComboBoxColumn{Header="Horizontal",ItemsSource=new[]{new KeyValuePair<string,string>("left","Gauche"),new("center","Centré"),new("right","Droite")},SelectedValuePath="Key",DisplayMemberPath="Value",SelectedValueBinding=new Binding("Align"),Width=85});grid.Columns.Add(new DataGridComboBoxColumn{Header="Vertical",ItemsSource=new[]{new KeyValuePair<string,string>("top","Haut"),new("center","Milieu"),new("bottom","Bas")},SelectedValuePath="Key",DisplayMemberPath="Value",SelectedValueBinding=new Binding("Vertical"),Width=80});grid.Columns.Add(new DataGridCheckBoxColumn{Header="Bordure",Binding=new Binding("Border"),Width=60});grid.Columns.Add(new DataGridTextColumn{Header="Image",Binding=new Binding("ImageId"){Converter=new ImagePresenceConverter()},IsReadOnly=true,Width=90});
        TitleBlockTemplate? current=null;ObservableCollection<TitleBlockCell> cells=[];bool loading=false;
        var feedback=Hint("");var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);bottom.Children.Add(feedback);var buttons=new WrapPanel();bottom.Children.Add(buttons);root.Children.Add(grid);
        void Flush() { if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Corriger la valeur de la cellule en cours.");if(current is null)throw new ArgumentException("Choisir un modèle.");current.Name=name.Text;current.Height=ReadNumber(height.Text);current.Cells=cells.ToList(); }
        void RefreshPreview() {try{Flush();DocumentModel.Validate(draft);preview.Set(draft,sheet,current!);feedback.Text="Aperçu avec les valeurs du folio actif. Champs inconnus conservés entre accolades.";}catch(Exception ex){feedback.Text=ex.Message;} }
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(_,_)=>{timer.Stop();RefreshPreview();};void Schedule(){if(loading)return;timer.Stop();timer.Start();}
        void Load(TitleBlockTemplate t){loading=true;current=t;name.Text=t.Name;height.Text=Number(t.Height);cells=new(t.Cells);grid.ItemsSource=cells;loading=false;RefreshPreview();}
        void Reload(TitleBlockTemplate t){loading=true;models.ItemsSource=null;models.ItemsSource=draft.TitleBlocks;models.SelectedItem=t;loading=false;Load(t);}
        models.SelectionChanged+=(_,_)=>{if(!loading&&models.SelectedItem is TitleBlockTemplate t){try{if(current is not null)Flush();Load(t);}catch(Exception ex){loading=true;models.SelectedItem=current;loading=false;feedback.Text=ex.Message;}}};
        name.TextChanged+=(_,_)=>Schedule();height.TextChanged+=(_,_)=>Schedule();grid.CellEditEnding+=(_,_)=>Schedule();
        toolsBar.Children.Add(DocButton("Insérer le champ",dialog,()=>{Flush();if(grid.SelectedItem is not TitleBlockCell c)throw new ArgumentException("Sélectionner une cellule.");c.Text+="{"+bindings.SelectedItem+"}";grid.Items.Refresh();RefreshPreview();}));
        toolsBar.Children.Add(DocButton("+ Cellule",dialog,()=>{Flush();var c=new TitleBlockCell();cells.Add(c);grid.SelectedItem=c;RefreshPreview();}));
        toolsBar.Children.Add(DocButton("Dupliquer cellule",dialog,()=>{Flush();if(grid.SelectedItem is TitleBlockCell c){var copy=DocumentModel.Copy(c);cells.Add(copy);grid.SelectedItem=copy;RefreshPreview();}}));
        toolsBar.Children.Add(DocButton("Retirer cellule",dialog,()=>{Flush();if(grid.SelectedItem is TitleBlockCell c){cells.Remove(c);RefreshPreview();}}));
        toolsBar.Children.Add(DocButton("Image…",dialog,()=>{Flush();if(grid.SelectedItem is not TitleBlockCell c)throw new ArgumentException("Sélectionner une cellule.");var image=ChooseImage(dialog);if(image is null)return;draft.Images[image.Value.Id]=image.Value.Data;c.ImageId=image.Value.Id;grid.Items.Refresh();RefreshPreview();}));
        toolsBar.Children.Add(DocButton("Retirer image",dialog,()=>{Flush();if(grid.SelectedItem is TitleBlockCell c){c.ImageId="";grid.Items.Refresh();RefreshPreview();}}));
        bar.Children.Add(DocButton("Nouveau",dialog,()=>{Flush();var t=new TitleBlockTemplate();draft.TitleBlocks.Add(t);Reload(t);}));
        bar.Children.Add(DocButton("Dupliquer modèle",dialog,()=>{Flush();var t=DocumentModel.Copy(current!);t.Id=Guid.NewGuid().ToString("N");t.Name+=" · copie";draft.TitleBlocks.Add(t);Reload(t);}));
        var all=new CheckBox{Content="Affecter à tous les folios",Margin=new(8),VerticalAlignment=VerticalAlignment.Center};buttons.Children.Add(all);
        buttons.Children.Add(DocButton("Enregistrer et affecter au folio",dialog,()=>{Flush();DocumentModel.Validate(draft);var templates=DocumentModel.Copy(draft.TitleBlocks);var images=new Dictionary<string,string>(draft.Images);var id=current!.Id;Editor.Change(()=>{Editor.Project.TitleBlocks=templates;Editor.Project.Images=images;foreach(var s in Editor.Project.Sheets.Where(s=>s.BomPage is null&&(all.IsChecked==true||s.Id==sheet.Id)))s.TitleBlockId=id;});feedback.Text="Cartouches enregistrés dans le projet ; affectation appliquée. Ctrl+Z dans l’éditeur pour annuler.";}));
        buttons.Children.Add(DocButton("Exporter modèle…",dialog,()=>{Flush();DocumentModel.Validate(draft);var file=new SaveFileDialog{Filter="Modèle de cartouche GeMMeElec|*.gemcartouche",FileName=current!.Name+".gemcartouche"};if(file.ShowDialog(dialog)==true){var ids=current.Cells.Where(c=>c.ImageId.Length>0).Select(c=>c.ImageId).Distinct();ProjectStore.AtomicWrite(file.FileName,JsonSerializer.Serialize(new TitleBlockPackage{Template=current,Images=ids.ToDictionary(id=>id,id=>draft.Images[id])},ProjectStore.Json));}}));
        buttons.Children.Add(DocButton("Importer modèle…",dialog,()=>{Flush();var file=new OpenFileDialog{Filter="Modèle de cartouche GeMMeElec|*.gemcartouche"};if(file.ShowDialog(dialog)!=true)return;if(new FileInfo(file.FileName).Length>16_000_000)throw new InvalidDataException("Modèle trop volumineux.");var pack=JsonSerializer.Deserialize<TitleBlockPackage>(File.ReadAllText(file.FileName),ProjectStore.Json)??throw new InvalidDataException("Modèle vide.");if(pack.Format!="GeMMeElec.TitleBlock"||pack.Template is null||pack.Images is null)throw new InvalidDataException("Modèle invalide.");var candidate=DocumentModel.Copy(draft);pack.Template.Id=Guid.NewGuid().ToString("N");foreach(var pair in pack.Images){if(candidate.Images.TryGetValue(pair.Key,out var existing)&&existing!=pair.Value)throw new InvalidDataException("Conflit d’identifiant image.");candidate.Images[pair.Key]=pair.Value;}candidate.TitleBlocks.Add(pack.Template);DocumentModel.Validate(candidate);draft.Images=candidate.Images;draft.TitleBlocks.Add(pack.Template);Reload(pack.Template);}));
        buttons.Children.Add(DocButton("Fermer",dialog,dialog.Close));dialog.Closed+=(_,_)=>timer.Stop();Reload(draft.TitleBlocks.FirstOrDefault(t=>t.Id==sheet.TitleBlockId)??draft.TitleBlocks.FirstOrDefault()??AddInitial());
        TitleBlockTemplate AddInitial(){var t=new TitleBlockTemplate();draft.TitleBlocks.Add(t);return t;}
        ShowDocumentDialog(dialog);
    }
    private void BomDialog()
    {
        Canvas.CancelDrag();var dialog=DocumentWindow("Nomenclature · colonnes, regroupement et images",1200,800);var options=DocumentModel.Copy(Editor.Project.BomOptions);var root=new DockPanel{Margin=new(16)};dialog.Content=root;
        var top=new WrapPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);var group=new CheckBox{Content="Regrouper les articles identiques",IsChecked=options.GroupArticles,Margin=new(8)};var aux=new CheckBox{Content="Inclure jonctions / terre / sources",IsChecked=options.IncludeAuxiliary,Margin=new(8)};top.Children.Add(group);top.Children.Add(aux);
        var scope=new ComboBox{Width=210,Margin=new(4),ItemsSource=new[]{new KeyValuePair<string,string>("","Tous les folios")}.Concat(Editor.Project.Sheets.Where(s=>s.BomPage is null).Select(s=>new KeyValuePair<string,string>(s.Id,DocumentModel.PageNumber(Editor.Project,s)+" · "+s.Title))),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=Editor.Project.Sheets.Any(s=>s.Id==options.SheetId)?options.SheetId:""};top.Children.Add(scope);
        var brands=new ComboBox{Width=180,Margin=new(4),ItemsSource=new[]{"Tous les fabricants"}.Concat(Editor.Project.Sheets.SelectMany(s=>s.Components).Select(c=>c.Brand).Where(b=>b.Length>0).Distinct().Order()),SelectedItem=options.Brand.Length>0?options.Brand:"Tous les fabricants"};top.Children.Add(brands);
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);var feedback=Hint("Choisir les colonnes à gauche, leur ordre puis Actualiser. Photo du composant si disponible ; sinon symbole générique. Le CSV contient une indication d’image, pas une image intégrée.");bottom.Children.Add(feedback);var actions=new WrapPanel();bottom.Children.Add(actions);
        var left=new DockPanel{Width=235,Margin=new(0,10,12,0)};DockPanel.SetDock(left,Dock.Left);root.Children.Add(left);var cols=new ObservableCollection<BomColumn>(options.Columns);var columns=new DataGrid{ItemsSource=cols,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Single};columns.Columns.Add(new DataGridCheckBoxColumn{Header="✓",Binding=new Binding("Enabled"),Width=35});columns.Columns.Add(new DataGridTextColumn{Header="Nom de colonne",Binding=new Binding("Label"),Width=175});
        var order=new WrapPanel();DockPanel.SetDock(order,Dock.Bottom);left.Children.Add(order);left.Children.Add(columns);
        void Move(int delta){columns.CommitEdit();columns.CommitEdit(DataGridEditingUnit.Row,true);if(columns.SelectedItem is BomColumn c){int index=cols.IndexOf(c),to=index+delta;if(to>=0&&to<cols.Count){cols.Move(index,to);columns.SelectedItem=c;}}}
        order.Children.Add(DocButton("↑ Monter",dialog,()=>Move(-1)));order.Children.Add(DocButton("↓ Descendre",dialog,()=>Move(1)));
        var table=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,EnableRowVirtualization=true,EnableColumnVirtualization=true,CanUserAddRows=false,Margin=new(0,10,0,0)};root.Children.Add(table);
        BomOptions Read(){if(!columns.CommitEdit(DataGridEditingUnit.Cell,true)||!columns.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Corriger la colonne en cours.");options.Columns=cols.ToList();options.GroupArticles=group.IsChecked==true;options.IncludeAuxiliary=aux.IsChecked==true;options.SheetId=(string?)scope.SelectedValue??"";options.Brand=brands.SelectedItem is string b&&b!="Tous les fabricants"?b:"";options.Validate();return options;}
        void RefreshTable()
        {
            var o=Read();var rows=BomReport.Rows(Editor.Project,o);var data=new DataTable();table.Columns.Clear();foreach(var col in o.Columns.Where(c=>c.Enabled)){data.Columns.Add(col.Key,col.Key=="image"?typeof(ImageSource):typeof(string));if(col.Key=="image"){var factory=new FrameworkElementFactory(typeof(Image));factory.SetBinding(Image.SourceProperty,new Binding("[image]"));factory.SetValue(FrameworkElement.HeightProperty,55.0);factory.SetValue(FrameworkElement.WidthProperty,70.0);table.Columns.Add(new DataGridTemplateColumn{Header=col.Label,CellTemplate=new DataTemplate{VisualTree=factory},Width=85});}else table.Columns.Add(new DataGridTextColumn{Header=col.Label,Binding=new Binding("["+col.Key+"]"),Width=col.Key=="quantity"?65:145});}
            var pictures=new Dictionary<string,ImageSource>();foreach(var row in rows){var r=data.NewRow();foreach(var col in o.Columns.Where(c=>c.Enabled)){if(col.Key=="image"){var key=row.ImageId.Length>0?row.ImageId:row.Symbol;if(!pictures.TryGetValue(key,out var picture))pictures[key]=picture=BomReport.Picture(Editor.Project,row);r[col.Key]=picture;}else r[col.Key]=row.Values[col.Key];}data.Rows.Add(r);}table.ItemsSource=data.DefaultView;feedback.Text=$"{rows.Count} ligne(s) · images intégrées dans HTML ; données textuelles dans CSV.";
        }
        actions.Children.Add(DocButton("Ajouter / actualiser les folios BOM",dialog,()=>{var generated=Editor.GenerateBom(DocumentModel.Copy(Read()));feedback.Text=$"{generated.Count} folio(s) de nomenclature ajoutés au projet, sans cartouche. Réutiliser cette commande après modification du schéma. Ctrl+Z pour annuler.";}));
        actions.Children.Add(DocButton("Actualiser l’aperçu",dialog,RefreshTable));actions.Children.Add(DocButton("Mémoriser ces options",dialog,()=>{var o=DocumentModel.Copy(Read());Editor.Change(()=>Editor.Project.BomOptions=o);feedback.Text="Options enregistrées dans le projet.";}));
        foreach(var format in new[]{"html","csv"})actions.Children.Add(DocButton(format=="html"?"Exporter avec images…":"Exporter CSV…",dialog,()=>{var o=Read();var file=new SaveFileDialog{Filter=format.ToUpper()+"|*."+format,FileName="Nomenclature."+format};if(file.ShowDialog(dialog)==true){ProjectStore.AtomicWrite(file.FileName,format=="html"?BomReport.Html(Editor.Project,o):"\uFEFF"+BomReport.Csv(Editor.Project,o));feedback.Text="Export créé : "+file.FileName;}}));actions.Children.Add(DocButton("Fermer",dialog,dialog.Close));RefreshTable();ShowDocumentDialog(dialog);
    }
}
public sealed class TitleBlockPackage
{
    public string Format { get; set; } = "GeMMeElec.TitleBlock";
    public TitleBlockTemplate Template { get; set; } = new();
    public Dictionary<string,string> Images { get; set; } = [];
}
public sealed class ImagePresenceConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>value is string s&&s.Length>0?"Intégrée":"";
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}
public sealed class TitleBlockPreview : FrameworkElement
{
    private DrawingGroup? drawing;
    private double width,height;
    public void Set(Project project,Sheet sheet,TitleBlockTemplate template)
    {width=sheet.Width-60;height=template.Height;drawing=new();using(var dc=drawing.Open()){dc.PushTransform(new TranslateTransform(-30,-(sheet.Height-30-height)));Drawing.Render(dc,DocumentModel.Scene(project,sheet,template));}drawing.Freeze();InvalidateVisual();}
    protected override void OnRender(DrawingContext dc)
    {dc.DrawRectangle(Brushes.White,new Pen(Drawing.Brush("#D5DFE9"),1),new(0,0,ActualWidth,ActualHeight));if(drawing is null)return;double scale=Math.Min((ActualWidth-12)/width,(ActualHeight-12)/height);if(scale<=0)return;dc.PushTransform(new TranslateTransform((ActualWidth-width*scale)/2,(ActualHeight-height*scale)/2));dc.PushTransform(new ScaleTransform(scale,scale));dc.DrawDrawing(drawing);dc.Pop();dc.Pop();}
}
