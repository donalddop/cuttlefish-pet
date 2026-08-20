using System.Windows;
using System.Windows.Controls;

namespace CuttlefishPet.Core;

/// <summary>
/// A cuttlebone: the chalky internal shell left behind when a cuttlefish dies, and
/// the reason the colour is called sepia. It is a flotation organ, so it drifts
/// upward and eventually washes out of the top of the tank — which is exactly how
/// they end up on beaches.
/// </summary>
public sealed class Bone
{
    public Point Pos;
    public Vector Vel;
    public double Age;
    /// <summary>Nudged out of the way, so pets leave it alone for a moment.</summary>
    public double Disturbed;
    public Image Visual = null!;

    private const double Buoyancy = -26;

    public bool Expired { get; private set; }

    public void Nudge(Vector push)
    {
        Vel += push;
        Disturbed = 3;
    }

    public void Tick(double dt, WorldState world)
    {
        Age += dt;
        Disturbed = Math.Max(0, Disturbed - dt);

        // Rises steadily, swaying as it goes.
        Vel = new Vector(Vel.X * Math.Exp(-1.1 * dt) + Math.Sin(Age * 0.9) * 14 * dt,
                         Vel.Y * Math.Exp(-1.1 * dt) + Buoyancy * dt);
        Pos += Vel * dt;

        var tank = world.VirtualScreen;
        Pos = new Point(Math.Clamp(Pos.X, tank.Left + 30, tank.Right - 30), Pos.Y);
        if (Pos.Y < tank.Top - 60 || Age > 300) Expired = true;
    }
}
