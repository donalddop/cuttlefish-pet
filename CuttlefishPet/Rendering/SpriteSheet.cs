using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CuttlefishPet.Rendering;

/// <summary>
/// Every chromatophore variant of one action, worked out the first time it is asked
/// for rather than all up front. The strips are drawn at twice their logical size
/// now, and fourteen palettes of every frame of every action came to a couple of
/// hundred megabytes -- for colours most animals never wear. A palette row is a few
/// milliseconds of pixel work, once, the first time something turns that colour.
/// </summary>
public sealed class PaletteFrames
{
    private readonly BitmapSource[] _art;
    private readonly BitmapSource[]?[] _rows;

    public PaletteFrames(BitmapSource[] art)
    {
        _art = art;
        _rows = new BitmapSource[]?[Rendering.Palettes.All.Length];
        _rows[0] = art;
    }

    public int Length => _rows.Length;

    public BitmapSource[] this[int palette]
    {
        get
        {
            if ((uint)palette >= (uint)_rows.Length) palette = 0;
            var row = Volatile.Read(ref _rows[palette]);
            if (row != null) return row;
            row = new BitmapSource[_art.Length];
            for (int i = 0; i < _art.Length; i++)
                row[i] = Rendering.Palettes.Apply(_art[i], Rendering.Palettes.All[palette]);
            // The warm-up thread may have got there first; whichever row lands
            // first is the one everybody uses. Both are frozen, so either is fine.
            return Interlocked.CompareExchange(ref _rows[palette], row, null) ?? row;
        }
    }

    /// <summary>Work a palette out now, off the UI thread, so nobody waits for it later.</summary>
    public void Warm(int palette) => _ = this[palette];
}

public sealed class SpriteAnim
{
    public required string Name { get; init; }
    /// <summary>[palette][frame]. Index 0 is the art as drawn.</summary>
    public required PaletteFrames Palettes { get; init; }
    public BitmapSource[] Frames => Palettes[0];
    /// <summary>
    /// Bitmap pixels per logical frame pixel. FrameW/H, Anchor and the eye are all in
    /// logical pixels, so only code that reads the bitmap itself needs to know this.
    /// </summary>
    public int Res { get; init; } = 1;
    public required double Fps { get; init; }
    public required bool Loop { get; init; }
    /// <summary>Foot/contact point within a frame, in frame pixels.</summary>
    public required Point Anchor { get; init; }
    public required int FrameW { get; init; }
    public required int FrameH { get; init; }
    /// <summary>Drawn size relative to the source frame; props set their own.</summary>
    public double Scale { get; init; } = 1;
    /// <summary>Eye centre in frame pixels; null when this action draws its own eye.</summary>
    public Point? EyeCenter { get; init; }
    public double EyeRadius { get; init; }
    /// <summary>Set when the arms are drawn live rather than painted into the frames.</summary>
    public ArmRig? Arms { get; init; }
    public double Duration => Frames.Length / Fps;

    private double[]? _backTop;

    /// <summary>
    /// Top of the body straight above the anchor in this frame, in frame px: the first
    /// solid row in a narrow column around the contact point. The frame itself has empty
    /// space above the mantle, so its top edge is not where anything could sit. Measured
    /// once per frame, on first use (Desktop Dude asks while riding).
    /// </summary>
    public double BackTop(int frame)
    {
        if (_backTop == null)
        {
            _backTop = new double[Frames.Length];
            Array.Fill(_backTop, double.NaN);
        }
        frame = Math.Clamp(frame, 0, Frames.Length - 1);
        if (double.IsNaN(_backTop[frame])) _backTop[frame] = MeasureTop(Frames[frame]);
        return _backTop[frame];
    }

    private double MeasureTop(BitmapSource frame)
    {
        var src = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        src.CopyPixels(px, stride, 0);
        int half = Math.Max(2, (int)(w * 0.12));
        int ax = (int)(Anchor.X * Res);
        int x0 = Math.Clamp(ax - half, 0, w - 1);
        int x1 = Math.Clamp(ax + half, 0, w - 1);
        for (int y = 0; y < h; y++)
            for (int x = x0; x <= x1; x++)
                if (px[y * stride + x * 4 + 3] > 140) return (double)y / Res;
        return Anchor.Y;
    }
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
            int res = m.TryGetProperty("res", out var r) ? Math.Max(1, r.GetInt32()) : 1;

