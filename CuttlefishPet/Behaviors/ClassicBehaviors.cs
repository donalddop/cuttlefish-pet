using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Bury into the "sand" of a ledge, vanish for a moment, then erupt somewhere else
/// along it. Cuttlefish really do this; eSheep drilled through the screen instead.
/// </summary>
public sealed class BurrowBehavior : BehaviorBase
{
    public override string Name => "burrow";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;

    private enum Phase { Digging, Hidden, Rising }
    private Phase _phase = Phase.Digging;
    private Surface _ledge = null!;
    private double _t, _hiddenFor;
    private double _emergeX;

    public static bool Possible(BehaviorContext c) =>
        c.Pet.Surface is { IsLandable: true } and { } s && s.X2 - s.X1 > 260;

    public override void Enter(BehaviorContext c)
    {
        _ledge = c.Pet.Surface!;
        c.Pet.Anim.Play("burrow", restart: true);
        _hiddenFor = 1.2 + c.Rng.NextDouble() * 2.5;
        _emergeX = _ledge.X1 + 30 + c.Rng.NextDouble() * (_ledge.X2 - _ledge.X1 - 60);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        var live = c.World.Find(_ledge, pet.Pos.X);
        if (live == null) { pet.Surface = null; Next = new SwimFreeBehavior(); Done = true; return; }
        _ledge = live;

        switch (_phase)
        {
            case Phase.Digging:
                pet.Pos = new Point(pet.Pos.X, live.Y);
                if (pet.Anim.Finished)
                {
                    _phase = Phase.Hidden;
                    _t = 0;
                    pet.Visual.Root.Visibility = Visibility.Hidden;   // fully under
                }
                break;

            case Phase.Hidden:
                if (_t >= _hiddenFor)
                {
                    _phase = Phase.Rising;
                    _t = 0;
                    pet.Pos = new Point(Math.Clamp(_emergeX, live.X1 + 20, live.X2 - 20), live.Y);
                    pet.Visual.Root.Visibility = Visibility.Visible;
                    pet.Anim.Play("burrow", restart: true);           // played in reverse below
                    c.Sound.Play("blip", 0.25);
                }
                break;

            case Phase.Rising:
                // Reverse the dig by walking the animation clock backwards.
                pet.Anim.SetTime(Math.Max(0, pet.Anim.Current.Duration - _t * 1.4));
                pet.Pos = new Point(pet.Pos.X, live.Y);
                if (_t * 1.4 >= pet.Anim.Current.Duration)
                {
                    pet.Surface = live;
                    Done = true;
                }
                break;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.Visual.Root.Visibility = Visibility.Visible;
}

/// <summary>Lay a clutch of eggs on a ledge; one of them hatches into a new pet.</summary>
public sealed class LayEggsBehavior : BehaviorBase
{
    public override string Name => "eggs";
    private double _t;
    private bool _laid;

    public static bool Possible(BehaviorContext c) =>
        c.Pet.Surface is { IsLandable: true } && c.Pet.Mature && c.World.PetCount < 11;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("sit");
        c.Pet.ShiftTo(Palettes.IndexOf("pearl"), 20);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.VisualBob = Math.Sin(_t * 5) * 2;

        if (!_laid && _t > 1.4)
        {
            _laid = true;

            // How many eggs is the whole population control in one line. An empty
            // tank gets a big clutch; a full one gets a token single egg that the
            // hatching cap will probably refuse anyway. A bloom adds two on top.
            int clutch = Math.Clamp(4 - c.World.PetCount, 1, 3) + (c.World.Bloom > 0 ? 3 : 0);
            for (int i = 0; i < clutch; i++)
            {
                var spot = new Point(pet.Pos.X + (pet.FacingRight ? -34 : 34) + (i - (clutch - 1) / 2.0) * 19,
                                     pet.Pos.Y);
                c.AddProp(new Prop
                {
                    Anim = "egg",
                    Pos = spot,
                    // Staggered a little so a brood trickles out instead of popping
                    // into existence all at once.
                    Life = 38 + i * 4 + c.Rng.NextDouble() * 3,
                    OnExpire = p => c.SpawnPet(new Point(p.X, p.Y - 40), hatchling: true),
                });
            }
            c.Sound.Play("bubble", 0.3);

            // Spawning is the last thing a cuttlefish does — real ones die shortly
            // after breeding. It is also what makes a boom collapse: a generation
            // that all bred at once all dies at once.
            pet.Lifespan = Math.Min(pet.Lifespan, pet.Age + 120);
        }
        if (_t > 3) Done = true;
    }
}

/// <summary>
/// Panic move: blast out a full ink cloud and be somewhere else entirely when it
/// clears. eSheep exploded; a cuttlefish just cheats.
/// </summary>
public sealed class InkBombBehavior : BehaviorBase
{
    public override string Name => "inkBomb";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;
    private double _t;
    private bool _jumped;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("startle", restart: true);
        pet.Surface = null;
        c.Renderer.SpawnInk(pet.Pos);
        c.Sound.Play("squirt", 0.45);
        pet.ShiftTo(Palettes.IndexOf("ink"), 8);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        if (!_jumped && _t > 0.22)
        {
            _jumped = true;
            pet.Visual.Root.Visibility = Visibility.Hidden;
        }
        if (_t > 0.6 && pet.Visual.Root.Visibility == Visibility.Hidden)
        {
            var t = c.World.VirtualScreen;
            pet.Pos = new Point(t.Left + 120 + c.Rng.NextDouble() * (t.Width - 240),
                                t.Top + 120 + c.Rng.NextDouble() * (t.Height - 300));
            pet.Vel = new Vector(0, 0);
            pet.Visual.Root.Visibility = Visibility.Visible;
            c.Renderer.SpawnInk(pet.Pos);
            Next = new SwimFreeBehavior();
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.Visual.Root.Visibility = Visibility.Visible;
}

/// <summary>Drift upward hanging from a big bubble, until it pops.</summary>
public sealed class BalloonBehavior : BehaviorBase
{
    public override string Name => "balloon";
    public override bool OverridesPhysics => true;
    private double _t;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("balloon", restart: true);
        c.Pet.Surface = null;
        c.Pet.Vel = new Vector(0, 0);
        c.Sound.Play("bubble", 0.3);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.Pos = new Point(pet.Pos.X + Math.Sin(_t * 1.1) * 26 * dt, pet.Pos.Y - 62 * dt);
        pet.Rotation = Math.Sin(_t * 1.4) * 6;
        PhysicsEngine.ClampToTank(pet, c.World);

