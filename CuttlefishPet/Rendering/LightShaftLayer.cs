using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CuttlefishPet.Core;

namespace CuttlefishPet.Rendering;

/// <summary>
/// Puts the light shafts on the overlay: one leaning rectangle per shaft, behind
/// the dust and everything else.
///
/// A rectangle plus a SkewTransform rather than a four-point Polygon per shaft.
/// The polygon was the first attempt and it is worse in the way that matters: the
/// lean changes every step, so a polygon means writing four Points into a
/// PointCollection and having WPF rebuild and re-tessellate the geometry, while a
/// skew is one double on a transform that is already there and leaves the
/// geometry alone. The rectangle's own width and height are written only when the
/// screen or the DPI actually changes, so a normal step costs two Canvas writes,
/// one angle and one opacity per shaft.
///
/// Skewed about its own top-left corner (CenterX and CenterY both 0), so the
/// surface end stays where the simulation put it and only the deep end swings --
/// which is what light entering at a fixed spot and bending through the water
/// does, and it also means the top edge of the screen never shows a shaft sliding
/// sideways out from under itself.
/// </summary>
public sealed class LightShaftLayer
{
    /// <summary>Behind the dust (-5), which is itself behind the animals.</summary>
    private const int ZDeepestBack = -8;

    /// <summary>Below this the shaft is not worth a blend; see MarineSnowLayer.</summary>
    private const double Invisible = 0.012;

    private readonly OverlayWindow _overlay;
    private readonly LightShafts _field;
    private readonly List<Rectangle> _bars = new();
    private readonly List<SkewTransform> _skews = new();
    private readonly Brush _brush;

    /// <summary>
    /// The showcase brush, built on demand and kept. A shaft at real strength sits
    /// at an element opacity near 1 already -- the faintness lives in the bitmap --
    /// so the debug command cannot get a brighter shaft by turning opacity up. It
    /// gets a brighter bitmap instead.
    /// </summary>
    private Brush? _loud;
    private double _loudGain;

    public LightShaftLayer(OverlayWindow overlay, LightShafts field)
    {
        _overlay = overlay;
        _field = field;
        _brush = WaterLook.Shaft();
    }

    /// <summary>Match the number of rectangles to the field, then lean and place them.</summary>
    public void Tick()
    {
        var shafts = _field.Shafts;

        while (_bars.Count > shafts.Count)
        {
            _overlay.PetCanvas.Children.Remove(_bars[^1]);
            _bars.RemoveAt(_bars.Count - 1);
            _skews.RemoveAt(_skews.Count - 1);
        }
        while (_bars.Count < shafts.Count)
        {
            var skew = new SkewTransform { CenterX = 0, CenterY = 0 };
            var bar = new Rectangle
            {
                Fill = _brush,
                IsHitTestVisible = false,
                RenderTransform = skew,
            };
            Panel.SetZIndex(bar, ZDeepestBack);
            _overlay.PetCanvas.Children.Add(bar);
            _bars.Add(bar);
            _skews.Add(skew);
        }

        double k = _overlay.DeviceToDiu;
        double tankHeight = _field.Bounds.Height;
        double top = _field.Bounds.Top;

        // Above 1 the extra strength has to come out of the brush; at or below it,
        // the element's opacity does the job and every shaft shares one brush.
        double gain = _field.Brightness;
        Brush fill = _brush;
        if (gain > 1)
        {
            if (_loud == null || Math.Abs(_loudGain - gain) > 0.01)
            {
                _loud = WaterLook.Shaft(gain);
                _loudGain = gain;
            }
            fill = _loud;
        }

        for (int i = 0; i < shafts.Count; i++)
        {
            var s = shafts[i];
            var bar = _bars[i];

            double a = s.Strength * Math.Min(1, gain);
            if (!double.IsFinite(a) || a < Invisible)
            {
                if (bar.Visibility != Visibility.Collapsed) bar.Visibility = Visibility.Collapsed;
                continue;
            }
            if (bar.Visibility != Visibility.Visible) bar.Visibility = Visibility.Visible;
            bar.Opacity = Math.Min(1, a);
            if (!ReferenceEquals(bar.Fill, fill)) bar.Fill = fill;

            Resize(bar, s, k, tankHeight);
            // Atan, not the raw ratio: a SkewTransform wants the angle whose tangent
            // is the lean. At this tilt the two differ by about a degree, which is
            // 20px of drift at the deep end -- visible when you put the two side by
            // side, which is why this is spelled out rather than approximated.
            _skews[i].AngleX = Math.Atan(s.Lean) * 180 / Math.PI;

            var corner = _overlay.PhysToDiu(new Point(s.X - s.Width / 2, top));
            Canvas.SetLeft(bar, corner.X);
            Canvas.SetTop(bar, corner.Y);
        }
    }

    /// <summary>
    /// Width and reach only change when the screen or the DPI does, and writing
    /// them dirties layout, so they are compared first. Half a pixel is well below
    /// anything visible and well above float noise.
    /// </summary>
    private static void Resize(Rectangle bar, Shaft s, double k, double tankHeight)
    {
        double w = s.Width * k, h = s.Reach * tankHeight * k;
        if (Math.Abs(bar.Width - w) > 0.5 || double.IsNaN(bar.Width)) bar.Width = w;
        if (Math.Abs(bar.Height - h) > 0.5 || double.IsNaN(bar.Height)) bar.Height = h;
    }

    /// <summary>What the first rectangle actually is, for the debug command.</summary>
    public string Describe()
    {
        if (_bars.Count == 0) return "no shafts";
        var b = _bars[0];
        return $"{_bars.Count} shafts, [0] {b.Width:F0}x{b.Height:F0} DIU at " +
               $"({Canvas.GetLeft(b):F0},{Canvas.GetTop(b):F0}) lean={_skews[0].AngleX:F1}deg " +
               $"opacity={b.Opacity:F3} vis={b.Visibility} z={Panel.GetZIndex(b)}";
    }
}
