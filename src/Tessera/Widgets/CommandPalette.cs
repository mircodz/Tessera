using System;
using System.Collections.Generic;
using Tessera.Layout;
using Tessera.Primitives;
using Tessera.Rendering;
using Tessera.Terminal;
using Tessera.Text;
using Tessera.Theming;

namespace Tessera.Widgets;

/// <summary>A selectable command in a <see cref="CommandPalette"/>.</summary>
public sealed class Command
{
    /// <summary>Primary label, matched against the query and shown in the list.</summary>
    public string Title { get; set; }

    /// <summary>Optional secondary text (e.g. a category or keybinding hint) shown dimmed.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Invoked when the command is chosen.</summary>
    public Action Action { get; set; }

    public Command(string title, Action action, string? subtitle = null)
    {
        Title = title;
        Action = action;
        Subtitle = subtitle;
    }
}

/// <summary>
/// A fuzzy-filtered command list with a query box (Ctrl+P / Cmd+K). Type to filter via
/// <see cref="FuzzyMatcher"/>, ↑/↓ to move, Enter to run. A pure widget — drop it into an
/// <see cref="Overlay"/> for the full palette; it manages its own query, filtering, and selection.
/// </summary>
public sealed class CommandPalette : Widget
{
    private readonly List<Command> _commands = new();
    private readonly Input _input = new() { Placeholder = "Type a command…" };
    private List<Scored> _filtered = new();
    private readonly List<int> _matchBuffer = new(); // reused for per-row highlight indices
    private int _selected;
    private int _scroll;

    // Per visible row, its screen y and the filtered-command index, for mouse hit-testing.
    private readonly List<(int y, int index)> _rowHits = new();

    private readonly record struct Scored(Command Command, int Score);

    /// <summary>Invoked after a command's action runs (e.g. to dismiss the hosting overlay).</summary>
    public Action? OnRun { get; set; }

    /// <summary>Invoked when the user cancels via Enter on an empty list, etc. (optional).</summary>
    public Action? OnCancel { get; set; }

    public override bool IsFocusable => true;

    public CommandPalette(IEnumerable<Command>? commands = null)
    {
        if (commands is not null)
        {
            _commands.AddRange(commands);
        }

        _input.OnChange = _ => Refilter();
        Refilter();
    }

    public CommandPalette Add(string title, Action action, string? subtitle = null)
    {
        _commands.Add(new Command(title, action, subtitle));
        Refilter();
        return this;
    }

    /// <summary>The current query text.</summary>
    public string Query => _input.Text;

    /// <summary>The currently highlighted command, or null when nothing matches.</summary>
    public Command? Selected =>
        _selected >= 0 && _selected < _filtered.Count ? _filtered[_selected].Command : null;

    private void Refilter()
    {
        var results = new List<Scored>();
        string q = _input.Text;
        foreach (var cmd in _commands)
        {
            // Zero-alloc scoring pass over all commands; matched indices are computed later,
            // only for the handful of rows actually rendered.
            if (FuzzyMatcher.Score(q, cmd.Title, out int score))
            {
                results.Add(new Scored(cmd, score));
            }
        }
        // Highest score first; stable by original order for ties.
        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        _filtered = results;
        _selected = 0;
        _scroll = 0;
    }

    public override void Render(Surface surface, Rect area)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var theme = Theme.Current;

        // Frame the palette in a themed panel; the query box sits on the first inner row,
        // a divider below it, then the results list fills the rest.
        var bg = theme.Background;
        var panel = new Panel(null, null)
        {
            BorderStyle = BorderStyle.Rounded,
            Background = bg,
            BorderColor = new Style(theme.Border, bg),
        };
        panel.Render(surface, area);

        var inner = Border.Inner(area);
        if (inner.IsEmpty)
        {
            return;
        }

        var rows = LayoutSolver.Split(inner, Direction.Vertical,
            Constraint.Length(1), Constraint.Length(1), Constraint.Fill());
        var queryRow = rows[0];
        var dividerRow = rows[1];
        var listRow = rows[2];

        // Query box: a prompt glyph + the input. All styled on the panel background so the
        // cells don't fall back to the terminal's default background.
        surface.DrawText(queryRow.X, queryRow.Y, "❯ ", new Style(theme.Accent, bg));
        _input.HasFocus = HasFocus;
        _input.TextStyle = new Style(theme.Foreground, bg);
        _input.PlaceholderStyle = new Style(theme.Muted, bg);
        _input.Render(surface, new Rect(queryRow.X + 2, queryRow.Y, queryRow.Width - 2, 1));

        new Rule { Style = new Style(theme.Border, bg) }.Render(surface, dividerRow);

