
namespace CuttlefishPet.Core;

/// <summary>
/// How far back in the tank an animal is, and everything that follows from it.
///
/// 0 = far back in the haze, 1 = right against the glass. Same convention and the
/// same direction as <see cref="Flake.Depth"/>, deliberately: the dust and the
/// animals have to agree about which way is back, or the two parallaxes read as
/// two unrelated effects instead of as one volume of water.
///
/// Everything here is arithmetic over a single number, with no reference to a
/// window or a visual, so the whole depth model can be driven for an hour in
/// DriveTests without a screen -- the same reason Current and MarineSnow are
/// shaped the way they are.
///
/// One number drives four things at once (size, speed, haze and draw order) and
/// that is the point: any one of them alone reads as a bug (a shrunken animal, a
/// sluggish one, a dirty one), and all four together read as distance. The first
/// attempt had size and draw order only, and a small cuttlefish crossing the
/// screen at full speed in front of a big one looked like a rendering mistake.
/// </summary>
/// <summary>
/// One animal's place in the volume of water, and nothing else. A separate little
/// object rather than three fields on Pet for the same reason Drives and Relations
/// are separate: it can then be driven for an hour in DriveTests without dragging
/// in the behaviour machine, the sprite library and half of WPF behind it.
/// </summary>
public sealed class DepthState
{
    /// <summary>Where it is now: 0 at the back, 1 against the glass.</summary>
    public double Now = 1;
    /// <summary>Where it is drifting to. Changes every minute or two.</summary>
    public double Target = 1;
    /// <summary>Seconds before it picks somewhere else to swim.</summary>
    public double ChangeIn;
}

public static class Depths
{
    /// <summary>Body size multiplier at the back of the tank and at the glass.</summary>
    public const double ScaleFar = 0.70, ScaleNear = 1.06;

    /// <summary>
    /// Screen speed multiplier at the back and at the glass. Deliberately almost
    /// the same ratio as the size: something far away covers fewer pixels per
    /// second at the same real speed, and if the two ratios disagree the animal
    /// reads as either skating or wading rather than as being further off.
    /// </summary>
    public const double SpeedFar = 0.70, SpeedNear = 1.08;

    /// <summary>
    /// Alpha of the water wash over the body at the very back. Nothing at the glass.
    ///
    /// The wash is painted in the colour of whatever is behind the animal, not in
    /// a water blue, and that is the hard-won part. Distance does not tint things
    /// blue on this screen; it takes away contrast against the background. A blue
    /// haze over a cuttlefish on a white document makes it *more* visible, which
    /// is the same mistake the pale marine snow made on a light desktop. Washing
    /// toward the sampled surroundings takes contrast away on anything.
    /// </summary>
    public const double HazeFar = 0.38;

    /// <summary>
    /// Depth at which the haze has run out entirely. Everything in front of this
    /// is painted with one layer fewer.
    ///
    /// This is a cost decision, not a look decision. The haze is an extra
    /// opacity-masked rectangle per animal, and those are expensive whatever
    /// their opacity -- the renderer already collapses the ones at zero for that
    /// reason. Most animals swim in the front half by design, so cutting the haze
    /// off at the halfway mark means most of them never build the layer at all,
    /// while the ones it was for (the back of the tank, where it is strongest)
    /// are untouched. Measured on a live desktop the difference was lost in the
    /// noise either way; this is the version that cannot cost anything.
    /// </summary>
    public const double HazeTo = 0.62;

    /// <summary>Overall opacity multiplier at the back. 1 at the glass.</summary>
    public const double OpacityFar = 0.88;

    /// <summary>Seconds a depth is held before the animal thinks about moving.</summary>
    public const double HoldMin = 35, HoldMax = 110;

    /// <summary>
    /// Ease constant for the drift between depths, in seconds. Long: a change of
    /// depth is a minute of swimming slightly toward you, not a zoom. At 2s it
    /// looked like the animal was being dollied by a camera.
    /// </summary>
    public const double Ease = 7.0;

    public static double Clamp01(double v) => double.IsFinite(v) ? Math.Clamp(v, 0, 1) : 1;

    public static double ScaleAt(double d) => ScaleFar + (ScaleNear - ScaleFar) * Clamp01(d);
    public static double SpeedAt(double d) => SpeedFar + (SpeedNear - SpeedFar) * Clamp01(d);
    public static double HazeAt(double d) =>
        HazeFar * Math.Max(0, 1 - Clamp01(d) / HazeTo);
    public static double OpacityAt(double d) => OpacityFar + (1 - OpacityFar) * Clamp01(d);

    /// <summary>
    /// Draw order from depth. Spread wide and doubled so that each body has a free
    /// slot just under it for its own tentacles, which are separate canvas children
    /// and must stay behind the arms they come out from.
    ///
    /// The range straddles the water layers on purpose (snow -5, waterline -7,
    /// shafts -8 before this change): an animal at the back of the tank belongs
    /// behind the dust and behind the shafts, and one against the glass belongs in
    /// front of both. The dust now takes its own z from its own depth, so the two
    /// fields interleave rather than sitting in two flat planes.
    /// </summary>
    /// Twenty steps rather than one per percent: an order only has to separate a
    /// dozen animals, and every step costs a re-sort of the whole canvas on the
    /// tick an animal crosses it. Spaced four apart so each body has a slot below
    /// it for its own tentacles with room to spare.
    public static int ZFor(double d) => (int)Math.Round(Clamp01(d) * 20) * 4 - 12;

    /// <summary>
    /// A depth to be born at. Biased toward the front half: the back of the tank
    /// is scenery, and a tank that put half its animals there would read as having
    /// fewer, smaller cuttlefish rather than as having depth.
    /// </summary>
    public static double Roll(Random rng)
    {
        double u = rng.NextDouble();
        return 0.18 + 0.82 * (u * u * (3 - 2 * u));   // smoothstep: thins out at both ends
    }

    /// <summary>
    /// Advance one animal's depth: hold, then drift to somewhere else. Touches
    /// nothing but this one little object, so a bad number in here can never reach
    /// a position or a behaviour.
    /// </summary>
    public static void Tick(DepthState d, Random rng, double dt)
    {
        if (!double.IsFinite(d.Now)) d.Now = 1;
        if (!double.IsFinite(d.Target)) d.Target = d.Now;

        d.ChangeIn -= dt;
        if (d.ChangeIn <= 0)
        {
            d.ChangeIn = HoldMin + rng.NextDouble() * (HoldMax - HoldMin);
            // Somewhere else, not just anywhere: a roll that lands on top of where
            // it already is wastes a whole hold doing nothing visible.
            double want = Roll(rng);
            if (Math.Abs(want - d.Target) < 0.22)
                want = d.Target > 0.55 ? want - 0.30 : want + 0.30;
            d.Target = Clamp01(want);
        }

        d.Now = Clamp01(d.Now + (d.Target - d.Now) * Math.Min(1, dt / Ease));
    }

    /// <summary>
    /// Debug: fan a tank out over the whole range, back to front, and hold it
    /// there. Depth drifts on a clock of tens of seconds and most animals sit in
    /// the front half, so waiting for a tank to arrange itself into a readable
    /// spread costs more than this does.
    /// </summary>
    public static void Spread(IReadOnlyList<DepthState> pets)
    {
        for (int i = 0; i < pets.Count; i++)
        {
            double d = pets.Count == 1 ? 0 : (double)i / (pets.Count - 1);
            pets[i].Now = pets[i].Target = d;
            pets[i].ChangeIn = 600;   // stay put long enough to be looked at
        }
    }
}
