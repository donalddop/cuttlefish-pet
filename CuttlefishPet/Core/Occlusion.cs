using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>
/// One animal's side of the glass: behind your windows, or in front of them, and
/// the fade it is in the middle of if it is changing sides.
///
/// A separate little object for the same reason <see cref="DepthState"/> is one:
/// it can be driven for an hour in DriveTests with no screen anywhere near it.
/// </summary>
public sealed class BehindState
{
    /// <summary>Drawn behind your windows: clipped away wherever one covers the desktop.</summary>
    public bool Behind;

    /// <summary>Seconds left of the fade it is switching through. 0 when settled.</summary>
    public double Switching;

    /// <summary>The side it is fading toward, while <see cref="Switching"/> runs.</summary>
    public bool Want;

    /// <summary>Opacity multiplier for the body, 1 unless it is mid-switch.</summary>
    public double Opacity = 1;
}

/// <summary>
/// Which animals swim *behind* your windows, and which strip of desktop is left
/// over for them to be seen in.
///
/// The overlay is one topmost, per-pixel-transparent window over the whole
/// virtual screen (see <see cref="Rendering.OverlayWindow"/>), so an animal can
/// never really be put between two of your application windows in the z-order.
/// What it can be is *not drawn* where a window is. On screen those two are the
/// same thing: the animal disappears at the edge of the browser and comes back
/// out the other side, which is what being behind it looks like.
///
/// Everything here is arithmetic over rectangles and one depth number, with no
/// reference to a visual or a window handle, for the same reason
/// <see cref="Depths"/> and <see cref="Glass"/> are shaped this way: the whole
/// decision can then be driven in DriveTests without a screen.
/// </summary>
public static class Occlusion
{
    /// <summary>
    /// Depth below which an animal belongs behind the windows. 0 is the back of
    /// the haze, 1 is against the glass.
    ///
    /// 0.40 rather than the halfway mark: <see cref="Depths.Roll"/> is biased
    /// toward the front on purpose (the back of the tank is scenery), so 0.40
    /// puts roughly a third of the tank behind your windows at any time. Higher
    /// empties the visible tank whenever a maximised window is up, which is the
    /// opposite of what this is for.
    /// </summary>
    public const double BehindUntil = 0.40;

    /// <summary>
    /// How much further forward it has to come before it is in front again.
    /// Depth eases over seconds (<see cref="Depths.Ease"/>), so an animal
    /// holding a target near the line would otherwise sit on the boundary and
    /// flick in and out of existence every few ticks.
    /// </summary>
    public const double Hysteresis = 0.06;

    /// <summary>
    /// Half of the fade a forced switch is made through: out over this long, the
    /// side flips at the trough, back in over the same again.
    ///
    /// This exists because of a live measurement, not by design. The first
    /// version only ever switched sides while no window overlapped the body --
    /// the one moment a switch cannot be seen. On the desktop it was built for,
    /// two windows covered 100% of the screen (a maximised browser), so no animal
    /// was ever clear of a window and not one of fifteen ever went behind
    /// anything. A gate that is never open is not a safeguard, it is an off
    /// switch. So the gate stays for the free case, and everything else fades.
    ///
    /// 0.34s each way, because this project has already learned that the visible
    /// range of a fade is in the time and not in the numbers: the hunter's
    /// 0.19s fade read as a cut between two frames. Two tenths is a wink; most of
    /// a second reads as sinking into the haze, which is what it is.
    /// </summary>
    public const double Dip = 0.34;

    /// <summary>
    /// How many windows are subtracted, topmost first. Each one can split every
    /// existing fragment into four, so the fragment count is bounded by this and
    /// not by how many windows the desktop happens to have open. Anything past
    /// the limit is ignored, which can only ever leave an animal visible where it
    /// should have been hidden -- never the other way round, and never slow.
    /// </summary>
    public const int MaxWindows = 24;

    /// <summary>The raw want, by depth alone, with the hysteresis band applied.</summary>
    public static bool WantsBehind(double depth, bool wasBehind)
    {
        if (!double.IsFinite(depth)) return false;
        return depth < (wasBehind ? BehindUntil + Hysteresis : BehindUntil);
    }

    /// <summary>
    /// The decision, with no fade in it: where this animal belongs, given where
    /// it was. Kept separate from <see cref="Tick"/> so the rule can be read and
    /// tested on its own.
    /// </summary>
    /// <param name="mustBeFront">
    /// It is holding on to something of yours (a window ledge, a window edge): it
    /// is on top of that window by definition, so it cannot be behind it. Also
    /// true for the one scene that must never be hidden behind a browser -- an
    /// animal dying.
    /// </param>
    public static bool Next(bool wasBehind, double depth, bool mustBeFront) =>
        !mustBeFront && WantsBehind(depth, wasBehind);

