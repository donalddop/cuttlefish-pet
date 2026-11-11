using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CuttlefishPet.Rendering;

/// <summary>
/// How the water is painted, with no dependency on the overlay or on Win32, so
/// Tools/WaterPreview can draw the real thing off-screen rather than its own
/// lookalike. A preview you cannot trust is worse than no preview.
/// </summary>
public static class WaterLook
{
    /// <summary>
    /// One mote: a pale core inside a soft dark halo.
    ///
    /// The halo is not decoration, it is the only reason the water exists at all on
    /// this desktop. A plain pale speck is what marine snow looks like in a dark
    /// tank, and that is what was built first -- rendered against a page of text it
    /// vanished completely, and against a bright game it may as well not have been
    /// drawn. A real mote is a lump of matter between you and the light, so it is
    /// dark against anything brighter than itself; giving it both means the core
    /// carries it on a dark background and the halo carries it on a light one,
    /// out of one brush and one ellipse.
    ///
    /// Soft-edged throughout: a hard-edged dot at this size reads as a dead pixel
    /// or a bit of dust on the monitor, which is exactly the wrong illusion.
    /// Frozen, and shared by every mote on screen.
    /// </summary>
    public static Brush Flake()
    {
        var g = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(255, 236, 246, 250), 0),
                new GradientStop(Color.FromArgb(225, 206, 226, 234), 0.30),
                new GradientStop(Color.FromArgb(120, 70, 96, 110), 0.58),
                new GradientStop(Color.FromArgb(55, 24, 40, 50), 0.80),
                new GradientStop(Color.FromArgb(0, 18, 32, 42), 1),
            },
        };
        g.Freeze();
        return g;
    }

    /// <summary>
    /// Peak alpha of a shaft's bright core, and of the shade beside it. The shade
    /// is nearly as strong as the beam on purpose: the beam only pays off on a
    /// dark background, and this screen is a white document most of the day.
    ///
    /// Set by looking, twice. At 0.105/0.085 the preview on deep water was lovely
    /// and the same light over a real screen -- a game at dusk, mid grey-blue, the
    /// kind of thing that is actually behind the tank -- was one faint band and
    /// nothing else. That is the mistake the dust already made once: an effect
    /// that only exists on the backgrounds you chose for the preview does not
    /// exist. Up 40%, which is the most the shade can take before it reads as a
    /// smear on a page of text rather than as water.
    ///
    /// And these are the peaks: they are reached only at the very top of a shaft
    /// and only while that shaft happens to be breathing at full strength, so what
    /// crosses a document is usually half of this.
    /// </summary>
    public const double ShaftCoreAlpha = 0.147, ShaftFlankAlpha = 0.118;

    /// <summary>
    /// One light shaft, as a tiny bitmap stretched over the whole band: bright in
    /// the middle, a touch of shade down either side, and fading to nothing at the
    /// bottom.
    ///
    /// The shade beside the core is the mote's dark halo again, for exactly the same
    /// reason and learned the same expensive way. A pale wash on white is not a
    /// faint effect, it is no effect, and Donald's screen is a white page most of
    /// the day. But a bright shaft in water is only bright *because the water
    /// beside it is darker*, so painting both is honest as well as useful: on deep
    /// blue you see the beam, on a document you see the shade, and it is one brush
    /// either way.
    ///
    /// A bitmap rather than a gradient brush because the falloff is two-dimensional
    /// -- across the band and down it -- and WPF gradients only run one way. The
    /// first version did it with a horizontal LinearGradientBrush fill plus a
    /// vertical OpacityMask, which works but costs a second render pass over a
    /// screen-tall element on every move. This is one stretched 96x128 image: the
    /// gradient is so smooth that bilinear scaling to 400x700 shows no banding, and
    /// the whole thing is frozen and shared by every shaft.
    ///
    /// Premultiplied, so the core and the shade can simply be added together; they
    /// barely overlap, and their alphas sum to well under 1 so nothing clips.
    /// </summary>
    /// <param name="gain">
    /// Multiplies both alphas. 1 is the real thing. The debug command needs more,
    /// and it cannot get it from the element's opacity -- a shaft already sits near
    /// opacity 1 and the strength lives in this bitmap -- so a louder showcase
    /// means a second, louder bitmap.
    /// </param>
    public static Brush Shaft(double gain = 1)
    {
        const int W = 96, H = 128;
        // The beam is slightly blue-white; the shade is the deep-water colour, not
        // neutral grey, or it reads as a dirty screen instead of as water.
        const double CoreR = 212 / 255.0, CoreG = 236 / 255.0, CoreB = 248 / 255.0;
        const double FlankR = 10 / 255.0, FlankG = 24 / 255.0, FlankB = 34 / 255.0;

        var px = new byte[W * H * 4];
        int i = 0;
        for (int y = 0; y < H; y++)
        {
            // Down the band. 1.7 was the first guess and it was wrong: it put
            // practically all the light in the top fifth of the band, so the screen
            // had a glow along its top edge rather than shafts coming down into it.
            // 1.25 still falls off faster than linear -- which is what "stronger at
            // the top" asks for -- but the band is readable as a shaft for most of
            // its length.
            double fade = Math.Pow(1 - (y + 0.5) / H, 1.25);
            for (int x = 0; x < W; x++)
            {
                double d = ((x + 0.5) / W - 0.5) * 2;      // -1 .. 1 across the band
                // Narrow enough to read as a beam. At the first width (0.38) the
                // core filled most of the band and two neighbouring shafts merged
                // into one pale smear.
                double core = Math.Exp(-(d / 0.30) * (d / 0.30));
                // Two lobes, one either side of the core. Pulled in to 0.52 and kept
                // narrow so both are practically zero at the band's edge -- at the
                // first spacing the shade was still 12% strong at the edge and the
                // stretched bitmap gave the shaft a visible straight border.
                double off = (Math.Abs(d) - 0.52) / 0.19;
                double flank = Math.Exp(-off * off);

                double ac = ShaftCoreAlpha * gain * core * fade;
                double af = ShaftFlankAlpha * gain * flank * fade;
                double sum = Math.Min(1, ac + af);
                px[i++] = (byte)Math.Round(Math.Min(sum, CoreB * ac + FlankB * af) * 255);
                px[i++] = (byte)Math.Round(Math.Min(sum, CoreG * ac + FlankG * af) * 255);
                px[i++] = (byte)Math.Round(Math.Min(sum, CoreR * ac + FlankR * af) * 255);
                px[i++] = (byte)Math.Round(sum * 255);
            }
        }

        var bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Pbgra32, null, px, W * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp) { Stretch = Stretch.Fill };
        brush.Freeze();
        return brush;
    }
}
