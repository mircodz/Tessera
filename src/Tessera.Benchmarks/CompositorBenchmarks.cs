using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Tessera.Primitives;
using Tessera.Rendering;

namespace Tessera.Benchmarks;

/// <summary>
/// Benchmarks the compositor's core loop: drawing into the back buffer and diffing it to
/// ANSI. These are the per-frame hot paths — allocations here become GC pressure that
/// shows up as stutter in a live TUI, so <see cref="MemoryDiagnoser"/> is on.
/// </summary>
[MemoryDiagnoser]
public class CompositorBenchmarks
{
    [Params(80, 200)]
    public int Width;

    [Params(24, 50)]
    public int Height;

    private Screen _screen = null!;
    private Screen _steady = null!;
    private readonly Style _style = new(Color.Rgb(200, 200, 200), Color.Rgb(20, 20, 30));

    [GlobalSetup]
    public void Setup()
    {
        _screen = new Screen(Width, Height, ColorDepth.TrueColor);

        // A screen already showing a full frame, so a re-render with the same content
        // measures the no-change diff path.
        _steady = new Screen(Width, Height, ColorDepth.TrueColor);
        PaintFrame(_steady.Back);
        _steady.ComputeDiff();
        PaintFrame(_steady.Back);
    }

    // Fills every cell with styled text — the worst case for the diff (all cells change).
    private void PaintFrame(Surface s)
    {
        for (int y = 0; y < s.Height; y++)
        {
            s.DrawText(0, y, new string('x', s.Width), _style);
        }
    }

    [Benchmark(Description = "Full-frame paint + diff (worst case)")]
    public int FullFrameDiff()
    {
        PaintFrame(_screen.Back);
        return _screen.ComputeDiff().Length;
    }

    [Benchmark(Description = "No-change diff (steady state)")]
    public int NoChangeDiff()
    {
        // Content identical to the front buffer: the diff should emit nothing.
        PaintFrame(_steady.Back);
        return _steady.ComputeDiff().Length;
    }

    [Benchmark(Description = "DrawText ASCII, one full row")]
    public void DrawTextAscii()
    {
        _screen.Back.DrawText(0, 0, new string('a', Width), _style);
    }

    [Benchmark(Description = "FillRect whole surface")]
    public void FillRectWhole()
    {
        _screen.Back.FillRect(_screen.Back.Bounds, _style);
    }
}
