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
    /// <summary>The skin being grown out of, underneath the new one.</summary>
    public required System.Windows.Shapes.Rectangle SkinPrev { get; init; }
    public required ImageBrush SkinPrevFill { get; init; }
    public required ImageBrush SkinPrevMask { get; init; }
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
    /// <summary>
    /// The two feeding tentacles, drawn as geometry rather than sprite frames — they
    /// have to reach whatever is actually being struck at, at whatever distance. Two
    /// paths over the same geometry: a dark one underneath so they stay visible on a
    /// pale desktop, a pale one on top for the muscle itself. Sit behind the body, so
    /// they read as coming out from under the arms.
    /// </summary>
    public required System.Windows.Shapes.Path TentacleEdge { get; init; }
    public required System.Windows.Shapes.Path Tentacle { get; init; }
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

        var skinPrevFill = new ImageBrush { Stretch = Stretch.Fill };
        var skinPrevMask = new ImageBrush { Stretch = Stretch.Fill };
        var skinPrev = new System.Windows.Shapes.Rectangle
        { Fill = skinPrevFill, OpacityMask = skinPrevMask, IsHitTestVisible = false };

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

        var tentacleEdge = new System.Windows.Shapes.Path
        {
            Stroke = new SolidColorBrush(Color.FromArgb(150, 74, 56, 44)),
            Fill = new SolidColorBrush(Color.FromArgb(150, 74, 56, 44)),
            StrokeEndLineCap = PenLineCap.Round,
            StrokeStartLineCap = PenLineCap.Round,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        var tentacle = new System.Windows.Shapes.Path
        {
            Stroke = new SolidColorBrush(Color.FromRgb(246, 234, 216)),
            Fill = new SolidColorBrush(Color.FromRgb(246, 234, 216)),
            StrokeEndLineCap = PenLineCap.Round,
            StrokeStartLineCap = PenLineCap.Round,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        _overlay.PetCanvas.Children.Add(tentacleEdge);
        _overlay.PetCanvas.Children.Add(tentacle);

        var flip = new ScaleTransform(1, 1);
        var swing = new RotateTransform(0);
        var root = new Grid
        {
            RenderTransform = new TransformGroup { Children = { flip, swing } },
        };
        root.Children.Add(sprite);
        root.Children.Add(shift);
        root.Children.Add(tint);
        root.Children.Add(skinPrev);
        root.Children.Add(skin);
        root.Children.Add(sheen);
        root.Children.Add(camo);
        root.Children.Add(eye);

        _overlay.PetCanvas.Children.Add(root);
        return new PetVisual
        {
            Root = root, Sprite = sprite, Shift = shift,
            Skin = skin, SkinFill = skinFill, SkinMask = skinMask,
            SkinPrev = skinPrev, SkinPrevFill = skinPrevFill, SkinPrevMask = skinPrevMask,
            Tint = tint, TintFill = tintFill, TintMask = tintMask,
            Sheen = sheen, SheenFill = sheenFill, SheenMask = sheenMask,
            Camo = camo, CamoMask = camoMask, Eye = eye, Flip = flip, Swing = swing,
            TentacleEdge = tentacleEdge, Tentacle = tentacle,
        };
    }

    public void RemoveVisual(PetVisual v)
    {
        _overlay.PetCanvas.Children.Remove(v.Root);
        _overlay.PetCanvas.Children.Remove(v.Tentacle);
        _overlay.PetCanvas.Children.Remove(v.TentacleEdge);
    }

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
            double strength = (0.66 + 0.28 * pet.Camo!.Busyness) * (1 - pet.Vividness) * skinVisible;

            // The skin it is growing out of shows through until the new one takes.
            if (pet.CamoPrev != null && pet.CamoBlend < 1)
            {
                v.SkinPrevFill.ImageSource = pet.CamoPrev.Texture;
                v.SkinPrevMask.ImageSource = frame;
                v.SkinPrev.Opacity = strength;
            }
            else
            {
                v.SkinPrev.Opacity = 0;
            }

            v.SkinFill.ImageSource = pet.Camo.Texture;
            v.SkinFill.TileMode = TileMode.None;
            v.SkinFill.Viewport = new Rect(0, 0, 1, 1);
            v.Skin.Opacity = strength * Math.Clamp(pet.CamoBlend, 0, 1);

            // A flat wash of the dominant colour underneath keeps the body reading as
            // one creature, but too much of it smears out the detail above.
            var d = pet.Camo.Dominant;
            v.TintFill.Color = d;
            v.TintMask.ImageSource = frame;
            v.Tint.Opacity = 0.22 * (1 - pet.Vividness) * skinVisible;
        }
        else
        {
            v.SkinFill.ImageSource = _skins.Patterns[pet.SkinPattern % _skins.Patterns.Length];
            v.SkinFill.TileMode = TileMode.Tile;
            // The speckles crawl slowly over the body — chromatophores never hold still.
            v.SkinFill.Viewport = new Rect(-pet.SkinPhase, -pet.SkinPhase * 0.55, 1, 1);
            v.Skin.Opacity = pet.SkinStrength * skinVisible;
            v.Tint.Opacity = 0;
            v.SkinPrev.Opacity = 0;
        }
        v.SkinMask.ImageSource = frame;
        v.SheenMask.ImageSource = frame;
        v.Sheen.Opacity = pet.SheenStrength * skinVisible;
        v.SheenFill.Viewport = new Rect(-pet.SheenPhase, 0, 1, 1);

        // Opacity-masked layers are costly even at zero opacity, so anything that
        // is not contributing gets collapsed outright.
        Show(v.Skin);
        Show(v.SkinPrev);
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
            double size = anim.EyeRadius * 2 * scale * pet.PupilScale;
            double travel = anim.EyeRadius * 0.52 * scale;

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

        UpdateTentacles(pet, v);
    }

    private static void Show(UIElement e) =>
        e.Visibility = e.Opacity > 0.02 ? Visibility.Visible : Visibility.Collapsed;

    // ---- props (shrimp treats) ----


    /// <summary>
    /// Draw the feeding tentacles for this frame. Two strands bowing apart and
    /// converging on a pair of clubs, rebuilt from scratch each tick — the geometry
    /// is four segments, so this is cheaper than it sounds and it lets the reach
    /// follow whatever the pet is actually striking at.
    /// </summary>
    private void UpdateTentacles(Pet pet, PetVisual v)
    {
        var mouth = TentacleStrike.Mouth(pet);
        var d = pet.StrikeTip - mouth;
        double len = d.Length;

        // Tucked away, or so nearly so that drawing it would just be a smudge.
        if (!pet.Striking || len < 6)
        {
            if (v.Tentacle.Visibility != Visibility.Collapsed)
            {
                v.Tentacle.Visibility = Visibility.Collapsed;
                v.TentacleEdge.Visibility = Visibility.Collapsed;
            }
            return;
        }

        double k = _overlay.DeviceToDiu;
        var a = _overlay.PhysToDiu(mouth);
        var b = _overlay.PhysToDiu(pet.StrikeTip);
        var dir = new Vector(b.X - a.X, b.Y - a.Y);
        double dlen = Math.Max(1, dir.Length);
        var normal = new Vector(-dir.Y, dir.X) / dlen;

        // They bow apart over the first stretch and come together at the clubs; the
        // longer the reach, the straighter they pull.
        double bow = Math.Min(13, dlen * 0.17) * pet.Scale;
        double club = 3.6 * pet.Scale * k;

        var group = new GeometryGroup();
        foreach (double side in stackalloc[] { -1.0, 1.0 })
        {
            var mid = new Point((a.X + b.X) / 2 + normal.X * bow * side,
                                (a.Y + b.Y) / 2 + normal.Y * bow * side);
            var figure = new PathFigure { StartPoint = a, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new QuadraticBezierSegment(mid, b, true));
            var strand = new PathGeometry();
            strand.Figures.Add(figure);
            group.Children.Add(strand);

            var tip = new Point(b.X + normal.X * club * 0.8 * side,
                                b.Y + normal.Y * club * 0.8 * side);
            group.Children.Add(new EllipseGeometry(tip, club, club * 0.72));
        }

        v.Tentacle.Data = group;
        v.TentacleEdge.Data = group;
        v.Tentacle.StrokeThickness = 2.1 * pet.Scale * k;
        v.TentacleEdge.StrokeThickness = 3.6 * pet.Scale * k;
        v.Tentacle.Opacity = 0.92 * pet.Fade;
        v.TentacleEdge.Opacity = 0.55 * pet.Fade;
        v.Tentacle.Visibility = Visibility.Visible;
        v.TentacleEdge.Visibility = Visibility.Visible;
    }
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
        img.Width = anim.FrameW * anim.Scale * k;
        img.Height = anim.FrameH * anim.Scale * k;
        img.RenderTransform = facingRight
            ? null
            : new ScaleTransform(-1, 1, anim.FrameW * anim.Scale * k / 2, 0);
        var tl = _overlay.PhysToDiu(new Point(physPos.X - anim.Anchor.X * anim.Scale,
                                              physPos.Y - anim.Anchor.Y * anim.Scale));
        Canvas.SetLeft(img, tl.X);
        Canvas.SetTop(img, tl.Y);
    }

    // ---- one-shot effects ----

    public void SpawnInk(Point physPos) => Spawn("ink", physPos, Pet.RenderScale, rise: 0);

    // The bubble frame is small; drawn at source size it is a speck beside a pet.
    public void SpawnBubble(Point physPos) => Spawn("bubble", physPos, 2.6, rise: 55, fade: true);

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
