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
    private const double Speed = 52;
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

/// <summary>
/// Properly asleep, which is not the same as sitting still. The eye is shut and
/// the animal is off duty; meanwhile the skin goes on firing off colour changes it
/// has no use for, which is the closest an invertebrate gets to dreaming.
///
/// Worth several times more than sitting, in the only currency that matters here:
/// tiredness actually goes away.
/// </summary>
public sealed class SleepBehavior : BehaviorBase
{
    public override string Name => "sleep";
    private double _remaining;
    private double _t;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("sleep");
        _remaining = 22 + c.Rng.NextDouble() * 38;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        _remaining -= dt;
        pet.VisualBob = Math.Sin(_t * 0.9) * 1.6;      // the slow swell of breathing
        // Resting on a perch already pays fatigue back; sleeping pays it back
        // properly, which is what makes it worth going to sleep rather than just
        // sitting down.
        pet.Drives.Fatigue = Math.Max(0, pet.Drives.Fatigue - dt / 26);

        // Anything frightening ends it. A sleeping cuttlefish is not a deaf one.
        if (_remaining <= 0 || pet.Drives.Fear > 0.45) Done = true;
    }
}

/// <summary>
/// Hold station quietly, barely moving. What they do when you have walked away —
/// they simply blend out rather than nodding off.
/// </summary>
public sealed class LurkBehavior : BehaviorBase
{
    public override string Name => "lurk";
    public override bool OverridesPhysics => true;
    private double _t;

    public override void Enter(BehaviorContext c) => c.Pet.Anim.Play("idle");

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.Vel *= Math.Exp(-2.5 * dt);
        pet.Pos += pet.Vel * dt;
        pet.VisualBob = Math.Sin(_t * 1.3) * 2.5;

        // Back the moment you touch anything.
        if (c.World.IdleSeconds < 1.5)
        {
            Next = new WakeStretchBehavior();
            Done = true;
        }
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
