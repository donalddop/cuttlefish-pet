using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;

namespace CuttlefishPet.Interop;

public enum MouseEventKind { Down, Up }
public readonly record struct MouseEvent(MouseEventKind Kind, int X, int Y);

/// <summary>
/// Global low-level mouse + keyboard hooks. Callbacks only store/enqueue (LL hooks
/// must return fast or Windows silently drops them); everything is consumed on the
/// game tick. Purely observational — events are always passed on.
/// </summary>
public sealed class GlobalInput : IDisposable
{
    private IntPtr _mouseHook, _keyHook;
    private Win32.HookProc? _mouseProc, _keyProc; // keep delegates alive for GC
    private readonly ConcurrentQueue<MouseEvent> _mouseEvents = new();
    private long _keyPresses;
    private long _wheel;
    private volatile int _cx, _cy;
    private Point _lastCursor;

    public Point Cursor => new(_cx, _cy);
    public Vector CursorVelocity { get; private set; }
    /// <summary>Decayed keypresses; ~0 idle, climbs past 3-5 while typing.</summary>
    public double TypingRate { get; private set; }
    /// <summary>Seconds the cursor has held (nearly) still.</summary>
    public double CursorStill { get; private set; }
    /// <summary>Recent scroll wheel motion, positive when scrolling up.</summary>
    public double ScrollCurrent { get; private set; }

    /// <summary>Seconds since the user last touched mouse or keyboard, system-wide.</summary>
    public static double IdleSeconds()
    {
        var info = new Win32.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<Win32.LASTINPUTINFO>() };
        if (!Win32.GetLastInputInfo(ref info)) return 0;
        return unchecked((uint)Environment.TickCount - info.dwTime) / 1000.0;
    }

    public void Install()
    {
        Win32.GetCursorPos(out var p);
        _cx = p.X; _cy = p.Y;
        _lastCursor = new Point(p.X, p.Y);

        var hMod = Win32.GetModuleHandle(null);
        _mouseProc = MouseProc;
        _keyProc = KeyProc;
        _mouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, hMod, 0);
        _keyHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyProc, hMod, 0);
    }

    private IntPtr MouseProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
            _cx = data.pt.X;
            _cy = data.pt.Y;
            switch ((int)wParam)
            {
                case Win32.WM_LBUTTONDOWN:
                    _mouseEvents.Enqueue(new MouseEvent(MouseEventKind.Down, data.pt.X, data.pt.Y));
                    break;
                case Win32.WM_LBUTTONUP:
                    _mouseEvents.Enqueue(new MouseEvent(MouseEventKind.Up, data.pt.X, data.pt.Y));
                    break;
                case Win32.WM_MOUSEWHEEL:
                    // High word of mouseData is the signed wheel delta.
                    Interlocked.Add(ref _wheel, (short)((data.mouseData >> 16) & 0xFFFF));
                    break;
            }
        }
        return Win32.CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private IntPtr KeyProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (int)wParam is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN)
            Interlocked.Increment(ref _keyPresses);
        return Win32.CallNextHookEx(_keyHook, code, wParam, lParam);
    }

    public bool TryDequeue(out MouseEvent e) => _mouseEvents.TryDequeue(out e);

    public void Tick(double dt)
    {
        var c = Cursor;
        CursorVelocity = (c - _lastCursor) / Math.Max(dt, 1e-3);
        _lastCursor = c;
        CursorStill = CursorVelocity.Length < 40 ? CursorStill + dt : 0;

        var pressed = Interlocked.Exchange(ref _keyPresses, 0);
        TypingRate = TypingRate * Math.Exp(-dt * 1.2) + pressed;

        // Scrolling stirs the water: decays away over about a second.
        var wheel = Interlocked.Exchange(ref _wheel, 0);
        ScrollCurrent = ScrollCurrent * Math.Exp(-dt * 2.2) + wheel / 120.0;
    }

    public void Dispose()
    {
        if (_mouseHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_mouseHook);
        if (_keyHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_keyHook);
        _mouseHook = _keyHook = IntPtr.Zero;
    }
}
