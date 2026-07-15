using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Tessera.Charts;
using Tessera.Layout;
using Tessera.Primitives;
using Tessera.Rendering;
using Tessera.Text;
using Tessera.Widgets;
using Tessera.Widgets.Trees;

namespace Tessera.Benchmarks;

/// <summary>
/// Benchmarks whole-widget rendering — the cost that dominates a real frame, since an app
/// renders a tree of widgets, not isolated primitives. Covers Table, TreeView, and a
/// realistic full-app frame (the shape of the actual gallery / Sherlock UI). The two lines
/// this suite exists to hold: a frame under ~1 ms, and steady-state allocation near zero.
/// <see cref="MemoryDiagnoser"/> is on because per-frame allocations are the thing to watch.
/// </summary>
[MemoryDiagnoser]
public class WidgetBenchmarks
{
    private Surface _surface = null!;
    private Table _table = null!;
    private TreeView<int> _wideTree = null!;
    private TreeView<int> _deepTree = null!;
    private Widget _appFrame = null!;
    private Rect _area;

    [Params(50, 200)]
    public int Rows;

    [GlobalSetup]
    public void Setup()
    {
        _surface = new Surface(120, 40);
        _area = _surface.Bounds;

        // A table with the requested row count and 5 columns.
        _table = new Table { ShowHeader = true, SelectedIndex = 3, Striped = true, Sortable = true };
        _table.Columns.Add(new Column("Location", Constraint.Fill(3)));
        _table.Columns.Add(new Column("Allocator", Constraint.Fill(2)));
        _table.Columns.Add(new Column("MiB", Constraint.Length(10), Alignment.Right)
            { SortKey = s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture) });
        _table.Columns.Add(new Column("Count", Constraint.Length(9), Alignment.Right));
        _table.Columns.Add(new Column("Own%", Constraint.Length(7), Alignment.Right));
        for (int i = 0; i < Rows; i++)
        {
            _table.Rows.Add(new[] { $"Frame.Method_{i}", "GC.Allocate", $"{i * 3.5:0.0}", $"{i * 128}", $"{i % 100}.0" });
        }

        // A wide tree: one root with `Rows` children, fully expanded.
        _wideTree = new TreeView<int> { RenderLabel = n => StyledText.Of($"node {n.Value}") };
        var wideRoot = _wideTree.AddRoot(0);
        for (int i = 1; i <= Rows; i++)
        {
            wideRoot.AddChild(i);
        }

        wideRoot.Expand();
        _wideTree.Invalidate();

        // A deep tree: a single chain `Rows` levels deep, fully expanded.
        _deepTree = new TreeView<int> { RenderLabel = n => StyledText.Of($"level {n.Value}") };
        var node = _deepTree.AddRoot(0);
        for (int i = 1; i <= Rows; i++) { var c = node.AddChild(i); node.Expand(); node = c; }
        node.Expand();
        _deepTree.Invalidate();

