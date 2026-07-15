using System;
using System.Collections.Generic;
using System.Globalization;
using Tessera.Charts;
using Tessera.Layout;
using Tessera.Primitives;
using Tessera.Text;
using Tessera.Theming;
using Tessera.Widgets;
using Tessera.Widgets.Trees;

namespace Tessera.Demo;

/// <summary>One frame in the tree demo: a name, self time, share, and a synthetic heap address.</summary>
internal readonly record struct TraceFrame(string Name, double Ms, double Percent, long Addr = 0);

// The catalog registry + one free function per widget family. Each function is self-contained:
// it owns its widgets, and live ones subscribe to ctx.Tick to animate.
internal static class Demos
{
    public static readonly IReadOnlyList<Demo> All =
    [
        new("Table", "sortable, striped, selectable rows", Table),
        new("Tree", "collapsible, lazy, clickable links", Tree),
        new("Tabs", "tabbed container with a lens bar", TabsDemo),
        new("Line chart", "multi-series braille plot (live)", LineChartDemo),
        new("Sparkline", "compact rolling series (live)", SparklineDemo),
        new("Bar chart", "horizontal bars, sub-cell precision", BarChartDemo),
        new("Flamegraph", "zoomable icicle, width = weight", Flamegraph),
        new("Proportion bar", "stacked shares + legend", Proportion),
        new("Progress", "sub-cell bars with labels (live)", Progress),
        new("Spinners", "indeterminate animations (live)", Spinners),
        new("Input & forms", "text fields, masking, live filter", Input),
        new("Panels & borders", "framed containers, border styles", Panels),
        new("Overlay", "modal dialog over dimmed content", OverlayDemo),
        new("Text & wrap", "styled runs, word wrapping, justify", TextDemo),
        new("Gradient", "truecolor interpolation", Gradient),
    ];

    // ---- Data widgets ----

    private static Widget Table(DemoContext ctx)
    {
        var t = MakeTable(("Location", Constraint.Fill(3), false), ("Allocator", Constraint.Fill(2), false),
            ("MiB", Constraint.Length(10), true), ("Count", Constraint.Length(9), true), ("Own%", Constraint.Length(7), true));
        var rng = new Random(7);
        string[] methods = { "Parse", "Serialize", "Compile", "Read", "Encode", "Match", "Build", "Hash" };
        string[] allocs = { "GC.Allocate", "new byte[]", "String.Concat", "List.Add", "Box" };
        for (int i = 0; i < 60; i++)
        {
            double mib = rng.NextDouble() * 200;
            t.Rows.Add(new[]
            {
                $"Frame.{methods[i % methods.Length]}_{i}",
                allocs[i % allocs.Length],
                mib.ToString("0.0", CultureInfo.InvariantCulture),
                (rng.Next(1, 9000)).ToString("N0", CultureInfo.InvariantCulture),
                (rng.NextDouble() * 100).ToString("0.0", CultureInfo.InvariantCulture),
            });
        }
        t.HasFocus = true;
        return Padded(t);
    }

