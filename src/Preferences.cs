using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GeMMeElec;

public sealed record PaletteColor(string Name, string Color);
public sealed class EditorPreferences
{
    public bool GridVisible { get; set; } = true;
    public double GridIntensity { get; set; } = 25;
    public string WireColor { get; set; } = "#243B53";
    public List<PaletteColor> Colors { get; set; } = [new("Noir", "#202020"), new("Bleu", "#2474D2"), new("Rouge", "#D32F2F"), new("Vert", "#2E8B57"), new("Orange", "#E87C20"), new("Violet", "#7B42AD"), new("Gris", "#737D87"), new("Brun", "#805333")];
    public static string PathName => Path.Combine(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("GEMMEELEC_TEST_SESSION") == "1" ? "GeMMeElec.test-preferences.json" : "GeMMeElec.preferences.json");
    public static bool IsColor(string? value) => value is not null && Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$");
    public void Validate()
    {
        if (!double.IsFinite(GridIntensity) || GridIntensity is < 0 or > 100 || !IsColor(WireColor) || Colors is null || Colors.Count > 64 || Colors.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 60 || !IsColor(c.Color))) throw new ArgumentException("Réglages de grille ou de palette invalides.");
    }
    public static EditorPreferences Load(string? path = null)
    {
        try { var p = JsonSerializer.Deserialize<EditorPreferences>(File.ReadAllText(path ?? PathName), ProjectStore.Json)!; p.Validate(); return p; }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or NullReferenceException) { return new(); }
    }
    public void Save(string? path = null) { Validate(); ProjectStore.AtomicWrite(path ?? PathName, JsonSerializer.Serialize(this, ProjectStore.Json)); }
}

