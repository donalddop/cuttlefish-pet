using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// True cuttlefish camouflage: capture the screen behind the pet, wear it as skin
/// (alpha-masked by the flatten/mimic frames), shimmer subtly, pop back on approach.
/// </summary>
public sealed class CamouflageBehavior : BehaviorBase
{
    public override string Name => "camouflage";
    public override bool Interruptible => false; // handles its own reveal

    private enum Phase { Morphing, Holding, Revealing }
    private Phase _phase = Phase.Morphing;
    private double _t, _holdRemaining, _recaptureIn;
    private bool _captureBusy;
    private string _maskAnim = "flatten";

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.FacingRight = true; // captured skin must not be mirrored
        _maskAnim = pet.Surface?.Kind == SurfaceKind.TaskbarTop && c.Rng.NextDouble() < 0.4
            ? "mimic_icon"   // hide among the taskbar icons
            : "flatten";     // press flat against whatever is behind
        pet.Anim.Play(_maskAnim, restart: true);
        _holdRemaining = 10 + c.Rng.NextDouble() * 20;
        StartCapture(c);
    }

    private async void StartCapture(BehaviorContext c)
    {
        if (_captureBusy) return;
        _captureBusy = true;
        var src = await ScreenSampler.CaptureBehindAsync(c.Pet.Visual.Root, c.Pet.Bounds);
        if (src != null) c.Pet.CamoSource = src;
        _captureBusy = false;
        _recaptureIn = 2.5;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        switch (_phase)
        {
            case Phase.Morphing:
                if (pet.CamoSource != null)
                    pet.CamoOpacity = Math.Min(1, pet.CamoOpacity + dt / 1.2);
                if (pet.CamoOpacity >= 1) _phase = Phase.Holding;
                break;

            case Phase.Holding:
                _holdRemaining -= dt;
                _recaptureIn -= dt;
                pet.CamoRipple = Math.Sin(_t * Math.PI) * 1.5; // living-skin shimmer
                if (_recaptureIn <= 0 && !_captureBusy) StartCapture(c);

                bool cursorClose = (c.World.Cursor - pet.Pos).Length < 100;
                if (cursorClose || _holdRemaining <= 0)
                {
                    _phase = Phase.Revealing;
                    _t = 0;
                    pet.Anim.Play("startle", restart: true);
                    c.Sound.Play("bubble", 0.35);
                }
                break;

            case Phase.Revealing:
                pet.CamoOpacity = Math.Max(0, pet.CamoOpacity - dt / 0.35);
                pet.CamoRipple = 0;
                if (pet.CamoOpacity <= 0 && _t > 0.6)
                {
                    pet.CamoSource = null;
                    Done = true;
                }
                break;
        }
    }

    public override void Exit(BehaviorContext c)
    {
        // Grabbed mid-camo etc: never leave the skin on.
        c.Pet.CamoOpacity = 0;
        c.Pet.CamoRipple = 0;
        c.Pet.CamoSource = null;
    }
}
