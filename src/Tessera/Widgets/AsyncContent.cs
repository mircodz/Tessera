using System;
using System.Threading;
using System.Threading.Tasks;
using Tessera.Primitives;
using Tessera.Rendering;
using Tessera.Terminal;
using Tessera.Text;
using Tessera.Theming;

namespace Tessera.Widgets;

/// <summary>
/// Loads an expensive widget off the UI thread without the consumer hand-rolling threading. On the
/// first render it paints a placeholder (a <see cref="Spinner"/> by default) and never blocks; a
/// user-supplied factory runs on a background <see cref="Task"/> exactly once (started lazily on
/// first render, so a lazy tab never shown does no work); when it completes the produced widget is
/// swapped in and a repaint is requested. On failure it shows an error widget.
/// </summary>
/// <remarks>
/// Performance posture (Tessera keeps the render path allocation-free):
/// <list type="bullet">
/// <item><b>No per-frame allocation</b> in Render/Measure/OnEvent — they read one cached reference
/// (<c>_loaded ?? _placeholder</c>) and delegate; no closures/LINQ per frame.</item>
/// <item><b>No polling</b> — completion pushes a single <see cref="Repaint.Request"/> through the
/// app's existing coalesced wake path; nothing spins waiting.</item>
/// <item><b>Single-handoff thread-safety</b> — the worker writes <c>_loaded</c> once (volatile) and
/// requests one repaint; the render thread reads that one reference. No lock is held across Render,
/// and the worker touches no other shared widget state.</item>
/// <item><b>Lazy + once</b> via <see cref="Interlocked"/>, even under re-entrant renders.</item>
/// <item><b>Cheap, correct cancellation</b> — unmount/dispose cancels the token; a late completer
/// that finds itself cancelled neither publishes nor repaints.</item>
/// </list>
/// </remarks>
public sealed class AsyncContent : Widget, IDisposable
{
    // The single swap point: the render thread reads this; the worker writes it exactly once.
    private volatile Widget? _loaded;

    private readonly Widget _placeholder;
    private readonly Func<CancellationToken, Task<Widget>> _factory; // normalized to the async form
    private readonly Func<Exception, Widget> _onError;
    private readonly CancellationTokenSource _cts = new();

    // 0 = not started, 1 = started. CompareExchange makes the launch lazy and exactly-once.
    private int _started;
    private bool _disposed;

    // The app captured at mount, used to mount the produced child once it is published (so a nested
    // Spinner/AsyncContent inside the loaded widget starts its own lifecycle). Volatile: written on
    // the UI thread at mount, read by the worker at publish. Null if not mounted yet.
    private volatile App? _mountedApp;

    /// <summary>Wraps a synchronous factory that builds the widget on a background thread.</summary>
    /// <param name="factory">Builds the real widget; honor the token to stop early on navigate-away.</param>
    /// <param name="placeholder">Shown while loading. Default: an auto-animating <see cref="Spinner"/>.</param>
    /// <param name="onError">Renders a widget for a factory exception. Default: a themed error label.</param>
    public AsyncContent(
        Func<CancellationToken, Widget> factory,
        Widget? placeholder = null,
        Func<Exception, Widget>? onError = null)
        : this(Wrap(factory), placeholder, onError)
    {
    }

    /// <summary>Wraps a naturally-async factory (e.g. one that awaits I/O).</summary>
    public AsyncContent(
        Func<CancellationToken, Task<Widget>> factory,
        Widget? placeholder = null,
        Func<Exception, Widget>? onError = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _placeholder = placeholder ?? new Spinner(SpinnerFrames.Dots, "Loading…") { AutoAnimate = true };
        _onError = onError ?? DefaultError;
    }

    // Runs a synchronous factory on the thread pool as a Task, so both ctors share one code path.
    private static Func<CancellationToken, Task<Widget>> Wrap(Func<CancellationToken, Widget> factory)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        return ct => Task.Run(() => factory(ct), ct);
    }

    /// <summary>The widget currently shown: the loaded child once ready, else the placeholder.</summary>
    private Widget Current => _loaded ?? _placeholder;

    /// <summary>True once the factory has produced a widget (or an error widget) and it is live.</summary>
    public bool IsLoaded => _loaded is not null;

    // ---- Lifecycle ----

    protected override void OnMount(App app)
    {
        _mountedApp = app;
        // Mount the placeholder so a self-animating spinner starts; the real child is mounted when
        // it is published (see Publish).
        _placeholder.Mount(app);
    }

    protected override void OnUnmount()
    {
        // Navigating away / app teardown: cancel the background work so a cooperative factory stops.
        _cts.Cancel();
        _mountedApp = null;
    }

    protected override void VisitChildren(Action<Widget> visit)
    {
        // Propagate lifecycle to whatever is current. During the initial mount pass _loaded is null,
        // so only the placeholder is visited (also handled directly in OnMount); at unmount this
        // reaches the loaded child if one exists.
        var loaded = _loaded;
        if (loaded is not null)
        {
            visit(loaded);
        }
    }

    // ---- Rendering / measuring / input all delegate to the current widget ----

    public override Size Measure(Size available) => Current.Measure(available);

    public override void Render(Surface surface, Rect area)
    {
        EnsureStarted();
        Current.Render(surface, area);
    }

    public override bool OnEvent(InputEvent e)
    {
        // While loading, the placeholder is inert (a spinner ignores input), so a loading tab does
        // not trap focus/keys. Once loaded, the real child handles input.
        return _loaded?.OnEvent(e) ?? false;
    }

    /// <summary>Focus flows to the loaded child once ready; while loading there is nothing focusable.</summary>
    public override bool HasFocus
    {
        get => _loaded?.HasFocus ?? false;
        set
        {
            if (_loaded is { } loaded)
            {
                loaded.HasFocus = value;
            }
        }
    }

    public override bool IsFocusable => _loaded?.IsFocusable ?? false;

    // Kicks off the background factory exactly once, on the first render (when actually visible).
    private void EnsureStarted()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return; // already started (guards re-entrant/repeat renders)
        }

        var token = _cts.Token;
        // Fire-and-forget: the continuation publishes on completion. We never await on the UI thread.
        _ = RunAsync(token);
    }

    private async Task RunAsync(CancellationToken token)
    {
        Widget produced;
        try
        {
            produced = await _factory(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return; // cancelled: do not publish or repaint
        }
        catch (Exception ex)
        {
            produced = _onError(ex);
        }

        Publish(produced);
    }

    // The single handoff: check cancellation, then write the reference once and request one repaint.
    private void Publish(Widget widget)
    {
        if (_cts.IsCancellationRequested)
        {
            return; // a late completer that lost the race to cancellation stays silent
        }

        // Mount the produced child so its own lifecycle (e.g. a nested AsyncContent/Spinner) runs.
        // Mount is safe to call off the UI thread here: the widget isn't yet reachable by the render
        // thread (it's published on the next line), and OnMount implementations only capture state.
        if (_mountedApp is { } app)
        {
            widget.Mount(app);
        }

        _loaded = widget;   // volatile publish — the render thread sees a fully-constructed widget
        Repaint.Request();  // coalesced, thread-safe; wakes the loop to paint the swap
    }

    private static Widget DefaultError(Exception ex) =>
        new Label(
            StyledText.Of("⚠ ").Fg(Theme.Current.Error)
                .Append(ex.Message).Fg(Theme.Current.Foreground),
            Justify.Left,
            wrap: true);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
