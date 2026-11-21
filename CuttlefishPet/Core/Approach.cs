namespace CuttlefishPet.Core;

/// <summary>
/// One hunter's place in the volume of water over one visit: it arrives far back
/// in the haze, comes forward as it fixes on something, strikes in the quarry's
/// own plane, and sinks away again.
///
/// Why this exists at all: a shark that slid in from the left edge at full size
/// was a cut-out being dragged across a photograph. The thing that reads as a
/// predator is not the fish, it is the *resolving* -- a vague shape at the back
/// that becomes definite. Same trick every nature documentary uses, and it is
/// almost free: depth already drives size, and size plus opacity plus draw order
/// is the whole illusion.
///
/// Arithmetic over three numbers with no window, no sprite and no Pet in sight,
/// for the same reason <see cref="Depths"/> and <see cref="Hunt"/> are shaped this
/// way: the whole arrival can then be driven for a minute in DriveTests, and an
/// arrival is a thing you otherwise wait eight to twenty minutes to see once.
///
/// Deliberately not <see cref="Depths"/> itself. An animal's depth wanders on its
/// own clock between 0.70x and 1.06x size -- a range tuned so a cuttlefish at the
/// back still reads as a cuttlefish. A hunter needs to be genuinely far off and
/// genuinely faint, which is a much wider range, and borrowing the animals' one
/// gave an arrival you could not see happening.
/// </summary>
public sealed class ApproachState
{
    /// <summary>Where it is now: 0 at the back of the haze, 1 against the glass.</summary>
    public double Depth = Approach.Far;

    /// <summary>0 to 1 while the shape resolves out of the haze on arrival.</summary>
    public double Appear;

    /// <summary>0 to 1 while it dissolves back into the haze on the way out.</summary>
    public double Vanish;
}

public static class Approach
{
    /// <summary>
    /// The depth it arrives at and retreats to: behind everything there is.
    ///
    /// Deliberately deep enough to land on the draw order below the light shafts
    /// (<see cref="Depths.ZFor"/> gives -12 here, the shafts sit at -8), so the
    /// light from the surface falls between you and it. That was the thing that
    /// sold the arrival when I finally looked at it: not the size and not the
    /// fade, but a shaft of light crossing in front of a shape that is clearly
    /// behind the water.
    /// </summary>
    public const double Far = 0.02;

    /// <summary>Body size multiplier at <see cref="Far"/>. 1 at the glass.</summary>
    /// 0.54 rather than something smaller because the shark sprite is already
    /// slim: below about a half it stops reading as a fish-shaped thing at all
    /// and becomes a smudge, which is a different and much less interesting
    /// effect than a shark that happens to be far away.
    public const double ScaleFar = 0.54;

    /// <summary>How solid it is at <see cref="Far"/>. 1 at the glass.</summary>
    /// The number that does most of the work. At 0.24 it is unmistakably there
    /// and unmistakably not here yet -- you can see it is a shark, and you cannot
    /// see its edges.
    public const double FadeFar = 0.24;

    /// <summary>
    /// The shallowest depth an animal is ever at (<see cref="Depths.Roll"/> floors
    /// at 0.18). At or in front of this the hunter is drawn by the animals' own
    /// rules; only behind it does it get its own, much steeper curve.
    ///
    /// This is the one thing the first live run got properly wrong, and it is worth
    /// the extra arithmetic. The animals' range is deliberately narrow -- 0.70x to
    /// 1.06x size, 0.88 to 1.00 cover -- because they are the subject and must stay
    /// legible wherever they swim. The hunter's was mine and far wider, so a shark
    /// stalking a cuttlefish that happened to be loafing at depth 0.29 sat 170px
    /// from it, in the same plane, at half its cover: a ghost beside a solid
    /// animal, which reads as two renderers rather than as one body of water. The
    /// drama lives behind the animals' floor, where nothing else can be, and there
    /// is no need for it anywhere they can reach.
    /// </summary>
    public const double AnimalFloor = 0.18;

    /// <summary>Screen-speed multiplier at <see cref="Far"/>, as for the animals.</summary>
    public const double SpeedFar = 0.62;

    /// <summary>Coming forward, in depth per second. A rate, not an ease.</summary>
    /// Two things were got wrong here before this number existed, both measured
    /// in ApproachTests rather than guessed:
    ///
    ///  * A staged rise (0.40 of the way while crossing, 0.85 while stalking, all
    ///    of it in the coil) on a 2.4s ease strikes from depth 0.48 -- a half-size
    ///    shark biting a full-size animal two planes in front of it.
    ///  * An ease of any length spends its last third of a second crawling the
    ///    final tenth, and that final tenth is the whole difference between
    ///    striking in the quarry's plane and striking from behind it. At 1.1s it
    ///    still arrived at 0.70 when the stalk was a short one.
    ///
    /// The window is 1.44s at its shortest (<see cref="Hunt.StalkMin"/> plus
    /// <see cref="Hunt.CoilLen"/>, when the quarry is already inside the hold
    /// distance), and 0.95 per second crosses the animals' water in 0.78s, so it
    /// arrives from anywhere in time. A rate also draws better than an ease for
    /// this one move: a change of plane is a drift (<see cref="Depths.Ease"/>),
    /// but something coming for you closes at a speed and does not ease off.
    public const double RiseRate = 0.95;

