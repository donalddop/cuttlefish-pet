using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>One catch of light on the underside of the surface. Physical px.</summary>
public sealed class Glint
{
    /// <summary>Centre of the highlight along the top edge.</summary>
    public double X;
    /// <summary>How wide the highlight is. Fixed for life.</summary>
    public double Width;
    /// <summary>0..1, how much of the light it is holding right now.</summary>
    public double Strength;

    internal double Drift;        // its own sideways speed, px/s, on top of the current
    internal double Pulse, Beat;  // its breathing clock
}

/// <summary>
/// The waterline: the underside of the surface along the top edge of the screen,
/// where the light catches and slides.
///
/// This is the other half of the shafts, and it is deliberately not a separate
/// effect. A shaft of light has to come in *somewhere*, and in any real tank the
/// place it comes in is a bright patch on the surface that moves. So a glint
/// brightens as it passes the root of a shaft -- not because that is cheap to do,
/// though it is, but because without it the top of the screen has two unrelated
/// things happening on it instead of one thing seen twice.
///
/// Why the glints slide rather than sit still: surface ripples travel. They carry
/// a share of the shared current -- about three times what a mote feels, since a
/// ripple runs over the water instead of being carried through it -- plus a small
/// speed of their own, so they neither move as one block nor wander off on their
/// own errand. A glint crosses a 1080p screen in a minute and a half at most.
///
/// Why a band and not a line: a hairline along the top edge reads as a rendering
/// fault or a stuck row of pixels, which is the single most expensive mistake
/// this project can make -- the whole thing lives or dies on not looking like
/// something is wrong with the screen. A band of a couple of dozen pixels with a
/// soft bottom edge reads as surface.
/// </summary>
public sealed class SurfaceShimmer
{
    /// <summary>One glint per this much width -- five on 1080p.</summary>
    private const double PixelsPerGlint = 380;
    private const int MinGlints = 3, MaxGlints = 9;

    /// <summary>
    /// Band depth as a fraction of screen height, and its limits in px. The first
    /// pass used 0.022 (24px on 1080p) and measured on the real desktop it gave a
    /// bright edge about twelve pixels deep -- which is exactly the hairline this
    /// class exists to avoid, just a slightly fat one. Deeper, and with a much
    /// softer highlight inside it (see WaterLook.Glint), so the light dissolves
    /// into the screen instead of ending.
    /// </summary>
    private const double BandFraction = 0.028, BandMin = 20, BandMax = 44;

    /// <summary>How much of the shared current a ripple carries, and its own speed.</summary>
    private const double RippleGain = 3.0, OwnSpeed = 6.0;

    /// <summary>A glint never goes fully dark; see LightShafts for why.</summary>
    private const double StrengthFloor = 0.25;

    /// <summary>How close to a shaft's root counts as "under the beam", and the lift it gives.</summary>
    private const double ShaftReach = 170, ShaftLift = 0.55;

    /// <summary>Off-screen slack before a glint is wrapped to the other side.</summary>
    private const double Margin = 40;

    private readonly Random _rng;
    private Rect _bounds;

    public List<Glint> Glints { get; } = new();

    /// <summary>How deep the band hangs below the top edge, physical px.</summary>
    public double Band { get; private set; } = BandMin;

    /// <summary>The tank the band was last fitted to.</summary>
    public Rect Bounds => _bounds;

    /// <summary>Multiplies every glint. 1 is the real thing; the debug command raises it.</summary>
    public double Brightness { get; set; } = 1;

    public SurfaceShimmer(Random? rng = null) => _rng = rng ?? new Random();

    /// <summary>Fit the band to the tank; does nothing unless the screen changed size.</summary>
    public void Fit(Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        bool resized = bounds != _bounds;
        _bounds = bounds;
        if (!resized) return;

        Band = Math.Clamp(bounds.Height * BandFraction, BandMin, BandMax);
        int want = Math.Clamp((int)Math.Round(bounds.Width / PixelsPerGlint), MinGlints, MaxGlints);

        while (Glints.Count > want) Glints.RemoveAt(Glints.Count - 1);
        while (Glints.Count < want) Glints.Add(NewGlint());
        // Spread whatever we ended up with over the width. Unlike the shafts these
        // slide, so the spacing only has to be right at the start.
        for (int i = 0; i < Glints.Count; i++)
            Glints[i].X = bounds.Left + bounds.Width * (i + 0.5) / Glints.Count;
    }

    private Glint NewGlint() => new()
    {
        Width = 160 + _rng.NextDouble() * 260,
        Drift = (_rng.NextDouble() * 2 - 1) * OwnSpeed,
        Pulse = _rng.NextDouble() * Math.PI * 2,
        Beat = Math.PI * 2 / (26 + _rng.NextDouble() * 34),
    };

    /// <param name="flow">The shared current, physical px/s.</param>
    /// <param name="shafts">
    /// Where the light comes in. A glint passing one of these roots brightens. Null
    /// is allowed and simply means no lift -- the band still works on its own, which
    /// is what keeps this testable without building a LightShafts as well.
    /// </param>
    public void Tick(double dt, Vector flow, IReadOnlyList<Shaft>? shafts)
    {
        if (Glints.Count == 0) return;

        for (int i = 0; i < Glints.Count; i++)
        {
            var g = Glints[i];
            g.Pulse += dt * g.Beat;
            g.X += (flow.X * RippleGain + g.Drift) * dt;

            if (g.X < _bounds.Left - Margin) g.X = _bounds.Right + Margin;
            else if (g.X > _bounds.Right + Margin) g.X = _bounds.Left - Margin;

            double own = StrengthFloor + (1 - StrengthFloor) * (0.5 + 0.5 * Math.Sin(g.Pulse));
            g.Strength = Math.Min(1, own * (1 + ShaftLift * NearestShaft(shafts, g.X)));
        }
    }

    /// <summary>1 right under a shaft, falling away to 0 about two shaft-widths off.</summary>
    private static double NearestShaft(IReadOnlyList<Shaft>? shafts, double x)
    {
        if (shafts == null) return 0;
        double best = 0;
        for (int i = 0; i < shafts.Count; i++)
        {
            double d = (shafts[i].X - x) / ShaftReach;
            double lift = Math.Exp(-d * d);
            if (lift > best) best = lift;
        }
        return best;
    }

    /// <summary>Jump the clocks and slide the band on, for the debug command.</summary>
    public void Advance(double seconds)
    {
        foreach (var g in Glints)
        {
            g.Pulse += seconds * g.Beat;
            g.X += g.Drift * seconds;
            while (g.X > _bounds.Right + Margin) g.X -= _bounds.Width + 2 * Margin;
            while (g.X < _bounds.Left - Margin) g.X += _bounds.Width + 2 * Margin;
        }
    }
}