            var strip = new BitmapImage();
            strip.BeginInit();
            strip.UriSource = new Uri(Path.Combine(spritesDir, m.GetProperty("file").GetString()!));
            strip.CacheOption = BitmapCacheOption.OnLoad;
            strip.EndInit();
            strip.Freeze();

            var frames = new BitmapSource[count];
            for (int i = 0; i < count; i++)
            {
                var f = new CroppedBitmap(strip, new Int32Rect(i * fw * res, 0, fw * res, fh * res));
                f.Freeze();
                frames[i] = f;
            }

            var palettes = new PaletteFrames(frames);

            var anchor = m.GetProperty("anchor");
            Point? eyeCenter = null;
            double eyeRadius = 0;
            if (m.TryGetProperty("eye", out var eye))
            {
                eyeCenter = new Point(eye[0].GetDouble(), eye[1].GetDouble());
                eyeRadius = eye[2].GetDouble();
            }

            ArmRig? arms = null;
            if (m.TryGetProperty("arms", out var ar))
            {
                var roots = ar.GetProperty("roots").EnumerateArray()
                    .Select(p => new Point(p[0].GetDouble(), p[1].GetDouble())).ToArray();
                var angles = ar.GetProperty("angles").EnumerateArray()
                    .Select(a => a.GetDouble()).ToArray();
                var wave = ar.GetProperty("wave");
                var width = ar.GetProperty("width");
                if (roots.Length > 0 && roots.Length == angles.Length)
                    arms = new ArmRig
                    {
                        Roots = roots,
                        Angles = angles,
                        Length = ar.GetProperty("len").GetDouble(),
                        Curl = ar.GetProperty("curl").GetDouble(),
                        WaveAmp = wave[0].GetDouble(),
                        WaveHz = wave[1].GetDouble(),
                        RootWidth = width[0].GetDouble(),
                        TipWidth = width[1].GetDouble(),
                    };
            }

            result[prop.Name] = new SpriteAnim
            {
                Name = prop.Name,
                Arms = arms,
                Palettes = palettes,
                Res = res,
                Fps = m.GetProperty("fps").GetDouble(),
                Loop = m.GetProperty("loop").GetBoolean(),
                Anchor = new Point(anchor[0].GetDouble(), anchor[1].GetDouble()),
                Scale = m.TryGetProperty("scale", out var sc) ? sc.GetDouble() : 1,
                FrameW = fw,
                FrameH = fh,
                EyeCenter = eyeCenter,
                EyeRadius = eyeRadius,
            };
        }

        // Glass is what a resting cuttlefish wears most of the time, so it is worth
        // having ready before anybody asks: otherwise the first animal to go pale
        // pays twenty-odd milliseconds of pixel work in the middle of a frame.
        var all = result.Values.ToArray();
        Task.Run(() =>
        {
            foreach (var a in all)
            {
                try { a.Palettes.Warm(Rendering.Palettes.Glass); }
                catch { /* a warm-up that fails just means it happens on demand */ }
            }
        });
        return result;
    }
}

public sealed class AnimationPlayer
{
    private readonly Dictionary<string, SpriteAnim> _library;
    private double _t;

    public AnimationPlayer(Dictionary<string, SpriteAnim> library) => _library = library;

    public SpriteAnim Current { get; private set; } = null!;

    /// <summary>
    /// Set once at startup so a missing animation can say so. A hook rather than
    /// a reference to the logger, to keep this class clear of the rest of the app.
    /// </summary>
    public static Action<string>? Report;

    private static readonly HashSet<string> _missing = new();

    public void Play(string name, bool restart = false)
    {
        if (!_library.TryGetValue(name, out var anim))
        {
            // Asking for an animation that does not exist used to bring the whole
            // app down mid-frame, taking the tank with it as far as the last save.
            // Behaviour names and animation names are two different sets that look
            // alike -- "dart" is a behaviour, the animation is "jump" -- so the
            // mistake is easy to make and invisible until something triggers it.
            // A desktop toy has no business dying over a typo.
            if (_missing.Add(name))
                Report?.Invoke($"animatie '{name}' bestaat niet -- idle gebruikt");
            if (!_library.TryGetValue("idle", out anim)) return;
        }
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
