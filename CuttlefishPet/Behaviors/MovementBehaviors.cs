using System.Windows;
using CuttlefishPet.Core;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Walk to a vertical edge — a window's side or the edge of the screen itself — then
/// tentacle-climb along it. Upwards it ends on the window top or under the ceiling;
/// downwards it rides the wall until something landable comes up. Sliding is the same
/// move at speed, with a squeal.
/// </summary>
public sealed class ClimbBehavior : BehaviorBase
{
    public override string Name => _slide ? "slide" : "climb";
    public override bool OverridesPhysics => _phase == Phase.OnWall;

    private enum Phase { Approach, OnWall }
    private Phase _phase = Phase.Approach;
    private Surface _edge;
    private readonly bool _down, _slide;
    private const double ApproachSpeed = 62;
    private double _speed;

    public ClimbBehavior(Surface edge, bool down = false, bool slide = false)
    {
        _edge = edge;
        _down = down;
        _slide = slide;
        _speed = slide ? 205 : 38;
    }

    /// <summary>Nearest wall worth climbing from where the pet is standing.</summary>
    public static Surface? FindTarget(BehaviorContext c, bool down = false)
    {
        var pet = c.Pet;
        Surface? best = null;
        double bestDist = 620;
        foreach (var s in c.World.Vertical())
        {
            double dist = Math.Abs(s.X1 + s.ClingOffset - pet.Pos.X);
            if (dist >= bestDist) continue;
            if (down)
            {
                // Need wall below us and somewhere to end up.
                if (s.Y > pet.Pos.Y - 20 || s.Y2 < pet.Pos.Y + 120) continue;
            }
            else
            {
                if (s.Y2 < pet.Pos.Y - 40 || s.Y > pet.Pos.Y - 120) continue;
            }
            best = s;
            bestDist = dist;
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
            pet.Pos.X += edge.X1 - _edge.X1; // window may drift while we walk over
            _edge = edge;
            if (pet.Surface == null) { Done = true; return; }

            double wallX = edge.X1 + edge.ClingOffset;
            double dx = wallX - pet.Pos.X;
            if (Math.Abs(dx) < 6)
            {
                if (pet.Pos.Y <= edge.Y || pet.Pos.Y > edge.Y2 + 60) { Done = true; return; }
                _phase = Phase.OnWall;
                pet.Anim.Play(_slide ? "slide" : "climb", restart: true);
                pet.FacingRight = edge.ClingOffset < 0;
                pet.Surface = null;
                pet.Vel = new Vector(0, 0);
            }
            else
            {
                pet.FacingRight = dx > 0;
                pet.Pos.X += Math.Sign(dx) * ApproachSpeed * dt;
            }
            return;
        }

        // Stuck to the wall: follow it sideways if the window moves, travel along it.
        double step = _speed * dt;
        pet.Pos.X = edge.X1 + edge.ClingOffset;
        double prevY = pet.Pos.Y;
        pet.Pos.Y += (edge.Y - _edge.Y) + (_down ? step : -step);
        _edge = edge;

        if (_down)
        {
            var landing = PhysicsEngine.FindLanding(pet.Pos.X, prevY, pet.Pos.Y, c.World);
            if (landing != null)
            {
                pet.Pos.Y = landing.Y;
                pet.Surface = landing;
                Done = true;
                return;
            }
            if (pet.Pos.Y >= edge.Y2) { Done = true; }  // ran out of wall → drop
            return;
        }

        if (pet.Pos.Y <= edge.Y + 4)
        {
            pet.Pos.Y = edge.Y;
            if (edge.Hwnd != IntPtr.Zero)
            {
                // Window edge: haul yourself over the rim onto the title bar.
                pet.Pos.X += edge.Kind == SurfaceKind.WindowLeft ? 26 : -26;
                foreach (var s in c.World.Horizontal())
                    if (s.Kind == SurfaceKind.WindowTop && s.Hwnd == edge.Hwnd &&
                        pet.Pos.X >= s.X1 && pet.Pos.X <= s.X2)
                        pet.Surface = s;
                if (pet.Surface != null) Next = new SitBehavior();
            }
            else
            {
                // Screen edge: reach over onto the ceiling and hang there.
                pet.Pos.X += edge.Kind == SurfaceKind.ScreenLeft ? 30 : -30;
                foreach (var s in c.World.Horizontal())
                    if (s.Kind == SurfaceKind.Ceiling && pet.Pos.X >= s.X1 && pet.Pos.X <= s.X2)
                        pet.Surface = s;
                if (pet.Surface != null) Next = new CeilingWalkBehavior();
            }
            Done = true;
        }
    }
}

/// <summary>Startled jet-blast away from a fast approaching cursor.</summary>
public sealed class FleeBehavior : BehaviorBase
{
    public override string Name => "flee";
    public override bool Interruptible => false;
    public override bool OverridesPhysics => true;
    private double _t;

    public override void Enter(BehaviorContext c)
    {
        var pet = c.Pet;
        pet.Anim.Play("jump", restart: true);
        c.Sound.Play("blip", 0.35);
        var away = pet.Pos - c.World.Cursor;
        if (away.Length < 1) away = new Vector(1, -1);
        away.Normalize();
        pet.Surface = null;
        pet.Vel = away * 950;
        pet.FacingRight = away.X > 0;
        pet.Alarmed = true;               // the others will see it and scatter too
        c.Renderer.SpawnInk(pet.Pos);
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;
        pet.Vel *= Math.Exp(-1.6 * dt);
        pet.Pos += pet.Vel * dt;
        PhysicsEngine.ClampToTank(pet, c.World);
        if (_t > 1.0 || pet.Vel.Length < 130)
        {
            Next = new SwimFreeBehavior();
            Done = true;
        }
    }
}

/// <summary>Swim over to the cursor and hover there, watching it.</summary>
public sealed class ChaseCursorBehavior : BehaviorBase
{
    public override string Name => "chase";
    public override bool OverridesPhysics => true;
    private const double Speed = 112;
    private double _watchTime, _elapsed;

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _elapsed += dt;
        if (_elapsed > 14) { Next = new SwimFreeBehavior(); Done = true; return; }

        // Hover just off to the side of the cursor rather than sitting on top of it.
        var target = c.World.Cursor + new Vector(pet.FacingRight ? -70 : 70, -20);
        var to = target - pet.Pos;

        if (to.Length > 45)
        {
            pet.Anim.Play("swim");
            var desired = to / to.Length * Math.Min(Speed, to.Length * 2.2);
            pet.Vel += (desired - pet.Vel) * Math.Min(1, 3.2 * dt);
            pet.Pos += pet.Vel * dt;
            PhysicsEngine.ClampToTank(pet, c.World);
            if (Math.Abs(pet.Vel.X) > 12) pet.FacingRight = pet.Vel.X > 0;
            _watchTime = 0;
        }
        else
        {
            pet.Anim.Play("idle"); // close enough: hang in the water and stare
            pet.Vel *= Math.Exp(-3 * dt);
            pet.Pos += pet.Vel * dt;
            pet.FacingRight = c.World.Cursor.X > pet.Pos.X;
            pet.VisualBob = Math.Sin(_elapsed * 3) * 2.5;
            _watchTime += dt;
            if (_watchTime > 3) { Next = new SwimFreeBehavior(); Done = true; }
        }
    }
}
