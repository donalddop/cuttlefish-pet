using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>One squid in a passing school. Pure numbers; the picture lives in PetManager.</summary>
public sealed class Squid
{
    public Point Pos;
    public Vector Vel;
    /// <summary>Which way the head points. Fixed for the whole crossing: squid do not turn round, they reverse.</summary>
    public bool FacingRight;
    /// <summary>Own size on top of the depth scale, so a school is not a row of clones.</summary>
    public double Size = 1;
    /// <summary>0 = far back of the tank, 1 = right up against the glass.</summary>
    public double Depth;
    /// <summary>Fin clock, in seconds of animation. Runs faster while jetting.</summary>
    public double Flap;
    /// <summary>What is left of the last jet, 1 at the pulse and decaying to 0.</summary>
    public double Jet;
    public double JetIn;
    /// <summary>Degrees of nose-down tilt, smoothed.</summary>
    public double Bank;
}

/// <summary>
/// A school of squid drifting across the screen, after the Monterey Bay Aquarium
/// footage: a dozen of them in loose formation, all facing the same way, gliding
/// rather than swimming -- a pulse of the mantle, a long coast, the fins sculling
/// to hold station in between.
///
/// Classic boids underneath (keep your distance, match your neighbours, stay with
/// the group), steered by a goal velocity that carries the school from one side of
/// the screen to the other along a lane that wanders a little. Two things make it
/// read as squid rather than fish:
///
/// * They swim both ways. Some schools come through tail first, and now and then a
///   school that is cruising head first stops and backs off for a few seconds. A
///   squid's facing never changes during a crossing -- only its direction of travel
///   does -- because that is what a jet-propelled animal does, and because a sprite
///   flipping round mid-screen is the first thing that would give the game away.
/// * Speed comes in pulses. Each squid jets on its own irregular clock and coasts
///   down from it; the fins beat faster during the pulse.
/// </summary>
public sealed class SquidSchool
{
    public readonly List<Squid> Members = new();
    /// <summary>The direction the school is crossing in, overall.</summary>
    public bool HeadingRight { get; private set; }
    /// <summary>True when the heads point against the direction of travel.</summary>
    public bool TailFirst { get; private set; }
    public double Cruise { get; private set; }
    public double Age { get; private set; }
    /// <summary>Seconds left of backing off, while a head-first school reverses for a moment.</summary>
    public double BackingOff { get; private set; }

    private Rect _tank;
    private double _laneY;
    private double _laneDrift;
    private double _nextBackOff;

    public const double SeparationRadius = 70;
    public const double NeighbourRadius = 240;
    public const double MaxBank = 22;
    /// <summary>Hard stop on a crossing that has somehow stalled, in seconds.</summary>
    public const double MaxAge = 150;

    public static SquidSchool Create(Rect tank, Random rng, int? count = null, bool? fromLeft = null)
    {
        var s = new SquidSchool { _tank = tank };
        bool left = fromLeft ?? rng.NextDouble() < 0.5;
        s.HeadingRight = left;
        s.TailFirst = rng.NextDouble() < 0.35;
        s.Cruise = 72 + rng.NextDouble() * 26;
        // The upper half of the screen, mostly: that is open water, away from the
        // taskbar where the cuttlefish spend their time.
        s._laneY = tank.Top + tank.Height * (0.14 + rng.NextDouble() * 0.40);
        s._laneDrift = rng.NextDouble() * Math.PI * 2;
        s._nextBackOff = s.TailFirst ? double.MaxValue : 9 + rng.NextDouble() * 10;

        int n = count ?? rng.Next(8, 15);
        bool facing = s.HeadingRight ^ s.TailFirst;
        double dir = s.HeadingRight ? 1 : -1;
        for (int i = 0; i < n; i++)
        {
            // Staggered in from off-screen, in a loose blob rather than a line.
            double back = 120 + rng.NextDouble() * 420;
            var sq = new Squid
            {
                Pos = new Point(left ? tank.Left - back : tank.Right + back,
                                s._laneY + (rng.NextDouble() - 0.5) * 230),
                FacingRight = facing,
                Size = 0.78 + rng.NextDouble() * 0.40,
                Depth = 0.12 + rng.NextDouble() * 0.45,
                Flap = rng.NextDouble() * 2,
                JetIn = rng.NextDouble() * 3,
            };
            sq.Vel = new Vector(dir * s.Cruise * (0.85 + rng.NextDouble() * 0.3), 0);
            s.Members.Add(sq);
        }
        return s;
    }

