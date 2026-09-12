using CuttlefishPet.Rendering;

namespace CuttlefishPet.Core;

/// <summary>
/// What a hatchling is handed by the animals that made it: the traits it is born
/// with, and a diluted head start on what its mother had worked out about this
/// particular desktop.
///
/// The second half is what turns learning into something the tank accumulates. An
/// animal lives half an hour and barely gets a dozen goes at any one behaviour --
/// far too few to form much of an opinion. A lineage lives as long as you keep the
/// app around, and opinions that keep being confirmed survive the animals holding
/// them.
/// </summary>
public sealed record Heritage(Genome Genome, IReadOnlyDictionary<string, double> Lore);

/// <summary>
/// The handful of numbers that make one cuttlefish not another. Fixed at hatching,
/// handed to the next generation with a little drift, and never touched again for
/// the rest of a life — which is what lets a tank drift toward whatever this
/// particular desktop happens to reward.
/// </summary>
public readonly record struct Genome(
    /// <summary>How close to the cursor it will work, and whether it escalates a food contest.</summary>
    double Boldness,
    /// <summary>Pull toward company: schooling and piling versus foraging alone.</summary>
    double Sociability,
    /// <summary>Appetite for anything new — fresh windows, bubbles, props.</summary>
    double Curiosity,
    /// <summary>How fast it burns a meal, and so how often it has to find one.</summary>
    double Metabolism,
    /// <summary>How quickly it tires of doing the same thing.</summary>
    double Restlessness,
    /// <summary>Resting colour, an index into <see cref="Palettes.All"/>.</summary>
    int Chroma,
    /// <summary>Resting skin pattern, 0..4.</summary>
    int Pattern)
{
    /// <summary>A founder: no parents to take after, so roll the lot.</summary>
    public static Genome Random(Random rng) => new(
        Middling(rng), Middling(rng), Middling(rng),
        0.72 + rng.NextDouble() * 0.66,
        Middling(rng),
        Palettes.PickRandom(rng),
        rng.Next(5));

    /// <summary>
    /// A hatchling is the average of its parents plus a nudge, so a brood takes
    /// after the pair that made it without being a copy of either. Colour follows
    /// one parent outright rather than blending — a mix of teal and coral is mud,
    /// and you want to be able to see the family.
    /// </summary>
    public static Genome Inherit(Genome a, Genome b, Random rng) => new(
        Mix(a.Boldness, b.Boldness, rng),
        Mix(a.Sociability, b.Sociability, rng),
        Mix(a.Curiosity, b.Curiosity, rng),
        Math.Clamp((a.Metabolism + b.Metabolism) / 2 + Drift(rng), 0.72, 1.38),
        Mix(a.Restlessness, b.Restlessness, rng),
        rng.NextDouble() < 0.07 ? Palettes.PickRandom(rng)          // the odd sport
                                : (rng.NextDouble() < 0.5 ? a.Chroma : b.Chroma),
        rng.NextDouble() < 0.5 ? a.Pattern : b.Pattern);

    /// <summary>Two rolls averaged: extremes stay rare, so a bold one means something.</summary>
    private static double Middling(Random rng) => (rng.NextDouble() + rng.NextDouble()) / 2;

    private static double Mix(double x, double y, Random rng) =>
        Math.Clamp((x + y) / 2 + Drift(rng), 0, 1);

    /// <summary>Mutation, near enough. Small, symmetric, occasionally worth something.</summary>
    private static double Drift(Random rng) => (rng.NextDouble() - rng.NextDouble()) * 0.13;
}
