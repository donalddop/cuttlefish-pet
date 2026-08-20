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

    // Camouflage worked out from the desktop behind this pet. A new reading does not
    // replace the old one outright — it bleeds over it, so the skin drifts rather
    // than flicking, and a striking patch can stay on a pet for a good while.
    public CamoSkin? Camo;
    public CamoSkin? CamoPrev;
    public double CamoBlend = 1;
    public double CamoResampleIn;
    /// <summary>Where the last sample was taken, so a move triggers a fresh look.</summary>
    public Point LastSampleAt;
    /// <summary>A sample is in flight on a background thread.</summary>
    public bool Sampling;
    /// <summary>Hanging on to this reading regardless of where it swims.</summary>
    public bool CamoHolding;

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
    /// <summary>A passing thing that caught its eye, e.g. where you just clicked.</summary>
    public Point? GlanceTarget;
    public double GlanceFor;

    /// <summary>Set the moment it takes fright, so the panic can spread to others.</summary>
    public bool Alarmed;

    /// <summary>Grabs in the recent past; enough of them and patience runs out.</summary>
    public double Pestered;

    /// <summary>Set after a successful courtship: eggs are coming.</summary>
    public bool WantsToNest;

    // Cuttlefish are short-lived and breed once, which is what keeps a tank from
    // filling up. Age counts real seconds; Lifespan is what this one gets.
    public double Age;
    public double Lifespan = 20 * 60;
    public bool Dying;

    // Size tells you how old one is: hatchlings are tiny and grow into it.
    /// <summary>Current size relative to a full-grown adult.</summary>
    public double Scale = 1;
    /// <summary>Size at birth — small for a hatchling, nearly grown for a newcomer.</summary>
    public double BirthScale = 1;
    /// <summary>How long it takes to reach full size.</summary>
    public double GrowUpSeconds = 1;

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
            double s = RenderScale * Scale;
            double ax = FacingRight ? a.Anchor.X : a.FrameW - a.Anchor.X;
            return new Rect(
                Pos.X - ax * s,
                Pos.Y - a.Anchor.Y * s,
                a.FrameW * s,
                a.FrameH * s);
        }
    }
}
