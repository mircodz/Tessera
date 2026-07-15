using System.Collections.Generic;
using Tessera.Primitives;

namespace Tessera.Theming;

/// <summary>Built-in themes. Modern, restrained palettes tuned for truecolor terminals.</summary>
public static class BuiltIn
{
    /// <summary>
    /// The base16 "default dark" palette (Chris Kempson). This is Tessera's default theme.
    /// Roles are mapped onto the scheme's normal/bright colors: blue is the accent, magenta
    /// the secondary, bright-black the muted/border tone.
    /// </summary>
    public static Theme Base16Dark { get; } = new()
    {
        Name = "base16-dark",
        Foreground = Colors.Hex("#d0d0d0"),   // normal white
        Background = Colors.Hex("#151515"),   // primary background / normal black
        Accent = Colors.Hex("#6a9fb5"),       // blue
        Secondary = Colors.Hex("#aa759f"),    // magenta
        Muted = Colors.Hex("#505050"),        // bright black
        Border = Colors.Hex("#505050"),       // bright black
        SelectionForeground = Colors.Hex("#151515"),
        SelectionBackground = Colors.Hex("#6a9fb5"),
        Success = Colors.Hex("#90a959"),      // green
        Warning = Colors.Hex("#f4bf75"),      // yellow
        Error = Colors.Hex("#ac4142"),        // red
        Info = Colors.Hex("#75b5aa"),         // cyan
    };

    /// <summary>A modern dark theme with a cyan accent.</summary>
    public static Theme Dark { get; } = new()
    {
        Name = "dark",
        Foreground = Colors.Hex("#e6e6e6"),
        Background = Colors.Hex("#1a1b26"),
        Accent = Colors.Hex("#7aa2f7"),
        Secondary = Colors.Hex("#bb9af7"),
        Muted = Colors.Hex("#565f89"),
        Border = Colors.Hex("#3b4261"),
        SelectionForeground = Colors.Hex("#1a1b26"),
        SelectionBackground = Colors.Hex("#7aa2f7"),
        Success = Colors.Hex("#9ece6a"),
        Warning = Colors.Hex("#e0af68"),
        Error = Colors.Hex("#f7768e"),
        Info = Colors.Hex("#7dcfff"),
    };

    /// <summary>A clean light theme with an indigo accent.</summary>
    public static Theme Light { get; } = new()
    {
        Name = "light",
        Foreground = Colors.Hex("#343b58"),
        Background = Colors.Hex("#f5f5fa"),
        Accent = Colors.Hex("#3d59a1"),
        Secondary = Colors.Hex("#8c4351"),
        Muted = Colors.Hex("#9699a3"),
        Border = Colors.Hex("#c4c8da"),
        SelectionForeground = Colors.Hex("#f5f5fa"),
        SelectionBackground = Colors.Hex("#3d59a1"),
        Success = Colors.Hex("#385f0d"),
        Warning = Colors.Hex("#8f5e15"),
        Error = Colors.Hex("#8c4351"),
        Info = Colors.Hex("#0f4b6e"),
    };

    /// <summary>A high-contrast, terminal-16-color-safe theme for maximum compatibility.</summary>
    public static Theme HighContrast { get; } = new()
    {
        Name = "high-contrast",
        Foreground = Color.BrightWhite,
        Background = Color.Black,
        Accent = Color.BrightCyan,
        Secondary = Color.BrightMagenta,
        Muted = Color.BrightBlack,
        Border = Color.White,
        SelectionForeground = Color.Black,
        SelectionBackground = Color.BrightCyan,
        Success = Color.BrightGreen,
        Warning = Color.BrightYellow,
        Error = Color.BrightRed,
        Info = Color.BrightBlue,
    };

    /// <summary>Nord — calm arctic blue-grey with muted cyan/blue accents.</summary>
    public static Theme Nord { get; } = new()
    {
        Name = "nord",
        Foreground = Colors.Hex("#d8dee9"),
        Background = Colors.Hex("#2e3440"),
        Accent = Colors.Hex("#88c0d0"),       // frost cyan (primary)
        Secondary = Colors.Hex("#81a1c1"),    // frost blue
        Muted = Colors.Hex("#4c566a"),        // polar night 3
        Border = Colors.Hex("#434c5e"),       // panel tone
        SelectionForeground = Colors.Hex("#2e3440"),
        SelectionBackground = Colors.Hex("#88c0d0"),
        Success = Colors.Hex("#a3be8c"),      // aurora green
        Warning = Colors.Hex("#ebcb8b"),      // aurora yellow
        Error = Colors.Hex("#bf616a"),        // aurora red
        Info = Colors.Hex("#b48ead"),         // aurora purple (accent)
    };

