using System.Windows;
using System.Windows.Media.Imaging;
using CuttlefishPet.Behaviors;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Core;

/// <summary>One cuttlefish. Position/velocity in physical screen pixels.</summary>
public sealed class Pet
{
    public const double RenderScale = 1.7;

    /// <summary>Anchor (foot/contact) point.</summary>
    public Point Pos;
    public Vector Vel;
    public bool FacingRight = true;
    /// <summary>Surface currently stood on / climbed (this tick's resolved instance).</summary>
    public Surface? Surface;

    public required AnimationPlayer Anim { get; init; }
    public BehaviorMachine Machine { get; set; } = null!;
    public PetVisual Visual { get; set; } = null!;

    // Camouflage state, applied by the renderer.
    public BitmapSource? CamoSource;
    public double CamoOpacity;
    public double CamoRipple;

    /// <summary>Purely visual hover-bob offset (physical px), set by behaviors.</summary>
    public double VisualBob;
    /// <summary>Visual tilt in degrees around the contact point (swinging, drifting).</summary>
    public double Rotation;

    // Chromatophores: the body cross-fades from FromPalette to Palette.
    public int Palette;
    public int FromPalette;
    public double PaletteBlend = 1;
    public double PaletteChangeIn = 8;
    /// <summary>This individual's own colour, shown only when something is going on.</summary>
    public int HomePalette;
    /// <summary>Own timer: displays must not keep re-rolling the personal colour.</summary>
    public double HomeChangeIn = 30;
    /// <summary>0 = translucent glass, 1 = solid colour. Eased toward the mood.</summary>
    public double Vividness;
    /// <summary>Overall alpha, low while glassy. Separate from <see cref="Fade"/>.</summary>
    public double BodyOpacity = 0.6;
    /// <summary>Extra multiplier used to dissolve a pet away entirely (the ghost).</summary>
    public double Fade = 1;

    // Skin: a pattern of spots/bands over the colour, plus a drifting pearl sheen.
    public int SkinPattern;
    public double SkinStrength = 0.5;
    public double SheenStrength = 0.25;
    public double SheenPhase;
    /// <summary>Drives the speckle pattern crawling across the skin.</summary>
    public double SkinPhase;

    /// <summary>Start shifting to another colour; ignored if already going there.</summary>
    public void ShiftTo(int palette, double holdSeconds = 25)
    {
        if (palette == Palette) return;
        FromPalette = PaletteBlend >= 1 ? Palette : FromPalette;
        Palette = palette;
        PaletteBlend = 0;
        PaletteChangeIn = holdSeconds;
    }

    // Eye: pupil aim in -1..1 body-local units, plus an independent blink timer.
    public Vector PupilOffset;
    /// <summary>Something specific to look at instead of the cursor.</summary>
    public Point? PupilTarget;

    /// <summary>Grabs in the recent past; enough of them and patience runs out.</summary>
    public double Pestered;

    /// <summary>Set after a successful courtship: eggs are coming.</summary>
    public bool WantsToNest;

    /// <summary>
    /// Seconds since this pet last visited each cell of a 3x3 grid over the screen.
    /// Swim targets favour the stalest cell, so they tour the corners instead of
    /// milling around the middle.
    /// </summary>
    public readonly double[] RegionAge = new double[9];
    public double BlinkIn = 2;
    public double BlinkLeft;
    public bool Blinking => BlinkLeft > 0;

    /// <summary>Which third-by-third cell of the tank a point falls in (0..8).</summary>
    public static int RegionOf(Point p, Rect tank)
    {
        int col = (int)Math.Clamp((p.X - tank.Left) / tank.Width * 3, 0, 2);
        int row = (int)Math.Clamp((p.Y - tank.Top) / tank.Height * 3, 0, 2);
        return row * 3 + col;
    }

    /// <summary>Screen-space bounding box of the current frame, physical px.</summary>
    public Rect Bounds
    {
        get
        {
            var a = Anim.Current;
            double ax = FacingRight ? a.Anchor.X : a.FrameW - a.Anchor.X;
            return new Rect(
                Pos.X - ax * RenderScale,
                Pos.Y - a.Anchor.Y * RenderScale,
                a.FrameW * RenderScale,
                a.FrameH * RenderScale);
        }
    }
}