    private static Widget Tree(DemoContext ctx)
    {
        var addrRng = new Random(0xBEEF);
        var tree = new TreeView<TraceFrame>
        {
            RenderLabel = n =>
            {
                var label = StyledText.Of(n.Value.Name).Fg(Theme.Current.Foreground).Append("  ");
                if (n.Value.Addr != 0)
                {
                    label.Append($"0x{n.Value.Addr:x8}").Fg(Theme.Current.Accent).Underline().Link(n.Value.Addr);
                }
                return label;
            },
            ShowHeader = true,
            Striped = true,
            ShowGuides = true,
        };
        tree.Columns.Add(new TreeColumn<TraceFrame>("time", 9,
            n => new StyledText($"{n.Value.Ms:0.0}ms", Theme.Current.MutedStyle)));
        tree.Columns.Add(new TreeColumn<TraceFrame>("self%", 5, n =>
        {
            double p = n.Value.Percent;
            var c = p > 40 ? Theme.Current.Error : p > 15 ? Theme.Current.Warning : Theme.Current.Success;
            return new StyledText($"{p:0}%", new Style(c, Color.Default));
        }));

        TraceFrame F(string name, double ms, double pct) =>
            new(name, ms, pct, 0x7f0000000000L + addrRng.Next(0x1000, 0xfffffff));

        var root = tree.AddRoot(F("HttpServer.HandleRequest", 128.4, 100));
        var router = root.AddChild(F("Router.Match", 12.3, 10));
        router.AddChild(F("Regex.Compile", 8.1, 6));
        router.AddChild(F("Trie.Lookup", 3.9, 3));
        var ctrl = root.AddChild(F("UserController.Get", 96.2, 75));
        var db = ctrl.AddChild(F("Db.Query(users)", 71.5, 56));
        db.AddChild(F("Connection.Open", 12.0, 9));
        var exec = db.AddChild(F("Sql.Execute", 55.1, 43));
        exec.AddChild(F("Network.RoundTrip", 48.0, 37));
        exec.AddChild(F("ResultSet.Read", 7.1, 6));
        var json = ctrl.AddChild(F("Json.Serialize", 21.0, 16));
        json.AddChild(F("Utf8.Encode", 14.1, 11));
        root.AddChild(F("Middleware.Log", 6.7, 5));
        root.ExpandAll();
        tree.Invalidate();
        tree.HasFocus = true;

        var jumped = new Label(new StyledText("click a 0x… address to jump", Theme.Current.MutedStyle));
        tree.OnLinkClick = p => jumped.Content =
            StyledText.Of($"→ jumped to 0x{(long)p:x8}").Bold().Fg(Theme.Current.Accent);

        return new Stack(Direction.Vertical)
            .Add(new Padding(tree, new Thickness(1, 0)), Constraint.Fill())
            .Add(new Padding(jumped, new Thickness(1, 0)), Constraint.Length(1));
    }

    private static Widget TabsDemo(DemoContext ctx) =>
        Padded(new Tabs()
            .Add("Overview", CenteredNote("Tab 1 — Left/Right or 1-9 to switch"))
            .Add("Details", CenteredNote("Tab 2 — each tab holds its own widget"))
            .Add("Settings", CenteredNote("Tab 3 — the active tab keeps focus")));

    // ---- Charts (live) ----

    private static Widget LineChartDemo(DemoContext ctx)
    {
        var chart = new LineChart { UseBraille = true };
        var sine = new Series("sin", Colors.Hex("#6a9fb5"));
        var cosine = new Series("cos", Colors.Hex("#90a959"));
        var walk = new Series("walk", Colors.Hex("#aa759f"));
        chart.SeriesList.Add(sine);
        chart.SeriesList.Add(cosine);
        chart.SeriesList.Add(walk);
        double t = 0;
        var rng = new Random(3);
        double last = 0;
        void Seed()
        {
            for (int i = 0; i < 120; i++) Step();
        }
        void Step()
        {
            t += 0.25;
            sine.Add(t, Math.Sin(t));
            cosine.Add(t, Math.Cos(t * 0.7));
            last = Math.Clamp(last + (rng.NextDouble() - 0.5) * 0.4, -1.2, 1.2);
            walk.Add(t, last);
            double cutoff = t - 30;
            sine.TrimBefore(cutoff);
            cosine.TrimBefore(cutoff);
            walk.TrimBefore(cutoff);
        }
        Seed();
        ctx.Tick += _ => Step();

        var legend = new Label(StyledText.Of("● sin ").Fg(Colors.Hex("#6a9fb5"))
            .Append("● cos ").Fg(Colors.Hex("#90a959"))
            .Append("● walk").Fg(Colors.Hex("#aa759f")));
        return new Stack(Direction.Vertical)
            .Add(new Padding(legend, new Thickness(1, 0)), Constraint.Length(1))
            .Add(new Padding(chart, new Thickness(1, 0)), Constraint.Fill());
    }

    private static Widget SparklineDemo(DemoContext ctx)
    {
        var spark = new Sparkline { BaselineZero = true };
        var rng = new Random(11);
        double v = 0.5;
        for (int i = 0; i < 80; i++) spark.Push(v = Math.Clamp(v + (rng.NextDouble() - 0.5) * 0.3, 0, 1));
        ctx.Tick += _ => spark.Push(v = Math.Clamp(v + (rng.NextDouble() - 0.5) * 0.3, 0, 1));
        return new Stack(Direction.Vertical)
            .Add(CenteredNote("a compact rolling series — great for inline metrics"), Constraint.Fill())
            .Add(new Padding(spark, new Thickness(1, 0)), Constraint.Length(4))
            .Add(new Label(StyledText.Empty()), Constraint.Fill());
    }

