using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>Shared per-tick snapshot of everything the pets can sense. Physical pixels.</summary>
public sealed class WorldState
{
    public List<Surface> Surfaces { get; } = new();
    public List<Treat> Treats { get; } = new();
    /// <summary>Live fish drifting through the tank, there to be hunted.</summary>
    public List<Prey> Prey { get; } = new();
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
    /// <summary>
    /// Every tracked window's rectangle, maximised ones included. Those produce no
    /// ledges to sit on, but they still cover the desktop underneath.
    /// </summary>
    public List<Rect> WindowRects { get; } = new();

    /// <summary>Cuttlebones drifting up through the tank.</summary>
    public List<Bone> Bones { get; } = new();

    /// <summary>Nearest bone worth going to look at, or null.</summary>
    public Bone? NearestBone(Pet pet)
    {
        Bone? best = null;
        double bestDist = 700;
        foreach (var b in Bones)
        {
            if (b.Expired || b.Disturbed > 0) continue;
            double d = (b.Pos - pet.Pos).Length;
            if (d < bestDist) { best = b; bestDist = d; }
        }
        return best;
    }

    /// <summary>Windows minimised since the last tick, by handle.</summary>
    public List<IntPtr> MinimisedWindows { get; } = new();
    /// <summary>An open Recycle Bin window, which they give a wide berth.</summary>
    public Rect? RecycleBin { get; set; }

    /// <summary>Is this point hidden behind some application window?</summary>
    public bool IsCovered(Point p)
    {
        foreach (var r in WindowRects)
            if (r.Contains(p)) return true;
        return false;
    }
    /// <summary>Everyone in the tank, so pets can notice each other.</summary>
    public List<Pet> Pets { get; } = new();
    public int PetCount => Pets.Count;

    /// <summary>User-tunable settings; the density rules are all measured against these.</summary>
    public Settings Settings { get; set; } = new();

    /// <summary>
    /// Seconds left of a plankton bloom. Food is everywhere, courtship is quick and
    /// clutches are large — this is what turns a quiet tank into a swarm. Zero the
    /// rest of the time.
    /// </summary>
    public double Bloom { get; set; }
    /// <summary>Recent scroll wheel motion — a current that pushes swimmers about.</summary>
    public double ScrollCurrent { get; set; }

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

    /// <summary>Nearest fish nobody else is already stalking, or null.</summary>
    public Prey? NearestPrey(Pet pet)
    {
        Prey? best = null;
        double bestDist = double.MaxValue;
        foreach (var f in Prey)
        {
            if (f.Expired || (f.StalkedBy != null && f.StalkedBy != pet)) continue;
            double d = (f.Pos - pet.Pos).Length;
            if (d < bestDist) { best = f; bestDist = d; }
        }
        return best;
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
