using System;
using Tessera.Primitives;
using Tessera.Rendering;
using Tessera.Text;
using Tessera.Theming;

namespace Tessera.Widgets;

/// <summary>
/// A horizontal divider line, optionally with a centered or left-aligned label. Handy for
/// visually separating sections. Defaults its color to the ambient theme's border/muted.
/// </summary>
public sealed class Rule : Widget
{
    public StyledText? Label { get; set; }
    public BorderStyle LineStyle { get; set; } = BorderStyle.Single;
    public Justify LabelPosition { get; set; } = Justify.Center;
    public Style? Style { get; set; }

    public Rule(StyledText? label = null)
    {
        Label = label;
    }

    private Style EffectiveStyle => Style ?? Theme.Current.BorderStyle;

    public override void Render(Surface surface, Rect area)
    {
        if (area.IsEmpty)
        {
            return;
        }

        int y = area.Y;
        string h = LineStyle == BorderStyle.Thick ? "━"
                 : LineStyle == BorderStyle.Double ? "═" : "─";
        var lineStyle = EffectiveStyle;

        // Draw the full rule line first.
        for (int x = area.Left; x < area.Right; x++)
        {
            surface.Set(x, y, Cell.FromGrapheme(h, lineStyle));
        }

        if (Label is null)
        {
            return;
        }

        int labelWidth = Label.Width;
        if (labelWidth == 0 || labelWidth + 2 > area.Width)
        {
            return;
        }

        // Carve out a padded slot for the label and draw it over the line.
        int slotWidth = labelWidth + 2; // one space padding each side
        int startX = LabelPosition switch
        {
            Justify.Left => area.Left + 2,
            Justify.Right => area.Right - slotWidth - 2,
            _ => area.Left + (area.Width - slotWidth) / 2,
        };
        startX = Math.Max(area.Left, startX);

        surface.DrawText(startX, y, " ", lineStyle);
        TextRenderer.DrawLine(surface, startX + 1, y, labelWidth, Label, Justify.Left);
        surface.DrawText(startX + 1 + labelWidth, y, " ", lineStyle);
    }
}
