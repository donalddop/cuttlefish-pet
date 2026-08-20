using System.Windows;

namespace CuttlefishPet.Core;

public static class PhysicsEngine
{
    public const double Gravity = 2500;       // px/s^2
    public const double TerminalVelocity = 1600;

    /// <summary>
    /// Glue pets to their surface (riding window moves for free) or apply gravity,
    /// landing on the first horizontal surface crossed while falling.
    /// </summary>
    public static void Tick(Pet pet, WorldState world, double dt)
    {
        if (pet.Machine.Current.OverridesPhysics) return;

        if (pet.Surface != null)
        {
            var s = world.Find(pet.Surface, pet.Pos.X);
            if (s == null)
            {
                pet.Surface = null; // window closed/minimized under our feet
            }
            else
            {
                pet.Pos.X += s.X1 - pet.Surface.X1; // ride horizontal window movement
                pet.Pos.Y = s.Y;
                pet.Surface = s;
                if (pet.Pos.X < s.X1 || pet.Pos.X > s.X2)
                    pet.Surface = null; // walked (or was slid) off the edge
            }
        }

        if (pet.Surface == null)
        {
            pet.Vel.Y = Math.Min(pet.Vel.Y + Gravity * dt, TerminalVelocity);
            double newX = pet.Pos.X + pet.Vel.X * dt;
            double newY = pet.Pos.Y + pet.Vel.Y * dt;

            // Clamp inside the virtual screen horizontally; bounce softly off walls.
            var vs = world.VirtualScreen;
            if (newX < vs.Left + 10) { newX = vs.Left + 10; pet.Vel.X = Math.Abs(pet.Vel.X) * 0.4; }
            if (newX > vs.Right - 10) { newX = vs.Right - 10; pet.Vel.X = -Math.Abs(pet.Vel.X) * 0.4; }

            if (pet.Vel.Y > 0)
            {
                var landing = FindLanding(newX, pet.Pos.Y, newY, world);
                if (landing != null)
                {
                    pet.Surface = landing;
                    newY = landing.Y;
                    pet.Vel = new Vector(0, 0);
                }
                else if (newY > vs.Bottom - 4)
                {
                    // Absolute bottom of the virtual screen: always solid.
                    newY = vs.Bottom - 4;
                    pet.Surface = new Surface(SurfaceKind.Floor, IntPtr.Zero, vs.Left, vs.Right, newY);
                    pet.Vel = new Vector(0, 0);
                }
            }

            pet.Pos.X = newX;
            pet.Pos.Y = newY;
        }
    }

    /// <summary>Highest horizontal surface crossed while moving down from fromY to toY at x.</summary>
    public static Surface? FindLanding(double x, double fromY, double toY, WorldState world)
    {
        Surface? landing = null;
        foreach (var s in world.Horizontal())
        {
            if (s.Y < fromY - 1 || s.Y > toY) continue;  // not crossed this tick
            if (x < s.X1 || x > s.X2) continue;          // not above it
            // Highest wins; on a tie prefer a real ledge over the screen-bottom floor,
            // since the taskbar top sits exactly on the working-area bottom.
            if (landing == null || s.Y < landing.Y ||
                (s.Y == landing.Y && landing.Kind == SurfaceKind.Floor && s.Kind != SurfaceKind.Floor))
                landing = s;
        }
        return landing;
    }
}
