namespace CuttlefishPet.Core;

/// <summary>
/// Names for them. Dry ones -- the sort of name a man who fixes your boiler has --
/// because "Harry went to sleep on the taskbar" is a sentence you can say out loud,
/// and "#73 went to sleep on the taskbar" is not.
///
/// A name is not identity: the genome is. It is a handle, so that watching a tank
/// over a week is watching somebody rather than a row of numbers.
/// </summary>
public static class Names
{
    private static readonly string[] All =
    {
        "Harry", "Piet", "Klaas", "Henk", "Harm", "Sjaak", "Gerrit", "Dirk",
        "Wim", "Joop", "Cor", "Jan", "Kees", "Freek", "Arie", "Teun",
        "Bram", "Guus", "Herman", "Jos", "Bas", "Rien", "Wouter", "Evert",
        "Jaap", "Siem", "Hendrik", "Willem", "Bertus", "Manus", "Toon", "Dries",
        "Koos", "Nico", "Ton", "Rinus", "Gijs", "Egbert", "Roel", "Bennie",
        "Miep", "Truus", "Bep", "Ans", "Riet", "Toos", "Greet", "Nel",
        "Sien", "Fien", "Door", "Aaltje", "Ria", "Corrie", "Jannie", "Diny",
        "Willie", "Sjaan", "Bertha", "Geertje", "Annie", "Grietje", "Neeltje", "Lien",
        "Mien", "Stien", "Roos", "Wilma", "Jopie", "Rika", "Dirkje", "Maartje",
    };

    /// <summary>
    /// A name nobody in the tank is using. If they somehow all are, the newcomer
    /// gets a number after it, the way a third Henk in a village would.
    /// </summary>
    public static string Pick(Random rng, IReadOnlyCollection<string> taken)
    {
        var free = new List<string>();
        foreach (var name in All)
            if (!taken.Contains(name)) free.Add(name);
        if (free.Count > 0) return free[rng.Next(free.Count)];

        string basis = All[rng.Next(All.Length)];
        for (int n = 2; ; n++)
            if (!taken.Contains($"{basis} {n}")) return $"{basis} {n}";
    }
}
