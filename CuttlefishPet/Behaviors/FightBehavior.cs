using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// A display that goes past display. Cuttlefish usually settle these with colour
/// alone — the zebra pattern in RivalDisplayBehavior is the whole argument — but
/// when neither backs down they grapple, and one of them can come off badly.
///
/// Both sides run their own copy of this, told in advance who wins. The loser
/// takes real damage: mostly it just limps off pale and shaken, but sometimes
/// that is the end of it. That is the only violent death in the tank, and it is
/// rare enough to be an event rather than a mechanic.
/// </summary>
public sealed class FightBehavior : BehaviorBase
{
    public override string Name => "fight";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;
    public override bool NeedsPerch => false;

    private const double Circling = 1.6;   // sizing each other up
    private const double Grappling = 2.4;  // arms locked, thrashing
    private const double Aftermath = 1.4;

    private readonly Pet _other;
    private readonly bool _wins;
    private readonly bool _fatal;
    private readonly Action<Pet>? _prize;
    private double _t;
    private bool _settled;

    /// <param name="prize">Handed the winner once it is over: the thing they fought over.</param>
    public FightBehavior(Pet other, bool wins, bool fatal, Action<Pet>? prize = null)
    {
        _other = other;
        _wins = wins;
        _fatal = fatal;
        _prize = prize;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("zebra", restart: true);
        c.Pet.Surface = null;
        c.Pet.FacingRight = _other.Pos.X > c.Pet.Pos.X;
        c.Sound.Play("blip", 0.3);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.PupilTarget = _other.Pos;
        var to = _other.Pos - pet.Pos;

        if (_t < Circling)
        {
            // Sidling in, broadside on, making themselves as wide as they can.
            var want = to.Length < 1 ? default : to / to.Length * Math.Max(0, to.Length - 70);
            pet.Vel += (want - pet.Vel) * Math.Min(1, 2.2 * dt);
            pet.Pos += pet.Vel * dt;
            PhysicsEngine.ClampToTank(pet, c.World);
            pet.VisualBob = Math.Sin(_t * 9) * 4;
            pet.FacingRight = to.X > 0;
            return;
        }

        if (_t < Circling + Grappling)
        {
            // Locked together and thrashing: shoved about, ink in the water.
            double k = (_t - Circling) / Grappling;
            var want = to.Length < 1 ? default : to / to.Length * 90;
            pet.Vel += (want - pet.Vel) * Math.Min(1, 3.5 * dt);
            pet.Pos += pet.Vel * dt + new Vector(Math.Sin(_t * 24) * 90 * dt,
                                                 Math.Cos(_t * 19) * 70 * dt);
            PhysicsEngine.ClampToTank(pet, c.World);
            pet.Rotation = Math.Sin(_t * 17) * 14;
            if (pet.Anim.Finished) pet.Anim.Play("zebra", restart: true);
            if (c.Rng.NextDouble() < dt * 2.2)
                c.Renderer.SpawnBubble(pet.Pos + new Vector(c.Rng.Next(-20, 21), -30));
            if (!_wins && k > 0.75 && c.Rng.NextDouble() < dt * 3)
                c.Renderer.SpawnInk(pet.Pos);
            return;
        }

        if (!_settled)
        {
            _settled = true;
            pet.Rotation = 0;
            pet.Pestered = Math.Min(1, pet.Pestered + 0.5);

            if (_wins)
            {
                _prize?.Invoke(pet);   // to the victor, the shrimp
                pet.Anim.Play("happy", restart: true);
                c.Sound.Play("blip", 0.35);
            }
            else if (_fatal)
            {
                // Beaten badly enough that it does not recover.
                c.Renderer.SpawnInk(pet.Pos);
                c.Sound.Play("squirt", 0.4);
                Next = new DyingBehavior();
                Done = true;
                return;
            }
            else
            {
                // Bolts, pale and shaken, and carries the injury: whatever life it
                // had left is cut short by a few minutes.
                double away = pet.Pos.X >= _other.Pos.X ? 1 : -1;
                pet.Vel = new Vector(away * 620, -380);
                pet.Lifespan = Math.Max(pet.Age + 60, pet.Lifespan - 180);
                c.Renderer.SpawnInk(pet.Pos);
                c.Sound.Play("squirt", 0.35);
                Next = new DriftBehavior();
                Done = true;
                return;
            }
        }

        pet.Vel *= Math.Exp(-2.5 * dt);
        pet.Pos += pet.Vel * dt;
        pet.VisualBob = Math.Sin(_t * 7) * 3;
        if (_t > Circling + Grappling + Aftermath)
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
