using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// The real thing: creep up on a live fish with the passing-cloud display running,
/// hold still when it looks your way, then fire the feeding tentacles. The fish bolts
/// if you get careless, so the hunt is genuinely won or lost.
/// </summary>
public sealed class StalkPreyBehavior : BehaviorBase
{
    public override string Name => "stalk";
    public override bool OverridesPhysics => true;

    private enum Phase { Creep, Strike, Feed }
    private Phase _phase = Phase.Creep;
    private readonly Prey _prey;
    private double _t, _phaseT;

    public StalkPreyBehavior(Prey prey) => _prey = prey;

    public override void Enter(BehaviorContext c)
    {
        _prey.StalkedBy = c.Pet;
        c.Pet.Anim.Play("hunt", restart: true);
        c.Pet.Surface = null;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        _phaseT += dt;
        pet.PupilTarget = _prey.Pos;

        if (_prey.Expired && _phase != Phase.Feed)
        {
            Next = new SwimFreeBehavior();
            Done = true;
            return;
        }

        var to = _prey.Pos - pet.Pos;

        switch (_phase)
        {
            case Phase.Creep:
                pet.FacingRight = to.X > 0;
                pet.VisualBob = Math.Sin(_t * 2.6) * 1.5;

                // Freeze when it has spooked: movement is what gives you away.
                double speed = _prey.Panicking ? 150 : 46;
                var creep = to.Length < 1 ? default : to / to.Length * speed;
                pet.Vel += (creep - pet.Vel) * Math.Min(1, 2.2 * dt);
                pet.Pos += pet.Vel * dt;
                PhysicsEngine.ClampToTank(pet, c.World);

                if (to.Length < 78)
                {
                    _phase = Phase.Strike;
                    _phaseT = 0;
                    pet.Vel = new Vector(0, 0);
                    pet.Anim.Play("strike", restart: true);
                    c.Sound.Play("blip", 0.3);
                }
                else if (_t > 20)
                {
                    Next = new SwimFreeBehavior();   // lost it
                    Done = true;
                }
                break;

            case Phase.Strike:
                // Lunge the last stretch; a hit is close enough at the end of the reach.
                pet.Pos += to * Math.Min(1, 7 * dt);
                if (pet.Anim.Finished)
                {
                    if ((_prey.Pos - pet.Pos).Length < 95)
                    {
                        _prey.Eaten = true;
                        pet.Feed(0.11);          // a whole fish is a proper meal
                        _phase = Phase.Feed;
                        _phaseT = 0;
                        pet.Anim.Play("eat", restart: true);
                        c.Sound.Play("blip", 0.35);
                    }
                    else
                    {
                        Next = new SwimFreeBehavior();   // missed, and it is long gone
                        Done = true;
                    }
                }
                break;

            case Phase.Feed:
                pet.Vel *= Math.Exp(-3 * dt);
                pet.Pos += pet.Vel * dt;
                pet.VisualBob = Math.Sin(_phaseT * 9) * 2;
                if (_phaseT > 1.6)
                {
                    Next = new HappyBehavior(1.8);
                    Done = true;
                }
                break;
        }
    }

    public override void Exit(BehaviorContext c)
    {
        c.Pet.PupilTarget = null;
        if (_prey.StalkedBy == c.Pet) _prey.StalkedBy = null;
    }
}

/// <summary>
/// Courtship: swim alongside another cuttlefish with the arms thrown up in a fan and
/// colour pulsing along the body. If it goes well the pair swim off together; if not,
/// the suitor slinks away pale.
/// </summary>
public sealed class CourtshipBehavior : BehaviorBase
{
    public override string Name => "court";
    public override bool OverridesPhysics => true;

    private readonly Pet _other;
    private readonly bool _suitor;
    private readonly bool _welcome;
    private double _t, _flash;

    public CourtshipBehavior(Pet other, bool suitor, bool welcome)
    {
        _other = other;
        _suitor = suitor;
        _welcome = welcome;
    }

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("court", restart: true);
        pet.Surface = null;
        pet.FacingRight = _other.Pos.X > pet.Pos.X;
        c.Sound.Play("bubble", 0.25);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        _flash -= dt;

        // Colour pulses along the body throughout the display.
        if (_flash <= 0)
        {
            _flash = 0.55;
            pet.ShiftTo(c.Rng.NextDouble() < 0.5 ? pet.HomePalette
                                                 : Palettes.IndexOf("magenta"), 3);
        }

        // Hold station just off the other's flank, mirroring it.
        var spot = _other.Pos + new Vector(_other.Pos.X > pet.Pos.X ? -95 : 95, -10);
        var to = spot - pet.Pos;
        var desired = to.Length < 1 ? default : to / to.Length * Math.Min(120, to.Length * 2);
        pet.Vel += (desired - pet.Vel) * Math.Min(1, 3 * dt);
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);
        pet.FacingRight = _other.Pos.X > pet.Pos.X;
        pet.VisualBob = Math.Sin(_t * 3.5) * 4;
        pet.PupilTarget = _other.Pos;

        if (_t < 5) return;

        if (_welcome)
        {
            // Accepted: swim off as a pair, and she will nest before long.
            if (!_suitor) c.Pet.WantsToNest = true;
            Next = new FollowBehavior(_other, new Vector(80, 26));
        }
        else if (_suitor)
        {
            pet.ShiftTo(Palettes.IndexOf("pearl"), 8);   // rebuffed, blanched
            Next = new SwimFreeBehavior();
        }
        else
        {
            Next = new DartBehavior();                   // not interested, off she goes
        }
        Done = true;
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}
