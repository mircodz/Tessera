using System;
using Tessera.Charts;
using Tessera.Primitives;
using Tessera.Rendering;

namespace Tessera.Widgets;

/// <summary>
/// A minimal debug HUD: FPS and frame-time sparklines plus per-frame allocation and diff size.
/// Fed by the <see cref="App"/> loop via <see cref="PushFrame"/>. Fixed light-gray-on-black
/// styling so it stands out over any theme.
/// </summary>
public sealed class DebugPanel : Widget
{
    private static readonly Color Bg = Color.Rgb(200, 200, 200);
    private static readonly Color Fg = Color.Rgb(0, 0, 0);

    private readonly Sparkline _fps = new() { Color = Fg, BaselineZero = true };
    private readonly Sparkline _frame = new() { Color = Fg, BaselineZero = true };

    private double _lastFps;
    private double _lastFrameMs;
    private long _lastAllocBytes;
    private int _lastDiffLen;

    /// <summary>Records one frame's stats: render duration, bytes allocated, and diff length.</summary>
    public void PushFrame(double frameMs, long allocBytes, int diffLen)
    {
        _lastFrameMs = frameMs;
        _lastFps = frameMs > 0 ? 1000.0 / frameMs : 0;
        _lastAllocBytes = allocBytes;
        _lastDiffLen = diffLen;
        _fps.Push(_lastFps);
        _frame.Push(frameMs);
    }

    public override void Render(Surface surface, Rect area)
    {
        if (area.Width < 12 || area.Height < 8)
        {
            return;
        }

        var style = new Style(Fg, Bg);
        surface.FillRect(area, style);

        int x = area.X + 1;
        int right = area.Right - 1;
        int w = right - x;
        int y = area.Y;

        // Headline numbers.
        surface.DrawText(x, y, $"FPS {_lastFps,5:0.0}   {_lastFrameMs,5:0.00}ms", style);
        y += 1;

        // FPS sparkline band (label row + multi-row chart).
        surface.DrawText(x, y, "fps", style);
        y += 1;
        int fpsRows = Math.Max(1, (area.Bottom - y - 5));
        _fps.Render(surface, new Rect(x, y, w, fpsRows));
        y += fpsRows;

        // Frame-time sparkline band.
        surface.DrawText(x, y, "frame ms", style);
        y += 1;
        int frameRows = Math.Max(1, area.Bottom - y - 1);
        _frame.Render(surface, new Rect(x, y, w, frameRows));
        y += frameRows;

        // Footer: allocation + diff size for the frame.
        surface.DrawText(x, y, $"alloc {Bytes(_lastAllocBytes)}  diff {Bytes(_lastDiffLen)}", style);
    }

    private static string Bytes(long n)
    {
        if (n < 1024)
        {
            return $"{n}B";
        }
        if (n < 1024 * 1024)
        {
            return $"{n / 1024.0:0.0}K";
        }
        return $"{n / (1024.0 * 1024.0):0.0}M";
    }
}
