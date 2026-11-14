using System.Windows;
using CuttlefishPet.Rendering;

/// <summary>The spring-chain arms: they settle, they trail, they keep their length, they never blow up.</summary>
static class ArmTests
{
    static ArmRig Rig() => new()
    {
        Roots = Enumerable.Range(0, 6).Select(i => new Point(46, 39 + i * 1.9)).ToArray(),
        Angles = new[] { -11.8, -7.1, -2.4, 2.4, 7.1, 11.8 },
        Length = 12.5, Curl = 12, WaveAmp = 0, WaveHz = 0.5,
    };

    public static void Run(Action<string, bool, string> check)
    {
        const double scale = 3.4;   // a full-grown animal on screen
        var rig = Rig();

        // Still water, still body: the arms come to rest on the pose.
        var sim = new ArmPhysics();
        var at = new Vector(500, 500);
        Point Still(Point p) => new Point(p.X * scale + at.X, p.Y * scale + at.Y);
        for (int i = 0; i < 240; i++) sim.Step(1.0 / 60, rig, Still, scale);
        double off = 0;
        for (int a = 0; a < rig.Count; a++)
            for (int j = 0; j <= ArmPhysics.Segments; j++)
                off = Math.Max(off, (sim.Pos[a][j] - Still(rig.Target(a, j, ArmPhysics.Segments, 0))).Length);
        check("arms settle onto their pose", off < 1.5, $"worst {off:F2}px off");

        // Climb at an ordinary 300px/s: the tips hang back below where the pose would
        // put them, which is the whole point. (Along the line of the arms there is
        // nothing to see -- the links just stay straight -- so this goes across it.)
        double lag = 0;
        for (int i = 0; i < 60; i++)
        {
            at += new Vector(0, -300.0 / 60);
            sim.Step(1.0 / 60, rig, Still, scale);
        }
        for (int a = 0; a < rig.Count; a++)
            lag += sim.Pos[a][ArmPhysics.Segments].Y - Still(rig.Target(a, ArmPhysics.Segments, ArmPhysics.Segments, 0)).Y;
        lag /= rig.Count;
        check("arms trail visibly at cruising speed", lag > 10, $"tips {lag:F1}px behind the pose, arm {rig.Length * scale:F0}px");

        // ... and swing back through once it stops.
        for (int i = 0; i < 120; i++) sim.Step(1.0 / 60, rig, Still, scale);
        double settle = 0;
        for (int a = 0; a < rig.Count; a++)
            settle = Math.Max(settle, (sim.Pos[a][ArmPhysics.Segments] - Still(rig.Target(a, ArmPhysics.Segments, ArmPhysics.Segments, 0))).Length);
        check("after a stop the arms come back to rest", settle < 2.5, $"{settle:F2}px off after 2s");

        // Links hold their length, whatever the root does.
        var rough = new ArmPhysics();
        var r = new Random(7);
        double worstLen = 0;
        bool finite = true;
        var p = new Vector(300, 300);
        for (int i = 0; i < 2000; i++)
        {
            // Jittery frame times, sudden lurches, the odd teleport.
            double dt = r.NextDouble() < 0.05 ? 0.2 : 1.0 / (30 + r.Next(120));
            p += new Vector(r.NextDouble() * 60 - 30, r.NextDouble() * 60 - 30);
            if (i % 500 == 499) p += new Vector(1500, -800);
            var at2 = p;
            rough.Step(dt, rig, q => new Point(q.X * scale + at2.X, q.Y * scale + at2.Y), scale);
            if (!rough.Ready) continue;
            for (int a = 0; a < rig.Count; a++)
            {
                double seg = rig.Length * (1 - 0.06 * (a % 3)) / ArmPhysics.Segments * scale;
                for (int j = 1; j <= ArmPhysics.Segments; j++)
                {
                    var pt = rough.Pos[a][j];
                    if (!double.IsFinite(pt.X) || !double.IsFinite(pt.Y)) finite = false;
                    worstLen = Math.Max(worstLen, Math.Abs((pt - rough.Pos[a][j - 1]).Length - seg) / seg);
                }
            }
        }
        check("arm links keep their length", worstLen < 0.02, $"worst {worstLen * 100:F2}% off");
        check("arms never blow up under rough frame times", finite, "");

        // A bad dt must not poison anything.
        var nan = new ArmPhysics();
        nan.Step(1.0 / 60, rig, Still, scale);
        nan.Step(double.NaN, rig, Still, scale);
        nan.Step(-1, rig, Still, scale);
        nan.Step(1.0 / 60, rig, Still, scale);
        check("a broken frame time is ignored", nan.Pos.All(arm => arm.All(q => double.IsFinite(q.X))), "");
    }
}
