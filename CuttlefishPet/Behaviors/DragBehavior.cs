using System.Windows;
using CuttlefishPet.Interop;

namespace CuttlefishPet.Behaviors;

/// <summary>Pet hangs from the cursor; on release it inherits the throw velocity.</summary>
public sealed class DragBehavior : BehaviorBase
{
    public override string Name => "drag";
    public override bool OverridesPhysics => true;
    public override bool Interruptible => false;

    private readonly Queue<(Point pos, double t)> _samples = new();
    private double _time;
    private Vector _grabOffset;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("drag");
        c.Pet.Surface = null;
        c.Pet.Vel = new Vector(0, 0);
        _grabOffset = c.Pet.Pos - c.World.Cursor;
        // Keep the grip near the mantle regardless of where the click landed.
        if (_grabOffset.Length > 40) _grabOffset = new Vector(0, 20);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _time += dt;
        var cursor = c.World.Cursor;
        c.Pet.Pos = cursor + _grabOffset;
        c.Pet.FacingRight = c.Input.CursorVelocity.X >= 0;

        _samples.Enqueue((c.Pet.Pos, _time));
        while (_samples.Count > 0 && _time - _samples.Peek().t > 0.12)
            _samples.Dequeue();
    }

    /// <summary>Called by the machine when the global mouse-up arrives.</summary>
    public void Release(BehaviorContext c)
    {
        var pet = c.Pet;
        if (_samples.Count >= 2)
        {
            var (oldPos, oldT) = _samples.Peek();
            double span = Math.Max(_time - oldT, 0.02);
            pet.Vel = (pet.Pos - oldPos) / span;
            // Cap so a wild fling doesn't teleport it across monitors.
            if (pet.Vel.Length > 2200) pet.Vel = pet.Vel * (2200 / pet.Vel.Length);
        }

        if (pet.Vel.Length > 900)
        {
            c.Renderer.SpawnInk(pet.Pos + new Vector(pet.FacingRight ? -30 : 30, 0));
            c.Sound.Play("squirt", 0.4);
        }
        Next = new DriftBehavior();
        Done = true;
    }
}
