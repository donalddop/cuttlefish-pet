using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>
/// The strike itself, shared by everything a cuttlefish tries to grab.
///
/// A cuttlefish does not swim into its food. It holds station, gathers, and fires
/// two club-tipped feeding tentacles out of the ring of arms — the extension takes
/// a few hundredths of a second and the animal barely moves. That contrast between
/// the still body and the tentacles snapping out is the whole appeal, so the timing
/// here is deliberately lopsided: a long wind-up, an almost instant shot.
/// </summary>
public sealed class TentacleStrike
{
    private const double WindUp = 0.34;   // gathering; tentacles still tucked away
    private const double Shoot = 0.07;    // ballistic, and over before you see it
    private const double Grip = 0.22;     // clubs closed on whatever was there
    private const double Haul = 0.42;     // reeling back in

    private double _t;

    /// <summary>True from the moment the clubs arrive on target.</summary>
    public bool Landed => _t >= WindUp + Shoot;
    /// <summary>True for the single tick the clubs land, so callers can react once.</summary>
    public bool JustLanded { get; private set; }
    public bool Reeling => _t >= WindUp + Shoot + Grip;
    public bool Finished => _t >= WindUp + Shoot + Grip + Haul;

    /// <summary>0 = tucked in, 1 = fully extended.</summary>
    public double Extension { get; private set; }

    /// <summary>
    /// Advance the strike and write the drawing state onto the pet. Pass the target
    /// fresh each tick: prey keeps moving right up to the moment it is caught.
    /// </summary>
    public void Tick(Pet pet, Point target, double dt, bool holdOn = false)
    {
        bool wasLanded = Landed;
        _t += dt;
        JustLanded = !wasLanded && Landed;

        if (_t < WindUp)
        {
            // Coiling back a fraction: the anticipation is what sells the shot.
            Extension = -0.05 * Math.Sin(_t / WindUp * Math.PI);
        }
        else if (_t < WindUp + Shoot)
        {
            double u = (_t - WindUp) / Shoot;
            Extension = 1 - (1 - u) * (1 - u);      // fast out, settling at the end
        }
        else if (holdOn || _t < WindUp + Shoot + Grip)
        {
            Extension = 1;
        }
        else
        {
            double u = Math.Min(1, (_t - WindUp - Shoot - Grip) / Haul);
            Extension = 1 - u * u;                  // hauls in slowly, then snaps home
        }

        var mouth = Mouth(pet);
        pet.Striking = true;
        pet.StrikeTip = mouth + (target - mouth) * Extension;
    }

    /// <summary>
    /// Where the arms meet and the tentacles are stowed, in world pixels. The sprite
    /// is anchored near its foot with the eye at roughly (39, 41) of a 64-wide frame,
    /// so the mouth sits a good way in front of and above the anchor — and all of it
    /// scales with the render scale, not just the pet's own size.
    /// </summary>
    public static Point Mouth(Pet pet)
    {
        double s = Pet.RenderScale * pet.Scale;
        return pet.Pos + new Vector((pet.FacingRight ? 20 : -20) * s, -11 * s);
    }
}
