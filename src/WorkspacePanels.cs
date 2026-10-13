using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace GeMMeElec;

public sealed class PaneSettings
{
    public Dictionary<string, string> States { get; set; } = [];
    public double LeftWidth { get; set; } = 290;
    public double RightWidth { get; set; } = 300;
}

// Panels keep their controls alive while hidden, preserving filters and edits.
public sealed class WorkspacePanels : Grid
{
    private sealed class Pane(string id, string title, UIElement content, bool right)
    {
        public string Id = id, Title = title, State = "pinned";
        public bool Right = right, Expanded;
        public Border Frame = new();
        public Button Pin = new();
        public UIElement Content = content;
    }
    private readonly List<Pane> panes;
    private readonly DockPanel left = new(), right = new();
    private readonly GridSplitter leftSplitter, rightSplitter;
    private readonly PaneSettings settings;
    private bool arranged;
    private readonly Border canvasHost = new();
    private UIElement? hostedCanvas;
    private readonly System.Windows.Input.MouseButtonEventHandler collapseHandler;
    internal static string SettingsPath => Path.Combine(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION") == "1" ? "GeMMeElec.test-layout.json" : "GeMMeElec.layout.json");
    public WorkspacePanels(UIElement canvas, UIElement library, UIElement pages, UIElement properties)
    {
        settings = ReadSettings();
        panes = [new("library", "Bibliothèque", library, false), new("pages", "Folios", pages, false), new("properties", "Propriétés", properties, true)];
        foreach (var pane in panes)
        {
            if (settings.States.TryGetValue(pane.Id, out var state) && state is "pinned" or "auto" or "closed") pane.State = state;
            var dock = UiTheme.Bind(new DockPanel(), Panel.BackgroundProperty, "Ui.Surface"); var header = UiTheme.Bind(new DockPanel { Height = 34 }, Panel.BackgroundProperty, "Ui.Raised");
            var close = new Button { Content = "×", Width = 28, Padding = new(0), Margin = new(1), ToolTip = "Fermer " + pane.Title };
            System.Windows.Automation.AutomationProperties.SetName(close, "Fermer " + pane.Title);
            close.Click += (_, _) => SetState(pane.Id, "closed"); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var pin = new Button { Content = "Ép.", Width = 36, Padding = new(0), Margin = new(1), ToolTip = "Épingler / masquer automatiquement " + pane.Title };
            pane.Pin = pin;
            System.Windows.Automation.AutomationProperties.SetName(pin, "Épingler " + pane.Title);
            pin.Click += (_, _) => SetState(pane.Id, pane.State == "pinned" ? "auto" : "pinned"); DockPanel.SetDock(pin, Dock.Right); header.Children.Add(pin);
            header.Children.Add(new TextBlock { Text = pane.Title, FontWeight = FontWeights.SemiBold, Margin = new(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            DockPanel.SetDock(header, Dock.Top); dock.Children.Add(header); dock.Children.Add(pane.Content); pane.Frame = UiTheme.Bind(new Border { Child = dock, BorderThickness = new(0, 0, 0, 1) }, Border.BorderBrushProperty, "Ui.Border");
        }
        ColumnDefinitions.Add(new() { Width = new(settings.LeftWidth) }); ColumnDefinitions.Add(new() { Width = new(5) }); ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star), MinWidth = 250 }); ColumnDefinitions.Add(new() { Width = new(5) }); ColumnDefinitions.Add(new() { Width = new(settings.RightWidth) });
        SetColumn(left, 0); Children.Add(left); SetColumn(canvasHost, 2); canvasHost.Child=canvas; Children.Add(canvasHost); SetColumn(right, 4); Children.Add(right);
        leftSplitter = Splitter(1); rightSplitter = Splitter(3); Refresh();
        // Collapse transient panes after the canvas has handled the event, so its
        // coordinate system does not change in the middle of a placement/drag.
        collapseHandler = (_, _) => Dispatcher.BeginInvoke(() => { if (panes.Any(p => p.State == "auto" && p.Expanded)) { RememberWidths(); foreach (var p in panes) p.Expanded = false; Refresh(); } }, System.Windows.Threading.DispatcherPriority.Background);
        SetCanvas(canvas);
    }
    public void SetCanvas(UIElement canvas)
    {
        if (hostedCanvas is not null) hostedCanvas.PreviewMouseUp -= collapseHandler;
        canvasHost.Child = canvas; hostedCanvas = canvas; canvas.PreviewMouseUp += collapseHandler;
    }
    private GridSplitter Splitter(int column)
    {
        var splitter = UiTheme.Bind(new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeBehavior = GridResizeBehavior.PreviousAndNext }, Control.BackgroundProperty, "Ui.Border");
        SetColumn(splitter, column); Children.Add(splitter); splitter.DragCompleted += (_, _) => { RememberWidths(); Save(); }; return splitter;
    }
    private bool Open(Pane p) => p.State == "pinned" || (p.State == "auto" && p.Expanded);
    public void Show(string id) => SetState(id, "pinned");
    public void Reset() { foreach (var p in panes) { p.State = "pinned"; p.Expanded = false; } settings.LeftWidth = 290; settings.RightWidth = 300; arranged = false; Refresh(); Save(); }
    public void SetState(string id, string state)
    {
        RememberWidths(); var p = panes.Single(p => p.Id == id); p.State = state; p.Expanded = false; Refresh(); Save();
    }
    private void RememberWidths()
    {
        if (!arranged) return;
        if (panes.Any(p => !p.Right && Open(p))) settings.LeftWidth = Math.Clamp(ColumnDefinitions[0].ActualWidth, 220, 600);
        if (panes.Any(p => p.Right && Open(p))) settings.RightWidth = Math.Clamp(ColumnDefinitions[4].ActualWidth, 240, 600);
    }
    private void Refresh()
    {
        foreach (var p in panes) { p.Pin.Content = p.State == "pinned" ? "●" : "○"; p.Pin.ToolTip = (p.State == "pinned" ? "Désépingler " : "Épingler ") + p.Title; System.Windows.Automation.AutomationProperties.SetName(p.Pin, (p.State == "pinned" ? "Désépingler " : "Épingler ") + p.Title); }
        ArrangeSide(left, false, 0, leftSplitter); ArrangeSide(right, true, 4, rightSplitter); arranged = true;
    }
    private void ArrangeSide(DockPanel shell, bool isRight, int column, GridSplitter splitter)
    {
        // Detach frames before rebuilding the docking containers.
        foreach (var pane in panes.Where(p => p.Right == isRight)) if (pane.Frame.Parent is Panel parent) parent.Children.Remove(pane.Frame);
        shell.Children.Clear(); var group = panes.Where(p => p.Right == isRight).ToList(); bool hasOpen = group.Any(Open); bool hasTabs = group.Any(p => p.State == "auto");
        var rail = UiTheme.Bind(new StackPanel { Width = 30 }, Panel.BackgroundProperty, "Ui.Raised");
        foreach (var p in group.Where(p => p.State == "auto"))
        {
            var tab = new Button { Content = new TextBlock { Text = p.Title, LayoutTransform = new RotateTransform(-90) }, Margin = new(2, 6, 2, 2), Padding = new(4, 10, 4, 10), ToolTip = "Ouvrir " + p.Title };
            System.Windows.Automation.AutomationProperties.SetName(tab, "Onglet " + p.Title);
            tab.Click += (_, _) => { RememberWidths(); p.Expanded = !p.Expanded; Refresh(); }; rail.Children.Add(tab);
        }
        if (hasTabs) { DockPanel.SetDock(rail, isRight ? Dock.Right : Dock.Left); shell.Children.Add(rail); }
        var body = new Grid(); var open = group.Where(Open).ToList();
        for (int i = 0; i < open.Count; i++) { body.RowDefinitions.Add(new() { Height = open.Count > 1 && open[i].Id == "pages" ? new GridLength(180) : new GridLength(1, GridUnitType.Star) }); SetRow(open[i].Frame, i); body.Children.Add(open[i].Frame); }
        shell.Children.Add(body);
        ColumnDefinitions[column].MinWidth = hasOpen ? isRight ? 240 : 220 : 0;
        ColumnDefinitions[column].MaxWidth = hasOpen ? 600 : hasTabs ? 30 : 0;
        ColumnDefinitions[column].Width = new(hasOpen ? isRight ? settings.RightWidth : settings.LeftWidth : hasTabs ? 30 : 0);
        splitter.Visibility = hasOpen ? Visibility.Visible : Visibility.Collapsed; ColumnDefinitions[isRight ? 3 : 1].Width = new(hasOpen ? 5 : 0);
    }
    private static PaneSettings ReadSettings()
    {
        try { var s = JsonSerializer.Deserialize<PaneSettings>(File.ReadAllText(SettingsPath), ProjectStore.Json) ?? new(); s.States ??= []; s.LeftWidth = double.IsFinite(s.LeftWidth) ? Math.Clamp(s.LeftWidth, 220, 600) : 290; s.RightWidth = double.IsFinite(s.RightWidth) ? Math.Clamp(s.RightWidth, 240, 600) : 300; return s; } catch { return new(); }
    }
    private void Save()
    {
        settings.States = panes.ToDictionary(p => p.Id, p => p.State);
        try { ProjectStore.AtomicWrite(SettingsPath, JsonSerializer.Serialize(settings, ProjectStore.Json)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