    /// <summary>The way the school wants to go right now, before the boids rules.</summary>
    private Vector Goal(Point centre)
    {
        double dir = HeadingRight ? 1 : -1;
        if (BackingOff > 0) dir = -dir * 0.55;
        double lane = _laneY + Math.Sin(Age * 0.21 + _laneDrift) * 70;
        return new Vector(dir * Cruise, Math.Clamp((lane - centre.Y) * 0.25, -30, 30));
    }

    public void Step(double dt, Random rng)
    {
        if (dt <= 0 || Members.Count == 0) return;
        dt = Math.Min(dt, 0.1);
        Age += dt;

        if (BackingOff > 0) BackingOff -= dt;
        else if (Age >= _nextBackOff)
        {
            BackingOff = 2.5 + rng.NextDouble() * 2.5;
            _nextBackOff = Age + BackingOff + 12 + rng.NextDouble() * 14;
        }

        var centre = new Point(Members.Average(m => m.Pos.X), Members.Average(m => m.Pos.Y));
        var goal = Goal(centre);

        foreach (var m in Members)
        {
            Vector sep = default, align = default;
            Point coh = default;
            int near = 0;
            foreach (var o in Members)
            {
                if (ReferenceEquals(o, m)) continue;
                var d = m.Pos - o.Pos;
                double dist = d.Length;
                if (dist < 0.01) { d = new Vector(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5); dist = 0.5; }
                double personal = SeparationRadius * (m.Size + o.Size) * 0.5;
                if (dist < personal) sep += d / dist * (personal - dist) / personal;
                if (dist < NeighbourRadius)
                {
                    align += o.Vel;
                    coh = new Point(coh.X + o.Pos.X, coh.Y + o.Pos.Y);
                    near++;
                }
            }

            var acc = (goal - m.Vel) * 0.7 + sep * 140;
            if (near > 0)
            {
                align /= near;
                var c = new Point(coh.X / near, coh.Y / near);
                acc += (align - m.Vel) * 0.45 + (c - m.Pos) * 0.06;
            }

            // The jet: a sharp shove along the line of travel, then a coast.
            m.JetIn -= dt;
            if (m.JetIn <= 0)
            {
                m.Jet = 1;
                m.JetIn = 2.2 + rng.NextDouble() * 3.8;
                var along = m.Vel.Length > 1 ? m.Vel / m.Vel.Length : new Vector(HeadingRight ? 1 : -1, 0);
                m.Vel += along * Cruise * 0.55;
            }
            m.Jet *= Math.Exp(-dt * 2.6);

            m.Vel += acc * dt;
            double speed = m.Vel.Length;
            double top = Cruise * 2.0, floor = Cruise * 0.25;
            if (speed > top) m.Vel *= top / speed;
            else if (speed < floor && speed > 0.01) m.Vel *= floor / speed;
            m.Pos += m.Vel * dt;

            m.Flap += dt * (1 + 1.8 * m.Jet);

            // Nose follows the line of travel when going forwards; going tail first
            // it is the tail that leads, so the same climb tips the nose down.
            bool forwards = (m.Vel.X >= 0) == m.FacingRight;
            double slope = Math.Atan2(m.Vel.Y, Math.Max(1, Math.Abs(m.Vel.X))) * 180 / Math.PI;
            double target = Math.Clamp(forwards ? slope : -slope, -MaxBank, MaxBank);
            m.Bank += (target - m.Bank) * Math.Min(1, dt * 3);
        }
    }

    /// <summary>Everybody is off the far side of the screen, or it has gone on far too long.</summary>
    public bool Gone
    {
        get
        {
            if (Age > MaxAge) return true;
            const double margin = 260;
            return HeadingRight
                ? Members.All(m => m.Pos.X > _tank.Right + margin)
                : Members.All(m => m.Pos.X < _tank.Left - margin);
        }
    }
}
