using System.Windows;

namespace CuttlefishPet.Interop;

/// <summary>Small read-only lookups of interesting places on the desktop.</summary>
public static class SystemProbes
{
    /// <summary>Centre of the taskbar clock, or null if it can't be found.</summary>
    public static Point? Clock()
    {
        var tray = Win32.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return null;
        var notify = Win32.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notify == IntPtr.Zero) return null;
        var clock = Win32.FindWindowEx(notify, IntPtr.Zero, "TrayClockWClass", null);
        if (clock == IntPtr.Zero || !Win32.GetWindowRect(clock, out var r)) return null;
        return new Point((r.Left + r.Right) / 2.0, (r.Top + r.Bottom) / 2.0);
    }

    /// <summary>
    /// The blinking text caret in whatever window has focus, in screen pixels.
    /// Null when nothing is being typed into.
    /// </summary>
    public static Point? Caret()
    {
        var fg = Win32.GetForegroundWindow();
        if (fg == IntPtr.Zero) return null;
        uint thread = Win32.GetWindowThreadProcessId(fg, out _);
        if (thread == 0) return null;

        var info = new Win32.GUITHREADINFO();
        info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.GUITHREADINFO>();
        if (!Win32.GetGUIThreadInfo(thread, ref info)) return null;
        if (info.hwndCaret == IntPtr.Zero) return null;
        if (info.rcCaret.Right - info.rcCaret.Left <= 0 &&
            info.rcCaret.Bottom - info.rcCaret.Top <= 0) return null;

        // Caret rect is in client coordinates of the caret's window.
        var pt = new Win32.POINT
        {
            X = (info.rcCaret.Left + info.rcCaret.Right) / 2,
            Y = (info.rcCaret.Top + info.rcCaret.Bottom) / 2,
        };
        if (!Win32.ClientToScreen(info.hwndCaret, ref pt)) return null;
        return new Point(pt.X, pt.Y);
    }

    /// <summary>Where the desktop icons live, and how far apart their slots are.</summary>
    public readonly record struct IconGrid(Rect Area, double CellW, double CellH);

    /// <summary>
    /// Locate the desktop icon view and its grid spacing. Only window handles and
    /// system metrics are read — working out which slots are actually occupied would
    /// mean reading Explorer's memory, so the pet checks that by looking at the
    /// screen instead.
    /// </summary>
    public static IconGrid? DesktopIcons()
    {
        IntPtr view = IntPtr.Zero;

        // Normally the icon view hangs off Progman.
        var progman = Win32.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
            view = Win32.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

        // With a wallpaper app running, Explorer moves it under a WorkerW instead.
        if (view == IntPtr.Zero)
        {
            IntPtr worker = IntPtr.Zero;
            while ((worker = Win32.FindWindowEx(IntPtr.Zero, worker, "WorkerW", null)) != IntPtr.Zero)
            {
                view = Win32.FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (view != IntPtr.Zero) break;
            }
        }
        if (view == IntPtr.Zero) return null;

        var list = Win32.FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
        if (list == IntPtr.Zero || !Win32.GetWindowRect(list, out var r)) return null;
        if (r.Width < 100 || r.Height < 100) return null;

        var metrics = new Win32.ICONMETRICS
        { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.ICONMETRICS>() };
        if (!Win32.SystemParametersInfo(Win32.SPI_GETICONMETRICS, metrics.cbSize, ref metrics, 0))
            return null;

        return new IconGrid(new Rect(r.Left, r.Top, r.Width, r.Height),
                            Math.Max(48, metrics.iHorzSpacing),
                            Math.Max(48, metrics.iVertSpacing));
    }

    /// <summary>Nudge a window sideways. Returns false if Windows refused.</summary>
    public static bool NudgeWindow(IntPtr hwnd, int dx)
    {
        if (!Win32.IsWindow(hwnd) || Win32.IsZoomed(hwnd)) return false;
        if (!Win32.GetWindowRect(hwnd, out var r)) return false;
        return Win32.SetWindowPos(hwnd, IntPtr.Zero, r.Left + dx, r.Top, 0, 0,
            Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }
}
