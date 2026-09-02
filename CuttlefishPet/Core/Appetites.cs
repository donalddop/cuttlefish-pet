namespace CuttlefishPet.Core;

/// <summary>
/// Turns "what this animal wants" into "what this animal is likely to do next".
///
/// The behaviour machine keeps a flat weight per behaviour -- how often it belongs
/// in a cuttlefish's life at all -- and multiplies it by what comes out of here,
/// which is this cuttlefish, right now. Kept apart from the machine because this is
/// the one piece of the arbitration worth testing on its own.
/// </summary>
public static class Appetites
{
    /// <summary>
    /// What a behaviour is good for, and the sort of animal that reaches for it.
    /// Drive weights say which need it settles; trait weights lean the odds by
    /// temperament, and go negative for anything a bold cuttlefish would not be
    /// seen doing. Everything left out of this table keeps its flat weight, so a
    /// behaviour is only ever pulled around by needs it actually serves.
    /// </summary>
    private sealed record Appetite(
        double Hunger = 0, double Fatigue = 0, double Loneliness = 0,
        double Boredom = 0, double Fear = 0,
        double Boldness = 0, double Sociability = 0, double Curiosity = 0,
        double Restlessness = 0);

    private static readonly Dictionary<string, Appetite> _table = new()
    {
        // food
        ["huntTreat"] = new(Hunger: 1.3),
        ["hunt"]      = new(Hunger: 1.0, Boredom: 0.4, Boldness: 0.6),
        ["stalk"]     = new(Hunger: 1.1, Boldness: 0.3),
        ["nibble"]    = new(Hunger: 0.6, Curiosity: 0.4),
        // rest
        ["settle"]    = new(Fatigue: 1.0, Fear: 0.5),
        ["sit"]       = new(Fatigue: 0.9),
        ["idle"]      = new(Fatigue: 0.6),
        ["hover"]     = new(Fatigue: 0.4),
        // keeping out of the way
        ["lurk"]       = new(Fear: 1.2, Boldness: -0.8),
        ["camouflage"] = new(Fear: 1.0, Boldness: -0.5),
        ["burrow"]     = new(Fear: 0.9, Boldness: -0.4),
        ["inkBomb"]    = new(Fear: 0.8),
        ["peek"]       = new(Curiosity: 0.6, Boldness: -0.3),
        // company
        ["pile"]       = new(Loneliness: 1.0, Fatigue: 0.5, Sociability: 0.9),
        ["cross"]      = new(Loneliness: 0.5, Sociability: 0.5),
        ["colourShow"] = new(Loneliness: 0.6, Sociability: 0.7, Boldness: 0.3),
        ["imitate"]    = new(Loneliness: 0.4, Boredom: 0.7, Curiosity: 0.8),
        // something to do
        ["play"]      = new(Boredom: 1.0, Curiosity: 0.8),
        ["bigBubble"] = new(Boredom: 0.8, Curiosity: 0.6),
        ["bone"]      = new(Boredom: 0.5, Curiosity: 0.7),
        ["balloon"]   = new(Boredom: 0.5, Curiosity: 0.7),
        ["blot"]      = new(Boredom: 0.5),
        ["ghost"]     = new(Boredom: 0.4, Curiosity: 0.4),
        ["swimFree"]  = new(Boredom: 0.8, Restlessness: 0.5),
        ["patrol"]    = new(Boredom: 0.6, Restlessness: 0.5),
        ["dart"]      = new(Boredom: 0.6, Restlessness: 0.8),
        ["jet"]       = new(Boredom: 0.5, Restlessness: 0.7),
        ["climb"]     = new(Curiosity: 0.4, Restlessness: 0.5),
        ["slide"]     = new(Restlessness: 0.6),
        // meddling with the desktop, which takes some nerve
        ["chase"] = new(Boredom: 0.5, Boldness: 0.8),
        ["push"]  = new(Boldness: 0.8, Curiosity: 0.5),
        ["tease"] = new(Boldness: 1.0, Curiosity: 0.4),
        ["ride"]  = new(Boldness: 0.9),
        ["shock"] = new(Boldness: 0.6),
        ["icon"]  = new(Boredom: 0.6, Curiosity: 0.9),
        ["read"]  = new(Boredom: 0.4, Curiosity: 0.8),
        ["caret"] = new(Curiosity: 0.7),
        ["clock"] = new(Curiosity: 0.5),
    };

    /// <summary>
    /// How well this behaviour answers what the animal currently wants. Neutral at
    /// 1.0, which is also what anything outside the table scores, so adding drives
    /// could never quietly switch off half the repertoire.
    /// </summary>
    private static double Fit(Appetite a, Drives d)
    {
        double total = a.Hunger + a.Fatigue + a.Loneliness + a.Boredom + a.Fear;
        if (total <= 0) return 1;
        double sum = a.Hunger * d.Hunger + a.Fatigue * d.Fatigue
                   + a.Loneliness * d.Loneliness + a.Boredom * d.Boredom
                   + a.Fear * d.Fear;
        return 0.5 + 1.6 * (sum / total);
    }

    /// <summary>Temperament: the same need, answered differently by different animals.</summary>
    private static double Suits(Appetite a, Genome g) => Math.Clamp(
        Lean(a.Boldness, g.Boldness) * Lean(a.Sociability, g.Sociability) *
        Lean(a.Curiosity, g.Curiosity) * Lean(a.Restlessness, g.Restlessness),
        0.35, 2.4);

    private static double Lean(double weight, double trait) =>
        weight == 0 ? 1 : Math.Clamp(1 + weight * (trait - 0.5) * 2, 0.3, 2.0);

    /// <summary>
    /// The multiplier on a behaviour's flat weight. 1.0 for anything not in the
    /// table, so needs can only ever pull around behaviours they actually serve.
    /// </summary>
    public static double Weigh(string behavior, Drives d, Genome g) =>
        _table.TryGetValue(behavior, out var a) ? Fit(a, d) * Suits(a, g) : 1;

    /// <summary>Behaviours the needs have an opinion about. Used by the tests.</summary>
    public static IReadOnlyCollection<string> Known => _table.Keys;
}
