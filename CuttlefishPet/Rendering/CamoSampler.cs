using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CuttlefishPet.Interop;

namespace CuttlefishPet.Rendering;

/// <summary>The skin a pet has worked out from its surroundings.</summary>
public sealed class CamoSkin
{
    /// <summary>Coarse pattern in the sampled colours, painted over the body.</summary>
    public required BitmapSource Texture { get; init; }
    /// <summary>The colour the surroundings mostly are.</summary>
    public required Color Dominant { get; init; }
    /// <summary>0 = flat background, 1 = busy. Drives how strongly to show pattern.</summary>
    public required double Busyness { get; init; }
}

/// <summary>
/// Reads the desktop behind a pet and boils it down to a handful of colours.
/// A cuttlefish does not copy its background pixel for pixel — it picks a few
/// chromatophore colours and lays down a pattern with roughly the right grain, which
/// is exactly what clustering a heavily downscaled screen grab gives you.
/// </summary>
public static class CamoSampler
{
    private const int GridW = 11;   // the grain of the pattern: coarse on purpose
    private const int GridH = 8;
    private const int Colours = 3;

    public static CamoSkin? Sample(Rect physRect)
    {
        int w = (int)physRect.Width, h = (int)physRect.Height;
        if (w < 8 || h < 8) return null;

        try
        {
            using var grab = new System.Drawing.Bitmap(w, h);
            using (var g = System.Drawing.Graphics.FromImage(grab))
                g.CopyFromScreen((int)physRect.X, (int)physRect.Y, 0, 0, grab.Size);

            // Downscaling this hard is the blur: fine detail averages into blobs.
            // Plain bilinear is plenty — the result gets crushed to three colours
            // anyway, and the high-quality resampler costs several milliseconds.
            using var small = new System.Drawing.Bitmap(GridW, GridH);
            using (var g = System.Drawing.Graphics.FromImage(small))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(grab, 0, 0, GridW, GridH);
            }

            // One LockBits beats GetPixel, which locks and unlocks on every call.
            var px = new (double R, double G, double B)[GridW * GridH];
            var data = small.LockBits(new System.Drawing.Rectangle(0, 0, GridW, GridH),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                var raw = new byte[data.Stride * GridH];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                for (int y = 0; y < GridH; y++)
                    for (int x = 0; x < GridW; x++)
                    {
                        int o = y * data.Stride + x * 4;
                        px[y * GridW + x] = (raw[o + 2], raw[o + 1], raw[o]);
                    }
            }
            finally
            {
                small.UnlockBits(data);
            }

            var (centroids, counts, assignment) = Cluster(px);

            // Build the pattern: every cell snapped to its nearest sampled colour.
            var bytes = new byte[GridW * GridH * 4];
            for (int i = 0; i < px.Length; i++)
            {
                var c = centroids[assignment[i]];
                bytes[i * 4 + 0] = (byte)Math.Clamp(c.B, 0, 255);
                bytes[i * 4 + 1] = (byte)Math.Clamp(c.G, 0, 255);
                bytes[i * 4 + 2] = (byte)Math.Clamp(c.R, 0, 255);
                bytes[i * 4 + 3] = 255;
            }
            var texture = BitmapSource.Create(GridW, GridH, 96, 96, PixelFormats.Bgra32,
                                              null, bytes, GridW * 4);
            texture.Freeze();

            int biggest = 0;
            for (int i = 1; i < Colours; i++) if (counts[i] > counts[biggest]) biggest = i;
            var dom = centroids[biggest];

            return new CamoSkin
            {
                Texture = texture,
                Dominant = Color.FromRgb((byte)Math.Clamp(dom.R, 0, 255),
                                         (byte)Math.Clamp(dom.G, 0, 255),
                                         (byte)Math.Clamp(dom.B, 0, 255)),
                Busyness = Spread(centroids, counts),
            };
        }
        catch
        {
            return null;   // capture is best-effort; the pet just keeps its old skin
        }
    }

    /// <summary>Tiny k-means over a few dozen pixels — cheap enough to run per pet.</summary>
    private static ((double R, double G, double B)[] centroids, int[] counts, int[] assignment)
        Cluster((double R, double G, double B)[] px)
    {
        var centroids = new (double R, double G, double B)[Colours];
        // Seed from the darkest, middling and lightest cells so the clusters spread
        // over the range instead of all landing on the same average.
        var byLuma = px.OrderBy(p => p.R * 0.3 + p.G * 0.6 + p.B * 0.1).ToArray();
        centroids[0] = byLuma[0];
        centroids[1] = byLuma[byLuma.Length / 2];
        centroids[2] = byLuma[^1];

        var assignment = new int[px.Length];
        var counts = new int[Colours];

        for (int iter = 0; iter < 6; iter++)
        {
            Array.Clear(counts);
            var sums = new (double R, double G, double B)[Colours];

            for (int i = 0; i < px.Length; i++)
            {
                int best = 0;
                double bestD = double.MaxValue;
                for (int k = 0; k < Colours; k++)
                {
                    double d = Sq(px[i].R - centroids[k].R) + Sq(px[i].G - centroids[k].G)
                             + Sq(px[i].B - centroids[k].B);
                    if (d < bestD) { bestD = d; best = k; }
                }
                assignment[i] = best;
                counts[best]++;
                sums[best] = (sums[best].R + px[i].R, sums[best].G + px[i].G, sums[best].B + px[i].B);
            }

            for (int k = 0; k < Colours; k++)
                if (counts[k] > 0)
                    centroids[k] = (sums[k].R / counts[k], sums[k].G / counts[k], sums[k].B / counts[k]);
        }
        return (centroids, counts, assignment);
    }

    /// <summary>How far apart the sampled colours are, normalised to roughly 0..1.</summary>
    private static double Spread((double R, double G, double B)[] c, int[] counts)
    {
        double max = 0;
        for (int i = 0; i < c.Length; i++)
            for (int j = i + 1; j < c.Length; j++)
            {
                if (counts[i] == 0 || counts[j] == 0) continue;
                double d = Math.Sqrt(Sq(c[i].R - c[j].R) + Sq(c[i].G - c[j].G) + Sq(c[i].B - c[j].B));
                max = Math.Max(max, d);
            }
        return Math.Clamp(max / 190.0, 0, 1);
    }

    private static double Sq(double v) => v * v;
}
