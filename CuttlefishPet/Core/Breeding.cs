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
/// console checks in Tools/DriveTests can hold it to account.
/// </summary>
public static class Breeding
{
    /// <summary>How much more often courtship comes round than before sexes.</summary>
    public const double CooldownFactor = 0.7;

    /// <summary>How much bigger a clutch is, on average, than before sexes.</summary>
    public const double ClutchFactor = 1.4;

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
    /// What sex a drifter is. A lone survivor always gets the other sex, which
    /// keeps the old promise that a lone survivor eventually gets a mate.
    /// </summary>
    public static bool DrifterFemale(IReadOnlyList<bool> residents, Random rng) =>
        residents.Count == 1 ? !residents[0] : RollFemale(rng);

    /// <summary>For the log and the graveyard: Dutch, like the rest of the log.</summary>
    public static string Word(bool female) => female ? "vrouwtje" : "mannetje";
}
