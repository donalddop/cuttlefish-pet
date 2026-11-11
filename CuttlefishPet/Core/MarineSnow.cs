using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>
/// One mote of marine snow: dead plankton and other leavings on their way down.
/// Physical pixels throughout.
/// </summary>
public sealed class Flake
{
    public double X, Y;
    /// <summary>0 = far back in the tank, 1 = right against the glass.</summary>
    public double Depth;
    /// <summary>Where it is in its own sideways weave, and how fast it weaves.</summary>
    public double Phase, Spin;
    /// <summary>Eased toward 0 over a window, 1 over bare desktop. See the field comment.</summary>
    public double Clarity = 1;

    /// <summary>Drawn diameter in physical px. Fixed for life -- see MarineSnow.</summary>
    public double Size => 1.8 + Depth * 3.4;
    /// <summary>Alpha over bare desktop. Near motes are brighter as well as bigger.</summary>
    public double Alpha => 0.085 + Depth * 0.215;
    /// <summary>How much of the shared current this mote feels: near ones feel it most.</summary>
    public double Pull => 0.25 + Depth * 0.95;
}

/// <summary>
/// The drifting dust of the tank. No visuals here on purpose: this is a plain
/// simulation over a rectangle so it can be driven for ten minutes in a test
/// without a screen.
///
/// Depth is the whole trick. Each mote keeps one depth for life, and size,
/// brightness, sink rate and how much current it feels all come out of it. Near
/// motes are big, bright and quick; far ones are small, faint and slow. Nothing
/// else differs between them, and that alone is enough to read as volume -- which
/// is why this is worth doing before the proper depth pass on the animals.
///
/// Depth is fixed for life rather than re-rolled when a mote wraps around, so the
/// renderer can set each ellipse's size and brush once and then only ever move
/// it. Re-rolling looked marginally livelier and cost a size and opacity write per
/// mote per tick for it; not worth it on something that runs all day.
///
/// Clarity is the readability concession, and it is a deliberate break with
/// physics: real water sits in front of everything, but this water sits on top of
/// whatever Donald is reading. So a mote fades back as it drifts over a window and
/// comes up again over bare desktop. Eased rather than switched, or it pops at
/// every window edge.
/// </summary>
public sealed class MarineSnow
{
    /// <summary>One mote per this many physical px^2 -- about 37 of them on 1080p.</summary>
    private const double PixelsPerFlake = 56_000;
    private const int MinFlakes = 18, MaxFlakes = 70;

    /// <summary>
    /// Sink speed in px/s at the back and at the glass. Slow on purpose: at the
    /// first pass (3.6 and 11.5) a near mote crossed a 1080p screen in a minute
    /// and a half, which read as drizzle. At these it takes about four minutes,
    /// and the sideways drift of the current becomes the larger of the two
    /// motions -- which is what makes it look suspended rather than falling.
    /// </summary>
    private const double SinkFar = 2.2, SinkNear = 7.0;

    /// <summary>How far off-screen a mote goes before it is wrapped around.</summary>
    private const double Margin = 12;

    private readonly Random _rng;
    private Rect _bounds;

    public List<Flake> Flakes { get; } = new();

    /// <summary>Multiplies every mote's alpha. 1 is the real thing; the debug command raises it.</summary>
    public double Brightness { get; set; } = 1;

    public MarineSnow(Random? rng = null) => _rng = rng ?? new Random();

    /// <summary>
    /// Fit the field to the tank. Called every tick; does nothing unless the
    /// virtual screen actually changed size, so plugging in a monitor restocks
    /// but a normal tick costs one Rect comparison.
    /// </summary>
    public void Fit(Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        bool resized = bounds != _bounds;
        _bounds = bounds;
        if (!resized) return;

        int want = Math.Clamp(
            (int)Math.Round(bounds.Width * bounds.Height / PixelsPerFlake),
            MinFlakes, MaxFlakes);

        while (Flakes.Count > want) Flakes.RemoveAt(Flakes.Count - 1);
        while (Flakes.Count < want) Flakes.Add(NewFlake(scatter: true));
    }

