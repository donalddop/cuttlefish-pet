using System.IO;
using System.Text.Json;

namespace CuttlefishPet.Core;

/// <summary>
/// The handful of things worth letting someone change while the app is running.
/// Kept in LocalAppData rather than next to the exe, so it survives a reinstall
/// and works whatever folder the app was dropped into.
/// </summary>
public sealed class Settings
{
    public const int MinPopulation = 1;
    /// <summary>
    /// Measured, not guessed: a hundred animals cost most of a CPU core and a
    /// quarter of a gigabyte, which is not what a thing that lives on your desktop
    /// all day should be spending. Thirty is busy, still runs cheap, and is well
    /// past the point where the tank behaves like a crowd rather than a handful.
    /// </summary>
    public const int MaxPopulation = 30;

    /// <summary>
    /// How full the tank should feel. Everything density-dependent is measured
    /// against this: crowding, clutch size, how readily two of them court, and the
    /// ceiling a swarm can reach. It is a resting level, not a quota — the tank
    /// still swings well above and below it.
    /// </summary>
    public int TargetPopulation { get; set; } = 5;


    private static string Path =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CuttlefishPet", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path)) ?? new Settings();
        }
        catch { /* a corrupt file is not worth failing to start over */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(this));
        }
        catch { /* read-only profile: keep running with it in memory */ }
    }
}
