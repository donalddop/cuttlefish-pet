using System.IO;
using System.Windows.Media;

namespace CuttlefishPet.Audio;

/// <summary>Fire-and-forget wav playback, throttled per sound. UI thread only.</summary>
public sealed class SoundService
{
    private readonly string _dir;
    private readonly Dictionary<string, DateTime> _lastPlayed = new();
    private readonly List<MediaPlayer> _live = new();

    public bool Muted { get; set; }

    public SoundService(string soundsDir) => _dir = soundsDir;

    public void Play(string name, double volume = 0.45)
    {
        if (Muted) return;
        var now = DateTime.UtcNow;
        if (_lastPlayed.TryGetValue(name, out var last) && (now - last).TotalMilliseconds < 400)
            return;
        _lastPlayed[name] = now;

        var path = Path.Combine(_dir, name + ".wav");
        if (!File.Exists(path)) return;

        var mp = new MediaPlayer { Volume = volume };
        mp.MediaEnded += (_, _) => { mp.Close(); _live.Remove(mp); };
        mp.MediaFailed += (_, _) => { mp.Close(); _live.Remove(mp); };
        mp.Open(new Uri(path));
        mp.Play();
        _live.Add(mp); // keep alive until finished
    }
}
