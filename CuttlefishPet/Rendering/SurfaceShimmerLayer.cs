using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CuttlefishPet.Core;

namespace CuttlefishPet.Rendering;

/// <summary>
/// Puts the waterline on the overlay: one soft highlight per glint, in a shallow
/// band hung from the top edge of the virtual screen.
///
/// Same shape as LightShaftLayer and for the same reasons -- a Rectangle whose
/// size is written only when the screen changes, moved with two Canvas writes --
/// but with no transform at all, because a glint slides and never tilts. Drawn
/// just in front of the shafts so that a beam appears to hang from the surface
/// rather than to pass through it.
/// </summary>
public sealed class SurfaceShimmerLayer
{
    /// <summary>In front of the shafts (-8), behind the dust (-5) and the animals.</summary>
    private const int ZSurface = -7;

    private const double Invisible = 0.012;

    private readonly OverlayWindow _overlay;
    private readonly SurfaceShimmer _field;
    private readonly List<Rectangle> _bars = new();
    private readonly Brush _brush;

    /// <summary>The showcase brush; see LightShaftLayer for why opacity cannot do this.</summary>
    private Brush? _loud;
    private double _loudGain;

    public SurfaceShimmerLayer(OverlayWindow overlay, SurfaceShimmer field)
    {
        _overlay = overlay;
        _field = field;
        _brush = WaterLook.Glint();
    }

    public void Tick()
    {
        var glints = _field.Glints;

        while (_bars.Count > glints.Count)
        {
            _overlay.PetCanvas.Children.Remove(_bars[^1]);
            _bars.RemoveAt(_bars.Count - 1);
        }
        while (_bars.Count < glints.Count)
        {
            var bar = new Rectangle { Fill = _brush, IsHitTestVisible = false };
            Panel.SetZIndex(bar, ZSurface);
            _overlay.PetCanvas.Children.Add(bar);
            _bars.Add(bar);
        }

        double k = _overlay.DeviceToDiu;
        double top = _field.Bounds.Top;
        double bandHeight = _field.Band * k;

        double gain = _field.Brightness;
        Brush fill = _brush;
        if (gain > 1)
        {
            if (_loud == null || Math.Abs(_loudGain - gain) > 0.01)
            {
                _loud = WaterLook.Glint(gain);
                _loudGain = gain;
            }
            fill = _loud;
        }

        for (int i = 0; i < glints.Count; i++)
        {
            var g = glints[i];
            var bar = _bars[i];

            double a = g.Strength * Math.Min(1, gain);
            if (!double.IsFinite(a) || a < Invisible)
            {
                if (bar.Visibility != Visibility.Collapsed) bar.Visibility = Visibility.Collapsed;
                continue;
            }
            if (bar.Visibility != Visibility.Visible) bar.Visibility = Visibility.Visible;
            bar.Opacity = Math.Min(1, a);
            if (!ReferenceEquals(bar.Fill, fill)) bar.Fill = fill;

            double w = g.Width * k;
            if (Math.Abs(bar.Width - w) > 0.5 || double.IsNaN(bar.Width)) bar.Width = w;
            if (Math.Abs(bar.Height - bandHeight) > 0.5 || double.IsNaN(bar.Height)) bar.Height = bandHeight;

            var corner = _overlay.PhysToDiu(new Point(g.X - g.Width / 2, top));
            Canvas.SetLeft(bar, corner.X);
            Canvas.SetTop(bar, corner.Y);
        }
    }

    /// <summary>What the first highlight actually is, for the debug command.</summary>
    public string Describe()
    {
        if (_bars.Count == 0) return "no glints";
        var b = _bars[0];
        return $"{_bars.Count} glints, band {_field.Band:F0}px, [0] {b.Width:F0}x{b.Height:F0} DIU at " +
               $"({Canvas.GetLeft(b):F0},{Canvas.GetTop(b):F0}) opacity={b.Opacity:F3} vis={b.Visibility}";
    }
}
