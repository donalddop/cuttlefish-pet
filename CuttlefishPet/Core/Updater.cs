using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;

namespace CuttlefishPet.Core;

/// <summary>
/// Checks GitHub for a newer release a minute after start and every six hours after that.
/// An installed copy downloads CuttlefishPet-setup.exe, checks it against the SHA-256 GitHub
/// publishes, runs it silently and closes (the tank is saved on exit); the installer starts
/// the new version. A portable copy only gets a balloon with a link. The setting lives in
/// its own file so it never races the tank's settings.json.
/// </summary>
public sealed class Updater
{
    private const string Repo = "donalddop/cuttlefish-pet";

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CuttlefishPet", "update.json");

    private readonly Action<string> _notify;
    private readonly Action _quit;
    private readonly DispatcherTimer _timer;
    private bool _busy, _auto = true;

    public Version Current { get; }
    public bool Installed { get; }
    public string? AvailableUrl { get; private set; }

    public bool Auto
    {
        get => _auto;
        set
        {
            _auto = value;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { auto = value }));
            }
            catch { /* not worth bothering anyone about */ }
        }
    }

    public Updater(Dispatcher ui, Action<string> notify, Action quit)
    {
        _notify = notify;
        _quit = quit;
        // Test hook: pretend to be an older installed copy.
        string? fake = Environment.GetEnvironmentVariable("CUTTLEFISHPET_FAKE_VERSION");
        Current = Normalize(fake != null && Version.TryParse(fake, out var fv)
            ? fv
            : Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0));
        Installed = fake != null ||
                    AppContext.BaseDirectory.Contains(@"\Programs\CuttlefishPet", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(SettingsPath))
                _auto = JsonDocument.Parse(File.ReadAllText(SettingsPath)).RootElement.GetProperty("auto").GetBoolean();
        }
        catch { _auto = true; }

        _timer = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = TimeSpan.FromHours(6);
            if (_auto) await CheckAsync(manual: false);
        };
        _timer.Start();
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    public async Task CheckAsync(bool manual)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CuttlefishPet-updater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var doc = JsonDocument.Parse(
                await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
            var rel = doc.RootElement;
            string tag = rel.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) || Normalize(latest) <= Current)
            {
                if (manual) _notify($"Je hebt de nieuwste versie ({Current.ToString(2)}).");
                return;
            }

            JsonElement? setup = null;
            foreach (var a in rel.GetProperty("assets").EnumerateArray())
                if ((a.GetProperty("name").GetString() ?? "").EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase))
                    setup = a;
            if (setup is not { } asset) return;

            if (!Installed)
            {
                AvailableUrl = rel.GetProperty("html_url").GetString();
                _notify($"Versie {tag} staat klaar. Klik hier om hem te downloaden.");
                return;
            }

            byte[] bytes = await http.GetByteArrayAsync(asset.GetProperty("browser_download_url").GetString());
            string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (asset.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String &&
                dg.GetString() is string digest && digest.StartsWith("sha256:") &&
                !digest[7..].Equals(sha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("checksum mismatch");

            string path = Path.Combine(Path.GetTempPath(), $"CuttlefishPet-{tag}-setup.exe");
            await File.WriteAllBytesAsync(path, bytes);
            _notify($"Updaten naar {tag}... de zeekatten zijn zo terug.");
            Process.Start(new ProcessStartInfo(path, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
            var bye = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            bye.Tick += (_, _) => { bye.Stop(); _quit(); };
            bye.Start();
        }
        catch
        {
            if (manual) _notify("Update-check mislukt, ik probeer het later nog eens.");
        }
        finally
        {
            _busy = false;
        }
    }
}
