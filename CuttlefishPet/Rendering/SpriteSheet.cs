using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CuttlefishPet.Rendering;

public sealed class SpriteAnim
{
    public required string Name { get; init; }
    /// <summary>[palette][frame]. Index 0 is the art as drawn.</summary>
    public required BitmapSource[][] Palettes { get; init; }
    public BitmapSource[] Frames => Palettes[0];
    public required double Fps { get; init; }
    public required bool Loop { get; init; }
    /// <summary>Foot/contact point within a frame, in frame pixels.</summary>
    public required Point Anchor { get; init; }
    public required int FrameW { get; init; }
    public required int FrameH { get; init; }
    /// <summary>Eye centre in frame pixels; null when this action draws its own eye.</summary>
    public Point? EyeCenter { get; init; }
    public double EyeRadius { get; init; }
    public double Duration => Frames.Length / Fps;
}

/// <summary>Skin textures laid over the body: patterns plus the iridescent sheen.</summary>
public sealed class SkinLibrary
{
    public required BitmapSource[] Patterns { get; init; }
    public required BitmapSource Sheen { get; init; }

    private static readonly string[] PatternFiles =
        { "skin_spots", "skin_mottle", "skin_bands", "skin_reticulate", "skin_flecks" };

    public static SkinLibrary Load(string spritesDir)
    {
        static BitmapSource Read(string path)
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(path);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }

        return new SkinLibrary
        {
            Patterns = PatternFiles
                .Select(f => Read(Path.Combine(spritesDir, f + ".png")))
                .ToArray(),
            Sheen = Read(Path.Combine(spritesDir, "skin_sheen.png")),
        };
    }
}

public static class SpriteLibrary
{
    public static Dictionary<string, SpriteAnim> Load(string spritesDir)
    {
        var json = File.ReadAllText(Path.Combine(spritesDir, "animations.json"));
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, SpriteAnim>();

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var m = prop.Value;
            int fw = m.GetProperty("frameW").GetInt32();
            int fh = m.GetProperty("frameH").GetInt32();
            int count = m.GetProperty("frames").GetInt32();

            var strip = new BitmapImage();
            strip.BeginInit();
            strip.UriSource = new Uri(Path.Combine(spritesDir, m.GetProperty("file").GetString()!));
            strip.CacheOption = BitmapCacheOption.OnLoad;
            strip.EndInit();
            strip.Freeze();

            var frames = new BitmapSource[count];
            for (int i = 0; i < count; i++)
            {
                var f = new CroppedBitmap(strip, new Int32Rect(i * fw, 0, fw, fh));
                f.Freeze();
                frames[i] = f;
            }

            // Every chromatophore variant of this action, built once up front.
            var palettes = new BitmapSource[Rendering.Palettes.All.Length][];
            palettes[0] = frames;
            for (int p = 1; p < palettes.Length; p++)
            {
                var variant = new BitmapSource[count];
                for (int i = 0; i < count; i++)
                    variant[i] = Rendering.Palettes.Apply(frames[i], Rendering.Palettes.All[p]);
                palettes[p] = variant;
            }

            var anchor = m.GetProperty("anchor");
            Point? eyeCenter = null;
            double eyeRadius = 0;
            if (m.TryGetProperty("eye", out var eye))
            {
                eyeCenter = new Point(eye[0].GetDouble(), eye[1].GetDouble());
                eyeRadius = eye[2].GetDouble();
            }

            result[prop.Name] = new SpriteAnim
            {
                Name = prop.Name,
                Palettes = palettes,
                Fps = m.GetProperty("fps").GetDouble(),
                Loop = m.GetProperty("loop").GetBoolean(),
                Anchor = new Point(anchor[0].GetDouble(), anchor[1].GetDouble()),
                FrameW = fw,
                FrameH = fh,
                EyeCenter = eyeCenter,
                EyeRadius = eyeRadius,
            };
        }
        return result;
    }
}

public sealed class AnimationPlayer
{
    private readonly Dictionary<string, SpriteAnim> _library;
    private double _t;

    public AnimationPlayer(Dictionary<string, SpriteAnim> library) => _library = library;

    public SpriteAnim Current { get; private set; } = null!;

    public void Play(string name, bool restart = false)
    {
        var anim = _library[name];
        if (!restart && ReferenceEquals(anim, Current)) return;
        Current = anim;
        _t = 0;
    }

    public void Tick(double dt) => _t += dt;

    /// <summary>Scrub the clock directly — used to play an action in reverse.</summary>
    public void SetTime(double seconds) => _t = Math.Max(0, seconds);

    public bool Finished => Current is { Loop: false } && _t >= Current.Duration;

    public int FrameIndex
    {
        get
        {
            int i = (int)(_t * Current.Fps);
            return Current.Loop ? i % Current.Frames.Length : Math.Min(i, Current.Frames.Length - 1);
        }
    }

    public BitmapSource Frame => Current.Frames[FrameIndex];
}
