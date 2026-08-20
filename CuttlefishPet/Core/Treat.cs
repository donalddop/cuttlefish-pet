using System.Windows;
using System.Windows.Controls;

namespace CuttlefishPet.Core;

/// <summary>A shrimp tossed onto the desktop for the pets to hunt down.</summary>
public sealed class Treat
{
    public Point Pos;
    public Vector Vel;
    public Surface? Surface;
    public bool Eaten;
    public double Age;
    /// <summary>Set once a pet commits to it, so several pets don't converge on one shrimp.</summary>
    public Pet? ClaimedBy;
    public Image Visual = null!;

    public bool Expired => Eaten || Age > 90;
}
