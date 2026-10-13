using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GeMMeElec;

internal static class StaWork
{
    public static Task<T> Run<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { completion.SetResult(action()); } catch (Exception ex) { completion.SetException(ex); } }) { IsBackground = true, Name = "GeMMeElec · export" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}

public sealed partial class MainWindow
{
    private bool exportInProgress;
    internal async Task<object> ExportPdfAsync(string path)
    {
        if (exportInProgress) throw new InvalidOperationException("Un export PDF est déjà en cours.");
        Canvas.CancelDrag(); string snapshot = Editor.ExportSnapshot;
        exportInProgress = true; status.Text = "Export PDF en cours · vous pouvez continuer le dessin…"; CommandManager.InvalidateRequerySuggested();
        try
        {
            await StaWork.Run(() => { var copy = ProjectStore.Deserialize(snapshot); Export.Pdf(copy, path); return true; });
            status.Text = "PDF créé · " + path; return new { path };
        }
        finally { exportInProgress = false; CommandManager.InvalidateRequerySuggested(); }
    }
}
