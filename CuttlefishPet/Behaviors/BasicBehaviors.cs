using System.Windows;
using CuttlefishPet.Core;

namespace CuttlefishPet.Behaviors;

public sealed class IdleBehavior : BehaviorBase
{
    public override string Name => "idle";
    private double _remaining, _t;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("idle");
        _remaining = 2 + c.Rng.NextDouble() * 4;
        if (c.Rng.NextDouble() < 0.2) c.Sound.Play("blip", 0.2);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _t += dt;
        c.Pet.VisualBob = Math.Sin(_t * 2.6) * 2.5; // gentle hover
        _remaining -= dt;
        if (_remaining <= 0) Done = true;
    }
}

public sealed class SwimBehavior : BehaviorBase
{
    public override string Name => "swim";
    private const double Speed = 75;
    private double _targetX;
    private bool _mayWalkOff;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("swim");
        double dir = c.Rng.NextDouble() < 0.5 ? -1 : 1;
        double dist = 120 + c.Rng.NextDouble() * 420;
        _targetX = pet.Pos.X + dir * dist;
        _mayWalkOff = c.Rng.NextDouble() < 0.3; // sometimes just swims off the edge
        pet.FacingRight = dir > 0;
    }

    private double _t;

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        if (pet.Surface == null) { Done = true; return; } // walked off; machine forces Fall

        _t += dt;
        pet.VisualBob = Math.Sin(_t * 4.5) * 3.5; // swimming undulation

        double min = pet.Surface.X1 + 8, max = pet.Surface.X2 - 8;
        if (!_mayWalkOff || pet.Surface.Kind is SurfaceKind.Floor or SurfaceKind.TaskbarTop)
            _targetX = Math.Clamp(_targetX, min, max);

        double dx = _targetX - pet.Pos.X;
        if (Math.Abs(dx) < 4) { Done = true; return; }
        pet.FacingRight = dx > 0;
        pet.Pos.X += Math.Sign(dx) * Speed * dt;
    }
}

public sealed class FallBehavior : BehaviorBase
{
    public override string Name => "fall";
    public override bool Interruptible => false;
    private double _peakSpeed;

    public override void Enter(BehaviorContext c) => c.Pet.Anim.Play("fall");

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _peakSpeed = Math.Max(_peakSpeed, pet.Vel.Y);
        if (pet.Surface != null)
        {
            if (_peakSpeed > 700) c.Sound.Play("splat", 0.35);
            pet.Vel = new Vector(0, 0);
            Done = true;
        }
    }
}

public sealed class SitBehavior : BehaviorBase
{
    public override string Name => "sit";
    private double _remaining;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("sit");
        _remaining = 5 + c.Rng.NextDouble() * 10;
        if (c.Rng.NextDouble() < 0.25) c.Sound.Play("bubble", 0.25);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _remaining -= dt;
        if (_remaining <= 0) Done = true;
    }
}

public sealed class SleepBehavior : BehaviorBase
{
    public override string Name => "sleep";
    private readonly bool _away;
    private double _remaining, _snoreIn;

    /// <param name="away">Sleep until the user comes back, then wake with a stretch.</param>
    public SleepBehavior(bool away = false) => _away = away;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("sleep");
        _remaining = 15 + c.Rng.NextDouble() * 25;
        _snoreIn = 4;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _snoreIn -= dt;
        if (_snoreIn <= 0)
        {
            c.Sound.Play("snore", 0.18);
            _snoreIn = 7 + c.Rng.NextDouble() * 4;
        }

        if (_away)
        {
            if (c.World.IdleSeconds < 1.5)
            {
                Next = new WakeStretchBehavior();
                Done = true;
            }
            return;
        }

        _remaining -= dt;
        // Wake if the cursor actively pokes around nearby (a parked cursor doesn't count).
        if ((c.World.Cursor - c.Pet.Pos).Length < 110 && c.World.CursorVelocity.Length > 60)
            _remaining = Math.Min(_remaining, 0.3);
        if (_remaining <= 0) Done = true;
    }
}

public sealed class TypingReactBehavior : BehaviorBase
{
    public override string Name => "typingReact";
    private double _elapsed;

    public override void Enter(BehaviorContext c) => c.Pet.Anim.Play("wiggle");

    public override void Tick(BehaviorContext c, double dt)
    {
        _elapsed += dt;
        if (_elapsed > 8 || c.World.TypingRate < 1) Done = true;
    }
}
