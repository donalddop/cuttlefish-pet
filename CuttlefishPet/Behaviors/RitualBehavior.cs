using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// The rare one. Three to seven of them gather, take up stations around a circle
/// and turn slowly about its centre, every eye locked inward, all wearing the same
/// colour. Nothing else in the tank does anything like it, and it is meant to be
/// something you catch maybe once an evening.
///
/// Each participant runs its own copy, told which station it has. They never
/// communicate — the shared centre and a shared angular speed are enough to keep
/// the ring together, which is also how a real school holds its shape.
/// </summary>
public sealed class RitualBehavior : BehaviorBase
{
    public override string Name => "ritual";
    public override bool OverridesPhysics => true;
    public override bool NeedsPerch => false;

    private const double Gather = 4.5;   // swimming to your station
    private const double Turn = 14.0;    // the circling itself
    private const double Break = 1.6;    // drifting apart again

    private readonly Point _centre;
    private readonly double _radius;
    private readonly double _phase;      // this one's station on the ring
    private readonly double _spin;       // radians per second, shared by all of them
    private double _t;

    public RitualBehavior(Point centre, double radius, double phase, double spin)
    {
        _centre = centre;
        _radius = radius;
        _phase = phase;
        _spin = spin;
    }

    /// <summary>Where this pet should be right now, if the ring is already turning.</summary>
    private Point Station(double t)
    {
        double a = _phase + _spin * Math.Max(0, t - Gather);
        return new Point(_centre.X + Math.Cos(a) * _radius,
                         _centre.Y + Math.Sin(a) * _radius * 0.62);   // seen at an angle
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

        // The whole point: everyone watching the middle, whatever else they do.
        pet.PupilTarget = _centre;

        var want = Station(_t);
        var to = want - pet.Pos;

        if (_t < Gather)
        {
            // Closing on your station. Still looking inward on the way in.
            var desired = to.Length < 1 ? default : to / to.Length * Math.Min(210, to.Length * 2.6);
            pet.Vel += (desired - pet.Vel) * Math.Min(1, 3.0 * dt);
            pet.Pos += pet.Vel * dt;
            PhysicsEngine.ClampToTank(pet, c.World);
            if (Math.Abs(pet.Vel.X) > 12) pet.FacingRight = pet.Vel.X > 0;
            return;
        }

        if (_t < Gather + Turn)
        {
            // Held on station while the ring turns, so the movement you see is the
            // circle itself rather than each of them swimming about.
            pet.Pos += (want - pet.Pos) * Math.Min(1, 6 * dt);
            pet.Vel = new Vector(0, 0);

            // Facing the way it is travelling — the body goes round, the eyes stay in.
            var ahead = Station(_t + 0.25) - Station(_t);
            if (Math.Abs(ahead.X) > 0.4) pet.FacingRight = ahead.X > 0;

            pet.VisualBob = Math.Sin(_t * 2.2 + _phase) * 3;
            pet.Rotation = Math.Sin(_t * 1.1 + _phase) * 6;

            if (c.Rng.NextDouble() < dt * 0.25)
                c.Renderer.SpawnBubble(pet.Pos + new Vector(0, -32));
            return;
        }

        // Breaking up: everyone drifts outward and goes back to their own business.
        pet.Rotation = 0;
        var out_ = pet.Pos - _centre;
        if (out_.Length > 1)
        {
            out_.Normalize();
            pet.Vel += (out_ * 120 - pet.Vel) * Math.Min(1, 2.2 * dt);
        }
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);

        if (_t > Gather + Turn + Break)
        {
            Next = new SwimFreeBehavior();
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c)
    {
        c.Pet.PupilTarget = null;
        c.Pet.Rotation = 0;
    }
}