    private static Widget BarChartDemo(DemoContext ctx)
    {
        var bar = new BarChart
        {
            Values = { ("heap", 42), ("stack", 18), ("code", 27), ("metadata", 9), ("jit", 33), ("threads", 14) },
            BarColors =
            {
                Colors.Hex("#6a9fb5"), Colors.Hex("#90a959"), Colors.Hex("#f4bf75"),
                Colors.Hex("#aa759f"), Colors.Hex("#ac4142"), Colors.Hex("#75b5aa"),
            },
        };
        return Padded(bar);
    }

    private static Widget Flamegraph(DemoContext ctx)
    {
        var flame = new FlameGraph<int> { LabelSelector = n => n.Label };
        var root = new FlameNode<int>(0, "root", 1_000_000);
        BuildFlame(root, 0, 7, 3, root.Weight, new Random(1));
        flame.Root = root;
        flame.HasFocus = true;
        return Padded(flame);
    }

    private static void BuildFlame(FlameNode<int> node, int depth, int maxDepth, int fanout, double weight, Random rng)
    {
        if (depth >= maxDepth) return;
        double remaining = weight * 0.9;
        for (int i = 0; i < fanout; i++)
        {
            double w = remaining / fanout;
            var child = node.Add(depth * 10 + i, Method(rng), w);
            BuildFlame(child, depth + 1, maxDepth, fanout, w, rng);
        }
    }

    private static readonly string[] MethodNames =
        { "Parse", "Read", "Write", "Encode", "Decode", "Match", "Hash", "Compile", "Serialize", "Lookup" };

    private static string Method(Random rng) => MethodNames[rng.Next(MethodNames.Length)];

    private static Widget Proportion(DemoContext ctx)
    {
        Color[] palette =
        {
            Colors.Hex("#6a9fb5"), Colors.Hex("#90a959"), Colors.Hex("#f4bf75"),
            Colors.Hex("#aa759f"), Colors.Hex("#ac4142"),
        };
        (string, double)[] parts = { ("String", 42), ("Byte[]", 23), ("Dictionary", 15), ("Node", 12), ("other", 8) };
        var bar = new ProportionBar { ShowInlineLabels = true };
        var legend = new Legend { Horizontal = true };
        double total = 0;
        foreach (var (_, v) in parts) total += v;
        for (int i = 0; i < parts.Length; i++)
        {
            bar.Segments.Add(new Segment(parts[i].Item1, parts[i].Item2, palette[i]));
            legend.Items.Add(new LegendItem(parts[i].Item1, palette[i], $"{100 * parts[i].Item2 / total:0}%"));
        }
        return new Stack(Direction.Vertical)
            .Add(new Label(StyledText.Of("heap composition").Bold().Fg(Theme.Current.Accent)), Constraint.Length(1))
            .Add(new Padding(bar, new Thickness(1, 0)), Constraint.Length(1))
            .Add(new Padding(legend, new Thickness(1, 0)), Constraint.Length(1))
            .Add(new Label(StyledText.Empty()), Constraint.Fill());
    }

    // ---- Live indicators ----

    private static Widget Progress(DemoContext ctx)
    {
        var download = new ProgressBar { LabelPlacement = LabelPlacement.Right, LabelType = ProgressLabel.Bytes, Total = 10_000_000, LabelWidth = 20 };
        var cpu = new ProgressBar { LabelPlacement = LabelPlacement.Right, LabelType = ProgressLabel.Percent };
        var mem = new ProgressBar { LabelPlacement = LabelPlacement.Right, LabelType = ProgressLabel.RateEta, Total = 100, LabelWidth = 22 };
        ctx.Tick += phase =>
        {
            double p = (phase * 0.15) % 1.0;
            download.Current = p * download.Total;
            cpu.Value = (p * 1.7) % 1.0;
            mem.Current = (0.3 + 0.4 * Math.Abs(Math.Sin(phase))) * mem.Total;
            mem.Rate = 500_000 + 1_500_000 * p;
        };
        return new Stack(Direction.Vertical)
            .Add(LabeledBar("download", download), Constraint.Length(1))
            .Add(LabeledBar("cpu", cpu), Constraint.Length(1))
            .Add(LabeledBar("memory", mem), Constraint.Length(1))
            .Add(new Label(StyledText.Empty()), Constraint.Fill());
    }

