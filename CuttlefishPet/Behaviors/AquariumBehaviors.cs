using System.Windows;
using CuttlefishPet.Core;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Open-water cruising: pick a spot anywhere in the tank and glide to it, fins
/// rippling, with a slow vertical weave on the way.
/// </summary>
public sealed class SwimFreeBehavior : BehaviorBase
{
    public override string Name => "swimFree";
    public override bool OverridesPhysics => true;

    private const double Cruise = 78;
    private const double Turn = 1.7;   // how lazily it swings onto a new heading
    private Point _target;
    private double _t, _weave;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
        _target = PickTarget(c);
        _weave = c.Rng.NextDouble() * 6.28;
    }

    /// <summary>
    /// Head for the corner of the tank this pet has neglected longest, with enough
    /// randomness that two cuttlefish don't tour in lockstep.
    /// </summary>
    private static Point PickTarget(BehaviorContext c)
    {
        var t = c.World.VirtualScreen;
        var pet = c.Pet;

        int best = -1;
        double bestScore = double.MinValue;
        for (int i = 0; i < 9; i++)
        {
            double score = pet.RegionAge[i] * (0.6 + c.Rng.NextDouble() * 0.8);
            if (score > bestScore) { bestScore = score; best = i; }
        }

        double cellW = t.Width / 3, cellH = t.Height / 3;
        double x0 = t.Left + best % 3 * cellW, y0 = t.Top + best / 3 * cellH;
        // Inset so the target is reachable without fighting the tank walls.
        return new Point(x0 + 70 + c.Rng.NextDouble() * Math.Max(1, cellW - 140),
                         y0 + 80 + c.Rng.NextDouble() * Math.Max(1, cellH - 150));
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        _weave += dt * 2.2;

        var to = _target - pet.Pos;
        double dist = to.Length;
        if (dist < 40 || _t > 14) { Done = true; return; }

        var desired = to / dist * Cruise;
        // Weave perpendicular to the heading so the path is never a straight line.
        var side = new Vector(-to.Y, to.X) / dist;
        desired += side * Math.Sin(_weave) * 34;

        pet.Vel += (desired - pet.Vel) * Math.Min(1, Turn * dt);
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);

        if (Math.Abs(pet.Vel.X) > 12) pet.FacingRight = pet.Vel.X > 0;
        pet.VisualBob = Math.Sin(_weave * 1.6) * 2.5;
        pet.Rotation = Math.Clamp(pet.Vel.Y * 0.045, -16, 16) * (pet.FacingRight ? 1 : -1);
    }

    public override void Exit(BehaviorContext c) => c.Pet.Rotation = 0;
}

/// <summary>
/// Coasting after a shove or a throw: tumble while fast, right yourself as the water
/// takes the speed out, then swim off as if nothing happened.
/// </summary>
public sealed class DriftBehavior : BehaviorBase
{
    public override string Name => "drift";
    public override bool Interruptible => false;
    private double _spin;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("fall");
        _spin = c.Pet.Vel.Length > 700 ? (c.Rng.NextDouble() < 0.5 ? -1 : 1) : 0;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        double speed = pet.Vel.Length;

        if (speed > 260)
        {
            pet.Rotation += _spin * speed * dt * 0.55;   // tumbling through the water
        }
        else
        {
            pet.Anim.Play("swim");
            pet.Rotation *= Math.Max(0, 1 - dt * 4);     // right yourself again
            if (Math.Abs(pet.Vel.X) > 8) pet.FacingRight = pet.Vel.X > 0;
        }

        if (speed < 55)
        {
            pet.Rotation = 0;
            Next = new SwimFreeBehavior();
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.Rotation = 0;
}

/// <summary>A short burst of jet propulsion — the cuttlefish sprint.</summary>
public sealed class DartBehavior : BehaviorBase
{
    public override string Name => "dart";
    public override bool Interruptible => false;
    private double _t;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("jump", restart: true);
        pet.Surface = null;
        double dir = pet.FacingRight ? 1 : -1;
        if (c.Rng.NextDouble() < 0.3) dir = -dir;
        pet.Vel = new Vector(dir * 680, (c.Rng.NextDouble() - 0.6) * 200);
        pet.FacingRight = dir > 0;
        c.Sound.Play("blip", 0.25);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _t += dt;
        var pet = c.Pet;
        pet.Vel = new Vector(pet.Vel.X * Math.Exp(-1.4 * dt), pet.Vel.Y * Math.Exp(-2.2 * dt));
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);
        if (_t > 0.9 || pet.Vel.Length < 120)
        {
            Next = new SwimFreeBehavior();
            Done = true;
        }
    }

    public override bool OverridesPhysics => true;
}

