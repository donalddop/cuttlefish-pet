using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// The end of a short life. Colour drains out, the fins stop working, and the pet
/// sinks slowly out of the tank. Cuttlefish live about a year and die soon after
/// breeding, so a crowded screen thins itself out without anyone intervening.
/// </summary>
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
            c.AddBone(pet.Pos + new Vector(0, -12));
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
