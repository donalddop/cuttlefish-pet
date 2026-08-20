namespace CuttlefishPet.Core;

public enum SurfaceKind
{
    Floor, TaskbarTop, WindowTop,
    WindowLeft, WindowRight,
    ScreenLeft, ScreenRight,
    /// <summary>Top of the screen: the pet hangs underneath it.</summary>
    Ceiling,
}

/// <summary>
/// A segment a pet can stand on (horizontal: Y is the walking line, X1..X2 its span)
/// or climb along (vertical: X1==X2 is the wall line, Y..Y2 its span, top to bottom).
/// All coordinates are physical screen pixels.
/// </summary>
public sealed record Surface(SurfaceKind Kind, IntPtr Hwnd, double X1, double X2, double Y, double Y2 = 0)
{
    public bool IsVertical =>
        Kind is SurfaceKind.WindowLeft or SurfaceKind.WindowRight
             or SurfaceKind.ScreenLeft or SurfaceKind.ScreenRight;

    /// <summary>Can a falling pet come to rest here? (Not on a ceiling.)</summary>
    public bool IsLandable =>
        Kind is SurfaceKind.Floor or SurfaceKind.TaskbarTop or SurfaceKind.WindowTop;

    /// <summary>Screen edges are climbed from the inside, window edges from the outside.</summary>
    public double ClingOffset => Kind switch
    {
        SurfaceKind.WindowLeft or SurfaceKind.ScreenRight => -12,
        SurfaceKind.WindowRight or SurfaceKind.ScreenLeft => 12,
        _ => 0,
    };

    /// <summary>Identity across ticks: same role on the same window.</summary>
    public bool SameAs(Surface o) => Kind == o.Kind && Hwnd == o.Hwnd;
}