/// <summary>
/// Swim over to a ledge, wall or ceiling and take hold of it. Everything the pet
/// does while perched follows from here.
/// </summary>
public sealed class SettleBehavior : BehaviorBase
{
    public override string Name => "settle";
    public override bool OverridesPhysics => true;

    private readonly Surface _target;
    private Point _spot;
    private double _t;

    public SettleBehavior(Surface target, Point spot)
    {
        _target = target;
        _spot = spot;
    }

    /// <summary>Pick somewhere worth resting: a nearby ledge, wall or the ceiling.</summary>
    public static SettleBehavior? Find(BehaviorContext c)
    {
        var pet = c.Pet;
        var options = new List<(Surface s, Point p)>();

        foreach (var s in c.World.Surfaces)
        {
            Point spot;
            if (s.IsVertical)
            {
                double y = Math.Clamp(pet.Pos.Y, s.Y + 40, s.Y2 - 40);
                if (s.Y2 - s.Y < 120) continue;
                spot = new Point(s.X1 + s.ClingOffset, y);
            }
            else
            {
                if (s.X2 - s.X1 < 90) continue;
                double x = Math.Clamp(pet.Pos.X, s.X1 + 20, s.X2 - 20);
                spot = new Point(x, s.Y);
            }
            if ((spot - pet.Pos).Length < 700) options.Add((s, spot));
        }

        if (options.Count == 0) return null;
        var (surface, point) = options[c.Rng.Next(options.Count)];
        return new SettleBehavior(surface, point);
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        var live = c.World.Find(_target, _spot.X);
        if (live == null || _t > 12) { Next = new SwimFreeBehavior(); Done = true; return; }
        _spot = live.IsVertical
            ? new Point(live.X1 + live.ClingOffset, _spot.Y)
            : new Point(Math.Clamp(_spot.X, live.X1 + 20, live.X2 - 20), live.Y);

        var to = _spot - pet.Pos;
        if (to.Length < 12)
        {
            pet.Pos = _spot;
            pet.Vel = new Vector(0, 0);
            pet.Surface = live;
            pet.Anim.Play(PerchAnim(live));
            Done = true;
            return;
        }

        var desired = to / to.Length * Math.Min(112, to.Length * 2.0);
        pet.Vel += (desired - pet.Vel) * Math.Min(1, 3.5 * dt);
        pet.Pos += pet.Vel * dt;
        if (Math.Abs(pet.Vel.X) > 12) pet.FacingRight = pet.Vel.X > 0;
        pet.VisualBob = Math.Sin(_t * 5) * 2;
    }

    private static string PerchAnim(Surface s) => s.Kind switch
    {
        SurfaceKind.Ceiling => "ceiling",
        SurfaceKind.WindowLeft or SurfaceKind.WindowRight
            or SurfaceKind.ScreenLeft or SurfaceKind.ScreenRight => "climb",
        _ => "sit",
    };
}

/// <summary>Push off a perch and go back to open water.</summary>
public sealed class LeavePerchBehavior : BehaviorBase
{
    public override string Name => "leave";

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        var from = pet.Surface;
        pet.Surface = null;
        pet.Anim.Play("swim");

        // Shove off away from whatever it was holding on to.
        var away = from?.Kind switch
        {
            SurfaceKind.Ceiling => new Vector(c.Rng.NextDouble() * 120 - 60, 190),
            SurfaceKind.ScreenLeft or SurfaceKind.WindowRight => new Vector(210, -60),
            SurfaceKind.ScreenRight or SurfaceKind.WindowLeft => new Vector(-210, -60),
            _ => new Vector(c.Rng.NextDouble() * 160 - 80, -210),
        };
        pet.Vel = away;
        Next = new SwimFreeBehavior();
        Done = true;
    }
}
