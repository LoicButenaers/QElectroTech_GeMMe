using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    private Action? updateCoilInspector;
    private sealed record CoilChoice(string Id, string Display);
    private void DeclareRelayFunction(Component c)
    {
        if (!Editor.Project.CustomSymbols.ContainsKey(c.Symbol)) return;
        PropertyContents.Children.Add(Hint("Fonction déclarée pour ce symbole personnel, sans déduction de bornage ou de caractéristiques. Changer le rôle d’une bobine détache ses contacts ; Ctrl+Z annule."));
        var roles = new[] { new CoilChoice("", "Autre symbole / non classé"), new CoilChoice("coil", "Bobine de relais / contacteur"), new CoilChoice("no", "Contact NO"), new CoilChoice("nc", "Contact NF"), new CoilChoice("changeover", "Contact inverseur"), new CoilChoice("power", "Contacts de puissance") };
        var choice = new ComboBox { Name = "RelayFunctionDeclaration", ItemsSource = roles, DisplayMemberPath = "Display", SelectedValuePath = "Id", SelectedValue = c.RelayFunction, Margin = new(0, 6, 0, 6) }; PropertyContents.Children.Add(choice);
        choice.SelectionChanged += (_, _) => Run(() => Editor.Change(() => Editor.SetRelayFunction(Editor.Sheet.Components.Single(x => x.Id == c.Id), (string?)choice.SelectedValue ?? "")));
    }
    private void CoilInspector(Component c)
    {
        if (!CoilContacts.IsCoil(c) && !CoilContacts.IsContact(c)) return;
        PropertyGroup("Bobine et contacts", () => {
            var destinations = new StackPanel();
            if (CoilContacts.IsContact(c))
            {
                PropertyContents.Children.Add(Hint("Choisir la bobine de cet appareil. Le repère sera partagé ; les bornes et conducteurs restent indépendants."));
                var refs = new CoilContacts(Editor.Project); var choices = new[] { new CoilChoice("", "Aucune bobine · fonction indépendante") }.Concat(refs.Coils.Select(x => new CoilChoice(x.Component.Id, x.Component.Tag + " · " + SheetZones.Address(Editor.Project, x.Sheet, x.Component)))).ToList();
                var choice = new ComboBox { Name = "CoilAssociation", ItemsSource = choices, DisplayMemberPath = "Display", SelectedValuePath = "Id", SelectedValue = c.CoilId, Margin = new(0, 5, 0, 5) }; PropertyContents.Children.Add(choice); RegisterPropertyControl(choice);
                choice.SelectionChanged += (_, _) => LiveProperty(choice, () => {
                    Editor.AssociateContact(c.Id, (string?)choice.SelectedValue ?? "");
                    updatingPropertyControls = true;
                    try { fields["tag"].Text = Editor.Sheet.Components.Single(x => x.Id == c.Id).Tag; } finally { updatingPropertyControls = false; }
                });
            }
            PropertyContents.Children.Add(destinations);
            void Update()
            {
                destinations.Children.Clear(); var current = Editor.Sheet.Components.Single(x => x.Id == c.Id); var refs = new CoilContacts(Editor.Project);
                if (CoilContacts.IsCoil(current))
                {
                    var linked = refs.Contacts(c.Id); if (linked.Count == 0) destinations.Children.Add(Hint("Aucun contact associé. Placer un contact NO/NF, puis choisir cette bobine dans ses propriétés."));
                    foreach (var x in linked) destinations.Children.Add(Button(CoilContacts.FunctionName(x.Component) + " · " + CoilContacts.Terminals(x.Component) + " · " + SheetZones.Address(Editor.Project, x.Sheet, x.Component), () => GoToReference(x)));
                }
                else if (refs.Coil(current) is ReferencePoint coil) { destinations.Children.Add(Hint("Repère commun : " + coil.Component.Tag)); destinations.Children.Add(Button("Aller à la bobine · " + SheetZones.Address(Editor.Project, coil.Sheet, coil.Component), () => GoToReference(coil))); }
                else destinations.Children.Add(Hint("Fonction indépendante : aucun renvoi à une bobine."));
            }
            updateCoilInspector = Update; Update(); PropertyContents.Children.Add(Button("Navigateur bobines / contacts…", CoilNavigator));
        });
    }
    private sealed record CoilRow(ReferencePoint Point, string Tag, string Function, string Terminals, string Address, string Coil, string Destinations);
    private void CoilNavigator()
    {
        Canvas.CancelDrag(); var dialog = DocumentWindow("Bobines et contacts · références croisées", 1080, 650); var root = new DockPanel { Margin = new(18) }; dialog.Content = root;
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top); top.Children.Add(Hint("Double-cliquer une fonction pour la localiser. Les associations ne créent pas de continuité électrique entre les circuits."));
        var filter = new TextBox { ToolTip = "Filtrer par repère, fonction ou emplacement", Margin = new(0, 5, 0, 12) }; top.Children.Add(filter);
        var actions = new WrapPanel(); DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        var grid = new DataGrid { Name = "CoilFunctions", AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, EnableRowVirtualization = true, SelectionMode = DataGridSelectionMode.Single }; root.Children.Add(grid);
        foreach (var (label, field, width) in new[] { ("Repère", "Tag", 110), ("Fonction", "Function", 100), ("Bornes", "Terminals", 140), ("Emplacement", "Address", 115), ("Bobine associée", "Coil", 160), ("Références", "Destinations", 330) }) grid.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(field), Width = width });
        var refs = new CoilContacts(Editor.Project); var rows = Editor.Project.Sheets.SelectMany(s => s.Components.Where(c => CoilContacts.IsCoil(c) || CoilContacts.IsContact(c)).Select(c => new CoilRow(new(s, c), c.Tag, CoilContacts.FunctionName(c), CoilContacts.Terminals(c), SheetZones.Address(Editor.Project, s, c), refs.Coil(c)?.Component.Tag ?? (CoilContacts.IsCoil(c) ? "—" : "Non associée"), refs.Label(c)))).ToList();
        void Filter() => grid.ItemsSource = rows.Where(r => Symbols.SearchKey(r.Tag + " " + r.Function + " " + r.Address).Contains(Symbols.SearchKey(filter.Text))).ToList(); filter.TextChanged += (_, _) => Filter(); Filter();
        void Go() { if (grid.SelectedItem is CoilRow row) { dialog.Close(); GoToReference(row.Point); } }
        actions.Children.Add(DocButton("Aller à la fonction", dialog, Go)); actions.Children.Add(DocButton("Fermer", dialog, dialog.Close)); grid.MouseDoubleClick += (_, _) => Go(); ShowDocumentDialog(dialog);
    }
}
