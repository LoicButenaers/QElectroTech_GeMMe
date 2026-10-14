using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GeMMeElec;

public sealed class ProjectSession(Editor editor, SheetCanvas canvas)
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Editor Editor { get; } = editor;
    public SheetCanvas Canvas { get; } = canvas;
    public string? RecoverySnapshot { get; set; }
}
public sealed partial class MainWindow
{
    private readonly List<ProjectSession> sessions = [];
    private ProjectSession activeSession = null!;
    private readonly StackPanel projectTabs = new() { Orientation = Orientation.Horizontal };
    public IReadOnlyList<ProjectSession> Sessions => sessions;
    public string ActiveSessionId => activeSession.Id;
    internal void CheckProjectSavePath(string path)
    {
        path=Path.GetFullPath(path);
        if(sessions.Any(s=>s!=activeSession&&s.Editor.FilePath is string other&&string.Equals(Path.GetFullPath(other),path,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("Ce fichier est ouvert dans un autre onglet. Choisir un autre nom de fichier ou enregistrer depuis cet onglet.");
    }
    private void InitializeSessions(DockPanel root)
    {
        activeSession = BindSession(Editor, Canvas); sessions.Add(activeSession);
        var bar = UiTheme.Bind(new ScrollViewer { Content = projectTabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }, Control.BackgroundProperty, "Ui.Surface");
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar); RefreshTabs();
    }
    private ProjectSession BindSession(Editor editor, SheetCanvas canvas)
    {
        var session = new ProjectSession(editor, canvas);
        canvas.ContextRequested += () => { if (activeSession == session) ShowSchematicMenu(); };
        canvas.BandRequested += (a,b,mode) => Run(() => BandDialog(a,b,mode));
        canvas.ReferenceRequested += FollowReference;
        canvas.Status += text => { if (activeSession == session) status.Text = text; };
        canvas.SelectionChanged += () => { if (activeSession == session) Inspect(); };
        canvas.TextRequested += p => { FreeTextDialog(p); };
        editor.Changed += () => { RefreshTabs(); if (activeSession == session) Refresh(); };
        return session;
    }
    private void RefreshTabs()
    {
        projectTabs.Children.Clear();
        foreach (var session in sessions)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var select = new Button { Content = (session.Editor.Dirty ? "● " : "") + session.Editor.Project.Title, ToolTip = session.Editor.FilePath ?? "Projet non enregistré", FontWeight = session == activeSession ? FontWeights.Bold : FontWeights.Normal, Margin = new(1,2,0,2), Padding = new(10,5,10,5) };
            select.Click += (_,_) => Run(() => SelectProjectTab(session.Id)); row.Children.Add(select);
            var close = new Button { Content = "×", ToolTip = "Fermer ce projet", Margin = new(0,2,5,2), Padding = new(7,5,7,5) }; close.Click += (_,_) => Run(() => CloseProjectTabWithPrompt(session.Id)); row.Children.Add(close); projectTabs.Children.Add(row);
        }
    }
    public ProjectSession AddProjectTab(Project? project = null, string? path = null, string? title = null)
    {
        if (path is not null)
        {
            path = Path.GetFullPath(path);
            var existing = sessions.FirstOrDefault(s => s.Editor.FilePath is string file && string.Equals(Path.GetFullPath(file), path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) { SelectProjectTab(existing.Id); return existing; }
        }
        if (sessions.Count >= 10) throw new ArgumentException("Maximum 10 projets ouverts ; fermer un onglet avant de continuer.");
        var editor = new Editor { CurrentWireColor = preferences.WireColor };
        if (path is not null) editor.Open(path); else if (project is not null) editor.ImportProject(project); else if (title is not null) editor.New(title);
        var canvas = new SheetCanvas(editor) { GridVisible = preferences.GridVisible, GridIntensity = preferences.GridIntensity };
        UiTheme.Bind(canvas, SheetCanvas.WorkspaceBackgroundProperty, "Ui.Background");
        var session = BindSession(editor, canvas); sessions.Add(session); SelectProjectTab(session.Id); return session;
    }
    public void SelectProjectTab(string id)
    {
        var session = sessions.Single(s => s.Id == id);
        if (activeSession == session) return;
        Canvas.CancelDrag(); Editor.EndPropertyEdit(); editingProperty = false;
        activeSession = session; Editor = session.Editor; Canvas = session.Canvas; workspace?.SetCanvas(Canvas);
        displayedCustomSymbols = null; Filter(); Refresh(); RefreshTabs();
    }
    public void CloseProjectTab(string id, bool discardUnsaved = false)
    {
        var session = sessions.Single(s => s.Id == id);
        if (session.Editor.InteractionInProgress) session.Canvas.CancelDrag();
        if (session.Editor.Dirty && !discardUnsaved) throw new ArgumentException("Enregistrer ce projet ou autoriser explicitement l’abandon de ses modifications.");
        if (session == activeSession)
        {
            if (sessions.Count == 1) { AddProjectTab(); }
            else SelectProjectTab(sessions.First(s => s != session).Id);
        }
        sessions.Remove(session); RefreshTabs();
    }
    private void CloseProjectTabWithPrompt(string id)
    {
        SelectProjectTab(id); Canvas.CancelDrag(); if (CanReplace()) CloseProjectTab(id, true);
    }
    private bool CanCloseAllProjects()
    {
        string original = ActiveSessionId;
        foreach (var session in sessions.ToList()) { SelectProjectTab(session.Id); Canvas.CancelDrag(); if (!CanReplace()) return false; }
        SelectProjectTab(original); return true;
    }
}
