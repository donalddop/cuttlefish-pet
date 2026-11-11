using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>
/// One shaft of daylight coming in through the surface. Physical pixels.
/// </summary>
public sealed class Shaft
{
    /// <summary>Where it enters the surface, physical px. The sway moves this.</summary>
    public double X;
    /// <summary>Width at the surface. Fixed for life; see LightShafts.</summary>
    public double Width;
    /// <summary>How far down the tank it still carries, as a fraction of its height. Fixed.</summary>
    public double Reach;
    /// <summary>
    /// Px sideways per px down: the tilt. Always the same sign on every shaft,
    /// because they all come from the same sun -- the one detail that makes a set
    /// of bright bands read as light instead of as wallpaper.
    /// </summary>
    public double Lean;
    /// <summary>0..1, its share of the light at this moment. Breathes slowly.</summary>
    public double Strength;

    internal double Home;        // the x it sways around
    internal double BaseLean;    // its own tilt, before sway and current
    internal double Phase, Rate; // its sway clock
    internal double Pulse, Beat; // its breathing clock
}

/// <summary>
/// Daylight coming in through the surface: a handful of broad, very faint bands
/// that lean one way, sway over minutes, and fade out partway down. No visuals
/// here on purpose, same as MarineSnow -- this is a plain simulation over a
/// rectangle so it can be driven for an hour in a test without a screen.
///
/// Three things carry the illusion, and they were each arrived at by building the
/// obvious version first and looking at it:
///
///  * **Strong at the top, gone by the middle.** Light entering water is eaten by
///    the water. A band that runs the full height of the screen reads as a
///    curtain or a UI artefact; one that dissolves two-thirds of the way down
///    reads as depth, and it also keeps the effect out of the taskbar end of the
///    screen where the animals and the windows are busiest.
///  * **One tilt for all of them.** Per-shaft random tilts looked like a stack of
///    slashes. Giving every shaft the same sign and a small spread puts a sun
///    somewhere off the top-left corner, which is a stronger cue than any amount
///    of extra detail.
///  * **Breathing, not flickering.** Real rays come and go as the surface chops.
///    Each shaft fades between 42% and 100% of its own strength on its own clock
///    of about a minute and a half. Faster than that and it reads as a monitor
///    fault; constant, and the top of the screen looks painted on.
///
/// The shafts also lean a little with the shared Current. Light does not drift,
/// so this is not physics -- but the ripples that make the rays are driven by the
/// same water that carries the dust, and tying the two together is what makes the
/// screen feel like one body of water rather than two effects. Kept deliberately
/// small: at full current it is about a third of the sway.
/// </summary>
public sealed class LightShafts
{
    /// <summary>One shaft per this much screen width -- four of them on 1080p.</summary>
    private const double PixelsPerShaft = 520;
    private const int MinShafts = 2, MaxShafts = 6;

    /// <summary>The sun's direction: px sideways per px down, before per-shaft spread.</summary>
    private const double Tilt = 0.17;
    private const double TiltSpread = 0.09;

    /// <summary>Sideways sway, px, and how much of the tilt sways with it.</summary>
    private const double SwayPx = 24, LeanSway = 0.045;

    /// <summary>Response to the shared current: px per px/s, and tilt at full current.</summary>
    private const double FlowSway = 4.0, FlowLean = 0.035;

    /// <summary>A shaft never goes fully dark; it would pop back in. See the class comment.</summary>
    private const double StrengthFloor = 0.42;

    private readonly Random _rng;
    private Rect _bounds;
    /// <summary>The last current we were given, so Fit and Advance land where Tick would.</summary>
    private Vector _flow;

    public List<Shaft> Shafts { get; } = new();

    /// <summary>The tank the shafts were last fitted to. The renderer needs its height.</summary>
    public Rect Bounds => _bounds;

    /// <summary>Multiplies every shaft. 1 is the real thing; the debug command raises it.</summary>
    public double Brightness { get; set; } = 1;

    public LightShafts(Random? rng = null) => _rng = rng ?? new Random();

    /// <summary>
    /// Fit the shafts to the tank. Called every step; does nothing unless the
    /// virtual screen actually changed size.
    /// </summary>
    /// <param name="flow">
    /// This moment's current. Fit takes it because a shaft's x includes a term for
    /// it: place a new shaft without it and the very next step moves it up to 34px
    /// sideways in one go. Nothing is drawn between the two, so it is invisible in
    /// the app -- but it is a discontinuity in the model, the test that watches for
    /// jumps caught it, and a test that has to be told to ignore the first step is
    /// a test with a hole in it.
    /// </param>
    public void Fit(Rect bounds, Vector flow = default)
    {
        _flow = flow;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        bool resized = bounds != _bounds;
        _bounds = bounds;
        if (!resized) return;

        int want = Math.Clamp((int)Math.Round(bounds.Width / PixelsPerShaft), MinShafts, MaxShafts);

        // Rebuilt from scratch rather than topped up: a shaft's home is its slot in
        // an even spread across the width, so adding one has to move the others.
        // This happens when a monitor is plugged in, not on a tick.
        Shafts.Clear();
        double slot = bounds.Width / want;
        for (int i = 0; i < want; i++)
        {
            // Evenly spaced with a jitter of a third of a slot: regular enough that
            // no part of the top edge is bare, irregular enough not to look ruled.
            double home = bounds.Left + slot * (i + 0.5 + (_rng.NextDouble() - 0.5) * 0.66);
            var s = new Shaft
            {
                Home = home,
                X = home,
                Width = 230 + _rng.NextDouble() * 190,
                Reach = 0.46 + _rng.NextDouble() * 0.30,
                BaseLean = Tilt + (_rng.NextDouble() - 0.5) * TiltSpread,
                Phase = _rng.NextDouble() * Math.PI * 2,
                Pulse = _rng.NextDouble() * Math.PI * 2,
                Rate = Math.PI * 2 / (72 + _rng.NextDouble() * 56),
                Beat = Math.PI * 2 / (54 + _rng.NextDouble() * 44),
            };
            Apply(s, _flow);
            Shafts.Add(s);
        }
    }

    /// <param name="flow">The shared current, physical px/s, from <see cref="Current"/>.</param>
    public void Tick(double dt, Vector flow)
    {
        _flow = flow;
        for (int i = 0; i < Shafts.Count; i++)
        {
            var s = Shafts[i];
            s.Phase += dt * s.Rate;
            s.Pulse += dt * s.Beat;
            Apply(s, flow);
        }
    }

    /// <summary>Jump every clock, for the debug command that wants a different arrangement now.</summary>
    public void Advance(double seconds)
    {
        foreach (var s in Shafts)
        {
            s.Phase += seconds * s.Rate;
            s.Pulse += seconds * s.Beat;
            Apply(s, _flow);
        }
    }

    private static void Apply(Shaft s, Vector flow)
    {
        s.X = s.Home + Math.Sin(s.Phase) * SwayPx + flow.X * FlowSway;
        // The tilt sways on a different multiple of the same clock, so a shaft
        // does not slide and tip in lockstep -- which looked like one rigid object
        // being dragged about.
        s.Lean = s.BaseLean
               + Math.Sin(s.Phase * 0.63 + 1.1) * LeanSway
               + Math.Clamp(flow.X / Current.MaxSideways, -1, 1) * FlowLean;
        s.Strength = StrengthFloor + (1 - StrengthFloor) * (0.5 + 0.5 * Math.Sin(s.Pulse));
    }
}
