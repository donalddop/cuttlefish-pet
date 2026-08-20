using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>Tuck in beside a sleeping neighbour instead of dozing off alone.</summary>
public sealed class SleepPileBehavior : BehaviorBase
{
    public override string Name => "pile";
    public override bool OverridesPhysics => true;

    private readonly Pet _neighbour;
    private Point _spot;
    private double _t;
    private bool _settled;

    private SleepPileBehavior(Pet neighbour) => _neighbour = neighbour;

    public static SleepPileBehavior? Find(BehaviorContext c)
    {
        foreach (var other in c.World.Pets)
        {
            if (ReferenceEquals(other, c.Pet)) continue;
            if (other.Surface is not { IsLandable: true }) continue;
            if (other.Machine.Current.Name is not ("sit" or "idle" or "pile" or "camouflage")) continue;
            if ((other.Pos - c.Pet.Pos).Length > 700) continue;
            return new SleepPileBehavior(other);
        }
        return null;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
        double side = _neighbour.Pos.X > c.Pet.Pos.X ? -1 : 1;
        _spot = new Point(_neighbour.Pos.X + side * 42, _neighbour.Pos.Y);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        if (_settled)
        {
            pet.VisualBob = Math.Sin(_t * 1.4) * 2;
            if (_t > 22 || _neighbour.Surface == null) Done = true;
            return;
        }

        var surface = _neighbour.Surface;
        if (surface == null || _t > 12) { Next = new SwimFreeBehavior(); Done = true; return; }
        _spot = new Point(_spot.X, surface.Y);

        var to = _spot - pet.Pos;
        if (to.Length < 14)
        {
            pet.Pos = _spot;
            pet.Vel = new Vector(0, 0);
            pet.Surface = surface;
            pet.Anim.Play("sit");
            pet.FacingRight = _neighbour.Pos.X > pet.Pos.X;
            _settled = true;
            _t = 0;
            return;
        }
        var desired = to / to.Length * Math.Min(150, to.Length * 2.5);
        pet.Vel += (desired - pet.Vel) * Math.Min(1, 3.5 * dt);
        pet.Pos += pet.Vel * dt;
        if (Math.Abs(pet.Vel.X) > 10) pet.FacingRight = pet.Vel.X > 0;
    }
}

/// <summary>Fall in beside another cuttlefish and swim as a pair.</summary>
public sealed class FollowBehavior : BehaviorBase
{
    public override string Name => "school";
    public override bool OverridesPhysics => true;

    private readonly Pet _leader;
    private readonly Vector _offset;
    private double _t;

    public FollowBehavior(Pet leader, Vector offset)
    {
        _leader = leader;
        _offset = offset;
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
        if (_t > 12 || _leader.Surface != null) { Next = new SwimFreeBehavior(); Done = true; return; }

        var spot = _leader.Pos + new Vector(_leader.FacingRight ? -_offset.X : _offset.X, _offset.Y);
        var to = spot - pet.Pos;
        var desired = to.Length < 1 ? _leader.Vel : to / to.Length * Math.Min(230, to.Length * 3);
        pet.Vel += (desired - pet.Vel) * Math.Min(1, 4 * dt);
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);
        pet.FacingRight = _leader.FacingRight;
        pet.VisualBob = Math.Sin(_t * 6) * 2.5;
    }
}

/// <summary>A flat-out sprint across the tank against another cuttlefish.</summary>
public sealed class RaceBehavior : BehaviorBase
{
    public override string Name => "race";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    private readonly double _finishX;
    private readonly int _dir;
    private readonly double _lane;
    private double _t;

    public RaceBehavior(double finishX, int dir, double lane)
    {
        _finishX = finishX;
        _dir = dir;
        _lane = lane;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("jump", restart: true);
        c.Pet.Surface = null;
        c.Pet.FacingRight = _dir > 0;
        c.Sound.Play("blip", 0.25);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        // Slight speed jitter so the race is not a dead heat every time.
        double speed = 330 + Math.Sin(_t * 3 + _lane) * 50;
        pet.Vel = new Vector(_dir * speed, (_lane - pet.Pos.Y) * 1.6);
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);
        pet.VisualBob = Math.Sin(_t * 12) * 3;

        bool finished = _dir > 0 ? pet.Pos.X >= _finishX : pet.Pos.X <= _finishX;
        if (finished || _t > 8)
        {
            Next = new HappyBehavior(1.4);   // everyone celebrates; nobody is counting
            Done = true;
        }
    }
}

/// <summary>Pure show: run the whole chromatophore range in a few seconds.</summary>
public sealed class ColourShowBehavior : BehaviorBase
{
    public override string Name => "colourShow";
    public override bool OverridesPhysics => true;
    private double _t, _nextShift;
    private int _shown;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("idle");
        c.Pet.Vel *= 0.3;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        _nextShift -= dt;
        pet.Vel *= Math.Exp(-2 * dt);
        pet.Pos += pet.Vel * dt;
        pet.VisualBob = Math.Sin(_t * 3) * 3;

        if (_nextShift <= 0 && _shown < 6)
        {
            _shown++;
            _nextShift = 0.75;
            pet.SkinPattern = c.Rng.Next(5);
            pet.ShiftTo(Palettes.PickRandom(c.Rng), 0.7);
        }
        if (_shown >= 6 && _nextShift <= 0) Done = true;
    }
}

/// <summary>Reeling after a hard throw: wobbling, unable to swim straight.</summary>
public sealed class DizzyBehavior : BehaviorBase
{
    public override string Name => "dizzy";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;
    private double _t;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("idle");
        c.Pet.ShiftTo(Palettes.IndexOf("pearl"), 6);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.Vel *= Math.Exp(-1.5 * dt);
        pet.Pos += pet.Vel * dt;
        pet.Rotation = Math.Sin(_t * 7) * 22 * Math.Max(0, 1 - _t / 2.6);
        pet.VisualBob = Math.Cos(_t * 5) * 4;
        PhysicsEngine.ClampToTank(pet, c.World);
        if (_t > 2.6)
        {
            pet.Rotation = 0;
            Next = new SwimFreeBehavior();
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.Rotation = 0;
}

/// <summary>
/// Patience gone after too much handling: goes dark, flares the zebra display and
/// stamps, then storms off.
/// </summary>
public sealed class AngryBehavior : BehaviorBase
{
    public override string Name => "angry";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;
    private double _t;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("zebra", restart: true);
        pet.ShiftTo(Palettes.IndexOf("ink"), 14);
        pet.Pestered = 0;
        c.Sound.Play("squirt", 0.35);
        c.Renderer.SpawnInk(pet.Pos);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.Vel *= Math.Exp(-2 * dt);
        pet.Pos += pet.Vel * dt;
        pet.VisualBob = Math.Abs(Math.Sin(_t * 9)) * -7;   // stamping
        pet.FacingRight = c.World.Cursor.X > pet.Pos.X;    // squaring up to you

        if (_t > 2.4)
        {
            // Storm off to the far side of the tank.
            var t = c.World.VirtualScreen;
            double away = pet.Pos.X < t.Left + t.Width / 2 ? 1 : -1;
            pet.Vel = new Vector(away * 620, -120);
            Next = new DriftBehavior();
            Done = true;
        }
    }
}