        bool atTop = pet.Pos.Y <= c.World.VirtualScreen.Top + 110;
        if (atTop || _t > 9)
        {
            c.Renderer.SpawnBubble(pet.Pos + new Vector(6, -46));
            c.Sound.Play("blip", 0.2);
            pet.Rotation = 0;
            pet.Vel = new Vector(0, 90);
            Next = new SwimFreeBehavior();
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.Rotation = 0;
}

/// <summary>
/// Fade out as a pale spirit and leave the tank — then a fresh one swims in, so the
/// crew size stays the same. Pure eSheep theatre.
/// </summary>
public sealed class GhostBehavior : BehaviorBase
{
    public override string Name => "ghost";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;
    private double _t;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("ghost", restart: true);
        c.Pet.Surface = null;
        c.Pet.Vel = new Vector(0, 0);
        c.Sound.Play("bubble", 0.2);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.Pos = new Point(pet.Pos.X + Math.Sin(_t * 1.6) * 20 * dt, pet.Pos.Y - 85 * dt);
        pet.Fade = Math.Max(0, 1 - _t / 4.5);

        if (_t > 4.5)
        {
            pet.Fade = 1;
            var t = c.World.VirtualScreen;
            c.SpawnPet(new Point(t.Left + 140 + c.Rng.NextDouble() * (t.Width - 280),
                                 t.Bottom - 200), hatchling: false);
            c.RemovePet(pet);
            Done = true;
        }
    }

    public override void Exit(BehaviorContext c) => c.Pet.Fade = 1;
}

/// <summary>Leave a small ink blot behind, the way eSheep left droppings.</summary>
public sealed class InkBlotBehavior : BehaviorBase
{
    public override string Name => "blot";
    private double _t;
    private bool _done;

    public static bool Possible(BehaviorContext c) =>
        c.Pet.Surface is { IsLandable: true };

    public override void Enter(BehaviorContext c) => c.Pet.Anim.Play("sit");

    public override void Tick(BehaviorContext c, double dt)
    {
        _t += dt;
        c.Pet.VisualBob = Math.Sin(_t * 8) * 2;
        if (!_done && _t > 0.8)
        {
            _done = true;
            c.AddProp(new Prop
            {
                Anim = "blot",
                Pos = new Point(c.Pet.Pos.X + (c.Pet.FacingRight ? -26 : 26), c.Pet.Pos.Y),
                Life = 14,
            });
            c.Sound.Play("squirt", 0.2);
        }
        if (_t > 1.6) Done = true;
    }
}

/// <summary>Nibble at the edge of whatever it is sitting on.</summary>
public sealed class NibbleBehavior : BehaviorBase
{
    public override string Name => "nibble";
    private double _t;

    public static bool Possible(BehaviorContext c) =>
        c.Pet.Surface is { Kind: SurfaceKind.WindowTop or SurfaceKind.TaskbarTop };

    public override void Enter(BehaviorContext c) => c.Pet.Anim.Play("eat", restart: true);

    public override void Tick(BehaviorContext c, double dt)
    {
        _t += dt;
        c.Pet.VisualBob = Math.Sin(_t * 11) * 1.5;
        if (_t > 2.5 + c.Rng.NextDouble() * 2) Done = true;
    }
}

/// <summary>A static jolt out of nowhere: blanched white with bolts crackling.</summary>
public sealed class ShockBehavior : BehaviorBase
{
    public override string Name => "shock";
    public override bool NeedsPerch => false;   // plays out mid-water just fine
    public override bool Interruptible => false;
    private double _t;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("shock", restart: true);
        c.Pet.ShiftTo(Palettes.IndexOf("pearl"), 5);
        c.Sound.Play("blip", 0.35);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _t += dt;
        c.Pet.VisualBob = Math.Sin(_t * 40) * 3;   // buzzing
        if (_t > 0.9) Done = true;
    }
}
