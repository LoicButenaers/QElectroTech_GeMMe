using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace GeMMeElec;

public sealed class EditableShape
{
    public string Kind { get; set; } = "line"; public double X { get; set; } public double Y { get; set; } public double A { get; set; } = 20; public double B { get; set; } public string Text { get; set; } = ""; public double Rotation { get; set; }
    public Shape Shape() => new(Kind, X, Y, A, B, Text) { Rotation = Rotation };
    public static EditableShape From(Shape s) => new() { Kind=s.Kind, X=s.X, Y=s.Y, A=s.A, B=s.B, Text=s.Text, Rotation=s.Rotation };
}
public sealed class EditablePort { public string Id { get; set; } = "1"; public double X { get; set; } public double Y { get; set; } }
public sealed class SymbolEditPreview : FrameworkElement
{
    public Func<SymbolDefinition>? Read; public Action<System.Windows.Point>? Click; private double scale = 1; private Vector origin;
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.WhiteSmoke, new Pen(Brushes.LightGray, 1), new(0,0,ActualWidth,ActualHeight));
        SymbolDefinition? d; try { d=Read?.Invoke(); if(d is not null)CustomSymbols.Validate(d); } catch { return; } if(d is null)return;
        scale=Math.Clamp(Math.Min((ActualWidth-70)/d.Width,(ActualHeight-70)/d.Height),.01,4); origin=new(ActualWidth/2,ActualHeight/2);
        dc.PushTransform(new TranslateTransform(origin.X,origin.Y));dc.PushTransform(new ScaleTransform(scale,scale));
        dc.DrawLine(new Pen(Brushes.LightGray,1/scale),new(-d.Width/2,0),new(d.Width/2,0));dc.DrawLine(new Pen(Brushes.LightGray,1/scale),new(0,-d.Height/2),new(0,d.Height/2));
        Drawing.Render(dc,d.Shapes.Select(s=>new Ink(s.Kind,s.X,s.Y,s.A,s.B,s.Text,Size:s.A,Rotation:s.Rotation)));
        foreach(var p in d.Ports){dc.DrawEllipse(Brushes.White,new Pen(Brushes.Teal,1.5/scale),new(p.X,p.Y),4/scale,4/scale);Drawing.Render(dc,[new("text",p.X+5/scale,p.Y-14/scale,Text:p.Id,Size:11/scale,Color:"#007F86")]);}dc.Pop();dc.Pop();
    }
    protected override void OnMouseDown(MouseButtonEventArgs e) { if(e.ChangedButton==MouseButton.Left) {var p=e.GetPosition(this);Click?.Invoke(new(Math.Round((p.X-origin.X)/scale,2),Math.Round((p.Y-origin.Y)/scale,2)));} }
}
public sealed partial class MainWindow
{
    private void SymbolEditor()
    {
        Canvas.CancelDrag(); var c=Editor.Sheet.Components.FirstOrDefault(c=>Canvas.Selected.Contains(c.Id));
        string id=c?.Symbol ?? Canvas.Symbol;
        if(id is "junction" or "folio_next" or "folio_previous")throw new ArgumentException("Sélectionner un symbole d’appareil ; les reports et jonctions se règlent dans leurs propriétés.");
        EditSymbol(CustomSymbols.Resolve(Editor.Project,id),c?.Id);
    }
    private async void ImportSymbol()
    {
        Canvas.CancelDrag();var picker=new OpenFileDialog{Filter="Symboles DXF / GeMMeElec|*.dxf;*.gemsymbol",Title="Importer un symbole"};if(picker.ShowDialog(this)!=true)return;
        try
        {
            IsEnabled=false;status.Text="Lecture du symbole…";
            var result=await Task.Run(()=>picker.FileName.EndsWith(".dxf",StringComparison.OrdinalIgnoreCase)?DxfImport.Read(picker.FileName):new DxfImportResult(CustomSymbols.Load(picker.FileName),[],"unités GeMMeElec"));
            IsEnabled=true;EditSymbol(result.Symbol,null,string.Join("\n",new[]{"Unité du fichier : "+result.Units}.Concat(result.Warnings)));
        } catch(Exception ex){MessageBox.Show(this,ex.Message,"Import du symbole");}finally{IsEnabled=true;status.Text="Prêt";}
    }
    internal void EditSymbol(SymbolDefinition source,string? replace=null,string warnings="")
    {
        var dialog=DocumentWindow("Symbole · modifier et enregistrer une copie",1230,830);var root=new DockPanel{Margin=new(16)};dialog.Content=root;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        top.Children.Add(Hint("La copie conserve ses bornes et son dessin dans le projet. Exporter en .gemsymbol permet de la réutiliser dans un autre projet. Les symboles d’origine restent disponibles."));
        var fields=new WrapPanel();top.Children.Add(fields);
        TextBox Field(string label,string value,int width){fields.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center,Margin=new(5)});var box=new TextBox{Text=value,Width=width,Margin=new(3),Padding=new(5)};fields.Children.Add(box);return box;}
        var name=Field("Nom",source.Name+" · copie",280);var prefix=Field("Préfixe",source.Prefix,55);var width=Field("Largeur",source.Width.ToString(CultureInfo.InvariantCulture),70);var height=Field("Hauteur",source.Height.ToString(CultureInfo.InvariantCulture),70);var factor=Field("Échelle ×","1",60);
        var shapes=new ObservableCollection<EditableShape>(source.Shapes.Select(EditableShape.From));var ports=new ObservableCollection<EditablePort>(source.Ports.Select(p=>new EditablePort{Id=p.Id,X=p.X,Y=p.Y}));
        var actions=new WrapPanel();DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        var body=new Grid();body.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new GridLength(380)});root.Children.Add(body);
        var left=new DockPanel();body.Children.Add(left);var bar=new WrapPanel();DockPanel.SetDock(bar,Dock.Top);left.Children.Add(bar);
        var tabs=new TabControl();left.Children.Add(tabs);var drawingGrid=GridFor(shapes,[("Type","Kind",70),("X","X",55),("Y","Y",55),("X2 / L / R / taille","A",115),("Y2 / hauteur","B",95),("Texte","Text",160),("Rotation","Rotation",70)]);var portGrid=GridFor(ports,[("ID de borne","Id",130),("X","X",90),("Y","Y",90)]);
        tabs.Items.Add(new TabItem{Header="Dessin",Content=drawingGrid});tabs.Items.Add(new TabItem{Header="Bornes électriques",Content=portGrid});
        var side=new DockPanel{Margin=new(12,0,0,0)};Grid.SetColumn(side,1);body.Children.Add(side);var hint=Hint("Ligne : X/Y → X2/Y2. Rectangle : largeur/hauteur. Cercle : rayon. Texte : taille et contenu.\nSélectionner une borne puis cliquer l’aperçu pour la positionner. Une ligne du dessin peut aussi être déplacée par clic.");DockPanel.SetDock(hint,Dock.Top);side.Children.Add(hint);
        if(warnings.Length>0){var warningBox=new TextBox{Text=warnings,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=150,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,BorderThickness=new(0),Background=Brushes.LemonChiffon};DockPanel.SetDock(warningBox,Dock.Bottom);side.Children.Add(warningBox);}
        double Number(string s)=>double.Parse(s.Replace(',','.'),CultureInfo.InvariantCulture);
        SymbolDefinition Read()=>new("draft",name.Text,prefix.Text,Number(width.Text),Number(height.Text),ports.Select(p=>new Port(p.Id,p.X,p.Y)).ToList(),shapes.Select(s=>s.Shape()).ToList()){Category="Mes symboles"};
        var preview=new SymbolEditPreview{MinHeight=300,Read=Read};side.Children.Add(preview);
        void Commit(){if(!drawingGrid.CommitEdit(DataGridEditingUnit.Cell,true)||!drawingGrid.CommitEdit(DataGridEditingUnit.Row,true)||!portGrid.CommitEdit(DataGridEditingUnit.Cell,true)||!portGrid.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Corriger la cellule avant de continuer.");}
        void Redraw(){Commit();CustomSymbols.Validate(Read());preview.InvalidateVisual();}
        foreach(var kind in new[]{"line","rect","circle","text"})bar.Children.Add(DocButton(kind switch{"line"=>"+ Ligne","rect"=>"+ Rectangle","circle"=>"+ Cercle",_=>"+ Texte"},dialog,()=>{var s=new EditableShape{Kind=kind,B=kind=="rect"?20:0,A=kind=="text"?12:20,Text=kind=="text"?"Texte":""};shapes.Add(s);drawingGrid.SelectedItem=s;tabs.SelectedIndex=0;preview.InvalidateVisual();}));
        bar.Children.Add(DocButton("+ Borne",dialog,()=>{int n=1;while(ports.Any(p=>p.Id==n.ToString()))n++;var port=new EditablePort{Id=n.ToString()};ports.Add(port);portGrid.SelectedItem=port;tabs.SelectedIndex=1;preview.InvalidateVisual();}));
        bar.Children.Add(DocButton("Supprimer",dialog,()=>{Commit();if(tabs.SelectedIndex==0&&drawingGrid.SelectedItem is EditableShape s)shapes.Remove(s);else if(portGrid.SelectedItem is EditablePort p)ports.Remove(p);preview.InvalidateVisual();}));
        preview.Click=p=>{try{Commit();if(tabs.SelectedIndex==1&&portGrid.SelectedItem is EditablePort port){port.X=p.X;port.Y=p.Y;portGrid.Items.Refresh();}else if(drawingGrid.SelectedItem is EditableShape shape){double dx=p.X-shape.X,dy=p.Y-shape.Y;shape.X=p.X;shape.Y=p.Y;if(shape.Kind=="line"){shape.A+=dx;shape.B+=dy;}drawingGrid.Items.Refresh();}preview.InvalidateVisual();}catch(Exception ex){MessageBox.Show(dialog,ex.Message);}};
        fields.Children.Add(DocButton("Appliquer l’échelle",dialog,()=>{Commit();var scaled=DxfImport.Scale(Read(),Number(factor.Text));CustomSymbols.Validate(scaled);shapes.Clear();foreach(var s in scaled.Shapes)shapes.Add(EditableShape.From(s));ports.Clear();foreach(var p in scaled.Ports)ports.Add(new(){Id=p.Id,X=p.X,Y=p.Y});width.Text=scaled.Width.ToString(CultureInfo.InvariantCulture);height.Text=scaled.Height.ToString(CultureInfo.InvariantCulture);factor.Text="1";preview.InvalidateVisual();}));
        actions.Children.Add(DocButton("Actualiser l’aperçu",dialog,Redraw));var replaceBox=new CheckBox{Content="Remplacer le composant sélectionné",IsEnabled=replace is not null,IsChecked=replace is not null,VerticalAlignment=VerticalAlignment.Center,Margin=new(12)};actions.Children.Add(replaceBox);
        actions.Children.Add(DocButton("Exporter .gemsymbol…",dialog,()=>{Commit();var d=CustomSymbols.Copy(Read(),name.Text);CustomSymbols.Validate(d);var save=new SaveFileDialog{Filter="Symbole GeMMeElec|*.gemsymbol",DefaultExt=".gemsymbol",FileName="symbole.gemsymbol"};if(save.ShowDialog(dialog)==true)CustomSymbols.Export(d,save.FileName);}));
        actions.Children.Add(DocButton("Enregistrer la copie",dialog,()=>{Commit();var d=Editor.SaveSymbolCopy(Read(),replaceBox.IsChecked==true?replace:null);Canvas.Symbol=d.Id;Canvas.CatalogId=null;libraryMode.SelectedIndex=2;Filter();dialog.Close();}));actions.Children.Add(DocButton("Annuler",dialog,dialog.Close));
        ShowDocumentDialog(dialog);
    }
    private static DataGrid GridFor(object items,(string Label,string Field,int Width)[] columns)
    {
        var grid=new DataGrid{ItemsSource=(System.Collections.IEnumerable)items,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,EnableRowVirtualization=true,SelectionMode=DataGridSelectionMode.Single};
        foreach(var c in columns)grid.Columns.Add(new DataGridTextColumn{Header=c.Label,Width=c.Width,Binding=new Binding(c.Field){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus,ValidatesOnExceptions=true}});return grid;
    }
}
