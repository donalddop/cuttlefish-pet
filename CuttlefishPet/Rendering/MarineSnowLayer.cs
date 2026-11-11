using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CuttlefishPet.Core;

namespace CuttlefishPet.Rendering;

/// <summary>
/// Puts a MarineSnow field on the overlay: one small ellipse per mote, behind
/// every animal.
///
/// One ellipse each rather than a single element that draws them all in OnRender.
/// That was the first attempt and it is the wrong shape for this window: the
/// overlay spans the whole virtual screen, so invalidating one full-screen visual
/// every tick hands the compositor a screen-sized dirty region thirty times a
/// second whether anything moved into it or not. Separate ellipses dirty only the
/// few pixels each mote actually occupies -- the same reason the animals are
/// separate visuals -- and a tick costs two Canvas writes per mote and nothing else.
///
/// Size and brush are set once at build time, because a mote's depth never
/// changes; see MarineSnow.
/// </summary>
public sealed class MarineSnowLayer
{
    /// <summary>Behind the animals and their tentacles, which sit at the default 0.</summary>
    private const int ZBehindEverything = -5;

    private readonly OverlayWindow _overlay;
    private readonly MarineSnow _field;
    private readonly List<Ellipse> _dots = new();
    private readonly Brush _brush;

    public MarineSnowLayer(OverlayWindow overlay, MarineSnow field)
    {
        _overlay = overlay;
        _field = field;
        _brush = WaterLook.Flake();
    }

    /// <summary>Match the number of ellipses to the field, then move them.</summary>
    public void Tick()
    {
        var flakes = _field.Flakes;

        while (_dots.Count > flakes.Count)
        {
            _overlay.PetCanvas.Children.Remove(_dots[^1]);
            _dots.RemoveAt(_dots.Count - 1);
        }
        while (_dots.Count < flakes.Count)
        {
            var dot = new Ellipse { Fill = _brush, IsHitTestVisible = false };
            Panel.SetZIndex(dot, ZBehindEverything);
            _overlay.PetCanvas.Children.Add(dot);
            _dots.Add(dot);
            Resize(dot, flakes[_dots.Count - 1]);
        }

        double k = _overlay.DeviceToDiu;
        for (int i = 0; i < flakes.Count; i++)
        {
            var f = flakes[i];
            var dot = _dots[i];

            double a = f.Alpha * f.Clarity * _field.Brightness;
            if (a < 0.012)
            {
                // Nothing to see: collapse it rather than leave the compositor a
                // transparent visual to keep blending every frame.
                if (dot.Visibility != Visibility.Collapsed) dot.Visibility = Visibility.Collapsed;
                continue;
            }
            if (dot.Visibility != Visibility.Visible) dot.Visibility = Visibility.Visible;
            dot.Opacity = Math.Min(1, a);

            double r = f.Size * k / 2;
            var tl = _overlay.PhysToDiu(new Point(f.X, f.Y));
            Canvas.SetLeft(dot, tl.X - r);
            Canvas.SetTop(dot, tl.Y - r);
        }
    }

    private void Resize(Ellipse dot, Flake f)
    {
        double d = f.Size * _overlay.DeviceToDiu;
        dot.Width = d;
        dot.Height = d;
    }

    /// <summary>What the first ellipse actually is, for the debug command.</summary>
    public string Describe()
    {
        if (_dots.Count == 0) return "no dots";
        var d = _dots[0];
        return $"{_dots.Count} dots, canvas has {_overlay.PetCanvas.Children.Count} children, " +
               $"[0] {d.Width:F2}x{d.Height:F2} DIU at ({Canvas.GetLeft(d):F0},{Canvas.GetTop(d):F0}) " +
               $"opacity={d.Opacity:F3} vis={d.Visibility} z={Panel.GetZIndex(d)} " +
               $"k={_overlay.DeviceToDiu:F3} origin={_overlay.OriginPhysical}";
    }

    /// <summary>Rebuild sizes after a DPI change; cheap and rare.</summary>
    public void Rescale()
    {
        for (int i = 0; i < _dots.Count && i < _field.Flakes.Count; i++)
            Resize(_dots[i], _field.Flakes[i]);
    }
}
