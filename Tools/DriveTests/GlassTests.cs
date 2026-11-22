using System.Windows;
using CuttlefishPet.Core;

/// <summary>
/// The hunter and the edges of the screen. The bug this is here for happened once
/// in a traced live run and cost fifteen seconds of empty stage: a shark missed a
/// dive at an animal near the floor, carried the burst through the bottom of the
/// screen, and crossed the rest of the visit at Y=1344 on a screen 1080 tall.
/// Watching for another one would mean a visit every eight to twenty minutes and a
/// steep dive in maybe one of three, so the whole thing is driven here instead --
/// and the live case got a debug command of its own (`dive`) rather than a wait.
/// </summary>
internal static class GlassTests
{
    /// <summary>
    /// His screen, and the one the bug was measured on. Top-left at the origin,
    /// so a y of 1344 in the log reads straight across to a y here.
    /// </summary>
    private static readonly Rect Tank = new(0, 0, 1920, 1080);

    // Mirrors Predator.Cruise and Predator.Burst, as Program.cs already does:
    // Predator.cs drags in Pet and a WPF Image and cannot be compiled into a
    // console runner. Only the two speeds matter here, and if either moves, the
    // numbers in the checks below are the thing that should start failing.
    private const double Cruise = 305;
    private const double Burst = 760;

