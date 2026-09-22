using System.Windows;
using System.Windows.Controls;

namespace CuttlefishPet.Core;

/// <summary>
/// The cousin that did not make it, drifting across and out again.
///
/// Ammonites were coiled shelled cephalopods for something like three hundred
/// million years and went out with the dinosaurs; cuttlefish are on the branch
/// that carried on. Once in a very long while one goes past, everything in the
/// tank stops to watch it, and then it is gone. Nothing comes of it, there is
/// nothing to collect and nothing to do -- it is here to be caught sight of.
/// </summary>
public sealed class Ammonite
{
    public Point Pos;
    public Vector Vel;
    public double Age;
    public bool FacingRight = true;
    public Image Visual = null!;

    /// <summary>
    /// Unhurried, but not a minute long. At fifty a second it took fifty-nine
    /// seconds to cross, and a whole tank standing still for a minute stops
    /// being a moment and starts being an interruption.
    /// </summary>
    public const double Drift = 112;
}
