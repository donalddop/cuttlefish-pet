using System.Windows;
using System.Windows.Controls;

namespace CuttlefishPet.Core;

/// <summary>
/// A little fish drifting through the tank. It wanders on its own and bolts when a
/// cuttlefish gets too close, which is what makes stalking it worth watching.
/// </summary>
public sealed class Prey
{
    public Point Pos;
    public Vector Vel;
    public double Age;
    public bool Eaten;
    public bool FacingRight = true;
    /// <summary>Set while a pet is committed to this fish, so they don't all converge.</summary>
    public Pet? StalkedBy;
    public Image Visual = null!;

    private Point _target;
    private double _retarget;
    private double _panic;

    public bool Expired => Eaten || Age > 75;
    public bool Panicking => _panic > 0;

    public void Tick(double dt, WorldState world, Random rng)
    {
        Age += dt;
        _retarget -= dt;
        _panic = Math.Max(0, _panic - dt);

        // Bolt from the nearest cuttlefish that has come within striking distance.
        Vector flee = default;
        foreach (var pet in world.Pets)
        {
            var away = Pos - pet.Pos;
            double d = away.Length;
            if (d < 200 && d > 1)
            {
                flee += away / d * (200 - d);
                _panic = 1.2;
            }
        }

        Vector desired;
        if (flee.Length > 1)
        {
            desired = flee / flee.Length * 260;
        }
        else
        {
            if (_retarget <= 0 || (_target - Pos).Length < 50)
            {
                var t = world.VirtualScreen;
                _target = new Point(t.Left + 90 + rng.NextDouble() * (t.Width - 180),
                                    t.Top + 90 + rng.NextDouble() * (t.Height - 220));
                _retarget = 3 + rng.NextDouble() * 4;
            }
            var to = _target - Pos;
            desired = to.Length < 1 ? default : to / to.Length * 95;
        }

        Vel += (desired - Vel) * Math.Min(1, 2.4 * dt);
        Pos += Vel * dt;

        var tank = world.VirtualScreen;
        Pos = new Point(Math.Clamp(Pos.X, tank.Left + 40, tank.Right - 40),
                        Math.Clamp(Pos.Y, tank.Top + 60, tank.Bottom - 40));
        if (Math.Abs(Vel.X) > 8) FacingRight = Vel.X > 0;
    }
}