        _appFrame = BuildAppFrame();
        _chartTab = BuildChartTab();
    }

    // Replica of the demo's Charts tab: proportion bar + legend + sparkline + bar chart + a
    // multi-series braille line chart. This is the tab reported as ~180k alloc/frame in the HUD.
    private Widget _chartTab = null!;

    private static Widget BuildChartTab()
    {
        var bar = new BarChart
        {
            Values = { ("heap", 42), ("stack", 18), ("code", 27), ("meta", 9), ("jit", 33) },
            BarColors =
            {
                Colors.Hex("#6a9fb5"), Colors.Hex("#90a959"), Colors.Hex("#f4bf75"),
                Colors.Hex("#aa759f"), Colors.Hex("#ac4142"),
            },
        };

        var comp = new ProportionBar
        {
            ShowInlineLabels = true,
            Segments =
            {
                new Segment("String", 42, Colors.Hex("#6a9fb5")),
                new Segment("Byte[]", 23, Colors.Hex("#90a959")),
                new Segment("Dictionary", 15, Colors.Hex("#f4bf75")),
                new Segment("Node", 12, Colors.Hex("#aa759f")),
                new Segment("other", 8, Colors.Hex("#505050")),
            },
        };
        var compLegend = new Legend
        {
            Items =
            {
                new LegendItem("String", Colors.Hex("#6a9fb5"), "42%"),
                new LegendItem("Byte[]", Colors.Hex("#90a959"), "23%"),
                new LegendItem("Dictionary", Colors.Hex("#f4bf75"), "15%"),
            },
        };

        var spark = new Sparkline();
        for (int i = 0; i < 120; i++) spark.Push(System.Math.Sin(i * 0.2));

        var lines = new LineChart { UseBraille = true };
        var sine = new Series("sin");
        var cosine = new Series("cos");
        var noise = new Series("walk");
        for (int i = 0; i < 120; i++)
        {
            double t = i * 0.25;
            sine.Add(t, System.Math.Sin(t));
            cosine.Add(t, System.Math.Cos(t * 0.7));
            noise.Add(t, System.Math.Sin(t * 1.3) * 0.6);
        }
        lines.SeriesList.Add(sine);
        lines.SeriesList.Add(cosine);
        lines.SeriesList.Add(noise);

        var legend = new Label(
            StyledText.Of("● sin ").Fg(Colors.Hex("#6a9fb5"))
                .Append("● cos ").Fg(Colors.Hex("#90a959"))
                .Append("● walk").Fg(Colors.Hex("#aa759f")));

        var chartHeader = new Stack(Direction.Horizontal)
            .Add(new Rule(StyledText.Of("line chart — f(time), multi-series")), Constraint.Fill())
            .Add(legend, Constraint.Length(20));

        return new Stack(Direction.Vertical)
            .Add(new Rule(StyledText.Of("heap composition (proportion bar)")), Constraint.Length(1))
            .Add(new Padding(comp, new Thickness(1, 0)), Constraint.Length(1))
            .Add(new Padding(compLegend, new Thickness(1, 0)), Constraint.Length(1))
            .Add(new Rule(StyledText.Of("sparkline (live)")), Constraint.Length(1))
            .Add(new Padding(spark, new Thickness(1, 0)), Constraint.Length(3))
            .Add(new Rule(StyledText.Of("bar chart")), Constraint.Length(1))
            .Add(new Padding(bar, new Thickness(1, 0)), Constraint.Length(6))
            .Add(chartHeader, Constraint.Length(1))
            .Add(new Padding(lines, new Thickness(1, 0)), Constraint.Fill());
    }

    // A realistic full-app frame in the shape of the gallery / Sherlock UI: a header bar, a
    // tabbed body whose active tab holds a panelled table beside a live chart, and a footer.
    // This is the number that actually represents the per-frame cost of the real UI.
    private Widget BuildAppFrame()
    {
        var header = new StatusBar { Left = " Tessera ", Center = "profiler frame", Right = "v0.1 " };
        var footer = new StatusBar { Left = " ^P palette ", Right = "ready " };

        var spark = new Sparkline();
        for (int i = 0; i < 120; i++) spark.Push(System.Math.Sin(i * 0.2));

        var body = new Stack(Direction.Horizontal)
            .Add(new Panel(_table, " Allocations "), Constraint.Fill(2))
            .Add(new Panel(spark, " Heap "), Constraint.Fill(1));

        var tabs = new Tabs()
            .Add("Heap", body)
            .Add("Types", new Label("second tab"));

        return new Stack(Direction.Vertical)
            .Add(header, Constraint.Length(1))
            .Add(tabs, Constraint.Fill())
            .Add(footer, Constraint.Length(1));
    }

    [Benchmark(Description = "Table render")]
    public void TableRender() => _table.Render(_surface, _area);

    [Benchmark(Description = "Table sort (numeric column, precomputed keys)")]
    public void TableSort() => _table.SortBy(2, SortState.Descending);

    [Benchmark(Description = "TreeView render (wide, 1 level)")]
    public void WideTreeRender() => _wideTree.Render(_surface, _area);

    [Benchmark(Description = "TreeView render (deep chain)")]
    public void DeepTreeRender() => _deepTree.Render(_surface, _area);

    [Benchmark(Description = "Realistic app frame (header+tabs+table+chart+footer)")]
    public void AppFrame() => _appFrame.Render(_surface, _area);

    [Benchmark(Description = "Charts tab (proportion+legend+sparkline+bar+braille lines)")]
    public void ChartTab() => _chartTab.Render(_surface, _area);
}

