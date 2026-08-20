using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CuttlefishPet.Core;

namespace CuttlefishPet.Rendering;

public sealed class PetVisual
{
    public required Grid Root { get; init; }
    public required Image Sprite { get; init; }
    /// <summary>The colour being shifted into, faded over the body.</summary>
    public required Image Shift { get; init; }
    /// <summary>Spot/band pattern, clipped to the body silhouette.</summary>
    public required System.Windows.Shapes.Rectangle Skin { get; init; }
    public required ImageBrush SkinFill { get; init; }
    public required ImageBrush SkinMask { get; init; }
    /// <summary>Flat wash in the dominant colour of the surroundings.</summary>
    public required System.Windows.Shapes.Rectangle Tint { get; init; }
    public required SolidColorBrush TintFill { get; init; }
    public required ImageBrush TintMask { get; init; }
    /// <summary>Iridescent sheen that drifts across the skin.</summary>
    public required System.Windows.Shapes.Rectangle Sheen { get; init; }
    public required ImageBrush SheenFill { get; init; }
    public required ImageBrush SheenMask { get; init; }
    public required Image Camo { get; init; }
    public required ImageBrush CamoMask { get; init; }
    public required Image Eye { get; init; }
    public required ScaleTransform Flip { get; init; }
    public required RotateTransform Swing { get; init; }
}

/// <summary>Draws pets (body + tracking pupil), props and one-shot effects.</summary>
public sealed class SpriteRenderer
{
    private sealed class Effect
    {
        public required Image Img;
        public required SpriteAnim Anim;
        public double T;
        public Point Pos;
        public double Rise;
        public bool FadeOut;
    }

    private readonly OverlayWindow _overlay;
    private readonly Dictionary<string, SpriteAnim> _library;
    private readonly SkinLibrary _skins;
    private readonly List<Effect> _effects = new();

    public SpriteRenderer(OverlayWindow overlay, Dictionary<string, SpriteAnim> library,
        SkinLibrary skins)
    {
        _overlay = overlay;
        _library = library;
        _skins = skins;
    }

    private static Image NewImage()
    {
        var img = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        return img;
    }

    public PetVisual CreateVisual()
    {
        var sprite = NewImage();
        var shift = NewImage();
        var camoMask = new ImageBrush { Stretch = Stretch.Fill };
        var camo = NewImage();
        camo.OpacityMask = camoMask;
        camo.Opacity = 0;

        // Pattern and sheen are painted as brushes and clipped to the body's alpha.
        var skinFill = new ImageBrush
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewport = new Rect(0, 0, 1, 1),
        };
        var skinMask = new ImageBrush { Stretch = Stretch.Fill };
        var skin = new System.Windows.Shapes.Rectangle
        { Fill = skinFill, OpacityMask = skinMask, IsHitTestVisible = false };

        var tintFill = new SolidColorBrush(Colors.Transparent);
        var tintMask = new ImageBrush { Stretch = Stretch.Fill };
        var tint = new System.Windows.Shapes.Rectangle
        { Fill = tintFill, OpacityMask = tintMask, IsHitTestVisible = false };

