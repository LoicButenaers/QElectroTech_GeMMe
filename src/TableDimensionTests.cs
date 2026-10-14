using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

internal static class TableDimensionTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> assert,Action<Action> reject,string dir)
    {
        test("Régression tableau : les champs simples largeur/hauteur modifient les cellules existantes",()=>
        {
            var window=new MainWindow();try
            {
                var e=window.Editor;var g=e.PutGraphic(new(){Kind="table",X=100,Y=100,Width=360,Height=120,Table=new()});string before=e.ExportSnapshot;
                window.DocumentTestProbe=dialog=>{var root=(FrameworkElement)dialog.Content;Arrange(root);var controls=Descendants(root).ToList();controls.OfType<TextBox>().Single(b=>b.Name=="TableCommonColumnWidth").Text="80";controls.OfType<TextBox>().Single(b=>b.Name=="TableCommonRowHeight").Text="50";controls.OfType<Button>().Single(b=>b.Content as string=="Appliquer les largeurs et hauteurs").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));controls.OfType<Button>().Single(b=>b.Content as string=="Enregistrer le tableau").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
                window.TableDialog(g.Id);var current=e.Sheet.Graphics.Single();assert(current.Width==240&&current.Height==150&&current.Table!.ColumnWidths.All(w=>w==80)&&current.Table.RowHeights.All(h=>h==50),"Les champs simples sont ignorés par Appliquer les largeurs et hauteurs.");e.Undo();assert(e.ExportSnapshot==before,"Undo des dimensions");
            }finally{window.DocumentTestProbe=null;window.DisposeTestBridge();}
        });
        test("Régression tableau : dimensions individuelles visibles dans la grille et aperçu à échelle fixe",()=>
        {
            var window=new MainWindow();try{var e=window.Editor;var g=e.PutGraphic(new(){Kind="table",X=100,Y=100,Width=360,Height=120,Table=new()});window.DocumentTestProbe=dialog=>{var root=(FrameworkElement)dialog.Content;Arrange(root);var controls=Descendants(root).ToList();controls.OfType<TextBox>().Single(b=>b.Name=="TableColumnWidths").Text="60 ; 140 ; 210";controls.OfType<TextBox>().Single(b=>b.Name=="TableRowHeights").Text="30 ; 55 ; 80";controls.OfType<Button>().Single(b=>b.Content as string=="Appliquer les largeurs et hauteurs").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Arrange(root);var grid=controls.OfType<DataGrid>().Single();assert(grid.Columns.Select(c=>c.Width.Value).SequenceEqual(new double[]{60,140,210}),"La grille affiche encore des largeurs bornées");assert(grid.Items.OfType<TableRowDraft>().Select(r=>r.Height).SequenceEqual(new double[]{30,55,80}),"Hauteurs de lignes ignorées");var first=(DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);assert(first.Height==30,"Hauteur visuelle de la ligne");var preview=controls.OfType<FreeGraphicPreview>().Single();assert(Math.Abs(preview.DesiredSize.Width-preview.Margin.Left-preview.Margin.Right-(410*.75+16))<.01&&Math.Abs(preview.DesiredSize.Height-preview.Margin.Top-preview.Margin.Bottom-(165*.75+16))<.01,$"L’aperçu masque la modification par un ajustement automatique : {preview.DesiredSize}");Save(root,Path.Combine(dir,"self-test-table-dimensions-ui.png"));controls.OfType<Button>().Single(b=>b.Content as string=="Enregistrer le tableau").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};window.TableDialog(g.Id);assert(e.Sheet.Graphics[0].Width==410&&e.Sheet.Graphics[0].Height==165,"Dimensions non enregistrées");}
            finally{window.DocumentTestProbe=null;window.DisposeTestBridge();}
        });
        test("Régression tableau : valeur unique pour toutes les cellules et largeur déplacée dans la grille",()=>
        {
            var window=new MainWindow();try{var e=window.Editor;var g=e.PutGraphic(new(){Kind="table",X=100,Y=100,Width=360,Height=120,Table=new()});window.DocumentTestProbe=dialog=>{var root=(FrameworkElement)dialog.Content;Arrange(root);var controls=Descendants(root).ToList();var widths=controls.OfType<TextBox>().Single(b=>b.Name=="TableColumnWidths");widths.Text="80";controls.OfType<TextBox>().Single(b=>b.Name=="TableRowHeights").Text="50";controls.OfType<Button>().Single(b=>b.Content as string=="Appliquer les largeurs et hauteurs").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var grid=controls.OfType<DataGrid>().Single();grid.Columns[1].Width=new DataGridLength(135);assert(widths.Text=="80 ; 135 ; 80","Redimensionner la colonne ne met pas à jour les dimensions");controls.OfType<Button>().Single(b=>b.Content as string=="Enregistrer le tableau").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};window.TableDialog(g.Id);assert(e.Sheet.Graphics[0].Table!.ColumnWidths.SequenceEqual(new double[]{80,135,80})&&e.Sheet.Graphics[0].Height==150,"Déplacement de colonne perdu à l’enregistrement");}
            finally{window.DocumentTestProbe=null;window.DisposeTestBridge();}
        });
        test("Régression propriétés : dimensions complètes, saisie intermédiaire et taille commune",()=>
        {
            var window=new MainWindow();try{var e=window.Editor;var g=e.PutGraphic(new(){Kind="table",X=100,Y=100,Width=360,Height=120,Table=new()});window.Canvas.Select(g.Id);string before=e.ExportSnapshot;var width=window.PropertyField("graphicWidth");foreach(string text in new[]{"","1","15","450"})width.Text=text;assert(e.ExportSnapshot==before&&window.PropertyError("graphicWidth")=="","Une saisie intermédiaire modifie/rejette le tableau");assert(window.CommitDimensionInputs()&&e.Sheet.Graphics[0].Width==450,"Dimension totale non appliquée");assert(window.PropertyField("tableColumnWidth").Text=="150","Largeur commune non synchronisée");var height=window.PropertyField("graphicHeight");height.Text="180";height.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,height,null){RoutedEvent=Keyboard.LostKeyboardFocusEvent});assert(e.Sheet.Graphics[0].Height==180&&window.PropertyField("tableRowHeight").Text=="60","Quitter le champ n’applique pas la hauteur");window.PropertyField("tableColumnWidth").Text="90";window.PropertyField("tableRowHeight").Text="45";assert(window.CommitDimensionInputs()&&e.Sheet.Graphics[0].Width==270&&e.Sheet.Graphics[0].Height==135&&window.PropertyField("graphicWidth").Text=="270"&&window.PropertyField("graphicHeight").Text=="135","Taille commune/totale non synchronisée");string path=Path.Combine(dir,"self-test-table-size-properties.gemelec");e.Save(path);assert(ProjectStore.Serialize(ProjectStore.Load(path))==e.ExportSnapshot,"Relecture des dimensions");}
            finally{window.DisposeTestBridge();}
        });
        test("Régression propriétés : pourcentages, limites de cellule et annulation sans perte",()=>
        {
            var window=new MainWindow();try{var e=window.Editor;var g=e.PutGraphic(new(){Kind="table",X=100,Y=100,Width=360,Height=120,Table=new()});window.Canvas.Select(g.Id);Arrange(window.PropertyPanel);var mode=Descendants(window.PropertyPanel).OfType<ComboBox>().Single(c=>c.Name=="PageCoordinateMode");mode.SelectedValue="percent";string before=e.ExportSnapshot;window.PropertyField("graphicWidth").Text="50";window.PropertyField("graphicHeight").Text="20";assert(window.CommitDimensionInputs()&&e.Sheet.Graphics[0].Width==700&&e.Sheet.Graphics[0].Height==198,"Dimensions % non appliquées");e.Undo();e.Undo();assert(e.ExportSnapshot==before,"Undo des dimensions %");var height=window.PropertyField("graphicHeight");height.Text="1";assert(!window.CommitDimensionInputs()&&window.PropertyError("graphicHeight").Contains("8 unités")&&e.ExportSnapshot==before,"Limite de cellule sans erreur compréhensible ou rollback");height.Text="";window.WorkspaceCommandForTest("undo").Execute(null,window);assert(e.Project.Sheets.Count==1,"Historique détruit par un champ incomplet");}
            finally{window.DisposeTestBridge();}
        });
        test("Régression tableau : annuler le dialogue conserve contenu, styles et dimensions",()=>
        {
            var window=new MainWindow();try{var e=RichGraphicsTests.Example();window.Editor.ImportProject(e.Project);string before=window.Editor.ExportSnapshot;var id=window.Editor.Sheet.Graphics.Last().Id;window.DocumentTestProbe=dialog=>{var root=(FrameworkElement)dialog.Content;Arrange(root);var controls=Descendants(root).ToList();controls.OfType<TextBox>().Single(b=>b.Name=="TableCommonColumnWidth").Text="100";controls.OfType<TextBox>().Single(b=>b.Name=="TableCommonRowHeight").Text="40";controls.OfType<Button>().Single(b=>b.Content as string=="Appliquer les largeurs et hauteurs").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));controls.OfType<Button>().Single(b=>b.Content as string=="Annuler").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};window.TableDialog(id);assert(window.Editor.ExportSnapshot==before,"Annuler modifie le folio");}
            finally{window.DocumentTestProbe=null;window.DisposeTestBridge();}
        });
    }
    private static void Arrange(FrameworkElement root){root.Measure(new(1110,800));root.Arrange(new Rect(0,0,1110,800));root.UpdateLayout();}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent){yield return parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(parent,i)))yield return child;}
    private static void Save(FrameworkElement element,string path){var bitmap=new RenderTargetBitmap(1110,800,96,96,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);}
}
