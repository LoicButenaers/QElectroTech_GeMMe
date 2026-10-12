using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace GeMMeElec;

public static class Export
{
    private static string N(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    public static void Svg(Project p, Sheet sheet, string path)
    {
        XNamespace ns = "http://www.w3.org/2000/svg";
        var root = new XElement(ns + "svg", new XAttribute("viewBox", $"0 0 {N(sheet.Width)} {N(sheet.Height)}"), new XAttribute("width", "420mm"), new XAttribute("height", "297mm"));
        root.Add(new XElement(ns + "title", $"{p.Title} — {sheet.Title}"));
        root.Add(new XElement(ns + "rect", new XAttribute("width", "100%"), new XAttribute("height", "100%"), new XAttribute("fill", "white")));
        foreach (var s in Drawing.Scene(p, sheet))
        {
            if(s.Kind=="image") { root.Add(new XElement(ns+"image",new XAttribute("x",N(s.X)),new XAttribute("y",N(s.Y)),new XAttribute("width",N(s.A)),new XAttribute("height",N(s.B)),new XAttribute("href","data:image/jpeg;base64,"+s.Text)));continue; }
            XElement el;
            if (s.Kind == "line") el = new(ns + "line", new XAttribute("x1", N(s.X)), new XAttribute("y1", N(s.Y)), new XAttribute("x2", N(s.A)), new XAttribute("y2", N(s.B)));
            else if (s.Kind == "rect") el = new(ns + "rect", new XAttribute("x", N(s.X)), new XAttribute("y", N(s.Y)), new XAttribute("width", N(s.A)), new XAttribute("height", N(s.B)));
            else if (s.Kind is "circle" or "dot") el = new(ns + "circle", new XAttribute("cx", N(s.X)), new XAttribute("cy", N(s.Y)), new XAttribute("r", N(s.A)));
            else if (s.Kind == "triangle") el = new(ns + "polygon", new XAttribute("points", $"{N(s.X)},{N(s.Y)} {N(s.X)},{N(s.Y+s.B)} {N(s.X+s.A)},{N(s.Y+s.B/2)}"));
            else { el = new(ns + "text", new XAttribute("x", N(s.X)), new XAttribute("y", N(s.Y + s.Size)), new XAttribute("font-family", "Segoe UI,Arial,sans-serif"), new XAttribute("font-size", N(s.Size)), s.Text); }
            el.Add(new XAttribute("fill", s.Kind is "text" or "dot" or "triangle" ? s.Color : s.Kind == "circle" ? "white" : "none"));
            if (s.Kind != "text") { el.Add(new XAttribute("stroke", s.Color)); el.Add(new XAttribute("stroke-width", "1.6")); }
            if (s.Rotation != 0) el.Add(new XAttribute("transform", $"rotate({N(s.Rotation)} {N(s.X)} {N(s.Y)})"));
            root.Add(el);
        }
        ProjectStore.AtomicWrite(path, new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString());
    }
    public static string Bom(Project p)
    {
        return BomReport.Csv(p,p.BomOptions);
    }
    public static void Csv(Project p, string path) => ProjectStore.AtomicWrite(path, "\uFEFF" + Bom(p));
    // A3 vector PDF, with a Unicode Type0 font built from the installed Segoe UI.
    // Glyph outlines stay embedded, so accents and electrical labels survive export.
    public static void Pdf(Project project, string path)
    {
        var typeface = new System.Windows.Media.Typeface("Segoe UI");
        if (!typeface.TryGetGlyphTypeface(out var font)) throw new InvalidOperationException("Police Segoe UI introuvable.");
        byte[] fontData = File.ReadAllBytes(font.FontUri.LocalPath);
        var glyphMap = new SortedDictionary<ushort, char>();
        string Glyphs(string text)
        {
            var b = new StringBuilder();
            foreach (var ch in text.Replace('\r', ' ').Replace('\n', ' ')) { ushort glyph = font.CharacterToGlyphMap.TryGetValue(ch, out var g) ? g : (ushort)0; glyphMap.TryAdd(glyph, ch); b.Append(glyph.ToString("X4")); }
            return b.ToString();
        }
        List<byte[]> objects = [[], [], [], [], [], [], []];
        byte[] Bytes(string s) => Encoding.ASCII.GetBytes(s);
        byte[] Stream(byte[] data, string extra = "") { using var m = new MemoryStream(); m.Write(Bytes($"<< /Length {data.Length} {extra} >>\nstream\n")); m.Write(data); m.Write(Bytes("\nendstream")); return m.ToArray(); }
        List<int> pages = [];
        var imageObjects=new Dictionary<string,(string Name,int Id)>();
        foreach (var sheet in project.Sheets)
        {
            var b = new StringBuilder(); double scale = Math.Min(1190.55 / sheet.Width, 841.89 / sheet.Height);
            b.AppendLine($"q {N(scale)} 0 0 {N(-scale)} 0 841.89 cm 1.6 w");
            foreach (var s in Drawing.Scene(project, sheet))
            {
                var color = (System.Windows.Media.SolidColorBrush)Drawing.Brush(s.Color);
                string rgb = $"{N(color.Color.R / 255.0)} {N(color.Color.G / 255.0)} {N(color.Color.B / 255.0)}";
                b.AppendLine($"q {rgb} RG {rgb} rg");
                if (s.Rotation != 0) { double a = s.Rotation * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a); b.AppendLine($"{N(cos)} {N(sin)} {N(-sin)} {N(cos)} {N(s.X - cos * s.X + sin * s.Y)} {N(s.Y - sin * s.X - cos * s.Y)} cm"); }
                switch (s.Kind)
                {
                    case "image":
                        if(!imageObjects.TryGetValue(s.Text,out var entry)) {var bitmap=ImageAssets.Decode(s.Text);entry=("Im"+imageObjects.Count,objects.Count+1);objects.Add(Stream(Convert.FromBase64String(s.Text),$"/Type /XObject /Subtype /Image /Width {bitmap.PixelWidth} /Height {bitmap.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode"));imageObjects[s.Text]=entry;}
                        b.AppendLine($"{N(s.A)} 0 0 {N(-s.B)} {N(s.X)} {N(s.Y+s.B)} cm /{entry.Name} Do");break;
                    case "line": b.AppendLine($"{N(s.X)} {N(s.Y)} m {N(s.A)} {N(s.B)} l S"); break;
                    case "triangle": b.AppendLine($"{N(s.X)} {N(s.Y)} m {N(s.X)} {N(s.Y+s.B)} l {N(s.X+s.A)} {N(s.Y+s.B/2)} l h f"); break;
                    case "rect": b.AppendLine($"{N(s.X)} {N(s.Y)} {N(s.A)} {N(s.B)} re S"); break;
                    case "circle": case "dot":
                        double x = s.X, y = s.Y, r = s.A, k = r * .55228475;
                        b.AppendLine($"{N(x + r)} {N(y)} m {N(x + r)} {N(y + k)} {N(x + k)} {N(y + r)} {N(x)} {N(y + r)} c {N(x - k)} {N(y + r)} {N(x - r)} {N(y + k)} {N(x - r)} {N(y)} c {N(x - r)} {N(y - k)} {N(x - k)} {N(y - r)} {N(x)} {N(y - r)} c {N(x + k)} {N(y - r)} {N(x + r)} {N(y - k)} {N(x + r)} {N(y)} c " + (s.Kind == "dot" ? "f" : "1 1 1 rg B")); break;
                    case "text": b.AppendLine($"BT /F1 {N(s.Size)} Tf 1 0 0 -1 {N(s.X)} {N(s.Y + s.Size)} Tm <{Glyphs(s.Text)}> Tj ET"); break;
                }
                b.AppendLine("Q");
            }
            b.AppendLine("Q");
            int contentId = objects.Count + 1; objects.Add(Stream(Bytes(b.ToString())));
            var imageResources=string.Join(' ',imageObjects.Values.Select(i=>$"/{i.Name} {i.Id} 0 R"));
            int pageId = objects.Count + 1; pages.Add(pageId); objects.Add(Bytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1190.55 841.89] /Resources << /Font << /F1 3 0 R >> /XObject << {imageResources} >> >> /Contents {contentId} 0 R >>"));
        }
        objects[0] = Bytes("<< /Type /Catalog /Pages 2 0 R >>");
        objects[1] = Bytes($"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(' ', pages.Select(p => $"{p} 0 R"))}] >>");
        objects[2] = Bytes("<< /Type /Font /Subtype /Type0 /BaseFont /SegoeUI /Encoding /Identity-H /DescendantFonts [4 0 R] /ToUnicode 7 0 R >>");
        var widths = string.Join(' ', glyphMap.Keys.Select(g => $"{g} [{N(font.AdvanceWidths[g] * 1000)}]"));
        objects[3] = Bytes($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /SegoeUI /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 5 0 R /CIDToGIDMap /Identity /DW 1000 /W [{widths}] >>");
        objects[4] = Bytes("<< /Type /FontDescriptor /FontName /SegoeUI /Flags 32 /FontBBox [-1000 -1000 3000 3000] /ItalicAngle 0 /Ascent 1100 /Descent -300 /CapHeight 750 /StemV 80 /FontFile2 6 0 R >>");
        objects[5] = Stream(fontData, $"/Length1 {fontData.Length}");
        var cmap = new StringBuilder("/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def /CMapName /GeMMeElec-UCS def /CMapType 2 def 1 begincodespacerange <0000> <FFFF> endcodespacerange\n");
        foreach (var chunk in glyphMap.Chunk(100)) { cmap.AppendLine($"{chunk.Length} beginbfchar"); foreach (var pair in chunk) cmap.AppendLine($"<{pair.Key:X4}> <{(int)pair.Value:X4}>"); cmap.AppendLine("endbfchar"); }
        cmap.Append("endcmap CMapName currentdict /CMap defineresource pop end end"); objects[6] = Stream(Bytes(cmap.ToString()));
        using var output = new MemoryStream(); output.Write(Bytes("%PDF-1.7\n%GeMMeElec\n")); List<long> offsets = [0];
        for (int i = 0; i < objects.Count; i++) { offsets.Add(output.Position); output.Write(Bytes($"{i + 1} 0 obj\n")); output.Write(objects[i]); output.Write(Bytes("\nendobj\n")); }
        long start = output.Position; output.Write(Bytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (long offset in offsets.Skip(1)) output.Write(Bytes($"{offset:0000000000} 00000 n \n"));
        output.Write(Bytes($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n"));
        var full = Path.GetFullPath(path); var temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllBytes(temp, output.ToArray()); File.Move(temp, full, true); } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
