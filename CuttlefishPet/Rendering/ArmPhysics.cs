using System.Windows;

namespace CuttlefishPet.Rendering;

/// <summary>
/// How an action wants its arms to lie, from animations.json. All in logical frame
/// pixels and screen-style degrees (y down: 0 is straight ahead, negative is up),
/// for the art facing right -- the renderer's transform does the mirroring.
/// </summary>
public sealed class ArmRig
{
    public required Point[] Roots { get; init; }
    public required double[] Angles { get; init; }
    public required double Length { get; init; }
    /// <summary>Extra bend from root to tip, in degrees.</summary>
    public double Curl { get; init; }
    public double WaveAmp { get; init; }
    public double WaveHz { get; init; }
    /// <summary>Radius at the root and at the tip.</summary>
    public double RootWidth { get; init; } = 1.75;
    public double TipWidth { get; init; } = 0.6;
    public int Count => Roots.Length;

    /// <summary>
    /// Where point <paramref name="j"/> of arm <paramref name="arm"/> would sit if the
    /// arm lay exactly in its pose, in logical frame pixels. The sway is a wave that
    /// travels from root to tip, each arm a little behind the one above it, which is
    /// what makes a crown of arms look alive rather than six sticks rocking in step.
    /// </summary>
    public Point Target(int arm, int j, int segments, double time)
    {
        double seg = Length * (1 - 0.06 * (arm % 3)) / segments;
        var p = Roots[arm];
        double w = 2 * Math.PI * WaveHz * time + arm * 0.9;
        for (int k = 1; k <= j; k++)
        {
            double along = (double)k / segments;
            double deg = Angles[arm] + Curl * along
                         + WaveAmp * Math.Sin(w - along * 2.2) * (0.35 + 0.65 * along);
            double rad = deg * Math.PI / 180;
            p = new Point(p.X + Math.Cos(rad) * seg, p.Y + Math.Sin(rad) * seg);
        }
        return p;
    }
}

/// <summary>
/// The arms as spring chains, in screen space. Each point keeps its own momentum and
/// is slowed by the water around it, pulled toward where the pose wants it, and held
/// at its distance from the point before it. Nothing more is needed for the things
/// the painted arms could never do: they trail behind a jet, swing on through when
/// the animal stops dead, and ease from one pose into the next instead of cutting.
///
/// Pure numbers -- no WPF elements -- so the test runner can push it around.
/// </summary>
public sealed class ArmPhysics
{
    public const int Segments = 6;

    /// <summary>[arm][point], point 0 is the root.</summary>
    public Point[][] Pos { get; private set; } = Array.Empty<Point[]>();
    private Point[][] _prev = Array.Empty<Point[]>();
    private bool _ready;
    private double _time;

    /// <summary>Per-second fraction of a point's speed the water takes away.</summary>
    public const double Drag = 6.0;
    /// <summary>
    /// Pull toward the pose: firm near the root, loose at the tip. Soft on purpose --
    /// stiff enough to hold a pose, but an animal cruising at a few hundred pixels a
    /// second has to leave its arm tips visibly behind, or none of this shows.
    /// </summary>
    public const double RootStiffness = 8, TipStiffness = 2.0;

    public void Reset() => _ready = false;
    public bool Ready => _ready;

    /// <param name="toScreen">Logical frame pixel to screen position, this tick.</param>
    /// <param name="scale">Screen units per logical pixel, this tick.</param>
    public void Step(double dt, ArmRig rig, Func<Point, Point> toScreen, double scale)
    {
        if (!double.IsFinite(dt) || dt < 0) dt = 0;
        dt = Math.Min(dt, 0.05);
        _time += dt;

        if (!_ready || Pos.Length != rig.Count)
        {
            Pos = new Point[rig.Count][];
            _prev = new Point[rig.Count][];
            for (int a = 0; a < rig.Count; a++)
            {
                Pos[a] = new Point[Segments + 1];
                _prev[a] = new Point[Segments + 1];
                for (int j = 0; j <= Segments; j++)
                    Pos[a][j] = _prev[a][j] = toScreen(rig.Target(a, j, Segments, _time));
            }
            _ready = true;
            return;
        }

        // Two substeps keep it steady when a frame comes in late.
        int steps = dt > 0.02 ? 2 : 1;
        double h = dt / steps;
        double keep = Math.Exp(-Drag * h);
        for (int s = 0; s < steps; s++)
        {
            for (int a = 0; a < rig.Count; a++)
            {
                var p = Pos[a];
                var q = _prev[a];
                double seg = rig.Length * (1 - 0.06 * (a % 3)) / Segments * scale;
                p[0] = q[0] = toScreen(rig.Roots[a]);
                for (int j = 1; j <= Segments; j++)
                {
                    var v = (p[j] - q[j]) * keep;
                    q[j] = p[j];
                    var next = p[j] + v;

                    double along = (double)j / Segments;
                    double k = RootStiffness + (TipStiffness - RootStiffness) * along;
                    var target = toScreen(rig.Target(a, j, Segments, _time));
                    next += (target - next) * (1 - Math.Exp(-k * h));

                    // Follow the leader: hold each link at its length from the one
                    // before it. Stable whatever the root did this tick.
                    var d = next - p[j - 1];
                    double len = d.Length;
                    next = len > 1e-6 ? p[j - 1] + d * (seg / len) : p[j - 1] + new Vector(seg, 0);
                    if (!double.IsFinite(next.X) || !double.IsFinite(next.Y)) { _ready = false; return; }
                    p[j] = next;
                }
            }
        }
    }
}
