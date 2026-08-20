using System.Windows;
using CuttlefishPet.Core;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Cuttlefish hunting sequence aimed at a stationary cursor: creep closer while
/// running a passing-cloud display over the mantle, then shoot the feeding
/// tentacles out. Hitting the cursor earns a pleased flush; a miss just deflates.
/// </summary>
public sealed class HuntCursorBehavior : BehaviorBase
{
    public override string Name => "hunt";
    private enum Phase { Stalk, Strike }
    private Phase _phase = Phase.Stalk;
    private double _elapsed;

    public static bool Possible(BehaviorContext c)
    {
        var pet = c.Pet;
        if (pet.Surface == null || c.World.CursorStill < 0.8) return false;
        var cur = c.World.Cursor;
        return Math.Abs(cur.X - pet.Pos.X) is > 60 and < 420 && Math.Abs(cur.Y - pet.Pos.Y) < 130;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("hunt", restart: true);
        c.Pet.FacingRight = c.World.Cursor.X > c.Pet.Pos.X;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _elapsed += dt;
        if (pet.Surface == null) { Done = true; return; }

        double dx = c.World.Cursor.X - pet.Pos.X;

        if (_phase == Phase.Stalk)
        {
            pet.VisualBob = Math.Sin(_elapsed * 3.2) * 1.5;
            if (c.World.CursorVelocity.Length > 400) { Done = true; return; } // prey bolted
            pet.FacingRight = dx > 0;
            if (Math.Abs(dx) > 55)
            {
                double nx = pet.Pos.X + Math.Sign(dx) * 45 * dt;  // slow creep
                pet.Pos.X = Math.Clamp(nx, pet.Surface.X1 + 8, pet.Surface.X2 - 8);
                if (_elapsed > 9) Done = true;
            }
            else
            {
                _phase = Phase.Strike;
                pet.Anim.Play("strike", restart: true);
                c.Sound.Play("blip", 0.3);
            }
        }
        else if (pet.Anim.Finished)
        {
            bool hit = Math.Abs(c.World.Cursor.X - pet.Pos.X) < 90;
            if (hit) Next = new HappyBehavior(1.2);
            Done = true;
        }
    }
}

/// <summary>Swim over to a shrimp, then eat it.</summary>
public sealed class HuntTreatBehavior : BehaviorBase
{
    public override string Name => "huntTreat";
    private readonly Treat _treat;
    private double _elapsed;

    public HuntTreatBehavior(Treat treat) => _treat = treat;

    public override void Enter(BehaviorContext c)
    {
        _treat.ClaimedBy = c.Pet;
        c.Pet.Anim.Play("swim");
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _elapsed += dt;
        if (_treat.Expired || pet.Surface == null || _elapsed > 14) { Done = true; return; }

        double dx = _treat.Pos.X - pet.Pos.X;
        double dy = _treat.Pos.Y - pet.Pos.Y;

        if (Math.Abs(dy) > 70)
        {
            // Shrimp landed on another level — hop across to its surface if we can.
            var target = JumpToWindowBehavior.FindTarget(c);
            if (target != null && Math.Abs(target.Y - _treat.Pos.Y) < 40)
                Next = new JumpToWindowBehavior(target);
            Done = true;
            return;
        }

        pet.VisualBob = Math.Sin(_elapsed * 6) * 3;
        if (Math.Abs(dx) < 26)
        {
            Next = new EatTreatBehavior(_treat);
            Done = true;
            return;
        }

        pet.FacingRight = dx > 0;
        double nx = pet.Pos.X + Math.Sign(dx) * 135 * dt;  // eager dash
        pet.Pos.X = Math.Clamp(nx, pet.Surface.X1 + 8, pet.Surface.X2 - 8);
    }

    public override void Exit(BehaviorContext c)
    {
        if (_treat.ClaimedBy == c.Pet && Next is not EatTreatBehavior)
            _treat.ClaimedBy = null;
    }
}

public sealed class EatTreatBehavior : BehaviorBase
{
    public override string Name => "eat";
    public override bool Interruptible => false;
    private readonly Treat _treat;
    private double _t;
    private bool _grabbed;

    public EatTreatBehavior(Treat treat) => _treat = treat;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("strike", restart: true);
        c.Pet.FacingRight = _treat.Pos.X > c.Pet.Pos.X;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _t += dt;
        if (!_grabbed && _t > 0.35)
        {
            _grabbed = true;
            _treat.Eaten = true;                 // snatched by the feeding tentacles
            c.Pet.Anim.Play("eat", restart: true);
            c.Sound.Play("blip", 0.35);
        }
        if (_t > 1.6)
        {
            Next = new HappyBehavior(1.6);
            Done = true;
        }
    }
}

