using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// The end of a short life. Colour drains out, the fins stop working, and the pet
/// sinks slowly out of the tank. Cuttlefish live about a year and die soon after
/// breeding, so a crowded screen thins itself out without anyone intervening.
/// </summary>
/// <summary>
/// Swallowed whole.
///
/// The hunter used to kill at arm's length: the pet simply started dying where
/// it floated while the hunter carried on past, which reads as being struck by
/// lightning rather than eaten. Here it is dragged to the back of the jaw,
/// shrinking and turning as it goes, and gone in under half a second. The
/// mouth is re-read every tick, so if the hunter is already turning away the
/// pet is carried off with it.
///
/// The cuttlebone is spat back out. That is not a flourish: it is why
/// cuttlebones wash up on beaches at all -- the shell is no use to a predator,
/// and dolphins in particular are known to work the mantle off and leave it.
/// </summary>
public sealed class EatenBehavior : BehaviorBase
{
    public override string Name => "eaten";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    /// <summary>
    /// Long enough to follow. At four tenths it was over between two frames of
    /// a screen recording -- you saw a cuttlefish, then you saw a cuttlebone.
    /// </summary>
    private const double Duration = 0.55;
    private double _t;
    private Point _from;
    private Point _mouth;
    private double _scale0;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Dying = true;
        pet.Surface = null;
        pet.Anim.Play("startle", restart: true);
        pet.ShiftTo(Palettes.IndexOf("pearl"), 0.3);
        _from = pet.Pos;
        _mouth = c.World.Hunter?.Mouth ?? pet.Pos;
        _scale0 = pet.Scale;
        c.Renderer.SpawnInk(pet.Pos);
        c.Sound.Play("squirt", 0.5);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        if (c.World.Hunter is { } h) _mouth = h.Mouth;
        _t += dt;

        double k = Math.Min(1, _t / Duration);
        double e = k * k * (3 - 2 * k);          // snatched, not slid
        pet.Pos = new Point(_from.X + (_mouth.X - _from.X) * e,
                            _from.Y + (_mouth.Y - _from.Y) * e);
        pet.Vel = new Vector(0, 0);
        pet.Scale = _scale0 * (1 - 0.94 * e);
        pet.Rotation = e * 55;
        // Stays solid nearly all the way in: fading early would read as
        // dissolving in mid-water rather than going down a throat.
        pet.Fade = Math.Max(0, Math.Min(1, (1 - e) * 4));

        if (k >= 1)
        {
            // Out past the lip and clear of the flank, or the shell appears to
            // surface from somewhere inside the animal that just swallowed it.
            var drift = c.World.Hunter is { } hh && hh.Vel.Length > 1
                ? hh.Vel / hh.Vel.Length
                : new Vector(0, 0);
            c.AddBone(_mouth + drift * 46 + new Vector(0, -34),
                      Math.Clamp(pet.GrownScale, 0.4, 1.3));
            c.RemovePet(pet);
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c)
    {
        c.Pet.Rotation = 0;
        c.Pet.Fade = 1;
    }
}

public sealed class DyingBehavior : BehaviorBase
{
    public override string Name => "dying";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    private const double Duration = 7.0;
    private double _t;
    private bool _boneLeft;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Dying = true;
        pet.Surface = null;
        pet.Anim.Play("ghost", restart: true);
        pet.ShiftTo(Palettes.IndexOf("pearl"), Duration + 2);
        c.Sound.Play("bubble", 0.2);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        double k = _t / Duration;

        // Sinking, tumbling gently, going transparent.
        pet.Vel = new Vector(pet.Vel.X * Math.Exp(-1.2 * dt), 24 + k * 30);
        pet.Pos += pet.Vel * dt;
        pet.Rotation = Math.Sin(_t * 0.9) * 16 * k;
        pet.VisualBob = Math.Sin(_t * 1.4) * 3 * (1 - k);
        pet.Fade = Math.Max(0, 1 - k);
        PhysicsEngine.ClampToTank(pet, c.World);

        // A last few bubbles on the way out.
        if (_t < Duration * 0.6 && c.Rng.NextDouble() < dt * 1.4)
            c.Renderer.SpawnBubble(pet.Pos + new Vector(c.Rng.Next(-10, 11), -40));

        // Two thirds of the way down, the shell works its way loose and starts to
        // rise while what is left of the body keeps sinking.
        if (!_boneLeft && k > 0.62)
        {
            _boneLeft = true;
            c.AddBone(pet.Pos + new Vector(0, -12), Math.Clamp(pet.GrownScale, 0.4, 1.3));
            c.Sound.Play("bubble", 0.18);
        }

        if (_t >= Duration)
        {
            pet.Fade = 1;
            c.RemovePet(pet);
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c)
    {
        c.Pet.Rotation = 0;
        c.Pet.Fade = 1;
    }
}

/// <summary>
/// Leave a copy of yourself behind and go.
///
/// Cephalopod ink is bound with mucus, and a frightened cuttlefish can put out a
/// blob that holds roughly its own size and shape for a second or two. It blanches
/// white in the same instant -- so the two of them stop looking alike -- and jets
/// off at an angle. Whatever is chasing has to pick one, and often picks wrong.
///
/// Nothing in the drives can judge this: getting away settles no hunger and eases
/// no fatigue. So it is scored by hand, and since what an animal has worked out is
/// halved and handed to its brood, a tank left running for days gets measurably
/// harder to hunt.
/// </summary>
public sealed class PseudomorphBehavior : BehaviorBase
{
    public override string Name => "decoy";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    private readonly Vector _away;
    private double _t;

    public PseudomorphBehavior(Vector away) => _away = away;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Surface = null;
        pet.Anim.Play("jump", restart: true);   // the jet, same as a dart
        // Blanching is half the trick: the blot stays dark and the animal does not.
        pet.ShiftTo(Palettes.IndexOf("pearl"), 7);
        c.AddProp(new Prop
        {
            Anim = "decoy",
            Pos = pet.Pos,
            Life = 1.9,
            Scale = pet.Scale,
        });
        pet.Vel = _away * 430;
        c.Sound.Play("squirt", 0.45);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.Vel *= Math.Exp(-1.4 * dt);
        pet.Pos += pet.Vel * dt;
        if (Math.Abs(pet.Vel.X) > 10) pet.FacingRight = pet.Vel.X > 0;
        PhysicsEngine.ClampToTank(pet, c.World);
        if (_t > 1.3)
        {
            Next = new FleeBehavior();
            Done = true;
        }
    }
}