    private Flake NewFlake(bool scatter)
    {
        // Biased toward the back of the tank: a handful of near motes in front of
        // a haze of far ones. An even spread put too many big bright ones over the
        // text and read as a dirty screen rather than as water.
        double u = _rng.NextDouble();
        return new Flake
        {
            X = _bounds.Left + _rng.NextDouble() * _bounds.Width,
            Y = scatter
                ? _bounds.Top + _rng.NextDouble() * _bounds.Height
                : _bounds.Top - Margin,
            Depth = u * u * (3 - 2 * u),   // smoothstep: thins out at both ends
            Phase = _rng.NextDouble() * Math.PI * 2,
            Spin = 0.17 + _rng.NextDouble() * 0.34,
        };
    }

    /// <param name="flow">The shared current, physical px/s.</param>
    /// <param name="scroll">
    /// WorldState.ScrollCurrent: positive scrolling up. Scrolling a page stirs the
    /// water beside it, which is the one place the tank reacts to ordinary work
    /// rather than to being poked.
    /// </param>
    /// <param name="covers">Window rectangles; a mote over one of these fades back.</param>
    public void Tick(double dt, Vector flow, double scroll, IReadOnlyList<Rect>? covers)
    {
        if (Flakes.Count == 0) return;

        for (int i = 0; i < Flakes.Count; i++)
        {
            var f = Flakes[i];
            double pull = f.Pull;

            f.Phase += dt * f.Spin;
            // The weave is what keeps it from being rain. Near motes weave wider,
            // for the same reason they drift further: they are closer to the eye.
            double weave = Math.Sin(f.Phase) * (0.8 + 2.4 * f.Depth);

            double sink = SinkFar + (SinkNear - SinkFar) * f.Depth;
            // Scrolling lifts the dust the way it pushes the animals (same sign as
            // PhysicsEngine.ApplyScrollCurrent), and only the near motes much feel it.
            double stir = -scroll * 55 * pull;

            f.X += (flow.X * pull + weave) * dt;
            f.Y += (sink + flow.Y * pull + stir) * dt;

            Wrap(f);

            // 0.55, after walking this one down and back up again. The first pass
            // used 0.24, on the assumption that anything over a window would be in
            // the way. Rendering a full-strength field over a page of body text
            // (Tools/WaterPreview, water-text.png) showed the assumption was wrong:
            // at this alpha it does not touch the text even undimmed. And since
            // Donald's screen is nearly all window nearly all the time, 0.24 meant
            // the water effectively did not exist anywhere he was looking. So: a
            // real step back, not a disappearance.
            double want = IsCovered(covers, f.X, f.Y) ? 0.55 : 1.0;
            // ~0.4s to settle: slow enough not to flicker at a window edge, quick
            // enough that a mote is not still bright halfway across a document.
            f.Clarity += (want - f.Clarity) * Math.Min(1, dt * 2.5);
        }
    }

    private void Wrap(Flake f)
    {
        // Off the bottom is the normal end of a life: back to the top at a fresh x,
        // so the field never settles into visible columns. Off the top only happens
        // when the current is pushing up hard, and off the sides is the current
        // carrying it away; those wrap straight across and keep their height.
        if (f.Y > _bounds.Bottom + Margin)
        {
            f.Y = _bounds.Top - Margin;
            f.X = _bounds.Left + _rng.NextDouble() * _bounds.Width;
        }
        else if (f.Y < _bounds.Top - Margin)
        {
            f.Y = _bounds.Bottom + Margin;
            f.X = _bounds.Left + _rng.NextDouble() * _bounds.Width;
        }

        if (f.X < _bounds.Left - Margin) f.X = _bounds.Right + Margin;
        else if (f.X > _bounds.Right + Margin) f.X = _bounds.Left - Margin;
    }

    private static bool IsCovered(IReadOnlyList<Rect>? covers, double x, double y)
    {
        if (covers == null) return false;
        for (int i = 0; i < covers.Count; i++)
            if (covers[i].Contains(x, y)) return true;
        return false;
    }
}
