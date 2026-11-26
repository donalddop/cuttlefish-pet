using System.Windows;
using CuttlefishPet.Core;

/// <summary>
/// Animals behind your windows. Three halves, all of which can be wrong in ways a
/// screenshot will not show you: which desktop is left over (rectangle
/// subtraction, whose failure mode is a hole punched back open exactly where two
/// windows overlap), which animal belongs behind (a depth threshold with a band
/// on it), and how it gets from one side to the other without ever being seen to
/// pop.
/// </summary>
internal static class OcclusionTests
{
    public static void Run(Action<string, bool, string> check)
    {
        var screen = new Rect(0, 0, 1920, 1080);

        // ---- the exposed desktop ----

        var none = new List<Rect>();
        var free = new List<Rect>();
        Occlusion.Exposed(screen, none, free);
        check("with nothing open the whole screen is water",
              free.Count == 1 && free[0] == screen,
              $"{free.Count} stuk(ken), {Occlusion.ExposedFraction(screen, free) * 100:F0}%");

        // One window in the middle: four bands around it, and the area has to add
        // up. The area is the real test -- four bands that overlapped would also
        // report "four pieces".
        var one = new List<Rect> { new(400, 200, 800, 600) };
        Occlusion.Exposed(screen, one, free);
        double want = 1 - (800.0 * 600) / (1920.0 * 1080);
        check("a window in the middle leaves the desktop around it",
              Math.Abs(Occlusion.ExposedFraction(screen, free) - want) < 1e-9 && free.Count == 4,
              $"{free.Count} stukken, {Occlusion.ExposedFraction(screen, free) * 100:F1}% vrij");

        check("and no two pieces of that water overlap", Disjoint(free), $"{free.Count} stukken");

        // The one that matters: two windows that overlap each other. Subtracted
        // under an even-odd fill the overlap would be counted twice and handed
        // back as visible water, so a cuttlefish would show through the seam
        // between two of your windows -- the most obviously wrong thing this
        // could do.
        var overlapping = new List<Rect> { new(300, 200, 700, 500), new(800, 400, 700, 500) };
        Occlusion.Exposed(screen, overlapping, free);
        bool seamClosed = true;
        foreach (var w in overlapping)
            foreach (var r in free)
                if (r.IntersectsWith(w) && Rect.Intersect(r, w) is { Width: > 0.001, Height: > 0.001 })
                    seamClosed = false;
        check("two overlapping windows leave no seam to swim through",
              seamClosed && Disjoint(free),
              $"{free.Count} stukken, {Occlusion.ExposedFraction(screen, free) * 100:F1}% vrij");

        var maximised = new List<Rect> { screen };
        Occlusion.Exposed(screen, maximised, free);
        check("a maximised window leaves nothing", free.Count == 0, $"{free.Count} stukken");

        // Cost: the fragment count is what the renderer fills, every frame, for
        // every animal behind the windows. Twenty-four windows is the cap and
        // four fragments per window the worst case, so this cannot run away on a
        // desktop with fifty things open.
        var many = new List<Rect>();
        var rng = new Random(7);
        for (int i = 0; i < 60; i++)
            many.Add(new Rect(rng.Next(0, 1700), rng.Next(0, 900), rng.Next(160, 700), rng.Next(120, 500)));
        Occlusion.Exposed(screen, many, free);
        check("a crowded desktop stays bounded and disjoint",
              free.Count <= Occlusion.MaxWindows * 4 && Disjoint(free),
              $"{free.Count} stukken uit 60 vensters (cap {Occlusion.MaxWindows})");

        check("a window whose edge touches the body hides nothing",
              !Occlusion.Touches(new Rect(100, 100, 80, 80), new List<Rect> { new(180, 100, 400, 400) }),
              "rand op rand");
        check("and one that overlaps it does",
              Occlusion.Touches(new Rect(100, 100, 80, 80), new List<Rect> { new(170, 100, 400, 400) }),
              "10px overlap");

        // ---- who belongs behind ----

        check("deep animals belong behind the windows",
              Occlusion.Next(false, 0.20, false), $"drempel {Occlusion.BehindUntil:F2}");
        check("animals near the glass do not",
              !Occlusion.Next(false, 0.75, false), "d=0,75");
        check("an animal holding one of your window edges never does",
              !Occlusion.Next(true, 0.05, mustBeFront: true), "op een vensterrand");
        check("rubbish depth is drawn in front rather than nowhere",
              !Occlusion.Next(true, double.NaN, false) && !Occlusion.Next(false, double.NaN, false),
              "NaN");

        // Hysteresis: a target parked on the line must not flicker. Depth eases
        // over seconds, so without the band an animal sitting at 0.40 would cross
        // it back and forth on rounding alone.
        bool state = Occlusion.Next(false, 0.395, false);
        bool still = Occlusion.Next(state, 0.41, false);
        check("an animal parked on the line stays where it is",
              state && still, $"0,395 -> achter, 0,41 -> {(still ? "achter" : "voor")}");
        check("and only comes forward past the band",
              !Occlusion.Next(still, 0.47, false), "0,47 -> voor");

        // ---- and how it changes sides ----

        const double tick = 1.0 / 30;

        // In clear water the switch is free: one tick, no fade, nothing to see.
        var clear = new BehindState();
        Occlusion.Tick(clear, 0.05, false, false, touching: false, dt: tick);
        check("in clear water it switches at once and at full opacity",
              clear.Behind && clear.Opacity == 1 && clear.Switching == 0,
              $"achter, dekking {clear.Opacity:F2}");

        // Over a window it fades out, flips at the trough and fades back in. The
        // test that matters is the trough: the flip has to land where the body is
        // at its faintest, not at either end.
        var over = new BehindState();
        double lowest = 1, atFlip = -1;
        bool flipped = false;
        for (int i = 0; i < 60; i++)
        {
            bool before = over.Behind;
            Occlusion.Tick(over, 0.05, false, false, touching: true, dt: tick);
            if (over.Behind != before) { flipped = true; atFlip = over.Opacity; }
            lowest = Math.Min(lowest, over.Opacity);
        }
        check("over a window it fades through the switch instead of winking",
              flipped && over.Behind && over.Switching == 0 && over.Opacity == 1,
              $"na 2s: achter, dekking {over.Opacity:F2}");
        check("and the switch lands at the bottom of that fade",
              atFlip >= 0 && atFlip < 0.1 && lowest < 0.05,
              $"dekking bij de wissel {atFlip:F3}, laagste {lowest:F3}");

        // How long it takes, measured rather than assumed: a fade this project
        // can see is the whole reason the dip exists.
        var timed = new BehindState();
        int ticks = 0;
        while (ticks < 200)
        {
            Occlusion.Tick(timed, 0.05, false, false, touching: true, dt: tick);
            ticks++;
            if (timed.Switching == 0 && timed.Behind) break;
        }
        check("the fade is long enough to read as a fade",
              ticks * tick > 0.55 && ticks * tick < 0.80,
              $"{ticks * tick:F2}s heen en terug (2 x {Occlusion.Dip:F2})");

        // Dragged out from behind a browser: instant, because the pop is the
        // user's own doing and they need to see what they have hold of.
        var held = new BehindState { Behind = true };
        Occlusion.Tick(held, 0.05, mustBeFront: true, urgent: true, touching: true, dt: tick);
        check("one you are dragging comes out from behind the window at once",
              !held.Behind && held.Opacity == 1 && held.Switching == 0, "in de hand");

        // A settled animal costs nothing and stays at full opacity forever.
        var settled = new BehindState();
        for (int i = 0; i < 300; i++)
            Occlusion.Tick(settled, 0.90, false, false, touching: true, dt: tick);
        check("an animal that belongs where it is never fades at all",
              !settled.Behind && settled.Opacity == 1 && settled.Switching == 0,
              "10s in front of a window");

        // Rubbish time steps must not strand an animal halfway through a fade.
        var odd = new BehindState();
        Occlusion.Tick(odd, 0.05, false, false, true, double.NaN);
        for (int i = 0; i < 60; i++) Occlusion.Tick(odd, 0.05, false, false, true, tick);
        check("a bad time step does not strand it mid-fade",
              odd.Behind && odd.Opacity == 1 && odd.Switching == 0, "NaN dt");

        var empty = new List<Rect>();
        Occlusion.Exposed(new Rect(0, 0, 0, 0), one, empty);
        check("an empty screen produces no water instead of throwing", empty.Count == 0, "0x0");
    }

    private static bool Disjoint(List<Rect> rects)
    {
        for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i];
                var b = rects[j];
                if (a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom)
                    return false;
            }
        return true;
    }
}