        RenderList(surface, listRow, theme, bg);
    }

    private void RenderList(Surface surface, Rect area, Theme theme, Color bg)
    {
        if (area.IsEmpty)
        {
            return;
        }

        if (_filtered.Count == 0)
        {
            var none = new StyledText("No matching commands", new Style(theme.Muted, bg));
            TextRenderer.DrawLine(surface, area.X, area.Y, area.Width, none, Justify.Left);
            return;
        }

        EnsureVisible(area.Height);
        _rowHits.Clear();

        int rows = Math.Min(area.Height, _filtered.Count - _scroll);
        for (int i = 0; i < rows; i++)
        {
            int index = _scroll + i;
            var scored = _filtered[index];
            int y = area.Y + i;
            bool selected = index == _selected;
            _rowHits.Add((y, index));

            // Non-selected rows sit on the panel background; selected rows on the accent.
            var rowStyle = selected ? theme.SelectionStyle : new Style(theme.Foreground, bg);
            surface.FillRect(new Rect(area.X, y, area.Width, 1), rowStyle);

            // Compute matched indices for THIS visible row only, into the reused buffer.
            FuzzyMatcher.MatchInto(_input.Text, scored.Command.Title, _matchBuffer, out _);

            // Build the title with matched characters emphasized.
            var title = HighlightMatch(scored.Command.Title, _matchBuffer,
                baseStyle: rowStyle,
                matchColor: selected ? theme.SelectionForeground : theme.Accent,
                bold: true);

            int x = area.X + 1;
            surface.SetClip(new Rect(x, y, area.Width - 1, 1));
            TextRenderer.DrawLine(surface, x, y, area.Width - 1, title, Justify.Left);

            // Subtitle right-aligned and dimmed.
            if (!string.IsNullOrEmpty(scored.Command.Subtitle))
            {
                var subStyle = selected ? rowStyle : new Style(theme.Muted, bg);
                var sub = new StyledText(scored.Command.Subtitle, subStyle);
                TextRenderer.DrawLine(surface, x, y, area.Width - 2, sub, Justify.Right);
            }
            surface.ResetClip();
        }
    }

    // Emits the title as styled text with the matched indices recolored/bolded.
    private static StyledText HighlightMatch(string title, IReadOnlyList<int> indices,
        Style baseStyle, Color matchColor, bool bold)
    {
        var result = StyledText.Empty();
        int mi = 0;
        for (int i = 0; i < title.Length; i++)
        {
            bool isMatch = mi < indices.Count && indices[mi] == i;
            if (isMatch)
            {
                mi++;
            }

            var style = isMatch
                ? baseStyle.WithForeground(matchColor).AddAttributes(bold ? TextAttributes.Bold : TextAttributes.None)
                : baseStyle;
            result.Append(title[i].ToString(), style);
        }
        return result;
    }

    public override bool OnEvent(InputEvent e)
    {
        if (!HasFocus)
        {
            return false;
        }

        // Mouse: hovering a row selects it; clicking a row runs it.
        if (e is MouseEvent m)
        {
            for (int i = 0; i < _rowHits.Count; i++)
            {
                if (_rowHits[i].y == m.Y)
                {
                    if (m.Kind == MouseEventKind.Move)
                    {
                        if (_selected != _rowHits[i].index) { _selected = _rowHits[i].index; return true; }
                        return false;
                    }
                    if (m.Kind == MouseEventKind.Down)
                    {
                        _selected = _rowHits[i].index;
                        RunSelected();
                        return true;
                    }
                }
            }
            return false;
        }

        if (e is not KeyEvent key)
        {
            return false;
        }

        switch (key.Key)
        {
            case Key.Up: Move(-1); return true;
            case Key.Down: Move(1); return true;
            case Key.PageUp: Move(-5); return true;
            case Key.PageDown: Move(5); return true;
            case Key.Enter:
                RunSelected();
                return true;
        }

        // Everything else (typing, backspace, cursor) drives the query box.
        _input.HasFocus = true; // the query box is always the focus target within the palette
        bool used = _input.OnEvent(key);
        return used;
    }

    private void RunSelected()
    {
        var cmd = Selected;
        if (cmd is null) { OnCancel?.Invoke(); return; }
        cmd.Action();
        OnRun?.Invoke();
    }

    private void Move(int delta)
    {
        if (_filtered.Count == 0)
        {
            return;
        }

        _selected = Math.Clamp(_selected + delta, 0, _filtered.Count - 1);
    }

    private void EnsureVisible(int viewport)
    {
        if (viewport <= 0)
        {
            return;
        }

        if (_selected < _scroll)
        {
            _scroll = _selected;
        }
        else if (_selected >= _scroll + viewport)
        {
            _scroll = _selected - viewport + 1;
        }

        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _filtered.Count - 1));
    }
}
