using System.Windows;
using CuttlefishPet.Behaviors;

namespace CuttlefishPet.Core;

/// <summary>
/// Animals behind your windows: who is behind them this tick, and which strip of
/// desktop is left for them to be seen in.
///
/// Its own file rather than another hundred lines in PetManager, and the
/// arithmetic it leans on lives in <see cref="Occlusion"/> so the decision can be
/// driven in DriveTests without a screen. What is left here is only the wiring:
/// when to rebuild the mask, and what to ask about each animal.
/// </summary>
public sealed partial class PetManager
{
    /// <summary>The desktop left over once every window is subtracted, physical px.</summary>
    private readonly List<Rect> _exposed = new();

    /// <summary>Fingerprint of the window layout the current mask was built for.</summary>
    private int _maskSig = int.MinValue;

    private bool _behindHeld, _behindTrace;

    /// <summary>
    /// Rebuild the mask that deep animals are drawn inside, but only when the
    /// windows have actually moved.
    ///
    /// The subtraction is cheap -- a couple of dozen rectangles -- but it would
    /// run thirty times a second all day next to a browser, and on almost every
    /// one of those ticks the layout says exactly what it said before:
    /// WindowTracker re-reads the rects every tick precisely because they
    /// normally do not change. So the work hangs off a fingerprint of the layout,
    /// and dragging or resizing a window is the only thing that pays for it.
    /// </summary>
    private void UpdateWindowMask()
    {
        try
        {
            int sig = HashCode.Combine(_world.WindowRects.Count,
                (int)_world.VirtualScreen.Width, (int)_world.VirtualScreen.Height);
            foreach (var r in _world.WindowRects)
                sig = HashCode.Combine(sig, (int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height);
            if (sig == _maskSig) return;
            _maskSig = sig;

            Occlusion.Exposed(_world.VirtualScreen, _world.WindowRects, _exposed);
            _renderer.SetWindowMask(_exposed, _world.WindowRects.Count > 0);

            if (_behindTrace)
                Log($"masker: {_world.WindowRects.Count} vensters, {_exposed.Count} stukken " +
                    $"vrij water, {Occlusion.ExposedFraction(_world.VirtualScreen, _exposed) * 100:F0}% " +
                    "van het scherm vrij");
        }
        catch (Exception ex)
        {
            // No mask is a working tank: deep animals swim in front of your
            // windows, exactly as they did before any of this existed.
            _renderer.SetWindowMask(_exposed, anyWindows: false);
            Log($"masker: {ex.Message}");
        }
    }

    /// <summary>
    /// Advance whether this animal is behind your windows, for this frame.
    ///
    /// Three things keep it in front however deep it is: it is holding on to one
    /// of your window edges (it is on top of that window by definition), you are
    /// dragging it (then you need to see what you have hold of), or it is dying
    /// or being eaten -- the two scenes in this tank that should never happen
    /// invisibly behind a browser.
    /// </summary>
    private void UpdateOcclusion(Pet pet, double dt)
    {
        try
        {
            bool was = pet.BehindWindows;
            bool held = pet.Machine.Current is DragBehavior;
            bool onYourWindow = pet.Surface is { } s &&
                s.Kind is SurfaceKind.WindowTop or SurfaceKind.WindowLeft or SurfaceKind.WindowRight;

            Occlusion.Tick(pet.Behind, pet.Depth,
                mustBeFront: onYourWindow ||
                             pet.Machine.Current is DyingBehavior or EatenBehavior,
                urgent: held,
                touching: Occlusion.Touches(pet.Bounds, _world.WindowRects),
                dt: dt);

            if (_behindTrace && pet.BehindWindows != was)
                Log($"#{pet.Id} {(pet.BehindWindows ? "achter" : "voor")} de vensters, " +
                    $"d={pet.Depth:F2} op {pet.Pos.X:F0},{pet.Pos.Y:F0}, " +
                    $"dekking {pet.BehindFade:F2}");
        }
        catch (Exception ex)
        {
            pet.Behind.Behind = false;
            pet.Behind.Switching = 0;
            pet.Behind.Opacity = 1;
            Log($"occlusie #{pet.Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Debug: push the whole tank to the back and hold it there, so every animal
    /// is one that belongs behind your windows. Press again to let them go.
    ///
    /// Same reason as every other command here: a third of the tank is behind
    /// your windows at any time by design, but *which* third drifts on a clock of
    /// tens of seconds, and whether a given animal disappears under the browser
    /// you happen to be looking at is not something to sit and wait for. Traced
    /// while held -- every switch and every mask rebuild goes in the log, because
    /// a body clipped at a window edge is the one thing a screenshot cannot
    /// settle by eye: the cut is supposed to look exactly like the window's own
    /// edge, and "exactly" is a pixel column, not an impression.
    /// </summary>
    public void CycleBehind()
    {
        try
        {
            _behindHeld = !_behindHeld;
            _behindTrace = _behindHeld;

            if (_behindHeld)
            {
                // 0.12 rather than 0: the back of the tank is the haze, and an
                // animal washed out to nothing says nothing about whether the
                // clip lands in the right place.
                foreach (var p in _pets)
                {
                    p.Deep.Now = p.Deep.Target = 0.12;
                    p.Deep.ChangeIn = 600;
                }
            }
            else
            {
                foreach (var p in _pets) p.Deep.ChangeIn = _rng.NextDouble() * 4;
            }

            // The mask is cached on the window layout, which has not changed --
            // force one rebuild so the trace prints what the tank is looking at.
            _maskSig = int.MinValue;
            UpdateWindowMask();
            foreach (var p in _pets) _renderer.Update(p);

            Log($"achter de vensters: {(_behindHeld ? "vastgezet" : "losgelaten")}, " +
                string.Join(", ", _pets.OrderBy(p => p.Depth).Select(p =>
                    $"#{p.Id} d={p.Depth:F2} {(p.BehindWindows ? "achter" : "voor")}" +
                    $"{(Occlusion.Touches(p.Bounds, _world.WindowRects) ? " (op een venster)" : "")}")));
        }
        catch (Exception ex) { Log($"achter de vensters: {ex.Message}"); }
    }
}
