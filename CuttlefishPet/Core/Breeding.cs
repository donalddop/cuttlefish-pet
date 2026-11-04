namespace CuttlefishPet.Core;

/// <summary>
/// Two sexes, and what it takes to keep the tank breeding once only half of it can
/// lay.
///
/// The design note of 23 September predicted it: with sexes and nothing else
/// changed, the share of animals that ever leaves a clutch falls from about 36% to
/// about 18%, and the tank thins out over an evening without anything ever going
/// visibly wrong. The compensation is variant (c), a bit of both: courtship comes
/// round more often (the cooldown is 0.7x what it was, roughly 0.7-3 minutes
/// instead of 1-4) and a clutch is 1.4x bigger on average.
///
/// Every number lives here so the next tuning pass is one file, and so the
/// console checks in Tools/DriveTests can hold it to account. The four tuning
/// numbers can also be overridden from settings.json without a rebuild, e.g.
/// "Breeding": {"CooldownFactor": 0.5, "ClutchFactor": 2, "OneSexWaitMin": 60,
/// "OneSexWaitMax": 120}; leave a key out and its default stays.
/// </summary>
public static class Breeding
{
    public const double DefaultCooldownFactor = 0.7, DefaultClutchFactor = 1.4;
    public const double DefaultOneSexWaitMin = 150, DefaultOneSexWaitMax = 270;

    /// <summary>How much more often courtship comes round than before sexes.</summary>
    public static double CooldownFactor { get; private set; } = DefaultCooldownFactor;

    /// <summary>How much bigger a clutch is, on average, than before sexes.</summary>
    public static double ClutchFactor { get; private set; } = DefaultClutchFactor;

    /// <summary>
    /// Take the overrides from settings.json, or go back to the defaults for any
    /// that are missing or make no sense (zero, negative, absurdly large, NaN). A
    /// typo in a hand-edited file should never stop the tank from breeding.
    /// </summary>
    public static void Apply(BreedingOverrides? o)
    {
        CooldownFactor = Pick(o?.CooldownFactor, DefaultCooldownFactor, 0.05, 10);
        ClutchFactor = Pick(o?.ClutchFactor, DefaultClutchFactor, 0.05, 10);
        double lo = Pick(o?.OneSexWaitMin, DefaultOneSexWaitMin, 0, 3600);
        double hi = Pick(o?.OneSexWaitMax, DefaultOneSexWaitMax, 0, 3600);
        (OneSexWaitMin, OneSexWaitMax) = lo <= hi ? (lo, hi) : (hi, lo);
    }

    private static double Pick(double? v, double fallback, double min, double max) =>
        v is double d && double.IsFinite(d) && d >= min && d <= max ? d : fallback;

    /// <summary>Even odds. Hatchlings and arrivals both roll it.</summary>
    public static bool RollFemale(Random rng) => rng.NextDouble() < 0.5;

    /// <summary>Courtship only between a male and a female.</summary>
    public static bool CanCourt(bool aFemale, bool bFemale) => aFemale != bFemale;

    /// <summary>
    /// Seconds until the tank may court again. A bloom keeps its quick ten seconds;
    /// otherwise the old 60 + 90 x (count / target), scaled down.
    /// </summary>
    public static double CourtCooldown(bool bloom, int count, int target) =>
        bloom ? 10 : CooldownFactor * (60 + 90.0 * count / Math.Max(1, target));

    /// <summary>
    /// The old clutch rule (an empty tank gets up to three, a full one a token
    /// single egg, a bloom three on top), times <see cref="ClutchFactor"/>. The
    /// fraction is rolled rather than rounded, so 1.4 eggs is one egg most of the
    /// time and two eggs four times in ten, and the average holds.
    /// </summary>
    public static int ClutchSize(int target, int count, bool bloom, Random rng)
    {
        int old = Math.Clamp(target + 1 - count, 1, 3) + (bloom ? 3 : 0);
        double scaled = old * ClutchFactor;
        int eggs = (int)Math.Floor(scaled);
        if (rng.NextDouble() < scaled - eggs) eggs++;
        return eggs;
    }

    /// <summary>
    /// What sex a drifter is. A tank of one sex (a lone survivor, or three males
    /// together) always gets the other sex, which keeps the old promise that a
    /// lone survivor eventually gets a mate. An empty or mixed tank rolls it.
    /// </summary>
    public static bool DrifterFemale(IReadOnlyList<bool> residents, Random rng) =>
        OneSex(residents) ? !residents[0] : RollFemale(rng);

    /// <summary>True when there is someone in the tank and they are all one sex.</summary>
    public static bool OneSex(IReadOnlyList<bool> residents)
    {
        if (residents.Count == 0) return false;
        for (int i = 1; i < residents.Count; i++)
            if (residents[i] != residents[0]) return false;
        return true;
    }

    /// <summary>
    /// How long a tank of two or more, all one sex, waits before a newcomer of the
    /// other sex drifts in: a few minutes, so a hatching or a stray can still fix
    /// it first. (Below two the old immigration floor already takes care of it.)
    /// </summary>
    public static double OneSexWaitMin { get; private set; } = DefaultOneSexWaitMin;
    public static double OneSexWaitMax { get; private set; } = DefaultOneSexWaitMax;

    public static double OneSexWait(Random rng) =>
        OneSexWaitMin + rng.NextDouble() * (OneSexWaitMax - OneSexWaitMin);

    /// <summary>For the population window: "3 mannetjes, 2 vrouwtjes".</summary>
    public static string Census(int males, int females) =>
        $"{males} {(males == 1 ? "mannetje" : "mannetjes")}, " +
        $"{females} {(females == 1 ? "vrouwtje" : "vrouwtjes")}";

    /// <summary>For the population window: eggs laid that have not hatched yet.</summary>
    public static string EggsLine(int eggs) => eggs switch
    {
        <= 0 => "Geen eieren onderweg",
        1 => "1 ei onderweg",
        _ => $"{eggs} eieren onderweg",
    };

    /// <summary>For the log and the graveyard: Dutch, like the rest of the log.</summary>
    public static string Word(bool female) => female ? "vrouwtje" : "mannetje";
}

/// <summary>The "Breeding" block of settings.json: null = keep the default.</summary>
public sealed class BreedingOverrides
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? CooldownFactor { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? ClutchFactor { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? OneSexWaitMin { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? OneSexWaitMax { get; set; }
}
