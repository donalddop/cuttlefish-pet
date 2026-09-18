using System.IO;
using System.Text.Json;

namespace CuttlefishPet.Core;

/// <summary>One animal, as it was when it stopped being one.</summary>
public sealed class Epitaph
{
    public string Name { get; set; } = "";
    public int Id { get; set; }
    public Genome Genome { get; set; }

    /// <summary>Why it left: ouderdom, gevecht, weggezwommen, weggehaald.</summary>
    public string Fate { get; set; } = "onbekend";

    public string Born { get; set; } = "";
    public string Died { get; set; } = "";

    /// <summary>Seconds lived, against the seconds it was given.</summary>
    public double Age { get; set; }
    public double Lifespan { get; set; }

    /// <summary>How big it got, which is how well it ate.</summary>
    public double Size { get; set; }
    public int Meals { get; set; }

    /// <summary>Eggs it laid. The only number here that selection actually reads.</summary>
    public int Offspring { get; set; }

    /// <summary>What it had concluded about its own repertoire by the end.</summary>
    public Dictionary<string, double> Learned { get; set; } = new();
}

/// <summary>
/// Every animal that has been through the tank, one JSON object per line, appended
/// and never rewritten.
///
/// The point is not sentiment. A living tank only ever shows you the survivors, so
/// you cannot see selection happening by looking at it -- the animals that did badly
/// are precisely the ones that are not there. This is the other half of the record:
/// what each one was born with, how long it lasted, how well it ate and how many
/// eggs it left. Line it up over a few hundred deaths and the traits that keep
/// paying off on this particular desktop are visible as a drift in the averages.
///
/// A line per death, so it can be read by anything, appended to cheaply, and
/// truncated by hand without ceremony.
/// </summary>
public static class Graveyard
{
    private static string Path =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CuttlefishPet", "graveyard.jsonl");

    /// <summary>Returns null when it was written, and the reason when it was not.</summary>
    public static string? Record(Epitaph who)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, JsonSerializer.Serialize(who) + Environment.NewLine);
            return null;
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
