using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Two cuttlefish with their tentacles in the same shrimp, hauling in opposite
/// directions until one gives up. The loser lets go; the winner gets a meal.
/// </summary>
public sealed class TugOfWarBehavior : BehaviorBase
{
    public override string Name => "tug";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    private readonly Treat _treat;
    private readonly int _side;
    private readonly bool _wins;
    private double _t;

    public TugOfWarBehavior(Treat treat, int side, bool wins)
    {
        _treat = treat;
        _side = side;
        _wins = wins;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("strike", restart: true);
        c.Pet.Surface = null;
        c.Pet.FacingRight = _side < 0;
        c.Pet.ShiftTo(Palettes.IndexOf("crimson"), 8);
        c.Sound.Play("blip", 0.25);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        if (_treat.Expired) { Next = new SwimFreeBehavior(); Done = true; return; }

        // Both hang on either side of the shrimp, jerking back and forth.
        var anchor = _treat.Pos + new Vector(_side * (46 + Math.Sin(_t * 7) * 12), -6);
        pet.Pos += (anchor - pet.Pos) * Math.Min(1, 9 * dt);
        pet.Vel = new Vector(0, 0);
        pet.Rotation = Math.Sin(_t * 7) * 9 * _side;
        pet.VisualBob = Math.Cos(_t * 9) * 2;

        if (_t < 3.2) return;

        pet.Rotation = 0;
        if (_wins)
        {
            Next = new EatTreatBehavior(_treat);
        }
        else
        {
            // Let go and tumble backwards.
            pet.Vel = new Vector(_side * 520, -140);
            Next = new DriftBehavior();
        }
        Done = true;
    }

    public override void Exit(BehaviorContext c) => c.Pet.Rotation = 0;
}

/// <summary>Squirt ink over another cuttlefish and make off — you are it.</summary>
public sealed class InkTagBehavior : BehaviorBase
{
    public override string Name => "tag";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    private readonly Pet _target;
    private double _t;
    private bool _fired;

    public InkTagBehavior(Pet target) => _target = target;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("hunt", restart: true);
        c.Pet.Surface = null;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.PupilTarget = _target.Pos;

        if (!_fired)
        {
            var to = _target.Pos - pet.Pos;
            if (to.Length > 90 && _t < 6)
            {
                var desired = to / to.Length * Math.Min(190, to.Length * 2.5);
                pet.Vel += (desired - pet.Vel) * Math.Min(1, 4 * dt);
                pet.Pos += pet.Vel * dt;
                PhysicsEngine.ClampToTank(pet, c.World);
                if (Math.Abs(pet.Vel.X) > 10) pet.FacingRight = pet.Vel.X > 0;
                return;
            }

            _fired = true;
            _t = 0;
            c.Renderer.SpawnInk(_target.Pos);
            c.Sound.Play("squirt", 0.3);
            pet.Anim.Play("jump", restart: true);
            var away = pet.Pos - _target.Pos;
            if (away.Length < 1) away = new Vector(-1, 0);
            away.Normalize();
            pet.Vel = away * 620;
            // Now they are it.
            if (_target.Machine.Current.Interruptible)
                _target.Machine.Force(new ChasePetBehavior(pet));
            return;
        }

        pet.Vel *= Math.Exp(-1.6 * dt);
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);
        if (_t > 1.2) { Next = new SwimFreeBehavior(); Done = true; }
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}

/// <summary>Chase down another cuttlefish — usually to tag them back.</summary>
public sealed class ChasePetBehavior : BehaviorBase
{
    public override string Name => "chasePet";
    public override bool OverridesPhysics => true;

    private readonly Pet _quarry;
    private double _t;

    public ChasePetBehavior(Pet quarry) => _quarry = quarry;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
        c.Pet.ShiftTo(Palettes.IndexOf("coral"), 10);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.PupilTarget = _quarry.Pos;

        var to = _quarry.Pos - pet.Pos;
        if (to.Length < 80)
        {
            Next = new InkTagBehavior(_quarry);   // tag them back
            Done = true;
            return;
        }
        if (_t > 12) { Next = new SwimFreeBehavior(); Done = true; return; }

        var desired = to / to.Length * Math.Min(215, to.Length * 2.6);
        pet.Vel += (desired - pet.Vel) * Math.Min(1, 4 * dt);
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);
        if (Math.Abs(pet.Vel.X) > 10) pet.FacingRight = pet.Vel.X > 0;
        pet.VisualBob = Math.Sin(_t * 7) * 3;
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}

/// <summary>
/// Face another cuttlefish and copy it move for move, like a reflection. Real ones
/// match each other's posture constantly; it is how they size each other up.
/// </summary>
public sealed class MirrorBehavior : BehaviorBase
{
    public override string Name => "mirror";
    public override bool OverridesPhysics => true;

    private readonly Pet _other;
    private readonly bool _leads;
    private Vector _offset;
    private double _t;

