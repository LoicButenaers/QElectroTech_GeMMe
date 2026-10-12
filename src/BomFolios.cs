using System.IO;

namespace GeMMeElec;

public sealed class BomPage
{
    public List<BomColumn> Columns { get; set; } = [];
    public List<BomFragment> Rows { get; set; } = [];
}
public sealed class BomFragment
{
    public List<List<string>> Cells { get; set; } = [];
    public string ImageId { get; set; } = "";
    public string Symbol { get; set; } = "breaker1";
    public double Height { get; set; }
    public bool Continuation { get; set; }
}

// A generated snapshot belongs to the project. Refresh replaces the entire set
// in one undoable operation; ordinary pointer movement never rebuilds the BOM.
public static class BomFolios
{
    private const double Font = 11, Line = 16, Bottom = 930, TableWidth = 1320;
    private static double[] Widths(List<BomColumn> cols)
    {
        var weights = cols.Select(c => c.Key switch { "quantity" => .6, "image" => .9, "pages" => 1.4, "description" => 2, "source" => 1.8, _ => 1.2 }).ToArray();
        double sum = weights.Sum(); return weights.Select(w => TableWidth * w / sum).ToArray();
    }
    internal static List<string> Wrap(string text, double width)
    {
        List<string> lines = [];
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var remaining = paragraph;
            while (remaining.Length > 0)
            {
                int low = 1, high = remaining.Length;
                while (low < high) { int mid = (low + high + 1) / 2; if (Drawing.Measure(remaining[..mid], Font).Width <= width) low = mid; else high = mid - 1; }
                int count = low;
                if (count < remaining.Length) { int space = remaining.LastIndexOf(' ', count - 1, count); if (space > count / 2) count = space + 1; }
                lines.Add(remaining[..count]); remaining = remaining[count..];
            }
            if (paragraph.Length == 0) lines.Add("");
        }
        return lines;
    }
    private static double HeaderHeight(List<BomColumn> cols, double[] widths) => Math.Max(36, cols.Select((c, i) => Wrap(c.Label, widths[i] - 12).Count).Max() * Line + 12);
    public static List<Sheet> Create(Project project, BomOptions options)
    {
        options.Validate(); var cols = DocumentModel.Copy(options.Columns.Where(c => c.Enabled).ToList()); var widths = Widths(cols);
        double top = 100 + HeaderHeight(cols, widths); double y = top; bool images = cols.Any(c => c.Key == "image");
        List<Sheet> pages = [];
        Sheet NewPage()
        {
            if (pages.Count + project.Sheets.Count >= 100) throw new InvalidDataException("La nomenclature dépasserait la limite de 100 folios ; réduire les colonnes ou filtrer les articles.");
            var s = new Sheet { Title = $"Nomenclature · {pages.Count + 1}", TitleBlockId = "", BomPage = new() { Columns = DocumentModel.Copy(cols) } };
            pages.Add(s); y = top; return s;
        }
        var page = NewPage();
        foreach (var row in BomReport.Rows(project, options))
        {
            var cells = cols.Select((c, i) => c.Key == "image" ? new List<string>() : Wrap(row.Values[c.Key], widths[i] - 12)).ToList();
            int total = Math.Max(1, cells.Max(c => c.Count)), offset = 0;
            double minHeight = images ? 76 : 32;
            // Keep normal rows together; very long entries continue on following pages.
            double height = Math.Max(minHeight, total * Line + 12);
            if (y > top && height > Bottom - y) page = NewPage();
            while (offset < total)
            {
                int count = Math.Min(total - offset, (int)((Bottom - y - 12) / Line));
                if (count < 1 || Bottom - y < minHeight) { page = NewPage(); continue; }
                height = Math.Max(minHeight, count * Line + 12);
                page.BomPage!.Rows.Add(new() { Cells = cells.Select(c => c.Skip(offset).Take(count).ToList()).ToList(), ImageId = row.ImageId, Symbol = row.Symbol, Height = height, Continuation = offset > 0 });
                offset += count; y += height;
                if (offset < total) page = NewPage();
            }
        }
        return pages;
    }
    public static void Validate(Project p, Sheet s)
    {
        if (s.BomPage is not { } b) return;
        if (s.TitleBlockId != "" || s.Components.Count + s.Wires.Count + s.Notes.Count > 0 || s.Width != 1400 || s.Height != 990) throw new InvalidDataException("Un folio de nomenclature est généré, sans cartouche ni éléments de schéma.");
        new BomOptions { Columns = b.Columns }.Validate();
        if (b.Columns.Any(c => !c.Enabled) || b.Rows is null || b.Rows.Count > 100) throw new InvalidDataException("Folio BOM invalide.");
        double total = 100 + HeaderHeight(b.Columns, Widths(b.Columns));
        foreach (var row in b.Rows)
        {
            if (row is null || row.Cells is null || row.Cells.Count != b.Columns.Count || row.Cells.Any(c => c is null || c.Count > 60 || c.Any(t => t is null || t.Length > 2000)) || !double.IsFinite(row.Height) || row.Height < 32 || row.Height < row.Cells.Max(c => c.Count) * Line + 12 || row.Symbol is null || !Symbols.All.ContainsKey(row.Symbol) || row.ImageId is null || (row.ImageId.Length > 0 && !p.Images.ContainsKey(row.ImageId))) throw new InvalidDataException("Ligne de nomenclature invalide.");
            total += row.Height;
        }
        if (total > Bottom + .01) throw new InvalidDataException("La nomenclature dépasse la page.");
    }
    public static List<Ink> Scene(Project p, Sheet s)
    {
        var b = s.BomPage!; var widths = Widths(b.Columns); double header = HeaderHeight(b.Columns, widths), y = 100;
        var pages = p.Sheets.Where(x => x.BomPage is not null).ToList();
        List<Ink> ink = [new("text", 40, 30, Text: "Nomenclature", Size: 25), new("text", 40, 66, Text: Wrap(p.Title, TableWidth)[0], Size: Font), new("text", 40, 953, Text: $"Nomenclature {pages.IndexOf(s) + 1}/{pages.Count} · Folio {DocumentModel.PageNumber(p, s)}", Size: Font)];
        void Cell(double x, double rowY, double width, double height, IEnumerable<string> lines, string color = "#203348")
        {
            ink.Add(new("rect", x, rowY, width, height, Color: "#B3C2CF")); int line = 0;
            foreach (var text in lines) ink.Add(new("text", x + 6, rowY + 6 + line++ * Line, Text: text, Size: Font, Color: color));
        }
        double x = 40;
        for (int i = 0; i < b.Columns.Count; i++) { Cell(x, y, widths[i], header, Wrap(b.Columns[i].Label, widths[i] - 12), "#007F86"); x += widths[i]; }
        y += header;
        foreach (var row in b.Rows)
        {
            x = 40;
            for (int i = 0; i < b.Columns.Count; i++)
            {
                Cell(x, y, widths[i], row.Height, row.Cells[i]);
                if (b.Columns[i].Key == "image") Picture(ink, p, row, x + widths[i] / 2, y + row.Height / 2, Math.Min(widths[i] - 16, 60));
                x += widths[i];
            }
            if (row.Continuation) ink.Add(new("text", 6, y + 6, Text: "suite", Size: 9));
            y += row.Height;
        }
        if (b.Rows.Count == 0) ink.Add(new("text", 46, y + 16, Text: "Aucun article ne correspond aux filtres sélectionnés.", Size: 12));
        return ink;
    }
    private static void Picture(List<Ink> ink, Project p, BomFragment row, double x, double y, double size)
    {
        if (row.ImageId.Length > 0)
        {
            var image = ImageAssets.Decode(p.Images[row.ImageId]); double scale = size / Math.Max(image.PixelWidth, image.PixelHeight);
            double w = image.PixelWidth * scale, h = image.PixelHeight * scale;
            ink.Add(new("image", x - w / 2, y - h / 2, w, h, Text: p.Images[row.ImageId])); return;
        }
        var d = Symbols.All[row.Symbol]; double k = size / Math.Max(d.Width + 12, d.Height + 12);
        foreach (var shape in d.Shapes)
            ink.Add(new(shape.Kind, x + shape.X * k, y + shape.Y * k, shape.Kind == "line" ? x + shape.A * k : shape.A * k, shape.Kind == "line" ? y + shape.B * k : shape.B * k, shape.Text, Size: shape.A * k));
    }
}
