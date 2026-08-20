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
