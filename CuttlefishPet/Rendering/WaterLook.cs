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

    /// <summary>The beam colour, and the colour of the water shaded beside it.</summary>
    private static readonly Color Pale = Color.FromRgb(212, 236, 248);
    private static readonly Color Shade = Color.FromRgb(10, 24, 34);

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
    /// smear on a page of text rather than as water. Measured again on the real
    /// desktop afterwards: 25 of 255 at the top of a beam, 15 halfway down.
    ///
    /// And these are the peaks: they are reached only at the very top of a shaft
    /// and only while that shaft happens to be breathing at full strength, so what
    /// crosses a document is usually half of this.
    /// </summary>
    public const double ShaftCoreAlpha = 0.147, ShaftFlankAlpha = 0.118;

    /// <summary>
    /// Peak alpha of the surface highlight, and of the water shaded just under it.
    /// Lower than a shaft's: this band sits on the top edge of the screen, which
    /// is where the tab strip and the title bar live, and a wash over those is
    /// read as a display fault long before it is read as water.
    /// </summary>
    public const double GlintAlpha = 0.115, GlintUnderAlpha = 0.070;

    /// <param name="gain">
    /// Multiplies both alphas. 1 is the real thing. The debug command needs more,
    /// and it cannot get it from the element's opacity -- a shaft already sits near
    /// opacity 1 and the strength lives in this bitmap -- so a louder showcase
    /// means a second, louder bitmap.
    /// </param>
    public static Brush Shaft(double gain = 1)
    {
        // A bitmap rather than a gradient brush because the falloff is
        // two-dimensional -- across the band and down it -- and WPF gradients only
        // run one way. The first version did it with a horizontal
        // LinearGradientBrush fill plus a vertical OpacityMask, which works but
        // costs a second render pass over a screen-tall element on every move.
        // This is one stretched 96x128 image: the gradient is so smooth that
        // bilinear scaling to 400x700 shows no banding.
        return Bake(96, 128, (u, v) =>
        {
            double d = (u - 0.5) * 2;                     // -1 .. 1 across the band

            // Down the band. 1.7 was the first guess and it was wrong: it put
            // practically all the light in the top fifth of the band, so the screen
            // had a glow along its top edge rather than shafts coming down into it.
            // 1.25 still falls off faster than linear -- which is what "stronger at
            // the top" asks for -- but the band is readable as a shaft for most of
            // its length.
            double fade = Math.Pow(1 - v, 1.25);

            // Narrow enough to read as a beam. At the first width (0.38) the core
            // filled most of the band and two neighbouring shafts merged into one
            // pale smear.
            double core = Gauss(d, 0, 0.30);
            // Two lobes, one either side of the core. Pulled in to 0.52 and kept
            // narrow so both are practically zero at the band's edge -- at the first
            // spacing the shade was still 12% strong at the edge and the stretched
            // bitmap gave the shaft a visible straight border.
            double flank = Gauss(Math.Abs(d), 0.52, 0.19);

            return (ShaftCoreAlpha * gain * core * fade,
                    ShaftFlankAlpha * gain * flank * fade);
        });
    }

    /// <summary>
    /// One catch of light on the underside of the surface: bright along the very
    /// top, with the water just beneath it shaded, fading out by the bottom of the
    /// band.
    ///
    /// The shade sits *below* the highlight rather than beside it, which is the
    /// one way this differs from a shaft, and it is the same argument turned
    /// ninety degrees: a surface looks bright because the water hanging under it
    /// is darker. It is also what keeps the band from vanishing on a white title
    /// bar, where the highlight itself is invisible.
    /// </summary>
    public static Brush Glint(double gain = 1)
    {
        return Bake(128, 32, (u, v) =>
        {
            double d = (u - 0.5) * 2;
            // Wide and soft along its length: a glint with ends you can find reads
            // as an object floating on the screen rather than as a catch of light.
            double along = Gauss(d, 0, 0.42);
            // Wide enough that the highlight spans most of the band. At 0.22 it was
            // a ten pixel bright edge along the top of the screen with nothing under
            // it, which is the hairline failure this is supposed to avoid.
            double lit = Gauss(v, 0, 0.38);        // brightest against the top edge
            double under = Gauss(v, 0.62, 0.30);   // the water hanging below it
            return (GlintAlpha * gain * along * lit,
                    GlintUnderAlpha * gain * along * under);
        });
    }

    private static double Gauss(double x, double centre, double width)
    {
        double t = (x - centre) / width;
        return Math.Exp(-t * t);
    }

    /// <summary>
    /// Bake one frozen bitmap brush from a bright shape and a dark one, each given
    /// as an alpha at (u, v) in 0..1. Both the shaft and the glint are the same
    /// idea -- light with its own shadow attached, so that it exists on any
    /// background -- and premultiplied alpha is subtle enough to be worth getting
    /// right in one place rather than two.
    ///
    /// Premultiplied, so the two shapes can simply be added: they barely overlap
    /// and their alphas sum to well under 1, so nothing clips. The Min against the
    /// summed alpha is belt and braces -- a premultiplied channel above its own
    /// alpha is undefined and renders as a bright fringe.
    /// </summary>
    private static Brush Bake(int w, int h, Func<double, double, (double Bright, double Dark)> profile)
    {
        double pr = Pale.R / 255.0, pg = Pale.G / 255.0, pb = Pale.B / 255.0;
        double sr = Shade.R / 255.0, sg = Shade.G / 255.0, sb = Shade.B / 255.0;

        var px = new byte[w * h * 4];
        int i = 0;
        for (int y = 0; y < h; y++)
        {
            double v = (y + 0.5) / h;
            for (int x = 0; x < w; x++)
            {
                var (ab, ad) = profile((x + 0.5) / w, v);
                double sum = Math.Min(1, ab + ad);
                px[i++] = (byte)Math.Round(Math.Min(sum, pb * ab + sb * ad) * 255);
                px[i++] = (byte)Math.Round(Math.Min(sum, pg * ab + sg * ad) * 255);
                px[i++] = (byte)Math.Round(Math.Min(sum, pr * ab + sr * ad) * 255);
                px[i++] = (byte)Math.Round(sum * 255);
            }
        }

        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, px, w * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp) { Stretch = Stretch.Fill };
        brush.Freeze();
        return brush;
    }
}
