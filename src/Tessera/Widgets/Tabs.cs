using System;
using System.Collections.Generic;
using Tessera.Layout;
using Tessera.Primitives;
using Tessera.Rendering;
using Tessera.Terminal;
using Tessera.Theming;

namespace Tessera.Widgets;

/// <summary>One tab: a title and the widget shown when it is active.</summary>
public sealed class Tab
{
    public string Title { get; set; }
    public Widget Content { get; set; }

    public Tab(string title, Widget content)
    {
        Title = title;
        Content = content;
    }
}

/// <summary>
/// A tabbed container: a one-line tab bar above the active tab's content. Left/Right (or
/// Shift+Tab/Tab) cycle, number keys 1-9 jump, a click selects. The bar is drawn by
/// <see cref="RenderBar"/>, isolated from selection logic (a future <c>ITabsRenderer</c> seam).
/// </summary>
public sealed class Tabs : Widget
{
    private int _active;

    public List<Tab> Items { get; } = new();

    /// <summary>Records each tab header's x-range on the last render, for click hit-testing.</summary>
    private readonly List<(int start, int end, int index)> _hitRects = new();

    /// <summary>The row the tab bar was drawn on last render, for click hit-testing.</summary>
    private int _barRow = -1;

    public int ActiveIndex
    {
        get => _active;
        set
        {
            _active = Items.Count == 0 ? 0 : Math.Clamp(value, 0, Items.Count - 1);
            UpdateContentFocus();
        }
    }

    // Keeps only the active tab's content focused, so a ScrollView/Input inside it receives
    // keyboard input while inactive tabs' widgets stay dormant.
    private void UpdateContentFocus()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].Content.HasFocus = i == _active;
        }
    }

    public Tab? Active => Items.Count == 0 ? null : Items[_active];

    public override bool IsFocusable => true;

    public Tabs Add(string title, Widget content)
    {
        Items.Add(new Tab(title, content));
        UpdateContentFocus(); // keep the active tab's content focused as items are added
        return this;
    }

    public override void Render(Surface surface, Rect area)
    {
        if (area.IsEmpty || Items.Count == 0)
        {
            return;
        }

        var rows = LayoutSolver.Split(area, Direction.Vertical,
            Constraint.Length(1), Constraint.Fill());
        var barArea = rows[0];
        var contentArea = rows[1];

        RenderBar(surface, barArea);

        var content = Items[_active].Content;
        if (!contentArea.IsEmpty)
        {
            surface.SetClip(contentArea);
            content.Render(surface, contentArea);
            surface.ResetClip();
        }
    }

    // --- Isolated bar renderer (future ITabsRenderer seam). tmux-style. ---
    private void RenderBar(Surface surface, Rect area)
    {
        _hitRects.Clear();
        _barRow = area.Y;
        var theme = Theme.Current;
        var inactive = theme.MutedStyle;
        var active = new Style(theme.SelectionForeground, theme.Accent).Bold;

        int x = area.X;
        for (int i = 0; i < Items.Count && x < area.Right; i++)
        {
            bool isActive = i == _active;
            // tmux-style label: " index:title " with the active one on an accent field.
            string label = $" {i + 1}:{Items[i].Title} ";
            var style = isActive ? active : inactive;

            int start = x;
            int drawn = surface.DrawText(x, area.Y, label, style);
            _hitRects.Add((start, drawn, i));
            x = drawn;

            // A subtle separator between segments.
            if (i < Items.Count - 1 && x < area.Right)
            {
                surface.Set(x, area.Y, Cell.FromGrapheme("│", theme.BorderStyle));
                x++;
            }
        }

        // Fill the rest of the bar row so it reads as one continuous strip.
        for (int fx = x; fx < area.Right; fx++)
        {
            surface.Set(fx, area.Y, Cell.Blank(theme.TextStyle));
        }
    }

    public override bool OnEvent(InputEvent e)
    {
        switch (e)
        {
            case MouseEvent { Kind: MouseEventKind.Down } click:
                return HandleClick(click);

            case MouseEvent mouse:
                // Wheel/move/drag go straight to the active content (e.g. ScrollView).
                return Active?.Content.OnEvent(mouse) ?? false;

            case KeyEvent key:
                // The active content gets first refusal so a focused Input or ScrollView can
                // claim arrows, digits, etc. Only keys it ignores fall through to tab nav.
                if (Active?.Content.OnEvent(key) == true)
                {
                    return true;
                }

                return HandleNavKey(key);
        }
        return false;
    }

    private bool HandleNavKey(KeyEvent key)
    {
        switch (key.Key)
        {
            case Key.Tab:
            case Key.Right:
                ActiveIndex = (_active + 1) % Math.Max(1, Items.Count);
                return true;
            case Key.Left:
                ActiveIndex = (_active - 1 + Items.Count) % Math.Max(1, Items.Count);
                return true;
        }
        // Number keys 1-9 jump directly to a tab.
        if (key.IsChar && key.Rune.Value is >= '1' and <= '9')
        {
            int idx = key.Rune.Value - '1';
            if (idx < Items.Count) { ActiveIndex = idx; return true; }
        }
        return false;
    }

    private bool HandleClick(MouseEvent m)
    {
        // Only clicks on the tab bar's row select a tab; clicks in the content area below
        // must fall through to the active tab's content.
        if (m.Y != _barRow)
        {
            return Active?.Content.OnEvent(m) ?? false;
        }

        foreach (var (start, end, index) in _hitRects)
        {
            if (m.X >= start && m.X < end)
            {
                ActiveIndex = index;
                return true;
            }
        }
        return false;
    }
}
