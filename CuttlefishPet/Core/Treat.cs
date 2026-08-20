using System.Windows;
using System.Windows.Controls;

namespace CuttlefishPet.Core;

/// <summary>
/// A shrimp in the tank. Unlike the fish it does not really flee — it scuttles about
/// in short bursts and drifts, which is what makes it catchable.
/// </summary>
public sealed class Treat
{
    public Point Pos;
    public Vector Vel;
    public bool Eaten;
    public double Age;
    public bool FacingRight = true;
    /// <summary>Set once a pet commits to it, so several pets don't converge on one.</summary>
    public Pet? ClaimedBy;
    public Image Visual = null!;

    private Point _target;
    private double _restFor, _retarget;

    public bool Expired => Eaten || Age > 150;

    public void Tick(double dt, WorldState world, Random rng)
    {
        Age += dt;
        _retarget -= dt;
        _restFor -= dt;

        if (_restFor > 0)
        {
            // Sitting still on the bottom or mid-water, twitching now and then.
            Vel *= Math.Exp(-3 * dt);
        }
        else
        {
            if (_retarget <= 0 || (_target - Pos).Length < 40)
            {
                var t = world.VirtualScreen;
                // Shrimp keep to the lower half, the way they hug the seabed.
                _target = new Point(t.Left + 80 + rng.NextDouble() * (t.Width - 160),
                                    t.Top + t.Height * 0.45 + rng.NextDouble() * (t.Height * 0.5));
                _retarget = 4 + rng.NextDouble() * 5;
                if (rng.NextDouble() < 0.35) _restFor = 2 + rng.NextDouble() * 4;
            }

            var to = _target - Pos;
            var desired = to.Length < 1 ? default : to / to.Length * 52;
            Vel += (desired - Vel) * Math.Min(1, 1.8 * dt);
        }

        // A shrimp's swimming is all little hops rather than smooth gliding.
        Pos += (Vel + new Vector(0, Math.Sin(Age * 9) * 26)) * dt;

        var tank = world.VirtualScreen;
        Pos = new Point(Math.Clamp(Pos.X, tank.Left + 40, tank.Right - 40),
                        Math.Clamp(Pos.Y, tank.Top + 120, tank.Bottom - 30));
        if (Math.Abs(Vel.X) > 6) FacingRight = Vel.X < 0;   // the art faces left
    }
}
