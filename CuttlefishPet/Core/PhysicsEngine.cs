using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>
/// The screen is a tank, not a platformer. Nothing falls: momentum bleeds off into
/// the water and pets hover wherever they stop. Surfaces still matter, but as places
/// to perch on deliberately rather than as the floor everything lands on.
/// </summary>
public static class PhysicsEngine
{
    /// <summary>Velocity decay per second in open water (a throw dies out in ~1s).</summary>
    public const double WaterDrag = 1.9;
    /// <summary>Slow sinking while completely idle, so a resting pet drifts down a little.</summary>
    public const double Sink = 14;

    public static void Tick(Pet pet, WorldState world, double dt)
    {
        if (pet.Machine.Current.OverridesPhysics) return;

        if (pet.Surface != null)
        {
            var s = world.Find(pet.Surface, pet.Pos.X);
            if (s == null)
            {
                pet.Surface = null; // window closed under it — just start swimming
            }
            else
            {
                pet.Pos.X += s.X1 - pet.Surface.X1; // ride horizontal window movement
                pet.Pos.Y = s.Y;
                pet.Surface = s;
                if (pet.Pos.X < s.X1 || pet.Pos.X > s.X2)
                    pet.Surface = null; // drifted off the end of the perch
            }
            return;
        }

        pet.Vel = new Vector(pet.Vel.X * Math.Exp(-WaterDrag * dt),
                             pet.Vel.Y * Math.Exp(-WaterDrag * dt) + Sink * dt);
        pet.Pos += pet.Vel * dt;
        ClampToTank(pet, world);
    }

    /// <summary>
    /// Scrolling stirs the tank and drags the swimmers along with it. Applied by the
    /// manager after the behaviour has had its say, because nearly every swimming
    /// behaviour steers the pet itself and would otherwise ignore the current.
    /// </summary>
    public static void ApplyScrollCurrent(Pet pet, WorldState world, double dt)
    {
        double c = world.ScrollCurrent;
        if (Math.Abs(c) < 0.12) return;
        pet.Pos = new Point(pet.Pos.X + Math.Sin(pet.Pos.Y * 0.008) * c * 70 * dt,
                            pet.Pos.Y - c * 230 * dt);
        pet.Rotation = Math.Clamp(pet.Rotation - c * 9 * dt * 60, -20, 20);
        ClampToTank(pet, world);
    }

    /// <summary>
    /// Keep pets inside the glass with a soft bounce. Margins allow for the body
    /// hanging off the contact point, so nothing ends up half outside the screen.
    /// </summary>
    public static void ClampToTank(Pet pet, WorldState world)
    {
        var t = world.VirtualScreen;
        const double side = 62, top = 100, bottom = 20;
        if (pet.Pos.X < t.Left + side) { pet.Pos.X = t.Left + side; pet.Vel.X = Math.Abs(pet.Vel.X) * 0.35; }
        if (pet.Pos.X > t.Right - side) { pet.Pos.X = t.Right - side; pet.Vel.X = -Math.Abs(pet.Vel.X) * 0.35; }
        if (pet.Pos.Y < t.Top + top) { pet.Pos.Y = t.Top + top; pet.Vel.Y = Math.Abs(pet.Vel.Y) * 0.35; }
        if (pet.Pos.Y > t.Bottom - bottom) { pet.Pos.Y = t.Bottom - bottom; pet.Vel.Y = -Math.Abs(pet.Vel.Y) * 0.35; }
    }

    /// <summary>Highest surface crossed while moving down from fromY to toY at x.</summary>
    public static Surface? FindLanding(double x, double fromY, double toY, WorldState world)
    {
        Surface? landing = null;
        foreach (var s in world.Horizontal())
        {
            if (!s.IsLandable) continue;                 // can't land on a ceiling
            if (s.Y < fromY - 1 || s.Y > toY) continue;  // not crossed this tick
            if (x < s.X1 || x > s.X2) continue;          // not above it
            if (landing == null || s.Y < landing.Y ||
                (s.Y == landing.Y && landing.Kind == SurfaceKind.Floor && s.Kind != SurfaceKind.Floor))
                landing = s;
        }
        return landing;
    }
}
