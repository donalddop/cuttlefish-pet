using CuttlefishPet.Core;

namespace CuttlefishPet.Interop;

public static class TaskbarLocator
{
    /// <summary>Top edge of a horizontal taskbar as a landable surface, or null.</summary>
    public static Surface? GetSurface()
    {
        var hwnd = Win32.FindWindow("Shell_TrayWnd", null);
        if (hwnd == IntPtr.Zero || !Win32.IsWindowVisible(hwnd)) return null;
        if (!Win32.GetWindowRect(hwnd, out var r)) return null;
        if (r.Width < r.Height) return null; // vertical taskbar: not supported in v1
        return new Surface(SurfaceKind.TaskbarTop, hwnd, r.Left + 4, r.Right - 4, r.Top);
    }
}