    public MirrorBehavior(Pet other, bool leads)
    {
        _other = other;
        _leads = leads;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
        _offset = c.Pet.Pos - _other.Pos;
        if (_offset.Length < 40) _offset = new Vector(90, 0);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.PupilTarget = _other.Pos;

        if (_leads)
        {
            // The leader just drifts about; the mirror does the interesting part.
            pet.Vel += (new Vector(Math.Sin(_t * 1.3) * 70, Math.Cos(_t * 1.7) * 50) - pet.Vel)
                       * Math.Min(1, 2 * dt);
            pet.Pos += pet.Vel * dt;
        }
        else
        {
            // Hold station opposite, flipped, so every move is answered.
            var spot = _other.Pos + new Vector(-_offset.X, _offset.Y);
            var to = spot - pet.Pos;
            var desired = to.Length < 1 ? default : to / to.Length * Math.Min(200, to.Length * 3);
            pet.Vel += (desired - pet.Vel) * Math.Min(1, 5 * dt);
            pet.Pos += pet.Vel * dt;
        }

        PhysicsEngine.ClampToTank(pet, c.World);
        pet.FacingRight = _other.Pos.X > pet.Pos.X;
        pet.VisualBob = Math.Sin(_t * 3) * 3;

        if (_t > 6 + c.Rng.NextDouble() * 4) { Next = new SwimFreeBehavior(); Done = true; }
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}

/// <summary>
/// Swim clean off one side of the screen and come back on the other, as if the tank
/// wrapped around. The only time a pet is allowed past the glass.
/// </summary>
public sealed class EdgeCrossBehavior : BehaviorBase
{
    public override string Name => "cross";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    private readonly int _dir;
    private bool _wrapped;
    private double _t;

    public EdgeCrossBehavior(int dir) => _dir = dir;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("jump", restart: true);
        c.Pet.Surface = null;
        c.Pet.FacingRight = _dir > 0;
        c.Pet.Vel = new Vector(_dir * 340, 0);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        var tank = c.World.VirtualScreen;
        _t += dt;

        pet.Pos += pet.Vel * dt;
        pet.VisualBob = Math.Sin(_t * 5) * 2;

        if (!_wrapped)
        {
            // Deliberately not clamped: this is the one move that leaves the tank.
            bool gone = _dir > 0 ? pet.Pos.X > tank.Right + 120 : pet.Pos.X < tank.Left - 120;
            if (gone)
            {
                _wrapped = true;
                pet.Pos = new Point(_dir > 0 ? tank.Left - 110 : tank.Right + 110,
                                    Math.Clamp(pet.Pos.Y + c.Rng.Next(-120, 121),
                                               tank.Top + 140, tank.Bottom - 140));
            }
            else if (_t > 8) { Next = new SwimFreeBehavior(); Done = true; }
            return;
        }

        bool backInside = _dir > 0 ? pet.Pos.X > tank.Left + 130 : pet.Pos.X < tank.Right - 130;
        if (backInside) { Next = new SwimFreeBehavior(); Done = true; }
    }
}

/// <summary>
/// Drift down the page alongside the text while you scroll, keeping pace as though
/// reading over your shoulder.
/// </summary>
public sealed class ReadAlongBehavior : BehaviorBase
{
    public override string Name => "read";
    public override bool OverridesPhysics => true;
    private double _t, _idle;

    public static bool Possible(BehaviorContext c) => Math.Abs(c.World.ScrollCurrent) > 0.8;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("idle");
        c.Pet.Surface = null;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        // Sit off to the side of the pointer, tracking the line being read.
        var spot = new Point(c.World.Cursor.X - 150, c.World.Cursor.Y);
        var to = spot - pet.Pos;
        var desired = to.Length < 1 ? default : to / to.Length * Math.Min(200, to.Length * 2);
        pet.Vel += (desired - pet.Vel) * Math.Min(1, 3 * dt);
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);
        pet.FacingRight = true;
        pet.PupilTarget = c.World.Cursor;
        pet.VisualBob = Math.Sin(_t * 2.2) * 2;

        // Give up once the scrolling stops for a while.
        _idle = Math.Abs(c.World.ScrollCurrent) > 0.3 ? 0 : _idle + dt;
        if (_idle > 2.5 || _t > 20) { Next = new SwimFreeBehavior(); Done = true; }
    }

    public override void Exit(BehaviorContext c) => c.Pet.PupilTarget = null;
}

/// <summary>
/// The window it was sitting on just got minimised. Ride it down to the taskbar,
/// shrinking away, then pop back out and carry on.
/// </summary>
public sealed class RideMinimiseBehavior : BehaviorBase
{
    public override string Name => "minimise";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    private readonly Point _to;
    private Point _from;
    private double _t, _startScale;

    public RideMinimiseBehavior(Point taskbarSpot) => _to = taskbarSpot;

    public override void Enter(BehaviorContext c)
    {
        _from = c.Pet.Pos;
        _startScale = c.Pet.Scale;
        c.Pet.Anim.Play("drag", restart: true);
        c.Pet.Surface = null;
        c.Pet.Vel = new Vector(0, 0);
        c.Sound.Play("blip", 0.2);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        double k = Math.Clamp(_t / 0.7, 0, 1);

        // Swept down with the window, shrinking as it goes.
        pet.Pos = new Point(_from.X + (_to.X - _from.X) * k, _from.Y + (_to.Y - _from.Y) * k);
        pet.Scale = _startScale * (1 - 0.75 * k);
        pet.Rotation = k * 40;

        if (_t > 1.1)
        {
            pet.Scale = _startScale;
            pet.Rotation = 0;
            pet.Vel = new Vector(c.Rng.Next(-160, 161), -260);
            c.Renderer.SpawnBubble(pet.Pos + new Vector(0, -30));
            Next = new DriftBehavior();
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c)
    {
        c.Pet.Rotation = 0;
        c.Pet.Scale = _startScale;
    }
}
