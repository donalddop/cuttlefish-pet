using System.Windows;
using CuttlefishPet.Core;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Desktop Dude has it by the mantle. Pinned to his hand, wriggling, for as long as he
/// keeps saying so over the link; if he stops (punched, quit, crashed) it simply slips free.
/// </summary>
public sealed class DudeHeldBehavior : BehaviorBase
{
    public override string Name => "dudeheld";
    public override bool OverridesPhysics => true;
    public override bool Interruptible => false;
    public override bool NeedsPerch => false;

    private Point _at;
    private bool _right;
    private double _since, _t;
    private Vector? _thrown;

    public DudeHeldBehavior(Point at, bool right)
    {
        _at = at;
        _right = right;
    }

    public void Update(Point at, bool right)
    {
        _at = at;
        _right = right;
        _since = 0;
    }

    public void Throw(Vector v)
    {
        if (v.Length > 2200) v *= 2200 / v.Length;
        _thrown = v;
        _since = 99;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("drag");
        c.Pet.Surface = null;
        c.Pet.Vel = new Vector(0, 0);
        c.Pet.Pestered += 0.5;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        _since += dt;
        pet.Pos += (_at - pet.Pos) * Math.Min(1, dt * 18);
        pet.FacingRight = _right;
        pet.Vel = new Vector(0, 0);
        pet.VisualBob = Math.Sin(_t * 14) * 2.5; // wriggling
        if (_since <= 0.6) return;

        // Let go: thrown, or the dude simply stopped holding on.
        pet.VisualBob = 0;
        pet.Vel = _thrown ?? new Vector(0, 0);
        if (pet.Vel.Length > 900)
        {
            c.Renderer.SpawnInk(pet.Pos);
            c.Sound.Play("squirt", 0.4);
        }
        Next = pet.Pestered >= 4 ? new AngryBehavior()
             : pet.Vel.Length > 1100 ? new DizzyBehavior()
             : new DriftBehavior();
        Done = true;
    }
}

/// <summary>
/// The dude climbed on. It bolts and bucks around the screen, never off it, until it has
/// had enough and throws him -- then it is properly cross for a while.
/// </summary>
public sealed class DudeRideBehavior : BehaviorBase
{
    public override string Name => "dude-ride";
    public override bool OverridesPhysics => true;
    public override bool Interruptible => false;
    public override bool NeedsPerch => false;

    private readonly double _duration;
    private double _t, _turnIn;
    private Vector _dir = new(1, 0);

    public DudeRideBehavior(double seconds) => _duration = Math.Clamp(seconds, 3, 20);

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("jump", restart: true);
        pet.Surface = null;
        pet.Pestered += 1;
        pet.Alarmed = true;
        c.Sound.Play("blip", 0.35);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        var tank = c.World.VirtualScreen;
        _t += dt;
        _turnIn -= dt;
        if (_turnIn <= 0)
        {
            _turnIn = 0.6 + c.Rng.NextDouble() * 0.9;
            double a = c.Rng.NextDouble() * Math.PI * 2;
            _dir = new Vector(Math.Cos(a), Math.Sin(a) * 0.6);
        }
        // Keep away from the edges, and low enough that the rider stays on screen.
        if (pet.Pos.X < tank.Left + 140) _dir.X = Math.Abs(_dir.X);
        if (pet.Pos.X > tank.Right - 140) _dir.X = -Math.Abs(_dir.X);
        if (pet.Pos.Y < tank.Top + 220) _dir.Y = Math.Abs(_dir.Y);
        if (pet.Pos.Y > tank.Bottom - 160) _dir.Y = -Math.Abs(_dir.Y);

        var want = _dir * 430 + new Vector(0, Math.Sin(_t * 7) * 170); // the bucking
        pet.Vel += (want - pet.Vel) * Math.Min(1, dt * 3);
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);
        if (Math.Abs(pet.Vel.X) > 20) pet.FacingRight = pet.Vel.X > 0;

        if (_t < _duration) return;
        pet.Vel = new Vector(pet.FacingRight ? 650 : -650, -450);
        pet.Alarmed = false;
        Next = new AngryBehavior();
        Done = true;
    }
}