    /// <summary>
    /// Advance one animal's side of the glass.
    ///
    /// Three ways a switch can happen, and the order matters:
    ///
    /// 1. Clear of every window: switch at once. Nothing is covering the animal,
    ///    so there is no seam for the change to show up in -- this is the free
    ///    case and it is how most switches happen.
    /// 2. You are dragging it: switch at once as well. The pop is yours; you are
    ///    pulling the animal out from behind a window and you need to see what
    ///    you have hold of.
    /// 3. Overlapping a window: fade out, flip at the trough, fade back in. An
    ///    animal that simply winked out halfway across your browser would read as
    ///    a rendering fault -- the same "ghost beside a solid animal" the hunter's
    ///    approach ran into. Fading in and out of the haze is a thing water does.
    /// </summary>
    /// <returns>True while a fade is running, for the log.</returns>
    public static bool Tick(BehindState s, double depth, bool mustBeFront, bool urgent,
                            bool touching, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) dt = 0;

        if (s.Switching > 0)
        {
            s.Switching -= dt;
            // Flip at the trough, where the body is at its most transparent and
            // the clip appearing or disappearing costs the least.
            if (s.Switching <= Dip) s.Behind = s.Want;
            if (s.Switching <= 0)
            {
                s.Switching = 0;
                s.Opacity = 1;
                return false;
            }
            // Triangle: 1 at both ends of the fade, 0 at the trough.
            s.Opacity = Math.Clamp(Math.Abs(1 - s.Switching / Dip), 0, 1);
            return true;
        }

        s.Opacity = 1;
        bool want = Next(s.Behind, depth, mustBeFront);
        if (want == s.Behind) return false;

        if (urgent || !touching)
        {
            s.Behind = want;
            return false;
        }

        s.Want = want;
        s.Switching = 2 * Dip;
        return true;
    }

    /// <summary>Does any part of this body overlap any window?</summary>
    public static bool Touches(Rect body, IReadOnlyList<Rect> windows)
    {
        if (body.IsEmpty) return false;
        int n = Math.Min(windows.Count, MaxWindows);
        for (int i = 0; i < n; i++)
            if (Overlaps(body, windows[i])) return true;
        return false;
    }

    /// <summary>
    /// The desktop that is left over: the screen minus every window, as a set of
    /// disjoint rectangles. Disjoint matters -- these become one fill geometry,
    /// and overlapping pieces under an even-odd rule would punch holes back open
    /// exactly where two of your windows overlap.
    /// </summary>
    public static void Exposed(Rect screen, IReadOnlyList<Rect> windows, List<Rect> output)
    {
        output.Clear();
        if (screen.Width <= 0 || screen.Height <= 0) return;
        output.Add(screen);

        var next = new List<Rect>(output.Count + 4);
        int n = Math.Min(windows.Count, MaxWindows);
        for (int i = 0; i < n; i++)
        {
            var cut = windows[i];
            if (cut.Width <= 0 || cut.Height <= 0) continue;
            next.Clear();
            foreach (var r in output) SubtractInto(r, cut, next);
            output.Clear();
            output.AddRange(next);
            if (output.Count == 0) return;   // fully covered: nothing left to cut
        }
    }

    /// <summary>How much of the screen is still visible, 0..1. For the log.</summary>
    public static double ExposedFraction(Rect screen, IReadOnlyList<Rect> pieces)
    {
        double whole = screen.Width * screen.Height;
        if (whole <= 0) return 0;
        double sum = 0;
        foreach (var r in pieces) sum += r.Width * r.Height;
        return Math.Clamp(sum / whole, 0, 1);
    }

    /// <summary>
    /// Two rects overlap with area to spare. Deliberately strict inequalities:
    /// WPF's own IntersectsWith counts a shared edge as an intersection, and a
    /// window whose edge exactly touches the body hides nothing.
    /// </summary>
    private static bool Overlaps(Rect a, Rect b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    /// <summary>
    /// r minus cut, as up to four disjoint bands: the full-width strip above the
    /// cut, the full-width strip below it, and the two side strips beside it.
    /// Bands rather than a general polygon because a window is a rectangle and
    /// four rectangles are something a renderer can fill without building a path.
    /// </summary>
    private static void SubtractInto(Rect r, Rect cut, List<Rect> output)
    {
        if (!Overlaps(r, cut)) { output.Add(r); return; }

        double top = Math.Max(r.Top, cut.Top);
        double bottom = Math.Min(r.Bottom, cut.Bottom);
        double left = Math.Max(r.Left, cut.Left);
        double right = Math.Min(r.Right, cut.Right);

        if (top > r.Top) output.Add(new Rect(r.Left, r.Top, r.Width, top - r.Top));
        if (bottom < r.Bottom) output.Add(new Rect(r.Left, bottom, r.Width, r.Bottom - bottom));
        if (left > r.Left) output.Add(new Rect(r.Left, top, left - r.Left, bottom - top));
        if (right < r.Right) output.Add(new Rect(right, top, r.Right - right, bottom - top));
    }
}
