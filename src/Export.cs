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
        var containers=new Stack<XElement>();containers.Push(root);int clipIndex=0;
        foreach (var s in Drawing.Scene(p, sheet).SelectMany(Drawing.TextLines))
        {
            if(s.Kind=="clipStart")
            {
                string id="clip"+(++clipIndex);var rectangle=new XElement(ns+"rect",new XAttribute("x",N(s.X)),new XAttribute("y",N(s.Y)),new XAttribute("width",N(s.A)),new XAttribute("height",N(s.B)));
                root.Add(new XElement(ns+"defs",new XElement(ns+"clipPath",new XAttribute("id",id),rectangle)));var group=new XElement(ns+"g",new XAttribute("clip-path","url(#"+id+")"));containers.Peek().Add(group);containers.Push(group);continue;
            }
            if(s.Kind=="clipEnd"){containers.Pop();continue;}
            if(s.Kind=="image") { var img = new XElement(ns+"image",new XAttribute("x",N(s.X)),new XAttribute("y",N(s.Y)),new XAttribute("width",N(s.A)),new XAttribute("height",N(s.B)),new XAttribute("href",(s.Text.StartsWith("iVBOR") ? "data:image/png;base64," : "data:image/jpeg;base64,")+s.Text)); if(s.Rotation!=0) img.Add(new XAttribute("transform", $"rotate({N(s.Rotation)} {N(s.X)} {N(s.Y)})")); root.Add(img);continue; }
            XElement el;
            if (s.Kind == "line") el = new(ns + "line", new XAttribute("x1", N(s.X)), new XAttribute("y1", N(s.Y)), new XAttribute("x2", N(s.A)), new XAttribute("y2", N(s.B)));
            else if (s.Kind == "rect") el = new(ns + "rect", new XAttribute("x", N(s.X)), new XAttribute("y", N(s.Y)), new XAttribute("width", N(s.A)), new XAttribute("height", N(s.B)));
            else if(s.Kind=="ellipse")el=new(ns+"ellipse",new XAttribute("cx",N(s.X+s.A/2)),new XAttribute("cy",N(s.Y+s.B/2)),new XAttribute("rx",N(s.A/2)),new XAttribute("ry",N(s.B/2)));
            else if (s.Kind is "circle" or "dot") el = new(ns + "circle", new XAttribute("cx", N(s.X)), new XAttribute("cy", N(s.Y)), new XAttribute("r", N(s.A)));
            else if (s.Kind == "triangle") el = new(ns + "polygon", new XAttribute("points", $"{N(s.X)},{N(s.Y)} {N(s.X)},{N(s.Y+s.B)} {N(s.X+s.A)},{N(s.Y+s.B/2)}"));
            else { el = new(ns + "text", new XAttribute("x", N(s.X)), new XAttribute("y", N(s.Y + s.Size)), new XAttribute("font-family", "Segoe UI,Arial,sans-serif"), new XAttribute("font-size", N(s.Size)), s.Text); }
            el.Add(new XAttribute("fill", s.Fill.Length>0?s.Fill:s.Kind is "text" or "dot" or "triangle" ? s.Color : s.Kind == "circle" ? "white" : "none"));
            if (s.Kind != "text") { el.Add(new XAttribute("stroke", s.Color)); el.Add(new XAttribute("stroke-width", N(s.Thickness))); if (s.LineStyle != "solid") el.Add(new XAttribute("stroke-dasharray", string.Join(' ', Drawing.DashPattern(s.LineStyle).Select(n => N(n * s.Thickness))))); }
            if (s.Rotation != 0) el.Add(new XAttribute("transform", $"rotate({N(s.Rotation)} {N(s.X)} {N(s.Y)})"));
            if(s.Kind=="text"){if(s.Bold)el.Add(new XAttribute("font-weight","bold"));if(s.Italic)el.Add(new XAttribute("font-style","italic"));}containers.Peek().Add(el);
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
        List<byte[]> objects = [[], [], [], [], [], [], []];
        var fonts=new List<PdfTextFont>{new(1,3,false,false)};
        PdfTextFont Font(bool bold,bool italic)
        {
            var existing=fonts.FirstOrDefault(f=>f.Bold==bold&&f.Italic==italic);if(existing is not null)return existing;
            var next=new PdfTextFont(fonts.Count+1,objects.Count+1,bold,italic);fonts.Add(next);for(int i=0;i<5;i++)objects.Add([]);return next;
        }
        byte[] Bytes(string s) => Encoding.ASCII.GetBytes(s);
        byte[] Stream(byte[] data, string extra = "") { using var m = new MemoryStream(); m.Write(Bytes($"<< /Length {data.Length} {extra} >>\nstream\n")); m.Write(data); m.Write(Bytes("\nendstream")); return m.ToArray(); }
        List<int> pages = [];
        var pageRecords=new List<(PdfPage Page,int Id,int ContentId,string Images,double Scale)>();
        var imageObjects=new Dictionary<string,(string Name,int Id)>();
        foreach (var pdfPage in PdfNavigation.Pages(project))
        {
            var sheet=pdfPage.Sheet;
            var b = new StringBuilder(); double scale = Math.Min(1190.55 / sheet.Width, 841.89 / sheet.Height);
            b.AppendLine($"q {N(scale)} 0 0 {N(-scale)} 0 841.89 cm 1.6 w");
            foreach (var s in pdfPage.Scene.Select(i=>Presentation.TextInk(project,i)).SelectMany(Drawing.TextLines))
            {
                if(s.Kind=="clipStart"){b.AppendLine($"q {N(s.X)} {N(s.Y)} {N(s.A)} {N(s.B)} re W n");continue;}if(s.Kind=="clipEnd"){b.AppendLine("Q");continue;}
                var color = (System.Windows.Media.SolidColorBrush)Drawing.Brush(s.Color);
                string rgb = $"{N(color.Color.R / 255.0)} {N(color.Color.G / 255.0)} {N(color.Color.B / 255.0)}";
                b.AppendLine($"q {rgb} RG {rgb} rg");
                if (s.Thickness != 1.6 || s.LineStyle != "solid") b.AppendLine($"{N(s.Thickness)} w [{string.Join(' ', Drawing.DashPattern(s.LineStyle).Select(n => N(n * s.Thickness)))}] 0 d");
                if (s.Rotation != 0) { double a = s.Rotation * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a); b.AppendLine($"{N(cos)} {N(sin)} {N(-sin)} {N(cos)} {N(s.X - cos * s.X + sin * s.Y)} {N(s.Y - sin * s.X - cos * s.Y)} cm"); }
                switch (s.Kind)
                {
                    case "image":
                        if(!imageObjects.TryGetValue(s.Text,out var entry))
                        {
                            var bitmap=ImageAssets.Decode(s.Text); entry=("Im"+imageObjects.Count,objects.Count+1);
                            byte[] bytes; string filter;
                            if(s.Text.StartsWith("iVBOR"))
                            {
                                // Decode PNG to lossless RGB, compositing transparency onto paper.
                                var rgba = new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap,System.Windows.Media.PixelFormats.Bgra32,null,0);
                                var pixels = new byte[bitmap.PixelWidth*bitmap.PixelHeight*4]; rgba.CopyPixels(pixels,bitmap.PixelWidth*4,0);
                                var rgbBytes = new byte[bitmap.PixelWidth*bitmap.PixelHeight*3];
                                for(int i=0,j=0;i<pixels.Length;i+=4,j+=3) for(int channel=0;channel<3;channel++) rgbBytes[j+channel]=(byte)((pixels[i+2-channel]*pixels[i+3]+255*(255-pixels[i+3])+127)/255);
                                using var compressed=new MemoryStream(); using(var zip=new System.IO.Compression.ZLibStream(compressed,System.IO.Compression.CompressionLevel.Optimal,true)) zip.Write(rgbBytes);
                                bytes=compressed.ToArray();filter="/FlateDecode";
                            }
                            else {bytes=Convert.FromBase64String(s.Text);filter="/DCTDecode";}
                            objects.Add(Stream(bytes,$"/Type /XObject /Subtype /Image /Width {bitmap.PixelWidth} /Height {bitmap.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter {filter}"));imageObjects[s.Text]=entry;
                        }
                        b.AppendLine($"{N(s.A)} 0 0 {N(-s.B)} {N(s.X)} {N(s.Y+s.B)} cm /{entry.Name} Do");break;
                    case "line": b.AppendLine($"{N(s.X)} {N(s.Y)} m {N(s.A)} {N(s.B)} l S"); break;
                    case "triangle": b.AppendLine($"{N(s.X)} {N(s.Y)} m {N(s.X)} {N(s.Y+s.B)} l {N(s.X+s.A)} {N(s.Y+s.B/2)} l h f"); break;
                    case "rect":if(s.Fill.Length>0){var fill=(System.Windows.Media.SolidColorBrush)Drawing.Brush(s.Fill);b.AppendLine($"{N(fill.Color.R/255.0)} {N(fill.Color.G/255.0)} {N(fill.Color.B/255.0)} rg");}b.AppendLine($"{N(s.X)} {N(s.Y)} {N(s.A)} {N(s.B)} re "+(s.Fill.Length>0?"B":"S")); break;
                    case "circle": case "dot": case "ellipse":
                        double x=s.Kind=="ellipse"?s.X+s.A/2:s.X,y=s.Kind=="ellipse"?s.Y+s.B/2:s.Y,rx=s.Kind=="ellipse"?s.A/2:s.A,ry=s.Kind=="ellipse"?s.B/2:s.A,kx=rx*.55228475,ky=ry*.55228475;
                        if(s.Fill.Length>0){var fill=(System.Windows.Media.SolidColorBrush)Drawing.Brush(s.Fill);b.AppendLine($"{N(fill.Color.R/255.0)} {N(fill.Color.G/255.0)} {N(fill.Color.B/255.0)} rg");}
                        b.AppendLine($"{N(x+rx)} {N(y)} m {N(x+rx)} {N(y+ky)} {N(x+kx)} {N(y+ry)} {N(x)} {N(y+ry)} c {N(x-kx)} {N(y+ry)} {N(x-rx)} {N(y+ky)} {N(x-rx)} {N(y)} c {N(x-rx)} {N(y-ky)} {N(x-kx)} {N(y-ry)} {N(x)} {N(y-ry)} c {N(x+kx)} {N(y-ry)} {N(x+rx)} {N(y-ky)} {N(x+rx)} {N(y)} c "+(s.Kind=="dot"?"f":s.Kind=="circle"?"1 1 1 rg B":s.Fill.Length>0?"B":"S"));break;
                    case "text":var textFont=Font(s.Bold,s.Italic);b.AppendLine($"BT /F{textFont.Index} {N(s.Size)} Tf 1 0 0 -1 {N(s.X)} {N(s.Y + s.Size)} Tm <{textFont.Glyphs(s.Text)}> Tj ET"); break;
                }
                b.AppendLine("Q");
            }
            b.AppendLine("Q");
            int contentId = objects.Count + 1; objects.Add(Stream(Bytes(b.ToString())));
            var imageResources=string.Join(' ',imageObjects.Values.Select(i=>$"/{i.Name} {i.Id} 0 R"));
            int pageId = objects.Count + 1; pages.Add(pageId); objects.Add([]);pageRecords.Add((pdfPage,pageId,contentId,imageResources,scale));
        }
        var pageMap=pageRecords.ToDictionary(r=>r.Page.Sheet.Id);
        foreach(var record in pageRecords)
        {
            List<int> annotations=[];foreach(var link in record.Page.Links)
            {
                if(!pageMap.TryGetValue(link.TargetSheetId,out var target))continue;var r=link.Bounds;r.Intersect(new System.Windows.Rect(0,0,record.Page.Sheet.Width,record.Page.Sheet.Height));if(r.IsEmpty||r.Width<=0||r.Height<=0)continue;int id=objects.Count+1;annotations.Add(id);
                objects.Add(Bytes($"<< /Type /Annot /Subtype /Link /Rect [{N(r.Left*record.Scale)} {N(841.89-r.Bottom*record.Scale)} {N(r.Right*record.Scale)} {N(841.89-r.Top*record.Scale)}] /Border [0 0 0] /Dest [{target.Id} 0 R /XYZ {N(link.TargetX*target.Scale)} {N(841.89-link.TargetY*target.Scale)} null] >>"));
            }
            string annots=annotations.Count>0?" /Annots ["+string.Join(' ',annotations.Select(i=>$"{i} 0 R"))+"]":"";
            string fontResources=string.Join(' ',fonts.Select(f=>$"/F{f.Index} {f.Id} 0 R"));
            objects[record.Id-1]=Bytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1190.55 841.89] /Resources << /Font << {fontResources} >> /XObject << {record.Images} >> >> /Contents {record.ContentId} 0 R{annots} >>");
        }
        int outlineId=0;
        if(project.PdfOptions.Bookmarks&&pageRecords.Count>0)
        {
            outlineId=objects.Count+1;objects.Add([]);var itemIds=pageRecords.Select((_,i)=>objects.Count+i+1).ToList();
            for(int i=0;i<pageRecords.Count;i++){var r=pageRecords[i];string title="FEFF"+Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(r.Page.Title));string neighbors=(i>0?$" /Prev {itemIds[i-1]} 0 R":"")+(i+1<itemIds.Count?$" /Next {itemIds[i+1]} 0 R":"");objects.Add(Bytes($"<< /Title <{title}> /Parent {outlineId} 0 R /Dest [{r.Id} 0 R /Fit]{neighbors} >>"));}
            objects[outlineId-1]=Bytes($"<< /Type /Outlines /First {itemIds[0]} 0 R /Last {itemIds[^1]} 0 R /Count {itemIds.Count} >>");
        }
        objects[0] = Bytes("<< /Type /Catalog /Pages 2 0 R"+(outlineId>0?$" /Outlines {outlineId} 0 R /PageMode /UseOutlines":"")+" >>");
        objects[1] = Bytes($"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(' ', pages.Select(p => $"{p} 0 R"))}] >>");
        foreach(var textFont in fonts)
        {
            int at=textFont.Id-1;var font=textFont.Face;var glyphMap=textFont.Map;string name=textFont.Name;
            objects[at]=Bytes($"<< /Type /Font /Subtype /Type0 /BaseFont /{name} /Encoding /Identity-H /DescendantFonts [{textFont.Id+1} 0 R] /ToUnicode {textFont.Id+4} 0 R >>");
            var widths=string.Join(' ',glyphMap.Keys.Select(g=>$"{g} [{N(font.AdvanceWidths[g]*1000)}]"));
            objects[at+1]=Bytes($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{name} /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {textFont.Id+2} 0 R /CIDToGIDMap /Identity /DW 1000 /W [{widths}] >>");
            objects[at+2]=Bytes($"<< /Type /FontDescriptor /FontName /{name} /Flags {(textFont.Italic?96:32)} /FontBBox [-1000 -1000 3000 3000] /ItalicAngle {(textFont.Italic?-12:0)} /Ascent 1100 /Descent -300 /CapHeight 750 /StemV {(textFont.Bold?120:80)} /FontFile2 {textFont.Id+3} 0 R >>");
            byte[] fontData=File.ReadAllBytes(font.FontUri.LocalPath);objects[at+3]=Stream(fontData,$"/Length1 {fontData.Length}");
            var cmap=new StringBuilder("/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def /CMapName /GeMMeElec-UCS def /CMapType 2 def 1 begincodespacerange <0000> <FFFF> endcodespacerange\n");
            foreach(var chunk in glyphMap.Chunk(100)){cmap.AppendLine($"{chunk.Length} beginbfchar");foreach(var pair in chunk)cmap.AppendLine($"<{pair.Key:X4}> <{(int)pair.Value:X4}>");cmap.AppendLine("endbfchar");}
            cmap.Append("endcmap CMapName currentdict /CMap defineresource pop end end");objects[at+4]=Stream(Bytes(cmap.ToString()));
        }
        using var output = new MemoryStream(); output.Write(Bytes("%PDF-1.7\n%GeMMeElec\n")); List<long> offsets = [0];
        for (int i = 0; i < objects.Count; i++) { offsets.Add(output.Position); output.Write(Bytes($"{i + 1} 0 obj\n")); output.Write(objects[i]); output.Write(Bytes("\nendobj\n")); }
        long start = output.Position; output.Write(Bytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (long offset in offsets.Skip(1)) output.Write(Bytes($"{offset:0000000000} 00000 n \n"));
        output.Write(Bytes($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n"));
        var full = Path.GetFullPath(path); var temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllBytes(temp, output.ToArray()); File.Move(temp, full, true); } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private sealed class PdfTextFont
    {
        public int Index {get;}
        public int Id {get;}
        public bool Bold {get;}
        public bool Italic {get;}
        public string Name=>"SegoeUI"+(Bold?"-Bold":"")+(Italic?"-Italic":"");
        public System.Windows.Media.GlyphTypeface Face {get;}
        public SortedDictionary<ushort,char> Map {get;}=[];
        public PdfTextFont(int index,int id,bool bold,bool italic){Index=index;Id=id;Bold=bold;Italic=italic;if(!Drawing.TextFace(bold,italic).TryGetGlyphTypeface(out var face))throw new InvalidOperationException("Police Segoe UI introuvable.");Face=face;}
        public string Glyphs(string text){var builder=new StringBuilder();foreach(var ch in text.Replace('\r',' ').Replace('\n',' ')){ushort glyph=Face.CharacterToGlyphMap.TryGetValue(ch,out var g)?g:(ushort)0;Map.TryAdd(glyph,ch);builder.Append(glyph.ToString("X4"));}return builder.ToString();}
    }

}
