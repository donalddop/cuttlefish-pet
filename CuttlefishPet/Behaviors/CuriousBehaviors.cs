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
        _bubble = pet.Pos + new Vector(pet.FacingRight ? 26 : -26, -52);
        // A little cluster reads far better than one lone bubble.
        c.Renderer.SpawnBubble(_bubble);
        c.Renderer.SpawnBubble(_bubble + new Vector(c.Rng.Next(-14, 15), 16));
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
/// Swim up to a drifting cuttlebone, hang there looking at it, then push it on its
/// way. Cuttlefish do investigate the remains of their own kind; going pale over it
/// is the sort of thing that makes a tank feel inhabited.
/// </summary>
public sealed class InvestigateBoneBehavior : BehaviorBase
{
    public override string Name => "bone";
    public override bool OverridesPhysics => true;

    private readonly Bone _bone;
    private double _t;
    private bool _arrived;

    public InvestigateBoneBehavior(Bone bone) => _bone = bone;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.PupilTarget = _bone.Pos;

        if (_bone.Expired) { Next = new SwimFreeBehavior(); Done = true; return; }

        var to = _bone.Pos - pet.Pos;

        if (!_arrived)
        {
            if (to.Length < 62 || _t > 11)
            {
                _arrived = true;
                _t = 0;
                pet.Anim.Play("idle");
            }
            else
            {
                var desired = to / to.Length * Math.Min(150, to.Length * 2.2);
                pet.Vel += (desired - pet.Vel) * Math.Min(1, 3 * dt);
                pet.Pos += pet.Vel * dt;
                PhysicsEngine.ClampToTank(pet, c.World);
                if (Math.Abs(pet.Vel.X) > 10) pet.FacingRight = pet.Vel.X > 0;
            }
            return;
        }

        pet.Vel *= Math.Exp(-3 * dt);
        pet.Pos += pet.Vel * dt;
        pet.FacingRight = to.X > 0;
        pet.VisualBob = Math.Sin(_t * 1.8) * 3;

        // A parting shove — or, if there is a decent stretch of water overhead, he
        // decides the thing is worth striking at, which goes about as well as you
        // would expect from an animal grabbing hold of its own buoyancy aid.
        if (_t > 3.5)
        {
            if (_bone.Pos.Y > c.World.VirtualScreen.Top + 320 && c.Rng.NextDouble() < 0.72)
            {
                Next = new BoneRideBehavior(_bone);
                Done = true;
                return;
            }

            pet.Anim.Play("strike", restart: true);
            var push = to.Length < 1 ? new Vector(0, -40) : to / to.Length * 130;
            _bone.Nudge(push);
            c.Sound.Play("blip", 0.18);
            Next = new SwimFreeBehavior();
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}

/// <summary>
/// Work up one enormous bubble and let it burst. Everyone else in earshot jumps out
/// of their skin; the one that blew it looks rather pleased.
/// </summary>
public sealed class BigBubbleBehavior : BehaviorBase
{
    public override string Name => "bigBubble";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    private const double PopAt = 1.7;      // matches the burst frames of the prop
    private double _t;
    private bool _popped;
    private Point _where;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("hunt", restart: true);
        pet.Surface = null;
        _where = pet.Pos + new Vector(pet.FacingRight ? 74 : -74, -18);
        c.AddProp(new Prop { Anim = "bigbubble", Pos = _where, Life = 2.7 });
        c.Sound.Play("bubble", 0.3);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.Vel *= Math.Exp(-3 * dt);
        pet.Pos += pet.Vel * dt;
        pet.PupilTarget = _where;

        if (!_popped)
        {
            // Straining harder the bigger it gets.
            pet.VisualBob = Math.Sin(_t * 11) * (1 + _t * 1.6);
            if (_t < PopAt) return;

            _popped = true;
            c.Sound.Play("squirt", 0.4);
            for (int i = 0; i < 5; i++)
                c.Renderer.SpawnBubble(_where + new Vector(c.Rng.Next(-40, 41), c.Rng.Next(-30, 31)));

            // Everyone close enough to hear it bolts.
            foreach (var other in c.World.Pets)
            {
                if (ReferenceEquals(other, pet)) continue;
                if (!other.Machine.Current.Interruptible) continue;
                if ((other.Pos - _where).Length > 520) continue;
                other.Machine.Force(new StartleBehavior());
            }
            pet.Anim.Play("happy", restart: true);
            return;
        }

        pet.VisualBob = Math.Sin(_t * 8) * 4;
        if (_t > PopAt + 1.6)
        {
            Next = new SwimFreeBehavior();
            Done = true;
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

/// <summary>
/// He fires his feeding tentacles at a drifting cuttlebone — and a cuttlebone is
/// mostly gas. Nothing comes back down the tentacles; instead he goes up. Too
/// stubborn to let go, he gets towed to the top of the tank before working out that
/// this was not food, lets go in a fluster and sinks back down.
/// </summary>
public sealed class BoneRideBehavior : BehaviorBase
{
    public override string Name => "boneRide";
    public override bool OverridesPhysics => true;
    public override bool NeedsPerch => false;

    private readonly Bone _bone;
    private readonly TentacleStrike _strike = new();
    private Vector _grab;
    private bool _hooked, _released;
    private double _t, _rideT;

    public BoneRideBehavior(Bone bone) => _bone = bone;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("hunt", restart: true);
        c.Pet.Surface = null;
        c.Pet.Vel = new Vector(0, 0);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.PupilTarget = _bone.Pos;

        if (_bone.Expired && !_hooked) { Next = new SwimFreeBehavior(); Done = true; return; }

        var mouth = TentacleStrike.Mouth(pet);
        if (!_released)
        {
            _strike.Tick(pet, _hooked ? mouth + _grab : _bone.Pos, dt, holdOn: _hooked);
            if (_strike.JustLanded)
            {
                _grab = _bone.Pos - mouth;
                _hooked = _grab.Length < 150;
                if (_hooked)
                {
                    c.Sound.Play("blip", 0.3);
                    pet.Anim.Play("drag", restart: true);   // hanging off it
                }
            }
        }

        if (_hooked && !_released)
        {
            _rideT += dt;

            // Hauled along under a rising cuttlebone. He weighs it down, so the pair
            // climbs slower than the bone did alone — and wanders, because neither of
            // them is steering.
            var lift = new Vector(Math.Sin(_rideT * 1.6) * 26, -46);
            pet.Vel += (lift - pet.Vel) * Math.Min(1, 2.4 * dt);
            pet.Pos += pet.Vel * dt;
            _bone.Pos = pet.StrikeTip;
            _bone.Vel = pet.Vel;
            pet.Rotation = Math.Sin(_rideT * 3.1) * 9;
            pet.VisualBob = Math.Sin(_rideT * 6) * 2;
            pet.FacingRight = _grab.X > 0;

            // Fussing: a bubble now and then as it dawns on him what he is holding.
            if (_rideT > 2.2 && c.Rng.NextDouble() < dt * 1.2)
                c.Renderer.SpawnBubble(pet.Pos + new Vector(0, -34));

            // Lets go near the surface, or once he has had enough of being luggage.
            if (pet.Pos.Y < c.World.VirtualScreen.Top + 190 || _rideT > 5.5)
            {
                _released = true;
                pet.Striking = false;
                pet.Rotation = 0;
                _bone.Nudge(new Vector(c.Rng.Next(-80, 81), -140));
                c.Sound.Play("squirt", 0.35);
                Next = new StartleBehavior();
                Done = true;
                return;
            }
        }
        else if (!_hooked && _strike.Finished)
        {
            // Missed it entirely, which is its own small comedy.
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
