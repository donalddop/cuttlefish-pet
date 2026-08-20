using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CuttlefishPet.Interop;

namespace CuttlefishPet.Rendering;

/// <summary>
/// Single borderless, topmost, per-pixel-transparent window covering the whole
/// virtual screen. Never takes focus (WS_EX_NOACTIVATE) and is click-through
/// (WS_EX_TRANSPARENT) except while the cursor is over a pet.
/// </summary>
public partial class OverlayWindow : Window
{
    private IntPtr _hwnd;
    private bool _clickThrough = true;

    /// <summary>Physical-pixel origin of the window (virtual screen top-left).</summary>
    public Point OriginPhysical { get; private set; }
    /// <summary>Multiply physical px by this to get DIU for canvas placement.</summary>
    public double DeviceToDiu { get; private set; } = 1.0;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        var ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE).ToInt64();
        ex |= Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex));

        CoverVirtualScreen();

        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        DeviceToDiu = source.CompositionTarget.TransformFromDevice.M11;
    }

    public void CoverVirtualScreen()
    {
        var vs = System.Windows.Forms.SystemInformation.VirtualScreen; // physical px
        OriginPhysical = new Point(vs.Left, vs.Top);
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, vs.Left, vs.Top, vs.Width, vs.Height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
    }

    public void EnsureTopmost()
    {
        // SWP_SHOWWINDOW also recovers from external tools (screen capture filters)
        // that hide our window and forget to restore it.
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
    }

    /// <summary>Toggle WS_EX_TRANSPARENT so clicks reach the pet only when hovering it.</summary>
    public void SetClickThrough(bool enabled)
    {
        if (enabled == _clickThrough || _hwnd == IntPtr.Zero) return;
        _clickThrough = enabled;
        var ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE).ToInt64();
        if (enabled) ex |= Win32.WS_EX_TRANSPARENT;
        else ex &= ~(long)Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex));
    }

    public Point PhysToDiu(Point phys) =>
        new((phys.X - OriginPhysical.X) * DeviceToDiu, (phys.Y - OriginPhysical.Y) * DeviceToDiu);
}
