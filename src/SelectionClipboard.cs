using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace GeMMeElec;

public static class SelectionClipboard
{
    public const string Format = "GeMMeElec.Selection.v1";
    public static string Copy(Project project, Sheet sheet, IEnumerable<string> selection)
    {
        var ids = selection.ToHashSet();
        // A conductor cannot exist without its endpoints in an independent copy.
        foreach (var wire in sheet.Wires.Where(w => ids.Contains(w.Id))) { ids.Add(wire.From.ComponentId); ids.Add(wire.To.ComponentId); }
        var block = CircuitBlocks.Extract(project, sheet, ids, "Sélection copiée");
        string json = JsonSerializer.Serialize(block, ProjectStore.ProjectJson);
        if (Encoding.UTF8.GetByteCount(json) > Project.MaxFileBytes) throw new InvalidDataException("Sélection trop volumineuse pour le presse-papiers.");
        return json;
    }
    public static CircuitBlock Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > Project.MaxFileBytes || Encoding.UTF8.GetByteCount(json) > Project.MaxFileBytes) throw new InvalidDataException("Sélection absente ou trop volumineuse.");
        var block = JsonSerializer.Deserialize<CircuitBlock>(json, ProjectStore.Json) ?? throw new InvalidDataException("Sélection invalide.");
        CircuitBlocks.Validate(block); return block;
    }
}

public sealed partial class MainWindow
{
    // Injectable I/O keeps offscreen tests from replacing the user's clipboard.
    internal Action<string>? ClipboardWriteForTest;
    internal Func<string?>? ClipboardReadForTest;
    private bool CopyableSelection() => !Editor.InteractionInProgress && PagePlacement.Editable(Editor.Sheet) && (Editor.Sheet.Components.Any(c => Canvas.Selected.Contains(c.Id)) || Editor.Sheet.Wires.Any(w => Canvas.Selected.Contains(w.Id)) || Editor.Sheet.Notes.Any(n => Canvas.Selected.Contains(n.Id)) || Editor.Sheet.Graphics.Any(g => Canvas.Selected.Contains(g.Id)));
    private void CopySelection()
    {
        Canvas.CancelDrag(); string json = SelectionClipboard.Copy(Editor.Project, Editor.Sheet, Canvas.Selected);
        if (ClipboardWriteForTest is not null) ClipboardWriteForTest(json);
        else { var data = new DataObject(); data.SetData(SelectionClipboard.Format, json); Clipboard.SetDataObject(data, true); }
        status.Text = "Sélection copiée · Ctrl+V puis cliquer pour la placer";
    }
    private void PasteSelection()
    {
        if (!PagePlacement.Editable(Editor.Sheet)) throw new ArgumentException("Coller sur un folio de schéma ou une page de garde.");
        string? json = ClipboardReadForTest is not null ? ClipboardReadForTest() : Clipboard.GetData(SelectionClipboard.Format) as string;
        if (json is null) { status.Text = "Le presse-papiers ne contient aucun élément GeMMeElec."; return; }
        var block = SelectionClipboard.Read(json); PrepareBlock(block); Canvas.Focus();
        status.Text = "Coller : cliquer la position de la copie · Échap annule" + (block.DetachedConnections > 0 ? " · liaisons extérieures détachées" : "");
    }
    internal static bool TextClipboardTarget(object source)
    {
        for (var node = source as DependencyObject; node is not null; node = System.Windows.LogicalTreeHelper.GetParent(node))
            if (node is TextBoxBase || node is System.Windows.Controls.PasswordBox || node is System.Windows.Controls.ComboBox { IsEditable: true }) return true;
        return Keyboard.FocusedElement is TextBoxBase or System.Windows.Controls.PasswordBox;
    }
}
