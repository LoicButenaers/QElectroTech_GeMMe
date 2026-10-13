using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeMMeElec;

// Dedicated process: measurements are not polluted by the whole self-test suite.
// Software/offscreen WPF rendering is measured, not GPU frame rate or input latency.
internal static class EditorBenchmark
{
    public static int Run(int? folios = null)
    {
        Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION", "1");
        var app = new Application(); var window = new MainWindow(); string dir = AppContext.BaseDirectory; var samples = new List<object>();
        try
        {
            window.SetInterface("compact", "light", false);
            foreach (int count in folios.HasValue ? new[] { folios.Value } : new[] { 100, 200 })
            {
                if (count is not (100 or 200)) throw new ArgumentException("Choisir 100 ou 200 folios pour la mesure.");
                window.Editor.New("Benchmark"); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long memoryBefore = Process.GetCurrentProcess().PrivateMemorySize64;
                var p = Fixture(count); string path = Path.Combine(dir, $"benchmark-{count}.gemelec"); var clock = Stopwatch.StartNew(); ProjectStore.Save(p, path); clock.Stop(); double saveMs = clock.Elapsed.TotalMilliseconds;
                clock.Restart(); window.Editor.Open(path); clock.Stop(); double openMs = clock.Elapsed.TotalMilliseconds; p = window.Editor.Project;
                var e = window.Editor; window.Canvas.Select(e.Sheet.Components[0].Id); var root = (FrameworkElement)window.Content; root.Measure(new(1440, 900)); root.Arrange(new(0, 0, 1440, 900)); root.UpdateLayout(); window.Canvas.Fit();
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long memoryAfterOpen = Process.GetCurrentProcess().PrivateMemorySize64;
                var field = window.PropertyField("description"); var editMs = new List<double>();
                for (int i = 0; i < 5; i++) { clock.Restart(); field.Text = "Commande " + i; clock.Stop(); editMs.Add(clock.Elapsed.TotalMilliseconds); }
                clock.Restart(); e.Undo(); clock.Stop(); double undoMs = clock.Elapsed.TotalMilliseconds;
                clock.Restart(); e.Redo(); clock.Stop(); double redoMs = clock.Elapsed.TotalMilliseconds;
                clock.Restart(); e.Change(() => { e.Sheet.Components[0].X += 10; e.Sheet.Components[0].Rotation = 90; }); clock.Stop(); double moveRotateMs = clock.Elapsed.TotalMilliseconds;
                var textComponent = e.Sheet.Components[40]; window.Canvas.Select(textComponent.Id);
                var textBounds = Drawing.TextBounds(ComponentTexts.Ink(e.Project, textComponent, new(e.Project), "tag")!); var textPoint = textBounds.TopLeft + new Vector(2, 2);
                clock.Restart(); if (!window.Canvas.BeginTextMove(textPoint)) throw new InvalidOperationException("Texte de charge inaccessible."); clock.Stop(); double textDragBeginMs = clock.Elapsed.TotalMilliseconds;
                string snapshot = e.ExportSnapshot; clock.Restart(); for (int i = 0; i < 120; i++) window.Canvas.MoveText(textPoint + new Vector(40 + i / 120d, 20)); clock.Stop(); double textDrag120MovesMs = clock.Elapsed.TotalMilliseconds;
                if (e.ExportSnapshot != snapshot) throw new InvalidOperationException("Sérialisation pendant l’aperçu.");
                clock.Restart(); window.Canvas.EndPreviewMove(); clock.Stop(); double textDragCommitMs = clock.Elapsed.TotalMilliseconds;
                clock.Restart(); e.SetWireNumber(e.Sheet.Wires[30].Id, "CONTINUITE"); clock.Stop(); double conductorRenumberMs = clock.Elapsed.TotalMilliseconds;
                clock.Restart(); var scene = Drawing.Scene(e.Project, e.Sheet); clock.Stop(); double sceneMs = clock.Elapsed.TotalMilliseconds;
                clock.Restart(); root.UpdateLayout(); var image = new RenderTargetBitmap(1440, 900, 96, 96, PixelFormats.Pbgra32); image.Render(root); clock.Stop(); double offscreenRasterMs = clock.Elapsed.TotalMilliseconds;
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long memoryAfterEdits = Process.GetCurrentProcess().PrivateMemorySize64;
                clock.Restart(); Export.Pdf(e.Project, Path.Combine(dir, $"benchmark-{count}.pdf")); clock.Stop(); double pdfMs = clock.Elapsed.TotalMilliseconds;
                samples.Add(new { folios = count, components = count * 50, conductors = count * 49, annotations = count, associatedContacts = count * 20, projectBytes = new FileInfo(path).Length, saveMs, openMs, propertyEditMs = editMs, undoMs, redoMs, moveRotateMs, textDragBeginMs, textDrag120MovesMs, textDragCommitMs, conductorRenumberMs, sceneMs, offscreenRasterMs, pdfMs, privateMemoryBeforeBytes = memoryBefore, privateMemoryAfterOpenBytes = memoryAfterOpen, privateMemoryAfterEditsBytes = memoryAfterEdits, privateMemoryAfterPdfBytes = Process.GetCurrentProcess().PrivateMemorySize64, workingSetBytes = Process.GetCurrentProcess().WorkingSet64, primitivesOnActiveSheet = scene.Count });
            }
            File.WriteAllText(Path.Combine(dir, folios.HasValue ? $"benchmark-{folios}.json" : "benchmark.json"), JsonSerializer.Serialize(new { passed = true, version = "0.14", logicalProcessors = Environment.ProcessorCount, renderer = "WPF hors écran, logiciel", samples }, ProjectStore.Json)); return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(dir, "benchmark.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString(), samples }, ProjectStore.Json)); return 1; }
        finally { window.DisposeTestBridge(); }
    }
    internal static Project Fixture(int count)
    {
        var p = new Project { Title = $"Charge {count} folios / {count * 50} composants" }; p.Sheets.Clear();
        for (int page = 0; page < count; page++)
        {
            var s = new Sheet { Title = $"Commande · {page + 1}" }; p.Sheets.Add(s);
            for (int i = 0; i < 50; i++)
            {
                string symbol = i < 10 ? "coil" : i < 20 ? "contact_no" : i < 30 ? "contact_nc" : i < 40 ? "terminal" : "breaker1";
                var c = new Component { Symbol = symbol, Tag = $"A{page + 1}-{i + 1}", Description = "Fonction générique", X = 100 + i % 10 * 125, Y = 120 + i / 10 * 160 }; s.Components.Add(c);
                if (i is >= 10 and < 30) { var coil = s.Components[i % 10]; c.CoilId = coil.Id; c.Tag = coil.Tag; }
                if (i > 0) s.Wires.Add(new() { From = new(s.Components[i - 1].Id, s.Components[i - 1].Definition.Ports[1].Id), To = new(c.Id, c.Definition.Ports[0].Id), Number = $"W{page * 50 + i}" });
            }
            s.Notes.Add(new() { X = 100, Y = 50, Text = "Commande 24 V DC · jeu synthétique" });
        }
        return p;
    }
}
