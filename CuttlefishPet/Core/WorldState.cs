using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>Shared per-tick snapshot of everything the pets can sense. Physical pixels.</summary>
public sealed class WorldState
{
    public List<Surface> Surfaces { get; } = new();
    public List<Treat> Treats { get; } = new();
    public Rect VirtualScreen { get; set; }
    public Point Cursor { get; set; }
    public Vector CursorVelocity { get; set; }
    /// <summary>Seconds the cursor has been (nearly) stationary — prey worth stalking.</summary>
    public double CursorStill { get; set; }
    /// <summary>Decayed keypress rate; ~0 idle, >3 while actively typing.</summary>
    public double TypingRate { get; set; }
    /// <summary>Seconds since the user last touched mouse or keyboard, system-wide.</summary>
    public double IdleSeconds { get; set; }
    /// <summary>Windows that appeared since the last full enumeration.</summary>
    public List<Rect> AppearedWindows { get; } = new();
    /// <summary>How many cuttlefish are in the tank right now.</summary>
    public int PetCount { get; set; }

    /// <summary>
    /// Re-resolve a surface reference to this tick's version (windows move). A window
    /// top can split into several segments when partly occluded; when an x is given,
    /// prefer the segment under that x.
    /// </summary>
    public Surface? Find(Surface reference, double? x = null)
    {
        Surface? first = null;
        foreach (var s in Surfaces)
        {
            if (!s.SameAs(reference)) continue;
            if (x == null || (x >= s.X1 && x <= s.X2)) return s;
            first ??= s;
        }
        return first;
    }

    public IEnumerable<Surface> Horizontal()
    {
        foreach (var s in Surfaces)
            if (!s.IsVertical)
                yield return s;
    }

    public IEnumerable<Surface> Vertical()
    {
        foreach (var s in Surfaces)
            if (s.IsVertical)
                yield return s;
    }

    /// <summary>Nearest unclaimed shrimp, or null.</summary>
    public Treat? NearestTreat(Pet pet)
    {
        Treat? best = null;
        double bestDist = double.MaxValue;
        foreach (var t in Treats)
        {
            if (t.Expired || (t.ClaimedBy != null && t.ClaimedBy != pet)) continue;
            double d = (t.Pos - pet.Pos).Length;
            if (d < bestDist) { best = t; bestDist = d; }
        }
        return best;
    }
}
