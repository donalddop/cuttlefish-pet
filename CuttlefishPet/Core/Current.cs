using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>
/// The one shared drift of the whole tank: a slow vector, in physical px/s, that
/// everything afloat inherits. This is the piece that turns a set of separate
/// agents into one body of water -- when two flakes a screen apart lean the same
/// way at the same moment, the space between them reads as water rather than as
/// empty background.
///
/// Built from three sine waves with deliberately incommensurate periods (97s,
/// 231s, 143s) rather than from noise. Two reasons, both learned by trying the
/// other thing first:
///
///  * Random-walk drift (the first attempt) wanders but never *returns*, so over
///    ten minutes it parks in a corner of its range and the tank stops breathing.
///    Sines always come back, and the mismatched periods mean the pattern does
///    not visibly repeat within a working day.
///  * It is exactly reproducible from one double, which is what makes it testable
///    without a screen, and what lets a debug command jump the clock forward.
///
/// Mostly sideways: the vertical term is a third of the horizontal one, because a
/// current you can feel pushing things *down* reads as gravity, not as water.
/// </summary>
public sealed class Current
{
    /// <summary>Peak sideways speed in physical px/s. A flake crosses a 1080p screen in ~4 minutes.</summary>
    public const double MaxSideways = 8.5;
    /// <summary>Peak vertical speed. Deliberately small; see the class comment.</summary>
    public const double MaxVertical = 2.8;

    private double _t;

    /// <summary>Where the water is going, physical px/s.</summary>
    public Vector Flow { get; private set; }

    /// <summary>
    /// Start somewhere other than zero, so a freshly launched tank is already
    /// moving instead of easing up from dead still over its first minute.
    /// </summary>
    public Current(double startPhase = 41.0) { _t = startPhase; Recompute(); }

    public void Tick(double dt)
    {
        _t += dt;
        Recompute();
    }

    /// <summary>Jump the clock, for the debug command that wants to see a reversal now.</summary>
    public void Advance(double seconds)
    {
        _t += seconds;
        Recompute();
    }

    private void Recompute()
    {
        // The slow wave carries most of the weight; the faster ones only keep it
        // from being a clean oscillation you can predict by eye.
        double x = 0.62 * Math.Sin(_t / 97.0)
                 + 0.26 * Math.Sin(_t / 231.0 + 1.7)
                 + 0.12 * Math.Sin(_t / 43.0 + 0.4);
        double y = 0.74 * Math.Sin(_t / 143.0 + 0.6)
                 + 0.26 * Math.Sin(_t / 61.0 + 2.9);
        Flow = new Vector(x * MaxSideways, y * MaxVertical);
    }
}
