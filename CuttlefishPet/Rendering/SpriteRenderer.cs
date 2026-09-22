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
        /// <summary>Seconds on screen. 0 means "as long as the frames last".</summary>
        public double Life;
        /// <summary>Sideways sway amplitude in pixels, and where in the weave it starts.</summary>
        public double Wobble;
        public double Phase;
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

        _inspectorText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(236, 230, 220)),
        };
        _inspector = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(222, 22, 20, 26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 170, 158, 144)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 7, 9, 7),
            Child = _inspectorText,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        Panel.SetZIndex(_inspector, 10_000);   // over every animal, whatever the order
        _overlay.PetCanvas.Children.Add(_inspector);
    }

    private readonly Border _inspector;
    private readonly TextBlock _inspectorText;

    /// <summary>
    /// Put one animal's insides on screen beside it. Deliberately plain monospaced
    /// text: this is the honest view of a creature, and dressing it up would only
    /// make it harder to read the numbers against what the animal is doing.
    /// </summary>
    public void ShowInspector(Pet pet, string text)
    {
        _inspectorText.Text = text;
        _inspector.Visibility = Visibility.Visible;
        _inspector.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var b = pet.Bounds;
        double width = _inspector.DesiredSize.Width;
        var right = _overlay.PhysToDiu(new Point(b.Right + 14, b.Top));
        var left = _overlay.PhysToDiu(new Point(b.Left - 14, b.Top));

        // Beside the animal, flipping to its other side rather than running off the
        // screen. The canvas has not necessarily been measured yet, so an unmeasured
        // width must mean "plenty of room" and not "no room at all" -- which would
        // park the panel against the left edge of the screen for ever.
        double room = _overlay.PetCanvas.ActualWidth > 0
            ? _overlay.PetCanvas.ActualWidth
            : _overlay.ActualWidth;
        double x = room > 0 && right.X + width > room - 6 ? left.X - width : right.X;
        Canvas.SetLeft(_inspector, Math.Max(2, x));
        Canvas.SetTop(_inspector, Math.Max(2, right.Y));
    }

    public void HideInspector() => _inspector.Visibility = Visibility.Collapsed;

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

        // Carriage. A settled, bold animal stands up out of its own footprint and
        // narrows; an unsure or frightened one hunkers down and spreads. Scaled
        // about the contact point, so it grows from where it stands rather than
        // sinking through the surface it is on.
        double carriage = pet.Carriage;
        v.Flip.ScaleX = (pet.FacingRight ? 1 : -1) * (1 - carriage * 0.05);
        v.Flip.ScaleY = 1 + carriage * 0.09;
        v.Flip.CenterX = w / 2;
        v.Flip.CenterY = anim.Anchor.Y / anim.FrameH * h;

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
    /// Draw the feeding tentacle for this frame: one taut line from the mouth to
    /// the club, rebuilt from scratch each tick so the reach follows whatever the
    /// pet is actually striking at.
    ///
    /// A cuttlefish really does fire two of them, and they were drawn that way --
    /// but at this size two strands bowing apart read as a wishbone rather than a
    /// strike. One straight line is what the eye expects from something shot out
    /// at speed, so accuracy loses to legibility here.
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
        double club = 4.3 * pet.Scale * k;

        var group = new GeometryGroup();
        var figure = new PathFigure { StartPoint = a, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new LineSegment(b, true));
        var strand = new PathGeometry();
        strand.Figures.Add(figure);
        group.Children.Add(strand);

        // The club is the paddle on the end, laid along the line of the strike
        // rather than square to the screen.
        group.Children.Add(new EllipseGeometry(b, club * 1.3, club * 0.78)
        {
            Transform = new RotateTransform(
                Math.Atan2(dir.Y, dir.X) * 180 / Math.PI, b.X, b.Y),
        });

        v.Tentacle.Data = group;
        v.TentacleEdge.Data = group;
        v.Tentacle.StrokeThickness = 2.6 * pet.Scale * k;
        v.TentacleEdge.StrokeThickness = 4.3 * pet.Scale * k;
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

    /// <param name="scale">
    /// Multiplied onto the animation's own scale. A cuttlebone uses it to come
    /// out the size of the animal that left it; everything else leaves it at 1.
    /// </param>
    public void UpdateProp(Image img, string animName, Point physPos, double t,
        bool facingRight = true, double scale = 1)
    {
        var anim = _library[animName];
        double k = _overlay.DeviceToDiu;
        double s = anim.Scale * scale;
        int i = (int)(t * anim.Fps);
        i = anim.Loop ? i % anim.Frames.Length : Math.Min(i, anim.Frames.Length - 1);

        img.Source = anim.Frames[i];
        img.Width = anim.FrameW * s * k;
        img.Height = anim.FrameH * s * k;
        img.RenderTransform = facingRight
            ? null
            : new ScaleTransform(-1, 1, anim.FrameW * s * k / 2, 0);
        var tl = _overlay.PhysToDiu(new Point(physPos.X - anim.Anchor.X * s,
                                              physPos.Y - anim.Anchor.Y * s));
        Canvas.SetLeft(img, tl.X);
        Canvas.SetTop(img, tl.Y);
    }

    // ---- one-shot effects ----

    public void SpawnInk(Point physPos) => Spawn("ink", physPos, Pet.RenderScale, rise: 0);

    /// <summary>
    /// A bubble leaving a pet. Not a balloon on a string: it breaks away fast,
    /// it weaves on the way up because it keeps tipping out of its own wake,
    /// and a fat one overtakes a thin one. Randomising size, climb and sway
    /// buys all three, and buys a cluster that spreads instead of a row of
    /// identical dots moving in lockstep -- which is what made the old one
    /// look mechanical. The frame is small; drawn at source size it is a
    /// speck beside a pet, hence the scale.
    /// </summary>
    public void SpawnBubble(Point physPos)
    {
        double size = 1.9 + Random.Shared.NextDouble() * 1.5;
        Spawn("bubble", physPos, size, rise: 92 + size * 38, fade: true,
              life: 1.4 + Random.Shared.NextDouble() * 0.7,
              wobble: 4 + Random.Shared.NextDouble() * 8);
    }

    private void Spawn(string animName, Point physPos, double scale, double rise,
                       bool fade = false, double life = 0, double wobble = 0)
    {
        var anim = _library[animName];
        var img = NewImage();
        img.Source = anim.Frames[0];
        img.Width = anim.FrameW * scale * _overlay.DeviceToDiu;
        img.Height = anim.FrameH * scale * _overlay.DeviceToDiu;
        _overlay.PetCanvas.Children.Add(img);
        _effects.Add(new Effect
        {
            Img = img, Anim = anim, Pos = physPos, Rise = rise, FadeOut = fade,
            Life = life, Wobble = wobble,
            Phase = Random.Shared.NextDouble() * Math.PI * 2,
        });
    }

    public void TickEffects(double dt)
    {
        double k = _overlay.DeviceToDiu;
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var e = _effects[i];
            e.T += dt;
            double life = e.Life > 0 ? e.Life : e.Anim.Frames.Length / e.Anim.Fps;

            if (e.T >= life)
            {
                _overlay.PetCanvas.Children.Remove(e.Img);
                _effects.RemoveAt(i);
                continue;
            }

            // Given a lifetime of its own, the frames stretch over the whole
            // climb instead of running out a third of the way up. The bubble
            // sprite is drawn as four sizes, so that stretch reads as a bubble
            // swelling on the way up, which is what dropping pressure does.
            int frame = e.Life > 0
                ? Math.Min(e.Anim.Frames.Length - 1,
                           (int)(e.T / life * e.Anim.Frames.Length))
                : (int)(e.T * e.Anim.Fps);
            e.Img.Source = e.Anim.Frames[frame];
            // Hold, then go in the last third, rather than fading from the off.
            if (e.FadeOut) e.Img.Opacity = Math.Min(1, (1 - e.T / life) * 3.2);

            // Buoyancy wins almost at once, so the climb is a brief ease-in and
            // then a straight run at full speed -- the old one crept up at a
            // flat 55 px/s and was gone after 37 pixels, which is why it looked
            // like it was being winched.
            const double tau = 0.10;
            double climb = e.Rise * (e.T - tau * (1 - Math.Exp(-e.T / tau)));
            double sway = e.Wobble * Math.Sin(e.T * 6.0 + e.Phase)
                          * Math.Min(1, e.T * 4);

            var pos = new Point(e.Pos.X + sway, e.Pos.Y - climb);
            var tl = _overlay.PhysToDiu(new Point(
                pos.X - e.Img.Width / k / 2, pos.Y - e.Img.Height / k / 2));
            Canvas.SetLeft(e.Img, tl.X);
            Canvas.SetTop(e.Img, tl.Y);
        }
    }
}
