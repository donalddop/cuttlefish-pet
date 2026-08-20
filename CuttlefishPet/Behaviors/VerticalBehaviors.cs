using System.Windows;
using CuttlefishPet.Core;

namespace CuttlefishPet.Behaviors;

/// <summary>Hand-over-hand along the ceiling, body dangling underneath.</summary>
public sealed class CeilingWalkBehavior : BehaviorBase
{
    public override string Name => "ceiling";
    private const double Speed = 60;
    private double _targetX, _t;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("ceiling");
        double dir = c.Rng.NextDouble() < 0.5 ? -1 : 1;
        _targetX = pet.Pos.X + dir * (150 + c.Rng.NextDouble() * 500);
        pet.FacingRight = dir > 0;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        if (pet.Surface is not { Kind: SurfaceKind.Ceiling }) { Done = true; return; }

        _t += dt;
        pet.VisualBob = Math.Sin(_t * 3.4) * 2;
        _targetX = Math.Clamp(_targetX, pet.Surface.X1 + 10, pet.Surface.X2 - 10);

        double dx = _targetX - pet.Pos.X;
        if (Math.Abs(dx) < 5 || _t > 14)
        {
            // Arrived: either keep strolling, or let go and drop.
            if (c.Rng.NextDouble() < 0.35)
            {
                pet.Surface = null;
                pet.Vel = new Vector(0, 120);   // let go and sink away
                Next = new SwimFreeBehavior();
            }
            Done = true;
            return;
        }
        pet.FacingRight = dx > 0;
        pet.Pos.X += Math.Sign(dx) * Speed * dt;
    }
}

/// <summary>
/// Slip over the lip of the current ledge and dangle from it by two arms, swinging.
/// Enough swing and the pet launches itself off sideways.
/// </summary>
public sealed class HangBehavior : BehaviorBase
{
    public override string Name => "hang";
    public override bool OverridesPhysics => true;

    private readonly bool _launch;
    private Surface _ledge = null!;
    private double _t, _swing;

    public HangBehavior(bool launch = false) => _launch = launch;

    public static bool Possible(BehaviorContext c) =>
        c.Pet.Surface is { Kind: SurfaceKind.WindowTop or SurfaceKind.TaskbarTop } and { } s
        && s.X2 - s.X1 > 140;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        _ledge = pet.Surface!;
        // Slide to the nearer lip and drop over the side of it.
        bool leftLip = Math.Abs(_ledge.X1 - pet.Pos.X) < Math.Abs(_ledge.X2 - pet.Pos.X);
        pet.Pos = new Point(leftLip ? _ledge.X1 + 6 : _ledge.X2 - 6, _ledge.Y);
        pet.FacingRight = leftLip;
        pet.Anim.Play("hang", restart: true);
        pet.Vel = new Vector(0, 0);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        var ledge = c.World.Find(_ledge, pet.Pos.X);
        if (ledge == null) { pet.Rotation = 0; pet.Surface = null; Done = true; return; }

        pet.Pos = new Point(pet.Pos.X + ledge.X1 - _ledge.X1, ledge.Y);
        _ledge = ledge;
        _t += dt;

        if (_launch)
        {
            _swing = Math.Min(1, _swing + dt * 0.5);          // wind up
            pet.Rotation = Math.Sin(_t * 5) * 34 * _swing;
            if (_swing >= 1 && Math.Abs(pet.Rotation) > 30)
            {
                double dir = Math.Sign(pet.Rotation) * (pet.FacingRight ? 1 : -1);
                pet.Rotation = 0;
                pet.Surface = null;
                pet.Vel = new Vector(dir * 640, -520);
                c.Sound.Play("blip", 0.3);
                Next = new DriftBehavior();
                Done = true;
            }
            return;
        }

        pet.Rotation = Math.Sin(_t * 2.1) * 11;               // idle dangle
        if (_t > 4 + c.Rng.NextDouble() * 5)
        {
            pet.Rotation = 0;
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.Rotation = 0;
}

/// <summary>
/// Hover in place, fins rippling, going nowhere — the cuttlefish version of standing
/// still. Used in open water where sitting on something is not an option.
/// </summary>
public sealed class HoverBehavior : BehaviorBase
{
    public override string Name => "hover";
    public override bool OverridesPhysics => true;
    private double _t, _remaining;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("idle");
        _remaining = 2 + c.Rng.NextDouble() * 4;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        _remaining -= dt;
        pet.Vel *= Math.Exp(-2.5 * dt);
        pet.Pos += pet.Vel * dt;
        pet.VisualBob = Math.Sin(_t * 2.4) * 3.5;
        pet.FacingRight = c.World.Cursor.X > pet.Pos.X;
        if (_remaining <= 0) Done = true;
    }
}