        var sheenFill = new ImageBrush
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewport = new Rect(0, 0, 1, 1),
            ImageSource = _skins.Sheen,
        };
        var sheenMask = new ImageBrush { Stretch = Stretch.Fill };
        var sheen = new System.Windows.Shapes.Rectangle
        { Fill = sheenFill, OpacityMask = sheenMask, IsHitTestVisible = false };

        var eye = NewImage();
        eye.HorizontalAlignment = HorizontalAlignment.Left;
        eye.VerticalAlignment = VerticalAlignment.Top;

        var flip = new ScaleTransform(1, 1);
        var swing = new RotateTransform(0);
        var root = new Grid
        {
            RenderTransform = new TransformGroup { Children = { flip, swing } },
        };
        root.Children.Add(sprite);
        root.Children.Add(shift);
        root.Children.Add(tint);
        root.Children.Add(skin);
        root.Children.Add(sheen);
        root.Children.Add(camo);
        root.Children.Add(eye);

        _overlay.PetCanvas.Children.Add(root);
        return new PetVisual
        {
            Root = root, Sprite = sprite, Shift = shift,
            Skin = skin, SkinFill = skinFill, SkinMask = skinMask,
            Tint = tint, TintFill = tintFill, TintMask = tintMask,
            Sheen = sheen, SheenFill = sheenFill, SheenMask = sheenMask,
            Camo = camo, CamoMask = camoMask, Eye = eye, Flip = flip, Swing = swing,
        };
    }

    public void RemoveVisual(PetVisual v) => _overlay.PetCanvas.Children.Remove(v.Root);

    public void Update(Pet pet)
    {
        var v = pet.Visual;
        var anim = pet.Anim.Current;
        int idx = pet.Anim.FrameIndex;
        var frame = anim.Frames[idx];
        var bounds = pet.Bounds; // physical px

        double k = _overlay.DeviceToDiu;
        double w = bounds.Width * k, h = bounds.Height * k;
        var tl = _overlay.PhysToDiu(bounds.TopLeft);

        // Body in the colour it is leaving, with the new colour bleeding over it.
        v.Sprite.Source = anim.Palettes[pet.FromPalette][idx];
        if (pet.PaletteBlend < 1)
        {
            v.Shift.Source = anim.Palettes[pet.Palette][idx];
            v.Shift.Opacity = pet.PaletteBlend;
            v.Shift.Visibility = Visibility.Visible;
        }
        else
        {
            v.Sprite.Source = anim.Palettes[pet.Palette][idx];
            v.Shift.Visibility = Visibility.Collapsed;
        }

        v.Root.Width = w;
        v.Root.Height = h;
        // Glassy when idle, solid when displaying; Fade dissolves it away entirely.
        v.Root.Opacity = Math.Clamp(pet.BodyOpacity * pet.Fade, 0, 1);
        Canvas.SetLeft(v.Root, tl.X);
        Canvas.SetTop(v.Root, tl.Y + pet.VisualBob * k);

        v.Flip.ScaleX = pet.FacingRight ? 1 : -1;
        v.Flip.CenterX = w / 2;

        // Swing/tilt pivots on the contact point, and follows the mirrored body.
        v.Swing.Angle = pet.FacingRight ? pet.Rotation : -pet.Rotation;
        v.Swing.CenterX = anim.Anchor.X / anim.FrameW * w;
        v.Swing.CenterY = anim.Anchor.Y / anim.FrameH * h;

        // Skin pattern and iridescence, both clipped to the body silhouette. They
        // fade out under camouflage so the disguise stays clean.
        double skinVisible = 1 - pet.CamoOpacity;
        // At rest the skin is worked out from the desktop behind the pet: a few
        // sampled colours in a coarse pattern that follows the background's grain.
        // Once it is displaying, that gives way to its own markings.
        bool wearingSurroundings = pet.Camo != null && pet.Vividness < 0.6;
        if (wearingSurroundings)
        {
            v.SkinFill.ImageSource = pet.Camo!.Texture;
            v.SkinFill.TileMode = TileMode.None;
            v.SkinFill.Viewport = new Rect(0, 0, 1, 1);
            v.Skin.Opacity = (0.55 + 0.35 * pet.Camo.Busyness) * (1 - pet.Vividness) * skinVisible;

            var d = pet.Camo.Dominant;
            v.TintFill.Color = d;
            v.TintMask.ImageSource = frame;
            v.Tint.Opacity = 0.45 * (1 - pet.Vividness) * skinVisible;
        }
        else
        {
            v.SkinFill.ImageSource = _skins.Patterns[pet.SkinPattern % _skins.Patterns.Length];
            v.SkinFill.TileMode = TileMode.Tile;
            // The speckles crawl slowly over the body — chromatophores never hold still.
            v.SkinFill.Viewport = new Rect(-pet.SkinPhase, -pet.SkinPhase * 0.55, 1, 1);
            v.Skin.Opacity = pet.SkinStrength * skinVisible;
            v.Tint.Opacity = 0;
        }
        v.SkinMask.ImageSource = frame;
        v.SheenMask.ImageSource = frame;
        v.Sheen.Opacity = pet.SheenStrength * skinVisible;
        v.SheenFill.Viewport = new Rect(-pet.SheenPhase, 0, 1, 1);

        // Opacity-masked layers are costly even at zero opacity, so anything that
        // is not contributing gets collapsed outright.
        Show(v.Skin);
        Show(v.Tint);
        Show(v.Sheen);

        // Camouflage layer: background capture masked by the current frame's alpha.
        v.Camo.Opacity = pet.CamoOpacity;
        if (pet.CamoOpacity > 0 && pet.CamoSource != null)
        {
            v.Camo.Source = pet.CamoSource;
            v.CamoMask.ImageSource = frame;
            v.Camo.Margin = new Thickness(0, pet.CamoRipple * k, 0, -pet.CamoRipple * k);
        }
        Show(v.Camo);

        // Pupil overlay: tracks the cursor, blinks, hides while camouflaged.
        if (anim.EyeCenter is Point ec && pet.CamoOpacity < 0.95)
        {
            var eyeAnim = _library["eye"];
            double scale = w / anim.FrameW;
            double size = anim.EyeRadius * 2 * scale;
            double travel = anim.EyeRadius * 0.34 * scale;

            v.Eye.Source = eyeAnim.Palettes[pet.Palette][pet.Blinking ? 1 : 0];
            v.Eye.Width = size;
            v.Eye.Height = size;
            // A cuttlefish can hide everything except its eye, so cancel out the
            // body's translucency here — the eye stays the one thing that gives
            // a hidden pet away.
            v.Eye.Opacity = (1 - pet.CamoOpacity) *
                            Math.Min(1, 1 / Math.Max(0.25, pet.BodyOpacity * pet.Fade));
            v.Eye.Visibility = Visibility.Visible;
            v.Eye.Margin = new Thickness(
                ec.X * scale - size / 2 + pet.PupilOffset.X * travel,
                ec.Y * scale - size / 2 + pet.PupilOffset.Y * travel + pet.VisualBob * k * 0.15,
                0, 0);
        }
        else
        {
            v.Eye.Visibility = Visibility.Collapsed;
        }
    }

    private static void Show(UIElement e) =>
        e.Visibility = e.Opacity > 0.02 ? Visibility.Visible : Visibility.Collapsed;

    // ---- props (shrimp treats) ----

    public Image CreateProp(string anim)
    {
        var img = NewImage();
        img.Source = _library[anim].Frames[0];
        _overlay.PetCanvas.Children.Add(img);
        return img;
    }

    public void RemoveProp(Image img) => _overlay.PetCanvas.Children.Remove(img);

    public void UpdateProp(Image img, string animName, Point physPos, double t,
        bool facingRight = true)
    {
        var anim = _library[animName];
        double k = _overlay.DeviceToDiu;
        int i = (int)(t * anim.Fps);
        i = anim.Loop ? i % anim.Frames.Length : Math.Min(i, anim.Frames.Length - 1);

        img.Source = anim.Frames[i];
        img.Width = anim.FrameW * k;
        img.Height = anim.FrameH * k;
        img.RenderTransform = facingRight
            ? null
            : new ScaleTransform(-1, 1, anim.FrameW * k / 2, 0);
        var tl = _overlay.PhysToDiu(new Point(physPos.X - anim.Anchor.X, physPos.Y - anim.Anchor.Y));
        Canvas.SetLeft(img, tl.X);
        Canvas.SetTop(img, tl.Y);
    }

    // ---- one-shot effects ----

    public void SpawnInk(Point physPos) => Spawn("ink", physPos, Pet.RenderScale, rise: 0);

    public void SpawnBubble(Point physPos) => Spawn("bubble", physPos, 1.0, rise: 55, fade: true);

    private void Spawn(string animName, Point physPos, double scale, double rise, bool fade = false)
    {
        var anim = _library[animName];
        var img = NewImage();
        img.Source = anim.Frames[0];
        img.Width = anim.FrameW * scale * _overlay.DeviceToDiu;
        img.Height = anim.FrameH * scale * _overlay.DeviceToDiu;
        _overlay.PetCanvas.Children.Add(img);
        _effects.Add(new Effect { Img = img, Anim = anim, Pos = physPos, Rise = rise, FadeOut = fade });
    }

    public void TickEffects(double dt)
    {
        double k = _overlay.DeviceToDiu;
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var e = _effects[i];
            e.T += dt;
            int frame = (int)(e.T * e.Anim.Fps);
            double life = e.Anim.Frames.Length / e.Anim.Fps;

            if (frame >= e.Anim.Frames.Length)
            {
                _overlay.PetCanvas.Children.Remove(e.Img);
                _effects.RemoveAt(i);
                continue;
            }

            e.Img.Source = e.Anim.Frames[frame];
            if (e.FadeOut) e.Img.Opacity = Math.Max(0, 1 - e.T / life);

            var pos = new Point(e.Pos.X, e.Pos.Y - e.Rise * e.T);
            var tl = _overlay.PhysToDiu(new Point(
                pos.X - e.Img.Width / k / 2, pos.Y - e.Img.Height / k / 2));
            Canvas.SetLeft(e.Img, tl.X);
            Canvas.SetTop(e.Img, tl.Y);
        }
    }
}
