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

    // Skin: a pattern of spots/bands over the colour, plus a drifting pearl sheen.
    public int SkinPattern;
    public double SkinStrength = 0.5;
    public double SheenStrength = 0.25;
    public double SheenPhase;

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
    public double BlinkIn = 2;
    public double BlinkLeft;
    public bool Blinking => BlinkLeft > 0;

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