    /// <summary>Gruvbox — warm retro earth tones (browns, oranges, greens) on dark.</summary>
    public static Theme Gruvbox { get; } = new()
    {
        Name = "gruvbox",
        Foreground = Colors.Hex("#fbf1c7"),
        Background = Colors.Hex("#282828"),
        Accent = Colors.Hex("#85a598"),       // aqua-grey (primary)
        Secondary = Colors.Hex("#a89a85"),    // warm grey
        Muted = Colors.Hex("#665c54"),        // bg3
        Border = Colors.Hex("#504945"),       // bg2 / panel
        SelectionForeground = Colors.Hex("#282828"),
        SelectionBackground = Colors.Hex("#fabd2f"),
        Success = Colors.Hex("#b8bb26"),      // green
        Warning = Colors.Hex("#fe8019"),      // orange
        Error = Colors.Hex("#fb4934"),        // red
        Info = Colors.Hex("#fabd2f"),         // yellow (accent)
    };

    /// <summary>Catppuccin Mocha — soft pastels (lavender, pink, peach) on deep navy.</summary>
    public static Theme CatppuccinMocha { get; } = new()
    {
        Name = "catppuccin-mocha",
        Foreground = Colors.Hex("#cdd6f4"),
        Background = Colors.Hex("#181825"),
        Accent = Colors.Hex("#f5c2e7"),       // pink (primary)
        Secondary = Colors.Hex("#cba6f7"),    // mauve
        Muted = Colors.Hex("#585b70"),        // surface2
        Border = Colors.Hex("#45475a"),       // surface1 / panel
        SelectionForeground = Colors.Hex("#181825"),
        SelectionBackground = Colors.Hex("#f5c2e7"),
        Success = Colors.Hex("#a6e3a1"),      // green
        Warning = Colors.Hex("#fae3b0"),      // yellow
        Error = Colors.Hex("#f38ba8"),        // red
        Info = Colors.Hex("#fab387"),         // peach (accent)
    };

    /// <summary>Dracula — vivid purple/pink/green on dark grey, high energy.</summary>
    public static Theme Dracula { get; } = new()
    {
        Name = "dracula",
        Foreground = Colors.Hex("#f8f8f2"),
        Background = Colors.Hex("#282a36"),
        Accent = Colors.Hex("#bd93f9"),       // purple (primary)
        Secondary = Colors.Hex("#6272a4"),    // comment blue
        Muted = Colors.Hex("#6272a4"),        // comment
        Border = Colors.Hex("#44475a"),       // current line / panel
        SelectionForeground = Colors.Hex("#282a36"),
        SelectionBackground = Colors.Hex("#bd93f9"),
        Success = Colors.Hex("#50fa7b"),      // green
        Warning = Colors.Hex("#ffb86c"),      // orange
        Error = Colors.Hex("#ff5555"),        // red
        Info = Colors.Hex("#ff79c6"),         // pink (accent)
    };

    /// <summary>Solarized Dark — Ethan Schoonover's classic low-contrast teal/beige on deep blue-green.</summary>
    public static Theme SolarizedDark { get; } = new()
    {
        Name = "solarized-dark",
        Foreground = Colors.Hex("#839496"),   // base0
        Background = Colors.Hex("#002b36"),   // base03
        Accent = Colors.Hex("#268bd2"),       // blue (primary)
        Secondary = Colors.Hex("#2aa198"),    // cyan
        Muted = Colors.Hex("#586e75"),        // base01
        Border = Colors.Hex("#073642"),       // base02 / panel
        SelectionForeground = Colors.Hex("#fdf6e3"),
        SelectionBackground = Colors.Hex("#073642"),
        Success = Colors.Hex("#859900"),      // green
        Warning = Colors.Hex("#cb4b16"),      // orange
        Error = Colors.Hex("#dc322f"),        // red
        Info = Colors.Hex("#6c71c4"),         // violet (accent)
    };

    /// <summary>Solarized Light — the same palette on the warm beige background.</summary>
    public static Theme SolarizedLight { get; } = new()
    {
        Name = "solarized-light",
        Foreground = Colors.Hex("#586e75"),   // base01
        Background = Colors.Hex("#fdf6e3"),   // base3
        Accent = Colors.Hex("#268bd2"),       // blue (primary)
        Secondary = Colors.Hex("#2aa198"),    // cyan
        Muted = Colors.Hex("#93a1a1"),        // base1
        Border = Colors.Hex("#eee8d5"),       // base2 / panel
        SelectionForeground = Colors.Hex("#fdf6e3"),
        SelectionBackground = Colors.Hex("#268bd2"),
        Success = Colors.Hex("#859900"),      // green
        Warning = Colors.Hex("#cb4b16"),      // orange
        Error = Colors.Hex("#dc322f"),        // red
        Info = Colors.Hex("#6c71c4"),         // violet (accent)
    };

    /// <summary>All built-in themes, in a stable order (useful for a theme picker/cycle).</summary>
    public static IReadOnlyList<Theme> All { get; } = new[]
    {
        Base16Dark, Dark, Light, HighContrast,
        Nord, Gruvbox, CatppuccinMocha, Dracula, SolarizedDark, SolarizedLight,
    };
}
