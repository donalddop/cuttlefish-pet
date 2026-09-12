namespace CuttlefishPet.Core;

/// <summary>
/// What one cuttlefish has worked out about its own repertoire, on this desktop.
///
/// Two things are remembered per behaviour. What it is worth: a running estimate,
/// nudged every time the behaviour ends, of whether it actually settled the need it
/// claims to serve. And how much of it the animal has had lately, which fades over
/// a minute or two. The first is learning and the second is what stops learning
/// from eating the tank: a behaviour that keeps paying off keeps being chosen until
/// the animal has had its fill of it, at which point something else gets a turn.
///
/// Both start empty, so a newly hatched cuttlefish behaves exactly as it did before
/// any of this existed, and only diverges once it has lived a bit.
/// </summary>
public sealed class Memory
{
    /// <summary>How well it went, -1..1. Absent means no opinion yet.</summary>
    private readonly Dictionary<string, double> _worth = new();

    /// <summary>How much of it lately. Absent means none.</summary>
    private readonly Dictionary<string, double> _lately = new();

    private string _running = "";
    private DriveState _wanted;

    /// <summary>How fast an estimate moves toward the latest outcome.</summary>
    private const double LearningRate = 0.28;

    /// <summary>Seconds for one helping to wear off.</summary>
    private const double SatedFade = 95;

    public void Began(string behavior, DriveState wanted)
    {
        _running = behavior;
        _wanted = wanted;
        _lately[behavior] = Lately(behavior) + 0.55;
    }

    /// <summary>
    /// Judge whatever just finished against what the animal wanted when it started.
    /// </summary>
    public void Ended(DriveState got)
    {
        if (_running.Length == 0) return;
        if (Appetites.Payoff(_running, _wanted, got) is double reward)
        {
            double was = Worth(_running);
            _worth[_running] = Math.Clamp(was + LearningRate * (reward - was), -1, 1);
        }
        _running = "";
    }

    /// <summary>Having had enough of something wears off on its own.</summary>
    public void Tick(double dt)
    {
        if (_lately.Count == 0) return;
        foreach (var key in _lately.Keys.ToList())
        {
            double left = _lately[key] - dt / SatedFade;
            if (left <= 0) _lately.Remove(key);
            else _lately[key] = left;
        }
    }

    /// <summary>
    /// What experience does to the odds. Neutral at 1.0 for anything this animal
    /// has no opinion about and has not just done.
    /// </summary>
    public double Appeal(string behavior) =>
        Math.Clamp(1 + Worth(behavior) * 0.85, 0.30, 2.0) / (1 + Lately(behavior) * 0.7);

    public double Worth(string behavior) => _worth.GetValueOrDefault(behavior);
    public double Lately(string behavior) => _lately.GetValueOrDefault(behavior);

    /// <summary>Everything it has an opinion about, for saving and for the tests.</summary>
    public IReadOnlyDictionary<string, double> Learned => _worth;

    /// <summary>Put a saved opinion back, on restoring a tank from disk.</summary>
    public void Relearn(string behavior, double worth) =>
        _worth[behavior] = Math.Clamp(worth, -1, 1);

    /// <summary>How much of a parent's conviction a hatchling starts out with.</summary>
    private const double Dilution = 0.5;

    /// <summary>
    /// What the next generation begins believing. Halved, nudged either way, and
    /// dropped entirely when what is left comes to nothing -- so a brood takes
    /// after its mother's experience without being born certain of it, and an
    /// opinion only survives the generations that keep finding it true.
    /// </summary>
    public static Dictionary<string, double> PassedOn(
        IReadOnlyDictionary<string, double> parent, Random rng)
    {
        var lore = new Dictionary<string, double>();
        foreach (var (behavior, worth) in parent)
        {
            double passed = worth * Dilution;
            // Judged before the nudge, not after: an opinion she never really formed
            // must not reach the next generation as a small random conviction.
            if (Math.Abs(passed) < 0.03) continue;
            passed += (rng.NextDouble() - rng.NextDouble()) * 0.08;
            lore[behavior] = Math.Clamp(passed, -1, 1);
        }
        return lore;
    }
}
