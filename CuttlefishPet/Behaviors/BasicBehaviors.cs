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
/// Drift quietly, going nowhere in particular. What they do when you have
/// walked away — they blend out rather than nodding off.
///
/// Quiet, not stopped. The first version damped the velocity to nothing within
/// a second and then held it there until someone touched the mouse, so a tank
/// nobody had prodded was a still image rather than a slow one. The whole point
/// of the thing is a desktop that is alive in the corner of your eye, and it
/// cannot be alive and motionless.
/// </summary>
public sealed class LurkBehavior : BehaviorBase
{
    public override string Name => "lurk";
    public override bool OverridesPhysics => true;
    private double _t;
    private Vector _drift;
    private readonly double _hold;

    /// <param name="hold">
    /// Seconds to stay put regardless of input. Zero in the tank -- they wake
    /// the instant anyone touches anything. The debug command passes a few, so
    /// the drift can be watched without twelve minutes of nobody breathing on
    /// the mouse, which is otherwise the only way to see it at all.
    /// </param>
    public LurkBehavior(double hold = 0) => _hold = hold;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("idle");
        // Its own heading, or a tank full of them slides one way together like
        // a screensaver.
        double a = c.Rng.NextDouble() * Math.PI * 2;
        _drift = new Vector(Math.Cos(a), Math.Sin(a) * 0.45) * (15 + c.Rng.NextDouble() * 16);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        // The swell never drops far: at the bottom of the old one they were
        // making one or two pixels a second, which the eye reads as stopped.
        var want = _drift * (0.78 + 0.22 * Math.Sin(_t * 0.23));
        pet.Vel += (want - pet.Vel) * Math.Min(1, dt * 0.8);
        pet.Pos += pet.Vel * dt;
        if (Math.Abs(pet.Vel.X) > 4) pet.FacingRight = pet.Vel.X > 0;
        pet.VisualBob = Math.Sin(_t * 1.3) * 2.5;
        PhysicsEngine.ClampToTank(pet, c.World);

        // Back the moment you touch anything.
        if (c.World.IdleSeconds < 1.5 && _t >= _hold)
        {
            Next = new WakeStretchBehavior();
            Done = true;
        }
        // Otherwise pick a new heading now and then, so it wanders instead of
        // sliding down one line until it meets the edge.
        else if (_t > 26)
        {
            Next = new LurkBehavior(Math.Max(0, _hold - _t));
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
