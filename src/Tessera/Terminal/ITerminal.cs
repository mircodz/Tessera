using System;
using Tessera.Primitives;

namespace Tessera.Terminal;

/// <summary>
/// Abstracts the host terminal: raw mode, alt screen, size, output, and raw input reads.
/// Implemented by <see cref="AnsiTerminal"/>; the interface lets the loop and tests use a fake.
/// </summary>
public interface ITerminal : IDisposable
{
    /// <summary>The terminal's color capability, used to downgrade truecolor output.</summary>
    ColorDepth ColorDepth { get; }

    /// <summary>Current size in cells.</summary>
    Size Size { get; }

    /// <summary>Raised (on a background thread) when the terminal is resized; consumers marshal
    /// onto their own loop. Driven by SIGWINCH on Unix, a size poll on Windows.</summary>
    event Action<Size>? Resized;

    /// <summary>Puts the terminal into raw mode and enables the alt screen, mouse, and paste.</summary>
    void EnterRawMode();

    /// <summary>Restores the terminal to its original cooked state and main screen.</summary>
    void LeaveRawMode();

    /// <summary>Writes a pre-rendered ANSI string to the terminal.</summary>
    void Write(string ansi);

    /// <summary>Blocks until input arrives (or the timeout elapses), copies into
    /// <paramref name="buffer"/>, and returns the count. 0 on timeout.</summary>
    int ReadInput(Span<byte> buffer, TimeSpan timeout);
}
