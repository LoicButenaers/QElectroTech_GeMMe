using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace GeMMeElec;

internal static class InteractionBenchmark
{
    public static int Run()
    {
        Environment.SetEnvironmentVariable("GEMMEELEC_TEST_SESSION","1");
        var app=Application.Current??new Application();List<object> samples=[];
        try
        {
            foreach(int density in new[]{50,400})
            {
                var p=EditorBenchmark.Fixture(100);
                if(density==400){var first=p.Sheets[0];foreach(var other in p.Sheets.Skip(1).Take(7)){first.Components.AddRange(other.Components);first.Wires.AddRange(other.Wires);other.Components.Clear();other.Wires.Clear();}for(int i=0;i<first.Components.Count;i++){first.Components[i].X=100+i%20*120;first.Components[i].Y=120+i/20*140;}first.Width=2700;first.Height=3100;}
                var editor=new Editor();editor.ImportProject(p);var canvas=new SheetCanvas(editor);canvas.Measure(new(1000,760));canvas.Arrange(new Rect(0,0,1000,760));canvas.Fit();
                void Frame(){var visual=new DrawingVisual();using(var dc=visual.RenderOpen())canvas.RenderForBenchmark(dc);}
                Frame();
                object Measure(Action<int> action,int count){GC.Collect();long allocation=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();for(int i=0;i<count;i++)action(i);watch.Stop();return new{frames=count,totalMs=watch.Elapsed.TotalMilliseconds,meanMs=watch.Elapsed.TotalMilliseconds/count,allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocation};}
                var text=editor.Sheet.Components[40];var ink=ComponentTexts.Ink(editor.Project,text,new(editor.Project),"tag")!;var point=Drawing.TextBounds(ink).TopLeft+new Vector(2,2);canvas.Select(text.Id);if(!canvas.BeginTextMove(point))throw new Exception("Texte inaccessible.");
                var textDrag=Measure(i=>{canvas.MoveText(point+new Vector(15+i*.2,20));Frame();},120);canvas.CancelDrag();
                var component=editor.Sheet.Components[35];var origin=new Point(component.X,component.Y);canvas.Select(component.Id);canvas.BeginSelectionMove(origin);var componentDrag=Measure(i=>{canvas.MoveSelection(origin+new Vector(20+(i%20)*10,10));Frame();},120);canvas.CancelDrag();
                var hover=Measure(i=>{canvas.HoverForBenchmark(new(400+i%4,400));Frame();},120);
                var zoom=Measure(i=>{canvas.ViewportForBenchmark(.8+i%12*.03,new Vector(30,30));Frame();},60);
                var pan=Measure(i=>{canvas.ViewportForBenchmark(1.1,new Vector(30+i,30+i));Frame();},120);
                samples.Add(new{activeComponents=density,projectComponents=5000,activeWires=editor.Sheet.Wires.Count,textDrag,componentDrag,hover,zoom,pan});
            }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"interaction-benchmark.json"),JsonSerializer.Serialize(new{passed=true,version=typeof(Editor).Assembly.GetName().Version!.ToString(),measurement="Préparation des dessins WPF hors écran, sans rasterisation GPU",samples},ProjectStore.Json));return 0;
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"interaction-benchmark.json"),JsonSerializer.Serialize(new{passed=false,error=ex.ToString(),samples},ProjectStore.Json));return 1;}
    }
}
public sealed partial class SheetCanvas
{
    internal void RenderForBenchmark(DrawingContext dc)=>OnRender(dc);
    internal Endpoint? HoverForBenchmark(Point point){pointer=point;hoveredPort=HitPort(point);return hoveredPort;}
    internal void ViewportForBenchmark(double zoom,Vector pan){Zoom=zoom;offset=pan;}
}
