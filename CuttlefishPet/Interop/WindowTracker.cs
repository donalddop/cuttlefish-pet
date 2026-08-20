using System.Text;
using CuttlefishPet.Core;

namespace CuttlefishPet.Interop;

public sealed class TrackedWindow
{
    public IntPtr Hwnd;
    public Win32.RECT Rect; // extended frame bounds, physical px
    /// <summary>Maximized/fullscreen: occludes others but offers no surfaces itself.</summary>
    public bool Zoomed;
}

/// <summary>
/// Keeps a live list of "real" user windows in z-order (topmost first) and turns them
/// into landable/climbable surfaces. Full re-enumeration every ~0.5s; tracked window
/// rects are refreshed every tick so pets ride along smoothly when windows move.
/// </summary>
public sealed class WindowTracker
{
    private static readonly HashSet<string> BlockedClasses = new()
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow", "Windows.UI.Core.CoreWindow",
    };

    private readonly List<TrackedWindow> _windows = new();
    private readonly HashSet<IntPtr> _known = new();
    private readonly List<System.Windows.Rect> _appeared = new();
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private int _tick;
    private bool _firstEnum = true;

    /// <summary>Topmost first (EnumWindows order).</summary>
    public IReadOnlyList<TrackedWindow> Windows => _windows;

    /// <summary>Rects of windows that showed up since the last call (then clears).</summary>
    public List<System.Windows.Rect> TakeAppeared()
    {
        var result = new List<System.Windows.Rect>(_appeared);
        _appeared.Clear();
        return result;
    }

    public void Tick()
    {
        if (_tick++ % 30 == 0) FullEnum();
        else RefreshRects();
    }

    private void FullEnum()
    {
        _windows.Clear();
        var seen = new HashSet<IntPtr>();
        Win32.EnumWindows((hwnd, _) =>
        {
            if (IsRealWindow(hwnd) && TryGetRect(hwnd, out var rect) &&
                rect.Width >= 150 && rect.Height >= 100)
            {
                _windows.Add(new TrackedWindow { Hwnd = hwnd, Rect = rect, Zoomed = Win32.IsZoomed(hwnd) });
                seen.Add(hwnd);
                if (!_firstEnum && !_known.Contains(hwnd))
                    _appeared.Add(new System.Windows.Rect(
                        rect.Left, rect.Top, rect.Width, rect.Height));
            }
            return true;
        }, IntPtr.Zero);

        _known.Clear();
        _known.UnionWith(seen);
        _firstEnum = false;
    }

    private void RefreshRects()
    {
        _windows.RemoveAll(w =>
        {
            if (!Win32.IsWindow(w.Hwnd) || !Win32.IsWindowVisible(w.Hwnd) || Win32.IsIconic(w.Hwnd))
                return true;
            if (!TryGetRect(w.Hwnd, out var rect))
                return true;
            w.Rect = rect;
            return false;
        });
    }

    private bool IsRealWindow(IntPtr hwnd)
    {
        if (!Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd)) return false;
        if (Win32.GetWindowTextLength(hwnd) == 0) return false;

        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == _ownPid) return false;

        var sb = new StringBuilder(64);
        Win32.GetClassName(hwnd, sb, 64);
        if (BlockedClasses.Contains(sb.ToString())) return false;

        // Skip cloaked windows (UWP suspended, other virtual desktops)
        if (Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;
        return true;
    }

    private static bool TryGetRect(IntPtr hwnd, out Win32.RECT rect)
    {
        if (Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS,
                out rect, System.Runtime.InteropServices.Marshal.SizeOf<Win32.RECT>()) == 0)
            return true;
        return Win32.GetWindowRect(hwnd, out rect);
    }

    /// <summary>
    /// Emit surfaces for all tracked windows: visible top-edge segments (occlusion by
    /// higher-z windows subtracted) plus left/right climbable edges.
    /// </summary>
    public void AddSurfaces(List<Surface> output)
    {
        const int minSegment = 70;
        // The sprite is ~80px tall; a walking line closer to the screen top than that
        // would put the pet entirely off-screen.
        double minTop = System.Windows.Forms.SystemInformation.VirtualScreen.Top + 90;
        for (int i = 0; i < _windows.Count; i++)
        {
            var w = _windows[i];
            var r = w.Rect;
            // Maximized/fullscreen windows still occlude (handled below via j<i loops)
            // but offer no perches: their top edge sits at/above the screen edge.
            if (w.Zoomed || r.Top < minTop) continue;

            // Top edge, minus horizontal spans of windows above us in z-order that
            // straddle our top line.
            var segments = new List<(double a, double b)> { (r.Left + 6, r.Right - 6) };
            for (int j = 0; j < i; j++)
            {
                var o = _windows[j].Rect;
                if (o.Top < r.Top && o.Bottom > r.Top)
                    segments = Subtract(segments, o.Left, o.Right);
            }
            foreach (var (a, b) in segments)
                if (b - a >= minSegment)
                    output.Add(new Surface(SurfaceKind.WindowTop, w.Hwnd, a, b, r.Top));

            // Side edges (climbable) — dropped when a higher-z window covers the
            // edge's midpoint, so the pet doesn't scale invisible walls.
            if (r.Height >= 140)
            {
                double midY = (r.Top + r.Bottom) / 2.0;
                if (!PointOccluded(i, r.Left, midY))
                    output.Add(new Surface(SurfaceKind.WindowLeft, w.Hwnd, r.Left, r.Left, r.Top, r.Bottom - 10));
                if (!PointOccluded(i, r.Right, midY))
                    output.Add(new Surface(SurfaceKind.WindowRight, w.Hwnd, r.Right, r.Right, r.Top, r.Bottom - 10));
            }
        }
    }

    private bool PointOccluded(int index, double x, double y)
    {
        for (int j = 0; j < index; j++)
        {
            var o = _windows[j].Rect;
            if (x > o.Left && x < o.Right && y > o.Top && y < o.Bottom)
                return true;
        }
        return false;
    }

    private static List<(double a, double b)> Subtract(List<(double a, double b)> segs, double from, double to)
    {
        var result = new List<(double, double)>();
        foreach (var (a, b) in segs)
        {
            if (to <= a || from >= b) { result.Add((a, b)); continue; }
            if (from > a) result.Add((a, from));
            if (to < b) result.Add((to, b));
        }
        return result;
    }
}
