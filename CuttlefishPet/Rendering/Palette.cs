using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CuttlefishPet.Rendering;

/// <summary>
/// A chromatophore state: the base art is drawn in one warm hue family, so rotating
/// hue (plus a saturation/brightness nudge) recolours every pixel coherently —
/// anti-aliased edges included — instead of swapping a handful of flat colours.
/// </summary>
public readonly record struct Palette(string Name, double HueShift, double Sat, double Val,
                                      double Weight = 1)
{
    public bool IsIdentity => HueShift == 0 && Sat == 1 && Val == 1;
}

public static class Palettes
{
    /// <summary>
    /// Index 0 is the untouched art. The rest lean the way real cuttlefish do —
    /// purples, blues, greens and pearly whites — with the hot colours kept rare so
    /// they read as alarm rather than everyday wear.
    /// </summary>
    public static readonly Palette[] All =
    {
        new("sand",     0,   1.00, 1.00, 0.6),   // resting camouflage tan
        new("pearl",    322, 0.42, 1.04, 1.6),   // iridescent white-pink
        new("opal",     250, 0.50, 1.00, 1.5),   // pale blue-violet
        new("violet",   236, 0.85, 0.98, 1.6),   // purple display
        new("plum",     264, 0.90, 0.72, 1.2),   // deep purple
        new("indigo",   214, 0.85, 0.88, 1.3),   // dusky blue
        new("azure",    190, 0.95, 1.02, 1.5),   // bright reef blue
        new("teal",     158, 1.00, 0.95, 1.6),   // iridescent blue-green
        new("emerald",  120, 0.95, 0.92, 1.4),   // weedy green
        new("moss",     96,  0.80, 0.68, 1.0),   // dark green
        new("ink",      212, 0.75, 0.42, 0.7),   // deep blue-black threat display
        new("coral",    -20, 1.30, 1.00, 0.4),   // hot orange-red alarm
        new("crimson",  -38, 1.15, 0.78, 0.4),   // deep blood red
    };

    public static int IndexOf(string name)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i].Name == name) return i;
        return 0;
    }

    /// <summary>Weighted pick, so the cool iridescent tones dominate.</summary>
    public static int PickRandom(Random rng)
    {
        double total = 0;
        foreach (var p in All) total += p.Weight;
        double roll = rng.NextDouble() * total;
        for (int i = 0; i < All.Length; i++)
        {
            roll -= All[i].Weight;
            if (roll <= 0) return i;
        }
        return 0;
    }

    /// <summary>Recolour one frame. Alpha is preserved exactly.</summary>
    public static BitmapSource Apply(BitmapSource src, Palette p)
    {
        if (p.IsIdentity) return src;

        var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
        var px = new byte[h * stride];
        conv.CopyPixels(px, stride, 0);

        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] == 0) continue;
            Shift(ref px[i + 2], ref px[i + 1], ref px[i], p);
        }

        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), px, stride, 0);
        wb.Freeze();
        return wb;
    }

    private static void Shift(ref byte rb, ref byte gb, ref byte bb, Palette p)
    {
        double r = rb / 255.0, g = gb / 255.0, b = bb / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double v = max, delta = max - min;
        double s = max <= 0 ? 0 : delta / max;

        double hue;
        if (delta <= 1e-6) hue = 0;
        else if (max == r) hue = 60 * (((g - b) / delta) % 6);
        else if (max == g) hue = 60 * (((b - r) / delta) + 2);
        else hue = 60 * (((r - g) / delta) + 4);

        hue = (hue + p.HueShift) % 360;
        if (hue < 0) hue += 360;
        s = Math.Clamp(s * p.Sat, 0, 1);
        v = Math.Clamp(v * p.Val, 0, 1);

        double c = v * s, x = c * (1 - Math.Abs((hue / 60) % 2 - 1)), m = v - c;
        (double r2, double g2, double b2) = (hue / 60) switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        rb = (byte)Math.Round((r2 + m) * 255);
        gb = (byte)Math.Round((g2 + m) * 255);
        bb = (byte)Math.Round((b2 + m) * 255);
    }
}