public sealed partial class MainWindow
{
    private readonly EditorPreferences preferences = EditorPreferences.Load();
    private void GridDialog()
    {
        var dialog = DocumentWindow("Grille du plan", 500, 340); var panel = new StackPanel { Margin = new(22) }; dialog.Content = panel;
        var visible = new CheckBox { Content = "Afficher la grille pendant la conception", IsChecked = preferences.GridVisible, Margin = new(0, 8, 0, 16) }; panel.Children.Add(visible);
        var label = Hint(""); panel.Children.Add(label);
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = preferences.GridIntensity, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new(0, 10, 0, 10) }; panel.Children.Add(slider);
        panel.Children.Add(Hint("0 % : invisible · 100 % : contraste maximal.\nLe placement reste aligné sur la grille. La grille n’apparaît jamais dans les exports PDF et SVG, ni sur les folios de nomenclature."));
        void Preview() { label.Text = $"Visibilité : {slider.Value:0} %"; Canvas.GridVisible = visible.IsChecked == true; Canvas.GridIntensity = slider.Value; Canvas.InvalidateVisual(); }
        slider.ValueChanged += (_, _) => Preview(); visible.Checked += (_, _) => Preview(); visible.Unchecked += (_, _) => Preview();
        var buttons = new WrapPanel(); panel.Children.Add(buttons);
        buttons.Children.Add(DocButton("Enregistrer", dialog, () => { var copy = DocumentModel.Copy(preferences); copy.GridVisible = visible.IsChecked == true; copy.GridIntensity = slider.Value; copy.Save(); preferences.GridVisible = copy.GridVisible; preferences.GridIntensity = copy.GridIntensity; dialog.Close(); }));
        buttons.Children.Add(DocButton("Annuler", dialog, dialog.Close));
        dialog.Closed += (_, _) => { Canvas.GridVisible = preferences.GridVisible; Canvas.GridIntensity = preferences.GridIntensity; Canvas.InvalidateVisual(); };
        Preview(); ShowDocumentDialog(dialog);
    }
    private void PaletteDialog(string? initial = null, Action<string>? choose = null)
    {
        var draft = DocumentModel.Copy(preferences); var dialog = DocumentWindow("Palette des conducteurs", 660, 650);
        var root = new DockPanel { Margin = new(20) }; dialog.Content = root;
        var buttons = new WrapPanel(); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var panel = new StackPanel(); root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        panel.Children.Add(Hint("Choisir une couleur ou composer une teinte. Les couleurs enregistrées restent disponibles au prochain démarrage."));
        var swatches = new WrapPanel(); panel.Children.Add(swatches);
        var hex = DocField(panel, "Couleur #RRGGBB", initial ?? draft.WireColor);
        var preview = new Border { Height = 36, Margin = new(0, 8, 0, 8), BorderBrush = Brushes.Gray, BorderThickness = new(1) }; panel.Children.Add(preview);
        var sliders = new List<Slider>(); bool updating = false;
        foreach (var label in new[] { "Rouge", "Vert", "Bleu" })
        {
            panel.Children.Add(new TextBlock { Text = label }); var slider = new Slider { Minimum = 0, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new(0, 4, 0, 8) }; sliders.Add(slider); panel.Children.Add(slider);
            slider.ValueChanged += (_, _) => { if (!updating && sliders.Count == 3) hex.Text = $"#{(int)sliders[0].Value:X2}{(int)sliders[1].Value:X2}{(int)sliders[2].Value:X2}"; };
        }
        void Preview()
        {
            if (!EditorPreferences.IsColor(hex.Text)) return; updating = true;
            var c = (Color)ColorConverter.ConvertFromString(hex.Text); sliders[0].Value = c.R; sliders[1].Value = c.G; sliders[2].Value = c.B; preview.Background = Drawing.Brush(hex.Text); updating = false;
        }
        hex.TextChanged += (_, _) => Preview();
        var name = DocField(panel, "Nom de la couleur à enregistrer", "Ma couleur");
        var saved = new ComboBox { DisplayMemberPath = "Name", Margin = new(0, 12, 0, 6) }; panel.Children.Add(saved);
        saved.SelectionChanged += (_, _) => { if (saved.SelectedItem is PaletteColor c) { hex.Text = c.Color; name.Text = c.Name; } };
        void Refresh()
        {
            swatches.Children.Clear(); saved.ItemsSource = null; saved.ItemsSource = draft.Colors;
            foreach (var c in draft.Colors)
            {
                var b = new Button { Content = new Border { Width = 28, Height = 24, Background = Drawing.Brush(c.Color) }, Padding = new(3), Margin = new(3), ToolTip = c.Name + " · " + c.Color };
                b.Click += (_, _) => { hex.Text = c.Color; saved.SelectedItem = c; }; swatches.Children.Add(b);
            }
        }
        var actions = new WrapPanel(); panel.Children.Add(actions);
        actions.Children.Add(DocButton("Enregistrer cette couleur", dialog, () =>
        {
            if (!EditorPreferences.IsColor(hex.Text) || string.IsNullOrWhiteSpace(name.Text) || name.Text.Length > 60) throw new ArgumentException("Saisir un nom (60 caractères maximum) et une couleur #RRGGBB.");
            var c = new PaletteColor(name.Text.Trim(), hex.Text.ToUpperInvariant()); int index = draft.Colors.FindIndex(x => x.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) { if (draft.Colors.Count >= 64) throw new ArgumentException("La palette contient déjà 64 couleurs."); draft.Colors.Add(c); } else draft.Colors[index] = c;
            draft.Save(); preferences.Colors = draft.Colors.ToList(); Refresh(); saved.SelectedItem = c;
        }));
        actions.Children.Add(DocButton("Retirer la couleur sélectionnée", dialog, () => { if (saved.SelectedItem is PaletteColor c) { draft.Colors.Remove(c); draft.Save(); preferences.Colors = draft.Colors.ToList(); Refresh(); } }));
        buttons.Children.Add(DocButton(choose is null ? "Utiliser pour les prochains fils" : "Choisir cette couleur", dialog, () =>
        {
            if (!EditorPreferences.IsColor(hex.Text)) throw new ArgumentException("Couleur attendue : #RRGGBB.");
            if (choose is null) { draft.WireColor = hex.Text.ToUpperInvariant(); draft.Save(); preferences.WireColor = draft.WireColor; Editor.CurrentWireColor = draft.WireColor; } else choose(hex.Text.ToUpperInvariant()); dialog.Close();
        }));
        buttons.Children.Add(DocButton("Fermer", dialog, dialog.Close)); Refresh(); Preview(); ShowDocumentDialog(dialog);
    }
}