/// <summary>Pink flush and a bouncy flourish: petted, or pleased with a meal.</summary>
public sealed class HappyBehavior : BehaviorBase
{
    public override string Name => "happy";
    private double _remaining, _bubbleIn = 0.15, _t;

    public HappyBehavior(double seconds = 1.5) => _remaining = seconds;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("happy", restart: true);
        c.Sound.Play("bubble", 0.3);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _t += dt;
        _remaining -= dt;
        c.Pet.VisualBob = Math.Sin(_t * 9) * 4;
        _bubbleIn -= dt;
        if (_bubbleIn <= 0)
        {
            _bubbleIn = 0.32;
            c.Renderer.SpawnBubble(c.Pet.Pos + new Vector(
                (c.Pet.FacingRight ? 14 : -14) + c.Rng.Next(-6, 7), -46));
        }
        if (_remaining <= 0) Done = true;
    }
}

/// <summary>
/// Two cuttlefish met: both flash the zebra rival display, then the loser jets off.
/// </summary>
public sealed class RivalDisplayBehavior : BehaviorBase
{
    public override string Name => "rival";
    public override bool Interruptible => false;
    private readonly Pet _other;
    private readonly bool _retreats;
    private double _t;

    public RivalDisplayBehavior(Pet other, bool retreats)
    {
        _other = other;
        _retreats = retreats;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("zebra", restart: true);
        c.Pet.FacingRight = _other.Pos.X > c.Pet.Pos.X;
        c.Sound.Play("blip", 0.25);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _t += dt;
        c.Pet.VisualBob = Math.Sin(_t * 11) * 3;
        if (_t < 2.2) return;

        if (_retreats)
        {
            var pet = c.Pet;
            double away = pet.Pos.X >= _other.Pos.X ? 1 : -1;
            pet.Surface = null;
            pet.Vel = new Vector(away * 700, -420);
            pet.FacingRight = away > 0;
            c.Renderer.SpawnInk(pet.Pos);
            c.Sound.Play("squirt", 0.3);
            Next = new FallBehavior();
        }
        Done = true;
    }
}

/// <summary>A window just popped up next to the pet — jump out of your skin.</summary>
public sealed class StartleBehavior : BehaviorBase
{
    public override string Name => "startle";
    public override bool Interruptible => false;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("startle", restart: true);
        pet.Surface = null;
        pet.Vel = new Vector(pet.FacingRight ? -180 : 180, -430);
        c.Sound.Play("blip", 0.4);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        if (c.Pet.Surface != null) Done = true;
    }
}

/// <summary>Waking up after the user comes back: a long stretch.</summary>
public sealed class WakeStretchBehavior : BehaviorBase
{
    public override string Name => "stretch";

    public override void Enter(BehaviorContext c) => c.Pet.Anim.Play("stretch", restart: true);

    public override void Tick(BehaviorContext c, double dt)
    {
        if (c.Pet.Anim.Finished) Done = true;
    }
}

/// <summary>Shuffle to the lip of the current surface and peer over the edge.</summary>
public sealed class PeekBehavior : BehaviorBase
{
    public override string Name => "peek";
    private double _edgeX, _t;
    private bool _atEdge;

    public static bool Possible(BehaviorContext c)
    {
        var s = c.Pet.Surface;
        return s is { Kind: SurfaceKind.WindowTop or SurfaceKind.TaskbarTop } && s.X2 - s.X1 > 120;
    }

    public override void Enter(BehaviorContext c)
    {
        var s = c.Pet.Surface!;
        // Whichever lip is nearer.
        _edgeX = Math.Abs(s.X1 - c.Pet.Pos.X) < Math.Abs(s.X2 - c.Pet.Pos.X) ? s.X1 + 10 : s.X2 - 10;
        c.Pet.Anim.Play("swim");
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        if (pet.Surface == null) { Done = true; return; }
        _t += dt;

        if (!_atEdge)
        {
            double dx = _edgeX - pet.Pos.X;
            if (Math.Abs(dx) < 5 || _t > 6)
            {
                _atEdge = true;
                _t = 0;
                pet.Anim.Play("peek", restart: true);
                pet.FacingRight = _edgeX > pet.Surface.X1 + (pet.Surface.X2 - pet.Surface.X1) / 2;
            }
            else
            {
                pet.FacingRight = dx > 0;
                pet.Pos.X += Math.Sign(dx) * 80 * dt;
            }
            return;
        }

        pet.VisualBob = Math.Sin(_t * 2.2) * 2;
        if (_t > 3 + c.Rng.NextDouble() * 3) Done = true;
    }
}
