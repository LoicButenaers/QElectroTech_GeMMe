using System.Windows;
using System.Windows.Controls;

namespace GeMMeElec;

public sealed class PdfOptions
{
    public bool Contents { get; set; } = true;
    public bool Bookmarks { get; set; } = true;
    public bool Links { get; set; } = true;
}
public sealed record PdfLink(Rect Bounds,string TargetSheetId,double TargetX,double TargetY);
public sealed record PdfPage(Sheet Sheet,string Title,List<Ink> Scene,List<PdfLink> Links);
public static class PdfNavigation
{
    public static List<PdfPage> Pages(Project p)
    {
        List<PdfPage> pages=[];var refs=new CrossReferences(p);
        if(p.PdfOptions.Contents)
        {
            string ContentsId(int index){string id="pdf-contents-"+index;while(p.Sheets.Any(s=>s.Id==id))id+="-toc";return id;}
            int count=0;double y=110;Sheet sheet=new(){Id=ContentsId(count),Title="Sommaire",TitleBlockId=""};List<Ink> scene=[];List<PdfLink> links=[];
            void Header(){scene.Add(new("text",60,45,Text:"Sommaire du projet",Size:25));double titleY=82;foreach(string line in BomFolios.Wrap(p.Title,1030)){scene.Add(new("text",60,titleY,Text:line,Size:12));titleY+=16;}y=Math.Max(110,titleY+16);}
            void Finish(){scene.Add(new("text",60,940,Text:"Sommaire "+(count+1)+" · cliquer une ligne pour rejoindre le folio",Size:10));pages.Add(new(sheet,"Sommaire "+(count+1),scene,links));count++;sheet=new(){Id=ContentsId(count),Title="Sommaire",TitleBlockId=""};scene=[];links=[];y=110;}
            Header();foreach(var target in p.Sheets){var lines=BomFolios.Wrap(DocumentModel.PageNumber(p,target)+" · "+target.Title,1230);foreach(string text in lines){if(y+22>900){Finish();Header();}scene.Add(new("text",60,y,Text:text,Size:12,Color:"#087344"));links.Add(new(new Rect(58,y-2,1280,22),target.Id,30,30));y+=20;}y+=8;}Finish();
        }
        foreach(var sheet in p.Sheets)
        {
            List<PdfLink> links=[];
            if(p.PdfOptions.Links)foreach(var c in sheet.Components)
            {
                List<ReferencePoint> targets=[];string key="";
                if(CrossReferences.IsPoint(c)){targets=refs.Targets(c.Id).ToList();key="detail";}
                else if(CoilContacts.IsCoil(c)){targets=refs.Coils.Contacts(c.Id).ToList();key="coilReferences";}
                else if(CoilContacts.IsContact(c)&&refs.Coils.Coil(c) is ReferencePoint coil){targets=[coil];key="coilReferences";}
                if(targets.Count==0||ComponentTexts.Ink(p,c,refs,key) is not Ink ink)continue;var bounds=Drawing.TextBounds(ink);double height=bounds.Height/targets.Count;for(int i=0;i<targets.Count;i++){var target=targets[i];links.Add(new(new Rect(bounds.X,bounds.Y+i*height,bounds.Width,height),target.Sheet.Id,target.Component.X,target.Component.Y));}
            }
            pages.Add(new(sheet,DocumentModel.PageNumber(p,sheet)+" · "+sheet.Title,Drawing.Scene(p,sheet),links));
        }
        return pages;
    }
}
public sealed partial class MainWindow
{
    private void PdfOptionsDialog()
    {
        var dialog=DocumentWindow("PDF · sommaire signets et renvois",560,330);var panel=new StackPanel{Margin=new(20)};dialog.Content=panel;var options=Editor.Project.PdfOptions;var contents=new CheckBox{Content="Ajouter le sommaire avec liens vers les folios",IsChecked=options.Contents,Margin=new(0,8,0,8)};var bookmarks=new CheckBox{Content="Ajouter les signets de navigation",IsChecked=options.Bookmarks,Margin=new(0,8,0,8)};var links=new CheckBox{Content="Rendre les reports et références bobine/contact cliquables",IsChecked=options.Links,Margin=new(0,8,0,8)};panel.Children.Add(contents);panel.Children.Add(bookmarks);panel.Children.Add(links);panel.Children.Add(DocButton("Appliquer",dialog,()=>{Editor.Change(()=>Editor.Project.PdfOptions=new(){Contents=contents.IsChecked==true,Bookmarks=bookmarks.IsChecked==true,Links=links.IsChecked==true});dialog.Close();}));ShowDocumentDialog(dialog);
    }
}