    private static Widget Spinners(DemoContext ctx)
    {
        var spinners = new[]
        {
            new Spinner(SpinnerFrames.Dots, "Dots"),
            new Spinner(SpinnerFrames.Line, "Line"),
            new Spinner(SpinnerFrames.Arc, "Arc"),
            new Spinner(SpinnerFrames.Bounce, "Bounce"),
            new Spinner(SpinnerFrames.Circle, "Circle"),
        };
        ctx.Tick += _ => { foreach (var s in spinners) s.Advance(); };
        var row = new Stack(Direction.Horizontal);
        foreach (var s in spinners) row.Add(new Padding(s, new Thickness(1, 0)), Constraint.Fill());
        return new Stack(Direction.Vertical)
            .Add(new Label(StyledText.Empty()), Constraint.Fill())
            .Add(new Padding(row, new Thickness(1, 0)), Constraint.Length(1))
            .Add(new Label(StyledText.Empty()), Constraint.Fill());
    }

    // ---- Input / layout / text ----

    private static Widget Input(DemoContext ctx)
    {
        var name = new Input { Placeholder = "type your name…" };
        var secret = new Input { Placeholder = "password", Mask = '•' };
        var echo = new Label(new StyledText("(your input echoes here)", Theme.Current.MutedStyle));
        name.OnChange = v => echo.Content = v.Length == 0
            ? new StyledText("(your input echoes here)", Theme.Current.MutedStyle)
            : StyledText.Of("Hello, ").Fg(Theme.Current.Foreground).Append(v).Bold().Fg(Theme.Current.Accent).Append("!").Fg(Theme.Current.Foreground);

        string[] types =
        {
            "System.String", "System.Byte[]", "System.Int32[]", "Dictionary<string,object>",
            "List<Frame>", "HashSet<long>", "StringBuilder", "System.Uri", "MemoryStream",
        };
        var filter = new Input { Placeholder = "filter types…" };
        var results = new Label(StyledText.Empty()) { Wrap = true };
        void Refilter(string q)
        {
            var st = StyledText.Empty();
            int shown = 0;
            foreach (var ty in types)
            {
                if (q.Length > 0 && ty.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                st.Append((shown++ > 0 ? "\n" : "") + "• ").Fg(Theme.Current.Muted).Append(ty).Fg(Theme.Current.Foreground);
            }
            results.Content = shown == 0 ? new StyledText("no matches", Theme.Current.MutedStyle) : st;
        }
        filter.OnChange = Refilter;
        Refilter("");

        var body = new Stack(Direction.Vertical)
            .Add(new Label(new StyledText("Name:", Theme.Current.MutedStyle)), Constraint.Length(1))
            .Add(new Panel(name), Constraint.Length(3))
            .Add(new Label(new StyledText("Password (masked):", Theme.Current.MutedStyle)), Constraint.Length(1))
            .Add(new Panel(secret), Constraint.Length(3))
            .Add(echo, Constraint.Length(1))
            .Add(new Rule(StyledText.Of("live filter (Tab to switch fields)")), Constraint.Length(1))
            .Add(new Panel(filter), Constraint.Length(3))
            .Add(new Padding(results, new Thickness(1, 0)), Constraint.Fill());
        body.HasFocus = true;
        return Padded(body);
    }

    private static Widget Panels(DemoContext ctx) =>
        Padded(new Stack(Direction.Horizontal)
            .Add(new Panel(CenteredNote("Rounded"), "rounded") { BorderStyle = BorderStyle.Rounded }, Constraint.Fill())
            .Add(new Panel(CenteredNote("Single"), "single") { BorderStyle = BorderStyle.Single }, Constraint.Fill())
            .Add(new Panel(CenteredNote("Double"), "double") { BorderStyle = BorderStyle.Double }, Constraint.Fill())
            .Add(new Panel(CenteredNote("Thick"), "thick") { BorderStyle = BorderStyle.Thick }, Constraint.Fill()));

    private static Widget OverlayDemo(DemoContext ctx) =>
        CenteredNote("Press ^P for a real modal palette — overlays dim the content beneath and\ncapture input until dismissed.");

    private static Widget TextDemo(DemoContext ctx)
    {
        var wrapped = new Label(StyledText.Of(
            "Tessera renders ").Fg(Theme.Current.Foreground)
            .Append("styled").Bold().Fg(Theme.Current.Accent)
            .Append(", ").Fg(Theme.Current.Foreground)
            .Append("multi-color").Fg(Theme.Current.Success)
            .Append(" text that word-wraps to any width. Wide glyphs 你好 and combining marks é are measured correctly, so alignment always holds.")
            .Fg(Theme.Current.Foreground)) { Wrap = true };

        // A clickable link embedded in a text line, with its own custom hover style.
        var status = new Label(new StyledText("(click the link)", Theme.Current.MutedStyle));
        var linkLine = new Label(
            StyledText.Of("Links live inside text too — visit ").Fg(Theme.Current.Foreground)
                .Append("the docs").Fg(Theme.Current.Accent).Underline()
                    .Link("docs", new Style(Theme.Current.Background, Theme.Current.Accent))
                .Append(" or ").Fg(Theme.Current.Foreground)
                .Append("the repo").Fg(Theme.Current.Info).Underline()
                    .Link("repo", new Style(Theme.Current.Background, Theme.Current.Info))
                .Append(".").Fg(Theme.Current.Foreground));
        linkLine.OnLinkClick = p => status.Content =
            StyledText.Of($"→ opened: {p}").Bold().Fg(Theme.Current.Accent);

        return new Stack(Direction.Vertical)
            .Add(new Rule(StyledText.Of("word wrap")), Constraint.Length(1))
            .Add(new Padding(wrapped, new Thickness(1, 1)), Constraint.Length(6))
            .Add(new Rule(StyledText.Of("inline links (hover + click)")), Constraint.Length(1))
            .Add(new Padding(linkLine, new Thickness(1, 0)), Constraint.Length(1))
            .Add(new Padding(status, new Thickness(1, 0)), Constraint.Length(1))
            .Add(new Label(StyledText.Empty()), Constraint.Fill());
    }

    private static Widget Gradient(DemoContext ctx) =>
        Padded(new Stack(Direction.Vertical)
            .Add(new Label(StyledText.Empty()), Constraint.Length(1))
            .Add(new GradientBar("█", Colors.Hex("#ac4142"), Colors.Hex("#f4bf75"), Colors.Hex("#90a959")), Constraint.Length(1))
            .Add(new Label(StyledText.Empty()), Constraint.Length(1))
            .Add(new GradientBar("█", Colors.Hex("#6a9fb5"), Colors.Hex("#aa759f")), Constraint.Length(1))
            .Add(new Label(StyledText.Empty()), Constraint.Length(1))
            .Add(new GradientBar("█", Colors.Hsl(0, 0.7, 0.5), Colors.Hsl(120, 0.7, 0.5), Colors.Hsl(240, 0.7, 0.5)), Constraint.Length(1))
            .Add(new Label(StyledText.Empty()), Constraint.Fill()));

    // ---- helpers ----

    private static Table MakeTable(params (string Header, Constraint Width, bool Right)[] columns)
    {
        var t = new Table { ShowHeader = true, SelectedIndex = 0, Striped = true, ShowScrollbar = true, Sortable = true };
        foreach (var (header, width, right) in columns)
        {
            t.Columns.Add(new Column(header, width, right ? Alignment.Right : Alignment.Left)
                { SortKey = right ? s => ParseNum(s) : null });
        }
        return t;
    }

    private static Widget Padded(Widget w) => new Padding(w, new Thickness(1, 1));

    private static Widget CenteredNote(string text) =>
        new Label(new StyledText(text, Theme.Current.MutedStyle), Justify.Center) { Wrap = true };

    private static Widget LabeledBar(string name, ProgressBar bar) =>
        new Stack(Direction.Horizontal)
            .Add(new Label(new StyledText(name.PadRight(10), Theme.Current.MutedStyle)), Constraint.Length(11))
            .Add(bar, Constraint.Fill());

    private static double ParseNum(string s)
    {
        return double.TryParse(s.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;
    }
}
