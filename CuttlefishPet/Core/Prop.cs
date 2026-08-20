using System.Windows;
using System.Windows.Controls;

namespace CuttlefishPet.Core;

/// <summary>
/// Something a pet leaves behind in the tank: an ink blot, a clutch of eggs. It ages
/// out on its own and can fire one action when it goes (eggs hatch that way).
/// </summary>
public sealed class Prop
{
    public required string Anim { get; init; }
    public required Point Pos { get; set; }
    public required double Life { get; init; }
    public double Age;
    public Action<Point>? OnExpire { get; init; }
    /// <summary>Fade over the last second so nothing pops out of existence.</summary>
    public double Opacity => Math.Min(1, Math.Max(0, Life - Age));
    public Image Visual = null!;
}
