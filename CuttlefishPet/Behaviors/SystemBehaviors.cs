using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Interop;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Brace against the side of a window and actually shove it across the desktop —
/// the pet moves a real window, a few pixels at a time.
/// </summary>
public sealed class PushWindowBehavior : BehaviorBase
{
    public override string Name => "push";
    public override bool OverridesPhysics => true;

    private readonly Surface _edge;
    private readonly int _dir;
    private double _t, _pushed, _budget;

    private PushWindowBehavior(Surface edge, int dir)
    {
        _edge = edge;
        _dir = dir;
    }

    /// <summary>A movable window whose side the pet can get behind.</summary>
    public static PushWindowBehavior? Find(BehaviorContext c)
    {
        foreach (var s in c.World.Vertical())
        {
            if (s.Hwnd == IntPtr.Zero) continue;                 // screen edges don't move
            if (s.Kind is not (SurfaceKind.WindowLeft or SurfaceKind.WindowRight)) continue;
            var contact = new Point(s.X1 + s.ClingOffset, Math.Clamp(c.Pet.Pos.Y, s.Y + 40, s.Y2 - 40));
            if ((contact - c.Pet.Pos).Length > 460) continue;
            // Push away from the side the pet is standing on.
            return new PushWindowBehavior(s, s.Kind == SurfaceKind.WindowLeft ? 1 : -1);
        }
        return null;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("strike", restart: true);
        c.Pet.Surface = null;
        _budget = 60 + c.Rng.NextDouble() * 140;   // how far it bothers to shove
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        var edge = c.World.Find(_edge);
        if (edge == null || _t > 9 || _pushed >= _budget)
        {
            Next = new SwimFreeBehavior();
            Done = true;
            return;
        }

        var contact = new Point(edge.X1 + edge.ClingOffset,
                                Math.Clamp(pet.Pos.Y, edge.Y + 50, edge.Y2 - 50));
        var to = contact - pet.Pos;
        pet.FacingRight = _dir > 0;

        if (to.Length > 14)
        {
            // Swim into position against the window's flank.
            pet.Anim.Play("swim");
            var desired = to / to.Length * Math.Min(160, to.Length * 3);
            pet.Vel += (desired - pet.Vel) * Math.Min(1, 4 * dt);
            pet.Pos += pet.Vel * dt;
            return;
        }

        pet.Anim.Play("strike");
        pet.Vel = new Vector(0, 0);
        pet.Pos = contact;
        pet.VisualBob = Math.Sin(_t * 14) * 2;      // straining

        double step = 70 * dt;
        if (SystemProbes.NudgeWindow(edge.Hwnd, (int)Math.Round(_dir * step)))
            _pushed += step;
        else
            _pushed = _budget;                       // window won't budge; give up
    }
}

/// <summary>Sit by a window's close button and pretend to press it. Never does.</summary>
public sealed class TeaseCloseBehavior : BehaviorBase
{
    public override string Name => "tease";
    public override bool OverridesPhysics => true;

    private readonly Surface _top;
    private double _t;

    private TeaseCloseBehavior(Surface top) => _top = top;

    public static TeaseCloseBehavior? Find(BehaviorContext c)
    {
        foreach (var s in c.World.Horizontal())
        {
            if (s.Kind != SurfaceKind.WindowTop) continue;
            var button = new Point(s.X2 - 22, s.Y + 16);
            if ((button - c.Pet.Pos).Length < 520) return new TeaseCloseBehavior(s);
        }
        return null;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
        c.Pet.ShiftTo(Rendering.Palettes.IndexOf("coral"), 12);   // up to no good
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        var top = c.World.Find(_top);
        if (top == null || _t > 10) { Next = new SwimFreeBehavior(); Done = true; return; }

        var button = new Point(top.X2 - 26, top.Y + 4);
        var to = button - pet.Pos;
        pet.FacingRight = to.X > 0;

        if (to.Length > 16)
        {
            var desired = to / to.Length * Math.Min(170, to.Length * 3);
            pet.Vel += (desired - pet.Vel) * Math.Min(1, 4 * dt);
            pet.Pos += pet.Vel * dt;
            return;
        }

        // Hovering over the button, jabbing at it without ever clicking.
        pet.Vel = new Vector(0, 0);
        pet.Anim.Play("strike");
        pet.Pos = new Point(button.X - 26, button.Y);
        pet.VisualBob = Math.Sin(_t * 7) * 5;
        if (_t > 4.5) { Next = new SwimFreeBehavior(); Done = true; }
    }
}

/// <summary>Swim up to the taskbar clock, peer at it, and yawn.</summary>
public sealed class CheckClockBehavior : BehaviorBase
{
    public override string Name => "clock";
    public override bool OverridesPhysics => true;

    private Point _clock;
    private double _t;
    private bool _arrived;

