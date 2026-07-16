using System;

namespace Tessera.Widgets;

/// <summary>
/// An ambient seam for a widget to request a coalesced repaint from within itself, without
/// holding an <see cref="App"/> reference. Mirrors the <see cref="Theming.Theme.Current"/>
/// ambient: a running <see cref="App"/> wires its <see cref="App.Invalidate"/> in for the
/// duration of the loop and clears it on exit.
/// </summary>
/// <remarks>
/// Assumes one live <see cref="App"/> per process (true for a TUI). <see cref="Request"/> is a
/// null-check plus a delegate call — allocation-free and safe to call from any thread; the wired
/// <see cref="App.Invalidate"/> already marshals through the app's wake/coalesce path, so a burst
/// of requests collapses into a single frame.
/// </remarks>
public static class Repaint
{
    /// <summary>The active app's invalidate hook, or null when no app is pumping.</summary>
    internal static volatile Action? Callback;

    /// <summary>Requests a coalesced repaint. Safe from any thread. No-op if no app is running.</summary>
    public static void Request() => Callback?.Invoke();
}
