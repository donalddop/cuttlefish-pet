using System.Windows.Media;

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
}
