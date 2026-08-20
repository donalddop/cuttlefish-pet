using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Something new appeared on the desktop. A pet close enough to be startled bolts;
/// one further off comes over to have a proper look at it instead.
/// </summary>
public sealed class InspectBehavior : BehaviorBase
{
    public override string Name => "inspect";
    public override bool OverridesPhysics => true;

    private readonly Rect _what;
    private double _t;
    private bool _arrived;

    public InspectBehavior(Rect what) => _what = what;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        // Hang just off the corner, the way you look at something you don't trust.
        var spot = new Point(_what.X - 70, _what.Y + Math.Min(90, _what.Height / 2));
        var to = spot - pet.Pos;
        pet.PupilTarget = new Point(_what.X + _what.Width / 2, _what.Y + _what.Height / 2);

        if (!_arrived)
        {
            if (to.Length < 30 || _t > 10) { _arrived = true; _t = 0; pet.Anim.Play("idle"); }
            else
            {
                var desired = to / to.Length * Math.Min(170, to.Length * 2.4);
                pet.Vel += (desired - pet.Vel) * Math.Min(1, 3.2 * dt);
                pet.Pos += pet.Vel * dt;
                PhysicsEngine.ClampToTank(pet, c.World);
                if (Math.Abs(pet.Vel.X) > 12) pet.FacingRight = pet.Vel.X > 0;
            }
            return;
        }

        pet.Vel *= Math.Exp(-3 * dt);
        pet.Pos += pet.Vel * dt;
        pet.FacingRight = true;
        pet.VisualBob = Math.Sin(_t * 2.4) * 3;

        // Lean in for a closer look, then lose interest.
        if (_t > 1.2 && _t < 2.0) pet.Anim.Play("hunt");
        if (_t > 3.5 + c.Rng.NextDouble() * 2)
        {
            Next = new SwimFreeBehavior();
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}

/// <summary>
/// Blow a bubble and chase it up, over and over. Cuttlefish jet water about for no
/// obvious reason; this is the desktop version of that.
/// </summary>
public sealed class BubblePlayBehavior : BehaviorBase
{
    public override string Name => "play";
    public override bool OverridesPhysics => true;

    private Point _bubble;
    private double _t, _rounds;
    private bool _chasing;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("idle");
        c.Pet.Surface = null;
        Blow(c);
    }

    private void Blow(BehaviorContext c)
    {
        var pet = c.Pet;
        _bubble = pet.Pos + new Vector(pet.FacingRight ? 20 : -20, -48);
        c.Renderer.SpawnBubble(_bubble);
        c.Sound.Play("bubble", 0.22);
        _chasing = true;
        _t = 0;
        _rounds++;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.PupilTarget = _bubble;

        // The bubble drifts up and away while the pet noses after it.
        _bubble = new Point(_bubble.X + Math.Sin(_t * 2.2) * 30 * dt, _bubble.Y - 55 * dt);

        if (_chasing)
        {
            var to = _bubble - pet.Pos + new Vector(0, 40);
            var desired = to.Length < 1 ? default : to / to.Length * Math.Min(120, to.Length * 2.5);
            pet.Vel += (desired - pet.Vel) * Math.Min(1, 3 * dt);
            pet.Pos += pet.Vel * dt;
            PhysicsEngine.ClampToTank(pet, c.World);
            if (Math.Abs(pet.Vel.X) > 10) pet.FacingRight = pet.Vel.X > 0;
            pet.Anim.Play("swim");

            if (_t > 1.6)
            {
                _chasing = false;
                pet.Anim.Play("strike", restart: true);   // a nose-tap to pop it
            }
            return;
        }

        pet.Vel *= Math.Exp(-2.5 * dt);
        pet.Pos += pet.Vel * dt;
        if (pet.Anim.Finished)
        {
            if (_rounds >= 3 || pet.Pos.Y < c.World.VirtualScreen.Top + 160)
            {
                Next = new SwimFreeBehavior();
                Done = true;
            }
            else Blow(c);
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}

/// <summary>
/// Drift toward the colour of whoever you are swimming with. Cuttlefish take cues
/// from each other, and it makes a group look like a group.
/// </summary>
public static class ColourMimicry
{
    public static void Apply(Pet pet, WorldState world, Random rng, double dt)
    {
        if (pet.Vividness > 0.4) return;          // busy displaying, not copying
        if (rng.NextDouble() > dt * 0.12) return;

        foreach (var other in world.Pets)
        {
            if (ReferenceEquals(other, pet)) continue;
            if ((other.Pos - pet.Pos).Length > 260) continue;
            if (other.HomePalette == pet.HomePalette) return;
            pet.HomePalette = other.HomePalette;
            pet.SkinPattern = other.SkinPattern;
            return;
        }
    }
}
