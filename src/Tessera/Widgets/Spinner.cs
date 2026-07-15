using Tessera.Primitives;
using Tessera.Rendering;
using Tessera.Text;
using Tessera.Theming;

namespace Tessera.Widgets;

/// <summary>
/// A named set of spinner animation frames (each usually one cell). Built-in sets cover the
/// common terminal spinner looks.
/// </summary>
public sealed class SpinnerFrames
{
    public string[] Frames { get; }

    public SpinnerFrames(params string[] frames) => Frames = frames;

    /// <summary>The classic braille dot spinner (smooth, single-cell).</summary>
    public static SpinnerFrames Dots { get; } =
        new("⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏");

    /// <summary>A rotating ASCII line — works on any terminal.</summary>
    public static SpinnerFrames Line { get; } = new("|", "/", "-", "\\");

    /// <summary>A rotating arc.</summary>
    public static SpinnerFrames Arc { get; } =
        new("◜", "◠", "◝", "◞", "◡", "◟");

    /// <summary>A bouncing braille dot.</summary>
    public static SpinnerFrames Bounce { get; } =
        new("⠁", "⠂", "⠄", "⠂");

    /// <summary>A rotating quarter-filled circle.</summary>
    public static SpinnerFrames Circle { get; } =
        new("◐", "◓", "◑", "◒");
}

/// <summary>
/// An animated spinner for indeterminate work. Cycles a <see cref="SpinnerFrames"/> set; call
/// <see cref="Advance"/> once per tick. An optional label is drawn to the right.
/// </summary>
public sealed class Spinner : Widget
{
    private int _frame;

    public SpinnerFrames Frames { get; set; }

    /// <summary>Optional label shown after the spinner glyph (e.g. "Scanning heap…").</summary>
    public string? Label { get; set; }

    /// <summary>Spinner glyph color. Null uses the theme accent.</summary>
    public Color? Color { get; set; }

    public Spinner(SpinnerFrames? frames = null, string? label = null)
    {
        Frames = frames ?? SpinnerFrames.Dots;
        Label = label;
    }

    /// <summary>Advances to the next animation frame (call once per tick).</summary>
    public void Advance()
    {
        if (Frames.Frames.Length == 0)
        {
            return;
        }

        _frame = (_frame + 1) % Frames.Frames.Length;
    }

    /// <summary>Resets to the first frame.</summary>
    public void Reset() => _frame = 0;

    /// <summary>The current frame glyph.</summary>
    public string CurrentFrame =>
        Frames.Frames.Length == 0 ? " " : Frames.Frames[_frame % Frames.Frames.Length];

    public override Size Measure(Size available) => new(available.Width, 1);

    public override void Render(Surface surface, Rect area)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var theme = Theme.Current;
        var glyphStyle = new Style(Color ?? theme.Accent, Primitives.Color.Default);

        int x = surface.DrawText(area.X, area.Y, CurrentFrame, glyphStyle);

        if (!string.IsNullOrEmpty(Label))
        {
            x = surface.DrawText(x, area.Y, " ", glyphStyle);
            var labelStyle = new Style(theme.Foreground, Primitives.Color.Default);
            var remaining = area.Right - x;
            if (remaining > 0)
            {
                surface.SetClip(new Rect(x, area.Y, remaining, 1));
                TextRenderer.DrawLine(surface, x, area.Y, remaining,
                    new StyledText(Label, labelStyle), Justify.Left);
                surface.ResetClip();
            }
        }
    }
}
