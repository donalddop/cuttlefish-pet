using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>
/// Keeping a hunter inside the glass.
///
/// Everything that lives in the tank is held in by <see cref="PhysicsEngine.ClampToTank"/>,
/// but a predator is not a <see cref="Pet"/> and never went through it. It had a
/// check on left and right -- which is how a visit ends, not a containment -- and
/// vertically it had nothing at all. On 21 Nov a shark missed a dive at an animal
/// near the bottom, carried the burst straight through the floor, and crossed the
/// rest of the screen at Y=1344 on a screen 1080 tall: fifteen seconds of nothing
/// to look at, with the stage marked busy the whole time. That dead air is exactly
/// what <see cref="Director"/> exists to prevent, so the hunter leaking out of the
/// bottom of the frame undoes it from below.
///
/// Two parts, because neither alone is enough:
///
///  * A climb back in (<see cref="Return"/>), folded into the cruise heading. This
///    is the part you see: a fish whose dive ran out of water pulls up and comes
///    back into frame, and the nose-up bank falls out of it for free because
///    <see cref="Hunt.Animate"/> already banks to the heading.
///  * A hard line at the edge (<see cref="Hold"/>) that the anchor never crosses.
///    The climb is a heading and not a brake, so it cannot stop a 760px/s burst
///    inside a tenth of a second; it does not have to, because the burst is aimed
///    at an animal that is itself clamped, so the overshoot is always short and
///    this only catches the tail of it.
///
/// Arithmetic over a point, a vector and a rectangle, with no Pet, window or
/// sprite in sight, for the same reason <see cref="Depths"/>, <see cref="Hunt"/>
/// and <see cref="Approach"/> are shaped this way: an overshoot happens in maybe
/// one visit in three and a visit is eight to twenty minutes apart, so waiting to
/// see one costs an hour and driving it in DriveTests costs nothing.
/// </summary>
public static class Glass
{
    /// <summary>
    /// Headroom wanted above the anchor, in physical pixels at full size.
    ///
    /// Measured off the alpha of frame 0 rather than guessed: the shark's
    /// silhouette runs from 87px above its anchor (the tip of the dorsal) to 52px
    /// below it (the pelvic fins), and the dolphin sits inside that. Rounded up
    /// for the bank -- <see cref="Hunt.BankMax"/> tilts the body up to 38 degrees,
    /// which swings a 120px-long tail through roughly another 20px of height.
    /// </summary>
    public const double Head = 112;

    /// <summary>Clearance wanted below the anchor. See <see cref="Head"/>.</summary>
    public const double Foot = 78;

    /// <summary>
    /// The climb it puts in once it is right at the edge, in px/s.
    ///
    /// 240 against a cruise of 305 is about 38 degrees of nose-up: steep enough to
    /// read as pulling up, and inside what the sprite can bank to, so the body
    /// points where it is going instead of sliding up the screen level. Twice this
    /// looked like a fish fleeing the floor rather than levelling out.
    /// </summary>
    public const double Home = 240;

    /// <summary>
    /// How much of the velocity survives being stopped by the hard line.
    ///
    /// <see cref="PhysicsEngine.ClampToTank"/> uses 0.35 for the animals, and on a
    /// 760px/s burst that throws the hunter back up at 266px/s, which reads as a
    /// bounce off a wall. At 0.2 it is a dive that ran out of water, and the climb
    /// above -- not the bounce -- is what brings it home.
    /// </summary>
    public const double Bounce = 0.2;

    /// <summary>
    /// The band the body fits inside. Shrunk on a very short screen rather than
    /// inverted: on anything under ~630px tall the two margins would otherwise
    /// cross and every y would count as both too high and too low.
    /// </summary>
    private static double Ceiling(Rect tank) => tank.Top + Math.Min(Head, tank.Height * 0.3);
    private static double Floor(Rect tank) => tank.Bottom - Math.Min(Foot, tank.Height * 0.3);

    /// <summary>Part of the body is out of the frame. For the tests and the trace.</summary>
    public static bool Clipped(double y, Rect tank) =>
        !double.IsFinite(y) || tank.Height <= 1 || y < Ceiling(tank) || y > Floor(tank);

    /// <summary>
    /// The vertical speed that carries it back into the glass, in px/s; positive
    /// is down, as everywhere else. Zero while the whole body is inside, building
    /// linearly to <see cref="Home"/> at the edge of the screen itself.
    ///
    /// Linear on purpose. An ease-in would be gentlest exactly where the hunter is
    /// furthest out, which is the one place it needs to be doing something.
    /// </summary>
    public static double Return(double y, Rect tank)
    {
        if (!double.IsFinite(y) || tank.Height <= 1) return 0;

        double ceiling = Ceiling(tank);
        if (y < ceiling)
            return Home * Math.Clamp((ceiling - y) / Math.Max(1, ceiling - tank.Top), 0, 1);

        double floor = Floor(tank);
        if (y > floor)
            return -Home * Math.Clamp((y - floor) / Math.Max(1, tank.Bottom - floor), 0, 1);

        return 0;
    }

    /// <summary>
    /// The hard line: the anchor never leaves the glass, and the speed that took
    /// it there is turned round rather than kept. Half a body still hangs off the
    /// edge when this bites -- it is a backstop, not the containment -- but half a
    /// shark scraping the bottom of the frame is a thing to look at, and a shark
    /// 264px below the screen is not.
    ///
    /// By ref on the hunter's own fields, so a bad number is corrected in place
    /// before it can reach a Canvas offset: this runs every tick of every visit,
    /// and the one rule that has cost this project a tank is that nothing may
    /// throw on the UI thread.
    /// </summary>
    public static void Hold(ref Point pos, ref Vector vel, Rect tank)
    {
        if (!double.IsFinite(vel.Y)) vel.Y = 0;
        if (tank.Height <= 1) return;

        if (!double.IsFinite(pos.Y))
        {
            pos.Y = tank.Top + tank.Height / 2;   // lost: put it back in open water
            vel.Y = 0;
            return;
        }

        if (pos.Y < tank.Top) { pos.Y = tank.Top; vel.Y = Math.Abs(vel.Y) * Bounce; }
        else if (pos.Y > tank.Bottom) { pos.Y = tank.Bottom; vel.Y = -Math.Abs(vel.Y) * Bounce; }
    }
}