    public static void Run(Action<string, bool, string> check)
    {
        const double tick = 1.0 / 30;

        // ---- inside the glass it does nothing at all ----
        // This is the half that is easy to get wrong in the other direction: a
        // containment that is always pulling a little turns every crossing into a
        // fish gliding towards the middle of the screen.
        double worst = 0;
        for (double y = Glass.Head; y <= 1080 - Glass.Foot; y += 7)
            worst = Math.Max(worst, Math.Abs(Glass.Return(y, Tank)));
        check("a hunter with its whole body in frame is left alone",
              worst == 0 && !Glass.Clipped(540, Tank), $"{worst:F3}px/s over de hele band");

        // ---- and outside it, it turns back towards the water ----
        double low = Glass.Return(1080 - Glass.Foot + 40, Tank);
        double lower = Glass.Return(1075, Tank);
        double high = Glass.Return(Glass.Head - 40, Tank);
        check("below the floor it climbs, above the ceiling it pushes down",
              low < 0 && high > 0 && lower < low,
              $"net te laag {low:F0}, veel te laag {lower:F0}, te hoog {high:F0} px/s");
        check("and never harder than the climb it is allowed",
              Math.Abs(Glass.Return(1080, Tank)) <= Glass.Home + 1e-9 &&
              Math.Abs(Glass.Return(0, Tank)) <= Glass.Home + 1e-9,
              $"{Glass.Return(1080, Tank):F0} px/s aan de bodem, limiet {Glass.Home:F0}");

        // ---- the hard line holds, and turns the speed round rather than keeping it ----
        var pos = new Point(900, 1200);
        var vel = new Vector(0, Burst);
        Glass.Hold(ref pos, ref vel, Tank);
        check("the anchor never leaves the glass, and the dive is spent there",
              Math.Abs(pos.Y - Tank.Bottom) < 1e-9 && vel.Y < 0 &&
              Math.Abs(vel.Y) < Burst * 0.3,
              $"y {pos.Y:F0}, nog {vel.Y:F0}px/s");

        // ---- the regression: 21 Nov, replayed ----
        // The integration is the one in PetManager.TickPredatorCore: the cruise
        // branch leans the velocity towards (straight, wander + Glass.Return) on a
        // 1.2 ease, the step is scaled by depth, and Glass.Hold has the last word.
        // Mirrored rather than called because TickPredatorCore needs a tank, a
        // renderer and thirteen cuttlefish; the four lines that decide this are
        // these four.
        pos = new Point(1500, 1344);             // exactly where it was found
        vel = new Vector(-Cruise, Burst * 0.55); // still carrying the dive down
        // deepest starts at the top of the tank, not at pos.Y: the starting point
        // is the bug being replayed and counting it would make the check pass or
        // fail on its own input. What is being measured is where the containment
        // lets it get to from there.
        double backInside = -1, deepest = Tank.Top, age = 0;
        for (int i = 0; i < 30 * 20; i++)
        {
            age += tick;
            double straight = -Cruise;
            double level = Math.Sin(age * 0.7) * 26 + Glass.Return(pos.Y, Tank);
            vel += (new Vector(straight, level) - vel) * Math.Min(1, tick * 1.2);
            pos += vel * tick * Approach.SpeedAt(1);
            Glass.Hold(ref pos, ref vel, Tank);
            deepest = Math.Max(deepest, pos.Y);
            if (backInside < 0 && !Glass.Clipped(pos.Y, Tank)) backInside = age;
        }
        check("a hunter dropped below the screen climbs back into frame",
              backInside > 0 && backInside < 6,
              $"helemaal in beeld na {backInside:F1}s");
        check("and is never allowed that far out in the first place",
              deepest <= Tank.Bottom + 1e-9, $"diepste punt y={deepest:F0} van {Tank.Bottom:F0}");

        // The same from the other edge: the ceiling is the one the waterline and
        // the light shafts live at, so a hunter pinned there is more visible than
        // one pinned at the floor, not less.
        pos = new Point(400, -220);
        vel = new Vector(Cruise, -Burst * 0.55);
        // Its own clock. Sharing `age` across the scenarios is what made the first
        // run of this file fail: the second loop started at twenty seconds, so the
        // time-to-recover it reported was the time on the wall and not the time it
        // took, and a perfectly healthy recovery read as a failure.
        double climbedOut = -1;
        age = 0;
        for (int i = 0; i < 30 * 20; i++)
        {
            age += tick;
            double level = Math.Sin(age * 0.7) * 26 + Glass.Return(pos.Y, Tank);
            vel += (new Vector(Cruise, level) - vel) * Math.Min(1, tick * 1.2);
            pos += vel * tick * Approach.SpeedAt(1);
            Glass.Hold(ref pos, ref vel, Tank);
            if (climbedOut < 0 && !Glass.Clipped(pos.Y, Tank)) climbedOut = age;
            if (pos.Y < Tank.Top - 1e-9) { climbedOut = -99; break; }
        }
        check("and the same above the waterline", climbedOut > 0 && climbedOut < 6,
              climbedOut < 0 ? "boven het glas uit"
                             : $"helemaal in beeld na {climbedOut:F1}s, y={pos.Y:F0}");

        // ---- it settles instead of oscillating ----
        // A climb that overshoots the band and then has to come back down is a fish
        // porpoising along the bottom of the screen, which is worse to look at than
        // the bug: the sine wander is the only thing that should still be moving.
        pos = new Point(960, 1070);
        vel = new Vector(Cruise, 0);
        double top = double.MaxValue, bottom = 0;
        for (int i = 0; i < 30 * 30; i++)
        {
            age += tick;
            double level = Math.Sin(age * 0.7) * 26 + Glass.Return(pos.Y, Tank);
            vel += (new Vector(Cruise, level) - vel) * Math.Min(1, tick * 1.2);
            pos += vel * tick * Approach.SpeedAt(1);
            Glass.Hold(ref pos, ref vel, Tank);
            if (i > 30 * 8) { top = Math.Min(top, pos.Y); bottom = Math.Max(bottom, pos.Y); }
        }
        check("it levels off inside the glass rather than bobbing against it",
              !Glass.Clipped(pos.Y, Tank) && bottom - top < 260,
              $"na 30s y={pos.Y:F0}, laatste 22s binnen {bottom - top:F0}px");

        // ---- nothing bad escapes towards a Canvas offset ----
        // Same rule as everywhere else in here: the hunter is drawn on the UI
        // thread and a NaN reaching a transform is what has killed the tank before.
        var junkPos = new Point(900, double.NaN);
        var junkVel = new Vector(0, double.NaN);
        Glass.Hold(ref junkPos, ref junkVel, Tank);
        double junkReturn = Glass.Return(double.NaN, Tank);
        double emptyTank = Glass.Return(540, new Rect(0, 0, 0, 0));
        var tinyPos = new Point(10, 90);
        var tinyVel = new Vector(0, 300);
        Glass.Hold(ref tinyPos, ref tinyVel, new Rect(0, 0, 200, 120));
        check("rubbish numbers are put back rather than drawn",
              double.IsFinite(junkPos.Y) && double.IsFinite(junkVel.Y) &&
              junkReturn == 0 && emptyTank == 0 &&
              double.IsFinite(tinyPos.Y) && double.IsFinite(tinyVel.Y),
              $"y {junkPos.Y:F0}, snelheid {junkVel.Y:F0}, lege tank {emptyTank:F0}");

        // A screen shorter than Head + Foot would invert the band and make every y
        // both too high and too low, so the margins shrink with the glass. 120px
        // tall is not a real screen; a 180px-high strip of a multi-monitor desktop
        // where the taskbar eats the rest is, and it goes through the same code.
        var strip = new Rect(0, 0, 1920, 180);
        bool middleIsFine = !Glass.Clipped(90, strip) && Glass.Return(90, strip) == 0;
        check("a short screen shrinks the margins instead of inverting them",
              middleIsFine && Glass.Return(5, strip) > 0 && Glass.Return(175, strip) < 0,
              $"midden {Glass.Return(90, strip):F0}, boven {Glass.Return(5, strip):F0}, " +
              $"onder {Glass.Return(175, strip):F0} px/s");
    }
}