    public static CheckClockBehavior? Find(BehaviorContext c)
    {
        var clock = SystemProbes.Clock();
        return clock == null ? null : new CheckClockBehavior { _clock = clock.Value };
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
        var target = new Point(_clock.X - 60, _clock.Y - 70);
        var to = target - pet.Pos;

        if (!_arrived)
        {
            if (to.Length < 20 || _t > 11) { _arrived = true; _t = 0; pet.Anim.Play("idle"); }
            else
            {
                var desired = to / to.Length * Math.Min(190, to.Length * 2.5);
                pet.Vel += (desired - pet.Vel) * Math.Min(1, 3.5 * dt);
                pet.Pos += pet.Vel * dt;
                pet.FacingRight = pet.Vel.X > 0;
            }
            return;
        }

        pet.Vel *= Math.Exp(-3 * dt);
        pet.Pos += pet.Vel * dt;
        pet.FacingRight = true;
        pet.VisualBob = Math.Sin(_t * 2) * 3;
        pet.PupilTarget = _clock;                       // eyes on the time
        if (_t > 2.2 && _t < 3.4) pet.Anim.Play("stretch", restart: _t < 2.25);  // yawn
        if (_t > 4) { Next = new SwimFreeBehavior(); Done = true; }
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}

/// <summary>
/// Follow the blinking text caret around while the user types — the pet reads over
/// your shoulder.
/// </summary>
public sealed class CaretChaseBehavior : BehaviorBase
{
    public override string Name => "caret";
    public override bool OverridesPhysics => true;
    private double _t, _lost;

    public static bool Possible(BehaviorContext c)
    {
        var caret = SystemProbes.Caret();
        return caret != null && (caret.Value - c.Pet.Pos).Length < 900;
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

        var caret = SystemProbes.Caret();
        if (caret == null)
        {
            _lost += dt;
            if (_lost > 1.5) { Next = new SwimFreeBehavior(); Done = true; }
            return;
        }
        _lost = 0;
        if (_t > 16) { Next = new SwimFreeBehavior(); Done = true; return; }

        // Hover just behind and above the caret, watching the words appear.
        var spot = caret.Value + new Vector(-70, -60);
        var to = spot - pet.Pos;
        if (to.Length > 25)
        {
            var desired = to / to.Length * Math.Min(175, to.Length * 2.4);
            pet.Vel += (desired - pet.Vel) * Math.Min(1, 5 * dt);
            pet.Pos += pet.Vel * dt;
            pet.FacingRight = pet.Vel.X > 0;
            pet.Anim.Play("swim");
        }
        else
        {
            pet.Vel *= Math.Exp(-4 * dt);
            pet.Pos += pet.Vel * dt;
            pet.FacingRight = true;
            pet.Anim.Play(c.World.TypingRate > 2 ? "wiggle" : "idle");
            pet.VisualBob = Math.Sin(_t * 4) * 2;
        }
        pet.PupilTarget = caret;
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}

/// <summary>Grab hold of the mouse pointer and go wherever it goes.</summary>
public sealed class RideCursorBehavior : BehaviorBase
{
    public override string Name => "ride";
    public override bool OverridesPhysics => true;
    private double _t;

    public static bool Possible(BehaviorContext c) =>
        (c.World.Cursor - c.Pet.Pos).Length < 260;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("drag", restart: true);
        c.Pet.Surface = null;
        c.Sound.Play("blip", 0.2);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        // Hang off the pointer with a bit of lag so it swings when you move fast.
        var grip = c.World.Cursor + new Vector(0, 34);
        var to = grip - pet.Pos;
        pet.Pos += to * Math.Min(1, 9 * dt);
        pet.Rotation = Math.Clamp(-c.World.CursorVelocity.X * 0.02, -26, 26);
        pet.FacingRight = c.World.CursorVelocity.X >= 0;

        if (_t > 5 + c.Rng.NextDouble() * 5)
        {
            pet.Rotation = 0;
            pet.Vel = c.World.CursorVelocity * 0.35;
            Next = new DriftBehavior();
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.Rotation = 0;
}

/// <summary>Squirt a jet of water at the cursor.</summary>
public sealed class WaterJetBehavior : BehaviorBase
{
    public override string Name => "jet";
    public override bool OverridesPhysics => true;
    private double _t;
    private int _shots;

    public static bool Possible(BehaviorContext c)
    {
        double d = (c.World.Cursor - c.Pet.Pos).Length;
        return d is > 90 and < 420;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("strike", restart: true);
        c.Pet.Surface = null;
        c.Pet.FacingRight = c.World.Cursor.X > c.Pet.Pos.X;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.Vel *= Math.Exp(-3 * dt);
        pet.Pos += pet.Vel * dt;

        // Bubbles travelling from the funnel toward the pointer.
        if (_shots < 6 && _t > _shots * 0.12)
        {
            var to = c.World.Cursor - pet.Pos;
            if (to.Length > 1)
            {
                var step = to / to.Length * (26 + _shots * 22);
                c.Renderer.SpawnBubble(pet.Pos + step + new Vector(0, -18));
            }
            _shots++;
            if (_shots == 1) c.Sound.Play("squirt", 0.25);
        }

        if (_t > 1.4) { Next = new SwimFreeBehavior(); Done = true; }
    }
}
