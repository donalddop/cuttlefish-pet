namespace CuttlefishPet.Core;

/// <summary>
/// Who this cuttlefish knows, and how it feels about them. One number per animal,
/// from -1 for something it has learned to keep away from up to 1 for the one it
/// keeps ending up next to.
///
/// Time spent alongside another only ever gets as far as <see cref="Familiar"/>:
/// recognising somebody is not the same as liking them, and liking takes an
/// occasion -- a courtship that was welcome, a scrap that was not.
/// </summary>
public sealed class Relations
{
    /// <summary>As far as merely being around each other can take a bond.</summary>
    public const double Familiar = 0.5;

    /// <summary>
    /// The person at the keyboard, filed under an id no cuttlefish can have. They
    /// bond with the hand that feeds them and hold it against the one that raps on
    /// the glass, and because it lives in the same table as everybody else it is
    /// saved and restored along with them.
    /// </summary>
    public const int You = -1;

    private readonly Dictionary<int, double> _bond = new();

    public double With(int id) => _bond.GetValueOrDefault(id);

    /// <param name="ceiling">
    /// The most this particular kind of contact can be worth. Proximity passes
    /// <see cref="Familiar"/>; an occasion passes 1.
    /// </param>
    public void Warm(int id, double by, double ceiling = 1)
    {
        double now = With(id);
        if (now >= ceiling) return;                 // already past what this can earn
        _bond[id] = Math.Clamp(Math.Min(now + by, ceiling), -1, 1);
    }

    public void Cool(int id, double by) => _bond[id] = Math.Clamp(With(id) - by, -1, 1);

    /// <summary>The one it thinks most of, if it thinks much of anyone.</summary>
    public int? Dearest(double atLeast = 0.35)
    {
        int? best = null;
        double bestBond = atLeast;
        foreach (var (id, bond) in _bond)
            if (id != You && bond > bestBond) { bestBond = bond; best = id; }
        return best;
    }

    /// <summary>Nobody needs to carry a grudge against an animal that is gone.</summary>
    public void Forget(int id) => _bond.Remove(id);

    /// <summary>Everything it knows about anyone, for saving and for the tests.</summary>
    public IReadOnlyDictionary<int, double> Everyone => _bond;

    /// <summary>Put a saved bond back, on restoring a tank from disk.</summary>
    public void Remember(int id, double bond) => _bond[id] = Math.Clamp(bond, -1, 1);
}
