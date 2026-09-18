using System.IO;
using System.Text.Json;

namespace CuttlefishPet.Core;

/// <summary>One animal as it goes to disk.</summary>
public sealed class SavedPet
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Genome Genome { get; set; }
    public string Born { get; set; } = "";
    public int Offspring { get; set; }
    public int Meals { get; set; }
    public double Age { get; set; }
    public double Lifespan { get; set; }
    public double BirthScale { get; set; }
    public double GrowUpSeconds { get; set; }
    public double Nourishment { get; set; }
    public double X { get; set; }
    public double Y { get; set; }

    public double Hunger { get; set; }
    public double Fatigue { get; set; }
    public double Loneliness { get; set; }
    public double Boredom { get; set; }
    public double Fear { get; set; }

    /// <summary>What each behaviour turned out to be worth to this animal.</summary>
    public Dictionary<string, double> Learned { get; set; } = new();

    /// <summary>What it makes of everyone it has met, by their id.</summary>
    public Dictionary<int, double> Bonds { get; set; } = new();
}

/// <summary>
/// The tank between sessions.
///
/// Everything the earlier layers build up -- a gene pool that has been through a
/// few generations, animals that have worked out what pays off on this desktop,
/// pairs that have taken to each other -- is worth nothing if it is thrown away
/// every time the app closes. Fifteen minutes of drift is a curiosity; a gene pool
/// that has been drifting for a fortnight is the point of the whole thing.
///
/// Ages are saved but no time passes while the app is shut: come back in the
/// morning to the tank you left, not to a tank where everyone died overnight. It
/// is the hours the app is actually running that shape the population, which is
/// the only time anything in it can be responding to you.
/// </summary>
public sealed class TankState
{
    /// <summary>Bumped when the shape changes in a way older files cannot fill.</summary>
    public int Version { get; set; } = 1;

    /// <summary>So a restored tank never hands out an id twice.</summary>
    public int NextId { get; set; }

    public string SavedAt { get; set; } = "";

    public List<SavedPet> Pets { get; set; } = new();

    private static string Path =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CuttlefishPet", "tank.json");

    /// <summary>Why the last <see cref="Load"/> gave up, if it did.</summary>
    public static string? LastLoadError { get; private set; }

    public static TankState? Load()
    {
        LastLoadError = null;
        try
        {
            if (!File.Exists(Path)) return null;
            var state = JsonSerializer.Deserialize<TankState>(File.ReadAllText(Path));
            if (state == null) { LastLoadError = "leeg bestand"; return null; }
            if (state.Version != 1) { LastLoadError = $"versie {state.Version}"; return null; }
            return state;
        }
        catch (Exception ex)
        {
            // A corrupt tank is still a fresh tank rather than a crash -- but it
            // says so now instead of looking like an empty first run.
            LastLoadError = Describe(ex);
            return null;
        }
    }

    /// <summary>
    /// Writes the tank out. Returns null when that worked and the reason when it
    /// did not -- never throws, and never fails quietly. A save that gives up
    /// without saying so is indistinguishable from one that never ran, and the
    /// whole point of this file is that it is still there tomorrow.
    /// </summary>
    public string? Save()
    {
        string tmp = Path + ".tmp";
        try
        {
            SavedAt = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            // Written beside the target and moved into place, so a machine that
            // goes down mid-write leaves the previous tank intact rather than half
            // a file that loses the lot.
            File.WriteAllText(tmp, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, Path, overwrite: true);
            return null;
        }
        catch (Exception ex)
        {
            // Do not leave a half-written temporary lying next to a good tank.
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return Describe(ex);
        }
    }

    private static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";
}
