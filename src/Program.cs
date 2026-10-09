using System.IO;
using System.Windows;

namespace GeMMeElec;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--mcp")) return McpServer.Run().GetAwaiter().GetResult();
        if (args.Contains("--self-test")) return SelfTests.Run();
        using var single = new Mutex(true, "Local\\GeMMeElec-" + Environment.UserName, out var first);
        if (!first) { MessageBox.Show("GeMMeElec est déjà ouvert. Utilisez la fenêtre existante.", "GeMMeElec"); return 0; }
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (_, e) => { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "GeMMeElec-errors.log"), DateTime.Now + " " + e.Exception + Environment.NewLine); MessageBox.Show(e.Exception.Message, "GeMMeElec"); e.Handled = true; };
            return app.Run(new MainWindow(args.FirstOrDefault(a => a.EndsWith(".gemelec", StringComparison.OrdinalIgnoreCase))));
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "GeMMeElec-errors.log"), ex.ToString()); MessageBox.Show(ex.Message, "GeMMeElec · démarrage impossible"); return 1; }
    }
}
