using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GeMMeElec;

public sealed record RecoveryInfo(string File,string ProjectId,string Title,string SourcePath,string SheetId,DateTimeOffset SavedAt,string ProjectHash) { [System.Text.Json.Serialization.JsonIgnore] public string LocalDate => SavedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"); }
public static class RecoveryStore
{
    public static string Folder => Path.Combine(Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION")=="1"?AppContext.BaseDirectory:Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GeMMeElec-recovery");
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    public static RecoveryInfo Write(string snapshot,string projectId,string title,string sourcePath,string sheetId,int keep=5,string? folder=null)
    {
        if(keep is <1 or >20)throw new ArgumentException("Conserver 1 à 20 copies.");byte[] bytes=Encoding.UTF8.GetBytes(snapshot);if(bytes.Length>Project.MaxFileBytes)throw new InvalidDataException("Copie trop volumineuse.");folder??=Folder;Directory.CreateDirectory(folder);string group=Hash(Encoding.UTF8.GetBytes(projectId))[..20];string path=Path.Combine(Path.GetFullPath(folder),"recover-"+group+"-"+Guid.NewGuid().ToString("N")+".gemrecovery");var info=new RecoveryInfo(path,projectId,title,sourcePath,sheetId,DateTimeOffset.UtcNow,Hash(bytes));string temp=path+".tmp";
        try{using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create)){using(var writer=new StreamWriter(zip.CreateEntry("info.json").Open(),new UTF8Encoding(false)))writer.Write(JsonSerializer.Serialize(info,ProjectStore.Json));using var stream=zip.CreateEntry("project.gemelec",CompressionLevel.Fastest).Open();stream.Write(bytes);}File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
        foreach(var old in Directory.EnumerateFiles(folder,"recover-"+group+"-*.gemrecovery").OrderByDescending(File.GetLastWriteTimeUtc).Skip(keep))File.Delete(old);
        return info;
    }
    private static byte[] ReadEntry(ZipArchive archive,string name,int limit){var entry=archive.GetEntry(name)??throw new InvalidDataException("Copie incomplète.");if(entry.Length>limit)throw new InvalidDataException("Copie trop volumineuse.");using var stream=entry.Open();using var output=new MemoryStream();var buffer=new byte[16384];int read;while((read=stream.Read(buffer))>0){if(output.Length+read>limit)throw new InvalidDataException("Copie trop volumineuse.");output.Write(buffer,0,read);}return output.ToArray();}
    public static List<RecoveryInfo> List(string? folder=null)
    {
        folder??=Folder;if(!Directory.Exists(folder))return[];List<RecoveryInfo> result=[];foreach(var path in Directory.EnumerateFiles(folder,"recover-*.gemrecovery").OrderByDescending(File.GetLastWriteTimeUtc).Take(1000)){try{using var zip=ZipFile.OpenRead(path);var info=JsonSerializer.Deserialize<RecoveryInfo>(ReadEntry(zip,"info.json",65536),ProjectStore.Json)??throw new InvalidDataException();result.Add(info with{File=Path.GetFullPath(path)});}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException){ }}return result.OrderByDescending(r=>r.SavedAt).ToList();
    }
    public static (Project Project,RecoveryInfo Info) Read(string path)
    {
        using var zip=ZipFile.OpenRead(path);var info=JsonSerializer.Deserialize<RecoveryInfo>(ReadEntry(zip,"info.json",65536),ProjectStore.Json)??throw new InvalidDataException("Métadonnées absentes.");var bytes=ReadEntry(zip,"project.gemelec",Project.MaxFileBytes);if(Hash(bytes)!=info.ProjectHash)throw new InvalidDataException("Copie altérée ; le projet ouvert est conservé.");var p=ProjectStore.Deserialize(Encoding.UTF8.GetString(bytes));if(p.Id!=info.ProjectId)throw new InvalidDataException("Copie incohérente.");return(p,info with{File=Path.GetFullPath(path)});
    }
}
public sealed partial class MainWindow
{
    private readonly DispatcherTimer recoveryTimer=new();
    private bool recoveryWriting;

