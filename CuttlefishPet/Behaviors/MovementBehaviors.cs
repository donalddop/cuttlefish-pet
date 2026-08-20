using System.Windows;
using CuttlefishPet.Core;

namespace CuttlefishPet.Behaviors;

/// <summary>Swim to a window's side edge, then tentacle-climb up and settle on top.</summary>
public sealed class ClimbBehavior : BehaviorBase
{
    public override string Name => "climb";
    public override bool OverridesPhysics => _phase == Phase.Climbing;

    private enum Phase { Approach, Climbing }
    private Phase _phase = Phase.Approach;
    private Surface _edge;
    private const double ClimbSpeed = 55;
    private const double ApproachSpeed = 85;

    public ClimbBehavior(Surface edge) => _edge = edge;

    public static Surface? FindTarget(BehaviorContext c)
    {
        var pet = c.Pet;
        Surface? best = null;
        double bestDist = 500;
        foreach (var s in c.World.Vertical())
        {
            double dist = Math.Abs(s.X1 - pet.Pos.X);
            // Edge base must be reachable from the pet's level, and worth climbing.
            if (dist < bestDist && s.Y2 >= pet.Pos.Y - 40 && s.Y < pet.Pos.Y - 100)
            {
                best = s;
                bestDist = dist;
            }
        }
        return best;
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.FacingRight = _edge.X1 > c.Pet.Pos.X;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        var edge = c.World.Find(_edge);
        if (edge == null) { Done = true; return; } // window vanished

        if (_phase == Phase.Approach)
        {
            pet.Pos.X += _edge.X1 - edge.X1; // window may drift while we approach — X is ours to steer
            _edge = edge;

            if (pet.Surface == null) { Done = true; return; } // fell while approaching
            double wallX = edge.X1 + (edge.Kind == SurfaceKind.WindowLeft ? -12 : 12);
            double dx = wallX - pet.Pos.X;
            if (Math.Abs(dx) < 5)
            {
                if (pet.Pos.Y <= edge.Y || pet.Pos.Y > edge.Y2 + 60) { Done = true; return; } // level mismatch
                _phase = Phase.Climbing;
                pet.Anim.Play("climb");
                pet.FacingRight = edge.Kind == SurfaceKind.WindowLeft;
                pet.Surface = null;
                pet.Vel = new Vector(0, 0);
            }
            else
            {
                pet.FacingRight = dx > 0;
                pet.Pos.X += Math.Sign(dx) * ApproachSpeed * dt;
            }
        }
        else
        {
            // Stick to the wall (following the window if it moves) and go up.
            pet.Pos.X = edge.X1 + (edge.Kind == SurfaceKind.WindowLeft ? -12 : 12);
            pet.Pos.Y += (edge.Y - _edge.Y) - ClimbSpeed * dt;
            _edge = edge;

            if (pet.Pos.Y <= edge.Y + 4)
            {
                // Reached the top: hop over the rim onto the window top.
                pet.Pos.Y = edge.Y;
                pet.Pos.X += edge.Kind == SurfaceKind.WindowLeft ? 26 : -26;
                Surface? top = null;
                foreach (var s in c.World.Horizontal())
                    if (s.Kind == SurfaceKind.WindowTop && s.Hwnd == edge.Hwnd &&
                        pet.Pos.X >= s.X1 && pet.Pos.X <= s.X2)
                        top = s;
                pet.Surface = top; // null → falls, which is fine (top occluded)
                if (top != null) Next = new SitBehavior();
                Done = true;
            }
        }
    }
}

/// <summary>Ballistic jet-propelled hop onto another window top or the taskbar.</summary>
public sealed class JumpToWindowBehavior : BehaviorBase
{
    public override string Name => "jump";
    public override bool Interruptible => false;
    private double _airTime;

    public static Surface? FindTarget(BehaviorContext c)
    {
        var pet = c.Pet;
        var candidates = new List<Surface>();
        foreach (var s in c.World.Horizontal())
        {
            if (pet.Surface != null && s.SameAs(pet.Surface)) continue;
            double cx = Math.Clamp(pet.Pos.X, s.X1, s.X2);
            double dx = cx - pet.Pos.X, dy = s.Y - pet.Pos.Y;
            // A "target" on the same walking line right under our feet is not a jump.
            if (Math.Abs(dy) < 40 && Math.Abs(dx) < 120) continue;
            if (Math.Abs(dx) < 520 && dy > -320 && dy < 600 && s.X2 - s.X1 > 90)
                candidates.Add(s);
        }
        return candidates.Count == 0 ? null : candidates[c.Rng.Next(candidates.Count)];
    }

    private readonly Surface _target;
    public JumpToWindowBehavior(Surface target) => _target = target;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("jump");
        c.Sound.Play("blip", 0.25);

        double targetX = Math.Clamp(pet.Pos.X, _target.X1 + 30, _target.X2 - 30);
        double dx = targetX - pet.Pos.X;
        double dy = _target.Y - pet.Pos.Y;
        double t = 0.4 + Math.Sqrt(dx * dx + dy * dy) / 1300;

        pet.Surface = null;
        pet.Vel = new Vector(dx / t, dy / t - 0.5 * PhysicsEngine.Gravity * t);
        pet.FacingRight = dx >= 0;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        _airTime += dt;
        if (c.Pet.Surface != null) { Done = true; return; } // landed (target or not)
        if (_airTime > 4) { Next = new FallBehavior(); Done = true; }
    }
}

/// <summary>Startled jet-dash away from a fast approaching cursor.</summary>
public sealed class FleeBehavior : BehaviorBase
{
    public override string Name => "flee";
    public override bool Interruptible => false;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("jump");
        c.Sound.Play("blip", 0.35);
        double away = pet.Pos.X >= c.World.Cursor.X ? 1 : -1;
        pet.Surface = null;
        pet.Vel = new Vector(away * 750, -560);
        pet.FacingRight = away > 0;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        if (c.Pet.Surface != null) Done = true;
    }
}

/// <summary>Curiously stalk the cursor along the current surface, then watch it.</summary>
public sealed class ChaseCursorBehavior : BehaviorBase
{
    public override string Name => "chase";
    private const double Speed = 95;
    private double _watchTime, _elapsed;

    public override void Enter(BehaviorContext c) => c.Pet.Anim.Play("swim");

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _elapsed += dt;
        if (pet.Surface == null || _elapsed > 12) { Done = true; return; }

        double dx = c.World.Cursor.X - pet.Pos.X;
        if (Math.Abs(dx) > 900) { Done = true; return; } // lost interest

        if (Math.Abs(dx) > 100)
        {
            pet.Anim.Play("swim");
            pet.FacingRight = dx > 0;
            double nx = pet.Pos.X + Math.Sign(dx) * Speed * dt;
            pet.Pos.X = Math.Clamp(nx, pet.Surface.X1 + 8, pet.Surface.X2 - 8);
            _watchTime = 0;
        }
        else
        {
            pet.Anim.Play("idle"); // close enough: hover and stare
            pet.FacingRight = dx > 0;
            _watchTime += dt;
            if (_watchTime > 2.5) Done = true;
        }
    }
}
