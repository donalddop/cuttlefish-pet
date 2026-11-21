using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>
/// What the tank's one shared current (<see cref="Current"/>, read from
/// <see cref="WorldState.Drift"/>) does to an animal: it carries it, and the
/// animal sets itself against it.
///
/// Crabbing is the word from boats and aircraft: pointing off your course by just
/// enough that the water or the air puts you back on it. It is the thing that
/// makes a current look like a current rather than like everything sliding -- you
/// read the flow off the angle of the bodies, not off the motion, because the
/// motion is only 8.5 px/s at its strongest and nobody can see that.
///
/// Two halves, and they are deliberately different in kind:
///
///  * The carry is position only, never velocity. Added after the behaviour has
///    steered, so every one of the eighty-odd behaviours inherits it without
///    knowing, and a behaviour that steers toward a point corrects for it by
///    itself on the next tick -- which is crabbing, arrived at for free and for
///    nothing. Putting it in the velocity instead was the first attempt: the
///    drag term in PhysicsEngine eats it within a second, headings pick it up
///    (an idle animal would face into its own drift), and the behaviours that
///    set Vel outright throw it away. Position is the honest place for it: water
///    moves what is in it whether or not the passenger agrees.
///
///  * The lean is a pose, and it is the half you can actually see. It answers the
///    crosswise part of the flow only, and only while the animal is slow enough
///    for the water to matter to it -- a cuttlefish doing 200px/s is making its
///    own weather.
/// </summary>
public static class Crabbing
{
    /// <summary>Degrees of body angle at most. A pose, not a manoeuvre.</summary>
    public const double MaxLean = 9;

    /// <summary>
    /// Degrees of lean per px/s of crosswise flow. A big number on a small signal
    /// on purpose: the vertical part of the current peaks at 2.8px/s
    /// (<see cref="Current.MaxVertical"/>) and is usually nearer 1.5, and the
    /// whole point is to turn something you cannot see into something you can.
    /// </summary>
    public const double LeanPerPx = 3.4;

    /// <summary>Seconds for the body to come round to the angle it wants.</summary>
    /// Slow, and tied to the flow's own clock rather than to a behaviour: the
    /// current turns over minutes, so a pose that snapped to it would be the one
    /// fast thing in a slow system.
    public const double Ease = 1.1;

    /// <summary>
    /// Own speed in px/s at which the water stops mattering. Above this the
    /// animal is swimming, not drifting, and leans not at all.
    /// </summary>
    public const double Yields = 95;

    private static double Fin(double v, double fallback = 0) => double.IsFinite(v) ? v : fallback;

    /// <summary>How much this animal is at the water's mercy, 0 to 1.</summary>
    public static double Influence(Vector vel, double flowLen)
    {
        double speed = Fin(vel.Length);
        double flow = Math.Max(0, Fin(flowLen));
        if (flow < 0.05) return 0;
        return Math.Clamp(1 - speed / Yields, 0, 1);
    }

    /// <summary>
    /// The angle this animal wants, in the same convention as
    /// <see cref="Pet.Rotation"/>: positive is nose-down for an animal facing
    /// right, and the renderer mirrors it for one facing left.
    ///
    /// Only the vertical part of the flow is answered. A side-on body cannot
    /// express "pointing upstream" against a sideways current by tilting -- it
    /// would have to turn round, and turning an animal round against what its
    /// behaviour is doing is a fight the behaviour should win. Water pushing it
    /// down is answered by swimming slightly up, which is a tilt, and that is the
    /// part a side view can show honestly.
    /// </summary>
    public static double WantLean(Vector vel, Vector flow, bool facingRight)
    {
        double down = Fin(flow.Y);
        double pull = Influence(vel, new Vector(Fin(flow.X), down).Length);
        double want = -down * LeanPerPx * pull * (facingRight ? 1 : -1);
        return Math.Clamp(Fin(want), -MaxLean, MaxLean);
    }

    /// <summary>Ease the pose toward what it wants. Returns the new angle.</summary>
    public static double Step(double lean, double want, double dt)
    {
        lean = Math.Clamp(Fin(lean), -MaxLean, MaxLean);
        want = Math.Clamp(Fin(want), -MaxLean, MaxLean);
        dt = Math.Clamp(Fin(dt), 0, 0.25);
        return Math.Clamp(lean + (want - lean) * Math.Min(1, dt / Ease), -MaxLean, MaxLean);
    }

    /// <summary>
    /// How far the water moves something afloat in it this tick. Everything in
    /// open water gets all of it: a current that only some things felt would read
    /// as those things being broken rather than as water.
    /// </summary>
    public static Vector Carry(Vector flow, double dt)
    {
        dt = Math.Clamp(Fin(dt), 0, 0.25);
        return new Vector(Fin(flow.X) * dt, Fin(flow.Y) * dt);
    }
}