    private void StartRecovery()
    {
        if(Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION")=="1")return;
        recoveryTimer.Interval=TimeSpan.FromSeconds(preferences.RecoveryIntervalSeconds);recoveryTimer.Tick+=async(_,_)=>{if(!preferences.RecoveryEnabled||recoveryWriting)return;
            var pending=sessions.Where(s=>s.Editor.Dirty&&!s.Editor.InteractionInProgress&&!ReferenceEquals(s.RecoverySnapshot,s.Editor.ExportSnapshot)).Select(s=>(Session:s,Snapshot:s.Editor.ExportSnapshot,Id:s.Editor.Project.Id,Title:s.Editor.Project.Title,Path:s.Editor.FilePath??"",Sheet:s.Editor.Sheet.Id)).ToList();
            if(pending.Count==0)return;recoveryWriting=true;int keep=preferences.RecoveryKeepCount;
            try{await Task.Run(()=>{foreach(var item in pending)RecoveryStore.Write(item.Snapshot,item.Id,item.Title,item.Path,item.Sheet,keep);});foreach(var item in pending)item.Session.RecoverySnapshot=item.Snapshot;}
            catch(Exception ex){status.Text="Copie automatique échouée · "+ex.Message;}finally{recoveryWriting=false;}};recoveryTimer.Start();
        Loaded+=(_,_)=>{try{var pending=RecoveryStore.List().Where(r=>!string.IsNullOrWhiteSpace(r.SourcePath)&&!r.SourcePath.StartsWith("\\\\")&&File.Exists(r.SourcePath)?File.GetLastWriteTimeUtc(r.SourcePath)<r.SavedAt.UtcDateTime:true).ToList();if(pending.Count>0)RecoveryDialog();}catch(Exception ex){status.Text="Lecture des copies impossible · "+ex.Message;}};
    }
    public async Task<object> SaveRecoveryAsync()
    {
        if(recoveryWriting)throw new InvalidOperationException("Copie automatique déjà en cours.");if(Editor.InteractionInProgress)throw new InvalidOperationException("Terminer le déplacement avant la copie.");var capturedSession=activeSession;string snapshot=Editor.ExportSnapshot,id=Editor.Project.Id,title=Editor.Project.Title,path=Editor.FilePath??"",sheet=Editor.Sheet.Id;int keep=preferences.RecoveryKeepCount;recoveryWriting=true;try{var info=await Task.Run(()=>RecoveryStore.Write(snapshot,id,title,path,sheet,keep));capturedSession.RecoverySnapshot=snapshot;return info;}finally{recoveryWriting=false;}
    }
    private void RecoveryDialog()
    {
        Canvas.CancelDrag();var dialog=DocumentWindow("Sauvegarde automatique et récupération",1120,730);var root=new DockPanel{Margin=new(18)};dialog.Content=root;var settings=new WrapPanel();DockPanel.SetDock(settings,Dock.Top);root.Children.Add(settings);var enabled=new CheckBox{Content="Copies automatiques des projets modifiés",IsChecked=preferences.RecoveryEnabled,Margin=new(0,6,15,6)};settings.Children.Add(enabled);settings.Children.Add(new TextBlock{Text="Intervalle (s)",Margin=new(0,6,5,0)});var interval=new TextBox{Text=preferences.RecoveryIntervalSeconds.ToString(),Width=80,Padding=new(5),ToolTip="Intervalle en secondes · 30 à 3600",Margin=new(0,0,10,8)};settings.Children.Add(interval);settings.Children.Add(new TextBlock{Text="Copies par projet",Margin=new(0,6,5,0)});var keep=new TextBox{Text=preferences.RecoveryKeepCount.ToString(),Width=60,Padding=new(5),ToolTip="Copies par projet · 1 à 20",Margin=new(0,0,10,8)};settings.Children.Add(keep);settings.Children.Add(DocButton("Mémoriser",dialog,()=>{if(!int.TryParse(interval.Text,out int seconds)||seconds is <30 or >3600||!int.TryParse(keep.Text,out int count)||count is <1 or >20)throw new ArgumentException("Intervalle 30–3600 secondes ; copies 1–20.");var draft=DocumentModel.Copy(preferences);draft.RecoveryEnabled=enabled.IsChecked==true;draft.RecoveryIntervalSeconds=seconds;draft.RecoveryKeepCount=count;draft.Save();preferences.RecoveryEnabled=draft.RecoveryEnabled;preferences.RecoveryIntervalSeconds=seconds;preferences.RecoveryKeepCount=count;recoveryTimer.Interval=TimeSpan.FromSeconds(seconds);}));
        var hint=Hint("La copie automatique est séparée du fichier de travail. Restaurer ouvre un projet non enregistré ; utiliser Enregistrer sous pour conserver le fichier d’origine.");DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);var bottom=new WrapPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);var grid=new DataGrid{IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false};root.Children.Add(grid);foreach(var(header,path,width)in new[]{("Projet","Title",230d),("Date locale","LocalDate",200d),("Fichier de travail","SourcePath",540d)})grid.Columns.Add(new DataGridTextColumn{Header=header,Binding=new System.Windows.Data.Binding(path),Width=width});void RefreshRows()=>grid.ItemsSource=RecoveryStore.List();
        bottom.Children.Add(DocButton("Actualiser",dialog,RefreshRows));bottom.Children.Add(DocButton("Restaurer une copie",dialog,()=>{if(grid.SelectedItem is not RecoveryInfo row)return;var restored=RecoveryStore.Read(row.File);AddProjectTab(restored.Project);if(Editor.Project.Sheets.Any(s=>s.Id==restored.Info.SheetId))Editor.SelectSheet(restored.Info.SheetId);dialog.Close();}));bottom.Children.Add(DocButton("Fermer",dialog,dialog.Close));RefreshRows();ShowDocumentDialog(dialog);
    }
}
