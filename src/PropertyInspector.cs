using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GeMMeElec;

public sealed partial class MainWindow
{
    private Panel? propertyGroupBody;
    private Panel PropertyContents => propertyGroupBody ?? properties;
    private readonly Dictionary<Control, (TextBlock Error, object? Help)> propertyFeedback = [];
    private void PropertyGroup(string title, Action populate, bool expanded = true)
    {
        var body = new StackPanel { Margin = new(1, 3, 1, 4) };
        var group = new Expander { Header = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 12 }, Content = body, IsExpanded = expanded };
        properties.Children.Add(group); var old = propertyGroupBody; propertyGroupBody = body;
        try { populate(); } finally { propertyGroupBody = old; }
    }
    private TextBox Field(string key, string label, string value, string? help = null)
    {
        var caption = new TextBlock { Text = label, Margin = new(0, 8, 0, 4), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        UiTheme.Bind(caption, TextBlock.ForegroundProperty, "Ui.Muted"); PropertyContents.Children.Add(caption);
        var box = new TextBox { Text = value, ToolTip = help, Tag = "property:" + key }; fields[key] = box; PropertyContents.Children.Add(box);
        System.Windows.Automation.AutomationProperties.SetName(box, label);
        RegisterPropertyControl(box, help);
        return box;
    }
    private void RegisterPropertyControl(Control control, object? help = null)
    {
        var error = UiTheme.Bind(new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new(0, 3, 0, 2), Visibility = Visibility.Collapsed }, TextBlock.ForegroundProperty, "Ui.Error");
        PropertyContents.Children.Add(error); propertyFeedback[control] = (error, help ?? control.ToolTip);
    }
    private void SetPropertyFeedback(Control control, string? error)
    {
        if (error is null) { control.ClearValue(Control.BorderBrushProperty); control.ToolTip = propertyFeedback.GetValueOrDefault(control).Help; }
        else { UiTheme.Bind(control, Control.BorderBrushProperty, "Ui.Error"); control.ToolTip = error; }
        if (propertyFeedback.TryGetValue(control, out var feedback)) { feedback.Error.Text = error ?? ""; feedback.Error.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible; }
    }
    internal string PropertyError(string key) => propertyFeedback[fields[key]].Error.Text;
    private bool IsPropertyInput(DependencyObject source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node)) if (node == properties) return true;
        return false;
    }
    private void Inspect()
    {
        Editor.EndPropertyEdit(); properties.Children.Clear(); fields.Clear(); propertyFeedback.Clear(); propertyGroupBody = null; updateCoilInspector = null;
        var c = Editor.Sheet.Components.FirstOrDefault(c => Canvas.Selected.Contains(c.Id));
        var wire = Editor.Sheet.Wires.FirstOrDefault(w => Canvas.Selected.Contains(w.Id));
        var graphic=Editor.Sheet.Graphics.FirstOrDefault(g=>Canvas.Selected.Contains(g.Id));
        var note = Editor.Sheet.Notes.FirstOrDefault(n => Canvas.Selected.Contains(n.Id));
        if (Canvas.Selected.Count > 1)
        {
            properties.Children.Add(new TextBlock { Text = $"{Canvas.Selected.Count} éléments sélectionnés", FontSize = 18, TextWrapping = TextWrapping.Wrap, Margin = new(0, 14, 0, 4) });
            properties.Children.Add(Hint("Glisser pour déplacer le groupe. Les commandes ci-dessous s’appliquent à toute la sélection."));
            foreach (var id in new[] { "rotate", "duplicate", "delete" }) properties.Children.Add(CommandButton(id));
            return;
        }
        if (c is not null)
        {
            properties.Children.Add(new TextBlock { Text = c.Definition.Name, FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new(0, 14, 0, 4) });
            properties.Children.Add(Hint("Modifications immédiates · Ctrl+Z pour annuler.\nCtrl+S enregistre le fichier."));
            if (CrossReferences.IsPoint(c)) { ReferenceInspector(c); return; }
            PropertyGroup("Identification", () => {
                Field("tag", "Repère de l’appareil", c.Tag, "Exemple : KM1, Q1 ou A1. Ce texte est affiché sur le schéma.");
                var tagLock = new CheckBox { Name="TagNumberingLock", Content="Conserver ce repère lors de la numérotation automatique", IsChecked=c.TagLocked, Margin=new(0,6,0,8) }; PropertyContents.Children.Add(tagLock); RegisterPropertyControl(tagLock);
                void LockChanged() => LiveProperty(tagLock,()=>Editor.Change(()=>Editor.Project.Sheets.SelectMany(s=>s.Components).Single(x=>x.Id==c.Id).TagLocked=tagLock.IsChecked==true));tagLock.Checked+=(_,_)=>LockChanged();tagLock.Unchecked+=(_,_)=>LockChanged();
                Field("description", "Description / fonction", c.Description);
                Field("rating", "Caractéristiques affichées", c.Rating, "Texte libre avec unités : par exemple 24 V DC ou 16 A. Aucune valeur électrique n’est déduite.");
            });
            CoilInspector(c);
            PropertyGroup("Position sur le folio", () => ComponentPositionFields(c), false);
            PropertyGroup("Textes du symbole", () => TextPositionEditor(c));
            PropertyGroup("Article et nomenclature", () => {
                if (CoilContacts.IsContact(c)) PropertyContents.Children.Add(Hint("Si cette fonction est associée, l’article de nomenclature est porté par la bobine. Les fonctions associées sont exclues de la BOM par défaut."));
                Field("brand", "Fabricant", c.Brand); Field("range", "Gamme / série", c.Range);
                Field("reference", "Référence commandable", c.Reference, "Référence exacte du fabricant ; à vérifier dans sa documentation.");
                propertiesForArticle(c);
                PropertyContents.Children.Add(Button("Fiches fabricants hors ligne…",ArticleFichesDialog));
            }, false);
            PropertyGroup("Bornes de raccordement", () => {
                PropertyContents.Children.Add(Hint("Libellés affichés sur le dessin. Vérifier le bornage de la référence retenue."));
                foreach (var p in c.Definition.Ports) Field("port:" + p.Id, "Libellé de la borne " + p.Id, c.PortLabels.GetValueOrDefault(p.Id, p.Id));
            }, false);
            PropertyGroup("Définition du symbole", () => { DeclareRelayFunction(c); if (c.Symbol != "junction") PropertyContents.Children.Add(Button("Modifier le symbole en copie…", SymbolEditor)); var official = Catalog.Entries.FirstOrDefault(e => e.CanPlace && e.Symbol == c.Symbol); if (official is not null) PropertyContents.Children.Add(Button("Dessin original et provenance…", () => OfficialLibraryDetails(official))); }, false);
            BindComponentProperties(c.Id);
        }
        else if (wire is not null)
        {
            properties.Children.Add(new TextBlock { Text = "Conducteur", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new(0, 14, 0, 4) });
            properties.Children.Add(Hint("Modifications immédiates · Ctrl+S enregistre le fichier."));
            PropertyGroup("Identification et aspect", () => {
                var independent = new CheckBox { Name = "IndependentWireNumber", Content = "Numéro indépendant", IsChecked = wire.NumberIndependent, Margin = new(0, 4, 0, 8), ToolTip = "Ce conducteur garde son propre numéro. Décocher reprend le numéro commun aux fils raccordés." }; PropertyContents.Children.Add(independent); RegisterPropertyControl(independent);
                Field("number", "Numéro du conducteur", wire.Number, "Numéro indépendant coché : modifier seulement ce conducteur. Décoché : renommer la continuité commune."); Field("color", "Couleur · #RRGGBB", wire.Color, "Six chiffres hexadécimaux. Exemple : #2474D2.");
                PropertyContents.Children.Add(Button("Palette de couleurs…", () => PaletteDialog(fields["color"].Text, color => fields["color"].Text = color)));
                Field("thickness", "Épaisseur du trait · 0,25 à 10", Number(wire.Thickness));
                PropertyContents.Children.Add(Hint("Type de trait")); var style = new ComboBox { Name = "WireLineStyle", SelectedValuePath = "Tag", Margin = new(0, 3, 0, 4) };
                foreach (var (id, name) in new[] { ("solid", "Continu"), ("dashed", "Tirets"), ("dotted", "Pointillés"), ("dashdot", "Mixte · tiret / point") }) style.Items.Add(new ComboBoxItem { Content = name, Tag = id });
                style.SelectedValue = wire.LineStyle; PropertyContents.Children.Add(style); RegisterPropertyControl(style);
                void Independence() => LiveProperty(independent, () => { Editor.SetWireNumberIndependent(wire.Id, independent.IsChecked == true); updatingPropertyControls = true; try { fields["number"].Text = Editor.Sheet.Wires.Single(w => w.Id == wire.Id).Number; } finally { updatingPropertyControls = false; } });
                independent.Checked += (_, _) => Independence(); independent.Unchecked += (_, _) => Independence();
                style.SelectionChanged += (_, _) => LiveProperty(style, () => Editor.Change(() => Editor.Sheet.Wires.Single(w => w.Id == wire.Id).LineStyle = (string)style.SelectedValue));
            });
            PropertyGroup("Tracé et raccordements", () => {
                var a = Editor.Sheet.Components.Single(c => c.Id == wire.From.ComponentId); var b = Editor.Sheet.Components.Single(c => c.Id == wire.To.ComponentId);
                PropertyContents.Children.Add(Hint($"De : {a.Tag} · borne {a.PortLabels.GetValueOrDefault(wire.From.PortId, wire.From.PortId)}\nVers : {b.Tag} · borne {b.PortLabels.GetValueOrDefault(wire.To.PortId, wire.To.PortId)}"));
                var points = Field("waypoints", "Points de passage · X ; Y (unités de dessin)", string.Join(Environment.NewLine, wire.Waypoints.Select(p => $"{Number(p.X)} ; {Number(p.Y)}")), "Un point par ligne, par exemple 300 ; 450. Vide : tracé automatique."); points.AcceptsReturn = true; points.Height = 100; points.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                LiveText(points, value => { var parsed = ParsePoints(value); Editor.Change(() => {var routed=Editor.Sheet.Wires.Single(w => w.Id == wire.Id);routed.Waypoints=parsed;routed.RoutingMode="manual";routed.RoutingWarning="";}, wire.Id + ":waypoints"); });
                PropertyContents.Children.Add(Button("Rétablir le parcours automatique", () => Editor.Change(() => {var routed=Editor.Sheet.Wires.Single(w => w.Id == wire.Id);routed.Waypoints.Clear();routed.RoutingMode="manual";routed.RoutingWarning="";})));
                PropertyContents.Children.Add(Hint("Poignée carrée : déplacer un segment. Poignée ronde : changer de borne. Les simples croisements restent indépendants."));
            });
            LiveText(fields["number"], value => Editor.SetWireNumber(wire.Id, value, wire.Id + ":number"));
            LiveText(fields["thickness"], value => Editor.Change(() => Editor.Sheet.Wires.Single(w => w.Id == wire.Id).Thickness = ReadNumber(value), wire.Id + ":thickness"));
            LiveText(fields["color"], value => { if (!EditorPreferences.IsColor(value)) throw new ArgumentException("Saisir # suivi de six chiffres hexadécimaux (exemple : #2474D2)."); Editor.Change(() => Editor.Sheet.Wires.Single(w => w.Id == wire.Id).Color = value, wire.Id + ":color"); });
            WireLabelInspector(wire);
            PropertyGroup("Câble section et routage",()=>{Field("sectionMm2","Section du conducteur en mm² · vide = section du câble",wire.SectionMm2 is double area?Number(area):"");LiveText(fields["sectionMm2"],value=>Editor.Change(()=>Editor.Sheet.Wires.Single(w=>w.Id==wire.Id).SectionMm2=value.Trim().Length==0?null:ReadNumber(value),wire.Id+":section"));PropertyContents.Children.Add(Hint(wire.CableId.Length==0?"Aucun câble affecté.":"Câble : "+Editor.Project.Cables.Single(c=>c.Id==wire.CableId).Name+" · conducteur "+wire.CableCore));PropertyContents.Children.Add(Button("Câbles et sections…",CablesDialog));PropertyContents.Children.Add(Button("Routage intelligent",()=>Editor.RouteWires([wire.Id])));PropertyContents.Children.Add(Button("Insérer un symbole sur ce fil…",InsertSymbolOnWireDialog));if(wire.RoutingWarning.Length>0)PropertyContents.Children.Add(Hint(wire.RoutingWarning));});
        }
        else if(graphic is not null)GraphicProperties(graphic);
        else if (note is not null)
        {
            properties.Children.Add(new TextBlock { Text = "Texte du folio", FontSize = 18, Margin = new(0, 14, 0, 4) });
            PropertyGroup("Contenu", () => { var input = Field("text", "Texte affiché", note.Text); input.AcceptsReturn = true; input.MinHeight = 75; LiveText(input, value => Editor.Change(() => Editor.Sheet.Notes.Single(n => n.Id == note.Id).Text = value, note.Id + ":text")); });
            NoteStyleProperties(note);
            PropertyGroup("Position", () => { foreach (var axis in new[] { "X", "Y" }) { var input = Field("note" + axis, axis + " (unités de dessin)", Number(axis == "X" ? note.X : note.Y)); LiveText(input, value => { double n = ReadCoordinate(value, axis); Editor.Change(() => { var live = Editor.Sheet.Notes.Single(x => x.Id == note.Id); if (axis == "X") live.X = n; else live.Y = n; }, note.Id + ":" + axis); }); } });
        }
        else if (Editor.Sheet.CabinetPlanId.Length>0)
        {
            properties.Children.Add(new TextBlock { Text="Implantation d’armoire",FontSize=20,Margin=new(0,18,0,8) });properties.Children.Add(Hint("Plan lié aux appareils du projet. Dimensions en millimètres."));properties.Children.Add(Button("Implantation…",CabinetDialog));
        }
        else if (Editor.Sheet.TerminalPlanId.Length>0)
        {
            properties.Children.Add(new TextBlock { Text="Plan de bornier",FontSize=20,Margin=new(0,18,0,8) });properties.Children.Add(Hint("Plan lié au bornier du projet. Modifier les affectations puis régénérer ses folios."));properties.Children.Add(Button("Borniers et plans…",TerminalStripsDialog));
        }
        else if (Editor.Sheet.BomPage is not null)
        {
            properties.Children.Add(new TextBlock { Text = "Folio de nomenclature", FontSize = 20, TextWrapping = TextWrapping.Wrap, Margin = new(0, 18, 0, 12) });
            properties.Children.Add(Hint("Ce tableau est enregistré et inclus dans le PDF, sans cartouche. Après modification du schéma, actualiser les folios de nomenclature."));
            properties.Children.Add(Button("Configurer / actualiser la nomenclature", Bom)); properties.Children.Add(Button("Propriétés du folio", SheetInfo));
        }
        else
        {
            properties.Children.Add(new TextBlock { Text = Editor.Sheet.Title, FontSize = 20, TextWrapping = TextWrapping.Wrap, Margin = new(0, 18, 0, 8) });
            properties.Children.Add(Hint("Sélectionner un élément pour modifier ses propriétés.\n\nV : sélection · W : conducteur · T : texte\n\nDepuis la bibliothèque, choisir un symbole puis cliquer sur le folio. Tirer d’une borne à une autre pour les raccorder."));
            foreach (var id in new[] { "projectInfo", "sheetInfo", "titleblocks", "bom" }) properties.Children.Add(CommandButton(id));
        }
    }
    private void propertiesForArticle(Component c)
    {
        if (Uri.TryCreate(c.Source, UriKind.Absolute, out var uri) && uri.Scheme == "https") PropertyContents.Children.Add(Button("Documentation fabricant ↗", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })));
        PropertyContents.Children.Add(Button(c.ImageId.Length > 0 ? "Remplacer l’image de nomenclature…" : "Ajouter une image de nomenclature…", () => ComponentImage(c.Id)));
        if (c.ImageId.Length > 0) { PropertyContents.Children.Add(new Image { Source = ImageAssets.Decode(Editor.Project.Images[c.ImageId]), Height = 90, Margin = new(8), Stretch = Stretch.Uniform }); PropertyContents.Children.Add(Button("Retirer l’image", () => Editor.Change(() => Editor.Sheet.Components.Single(x => x.Id == c.Id).ImageId = ""))); }
    }
    private void ComponentPositionFields(Component c)
    {
        PropertyContents.Children.Add(Hint("Emplacement : " + SheetZones.Address(Editor.Project, Editor.Sheet, c)));
        Field("X", "Position X (unités de dessin)", Number(c.X), $"De 0 à {Number(Editor.Sheet.Width)}. Virgule ou point accepté.");
        Field("Y", "Position Y (unités de dessin)", Number(c.Y), $"De 0 à {Number(Editor.Sheet.Height)}. Virgule ou point accepté.");
        Field("rotation", "Rotation (°)", c.Rotation.ToString(), "Multiples de 90° : 0, 90, 180, 270. Les conducteurs suivent les bornes.");
    }
    private double ReadCoordinate(string value, string axis)
    {
        double n = ReadNumber(value), max = axis == "X" ? Editor.Sheet.Width : Editor.Sheet.Height;
        if (n < 0 || n > max) throw new ArgumentException($"{axis} doit être compris entre 0 et {Number(max)} unités de dessin."); return n;
    }
}