    /// <summary>Coming forward while still behind the animals' floor. Much slower.</summary>
    /// The band behind <see cref="AnimalFloor"/> carries almost the whole change
    /// in cover (0.24 to 0.90) in 0.18 of depth, so at the full rate it is crossed
    /// in 0.19s -- and on the screen that is not a fade, it is a pop: the fish
    /// goes from a grey silhouette to full colour and outline between two frames.
    /// Measured off a live capture, which is the only way this was ever going to
    /// be noticed. At 0.34 the same band takes 0.47s and reads as something coming
    /// up out of the dark, which is the whole of what this file is for. It still
    /// fits the shortest window: 0.47s behind the floor plus 0.78s in front of it
    /// is 1.25s against 1.44s available, and there is a test that says so. It was
    /// 0.28 for an hour, which measured 1.40s against 1.44s -- correct, and far
    /// too fine a margin for a number anybody might retune later.
    public const double DeepRise = 0.34;

    /// <summary>How fast it may come forward from this depth.</summary>
    public static double RiseAt(double d) => Clamp01(d) < AnimalFloor ? DeepRise : RiseRate;

    /// <summary>Ease constant sinking back. Slower than the rise: leaving is not urgent.</summary>
    public const double SinkEase = 7.0;

    /// <summary>Seconds for the shape to resolve on arrival.</summary>
    public const double AppearLen = 1.6;

    /// <summary>Seconds to dissolve on the way out.</summary>
    public const double VanishLen = 2.4;

    /// <summary>
    /// Seconds a visit spends in the haze before it is allowed to fix on anybody.
    ///
    /// The first live run with the trace on locked on at 0.6s, while the shape was
    /// still fading in: cover went 0.01, 0.08, 0.46 and the deep crossing -- the
    /// whole point of the thing -- never existed to be seen. The arrival has to
    /// finish arriving (<see cref="AppearLen"/>) and then be a shape out there for
    /// a moment before it becomes a shape with an interest in you. Three beats, in
    /// order, or it is just a fish that grew.
    /// </summary>
    public const double LockAfter = 2.4;

    /// <summary>
    /// How far back it has to have sunk before it is allowed to start dissolving.
    /// Without this a hunter that gave up right in front of you faded out on the
    /// spot like a switched-off hologram -- it has to go away *into* something.
    /// </summary>
    public const double VanishBelow = 0.34;

    public static double Clamp01(double v) => double.IsFinite(v) ? Math.Clamp(v, 0, 1) : 1;

    /// <summary>Size, cover and screen speed: the animals' curves, extended backwards.</summary>
    public static double ScaleAt(double d) => Below(Clamp01(d), ScaleFar, Depths.ScaleAt);
    public static double FadeAt(double d) => Below(Clamp01(d), FadeFar, Depths.OpacityAt);
    public static double SpeedAt(double d) => Below(Clamp01(d), SpeedFar, Depths.SpeedAt);

    /// <summary>
    /// The animals' curve where they swim, and a straight line from its floor down
    /// to the hunter's own value at the very back.
    /// </summary>
    private static double Below(double d, double far, Func<double, double> animals) =>
        d >= AnimalFloor
            ? animals(d)
            : far + (animals(AnimalFloor) - far) * (d / AnimalFloor);

    /// <summary>
    /// The depth this phase of the hunt wants to be at. Nothing to chase means the
    /// back of the tank; a quarry pulls it forward in three steps, so that coming
    /// forward is itself a sign that it has picked someone.
    /// </summary>
    public static double Want(HuntPhase phase, bool hasQuarry, bool leaving, double quarryDepth)
    {
        if (leaving || !hasQuarry) return Far;

        // Never further back than it already starts: a quarry loafing at the very
        // back of the tank would otherwise ask the hunter to retreat to reach it,
        // and the approach would play backwards.
        double q = Math.Max(Far, Clamp01(quarryDepth));

        // The strike has to happen in the quarry's own plane: a bite from two
        // planes back is a miss the geometry cannot show, and the bite radius is
        // measured in flat screen pixels that know nothing about depth. So
        // everything from the lock onward asks for the whole distance, and only
        // the one tick of crossing-with-a-quarry is a half-way house.
        double k = phase == HuntPhase.Crossing ? 0.55 : 1.0;
        return Far + (q - Far) * k;
    }

    /// <summary>
    /// Advance arrival, depth and departure. Touches nothing but this one little
    /// object: a bad number in here can never reach a position or a transform.
    /// </summary>
    public static void Tick(ApproachState a, double dt, HuntPhase phase,
                            bool hasQuarry, bool leaving, double quarryDepth)
    {
        if (!double.IsFinite(a.Depth)) a.Depth = Far;
        a.Depth = Clamp01(a.Depth);
        a.Appear = Clamp01(a.Appear);
        a.Vanish = Clamp01(a.Vanish);
        if (!double.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, 0.25);   // a stalled frame must not teleport it forward

        a.Appear = Clamp01(a.Appear + dt / AppearLen);

        double want = Want(phase, hasQuarry, leaving, quarryDepth);
        a.Depth = want > a.Depth
            ? Math.Min(want, a.Depth + RiseAt(a.Depth) * dt)   // closing: a speed
            : Clamp01(a.Depth + (want - a.Depth) * Math.Min(1, dt / SinkEase));

        if (leaving && a.Depth < VanishBelow)
            a.Vanish = Clamp01(a.Vanish + dt / VanishLen);
    }

    /// <summary>What to draw it at: distance, plus the arrival and the departure.</summary>
    public static double Opacity(ApproachState a) =>
        Clamp01(FadeAt(a.Depth) * Clamp01(a.Appear) * (1 - Clamp01(a.Vanish)));

    /// <summary>Gone back into the haze; the visit is over.</summary>
    public static bool Dissolved(ApproachState a) => Clamp01(a.Vanish) >= 1;
}