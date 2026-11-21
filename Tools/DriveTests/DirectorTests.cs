using System.Windows;
using CuttlefishPet.Core;

/// <summary>
/// The stage: one thing at a time, a quiet afterwards, and a heartbeat. A day of
/// it in about a second, which is the only way to find out whether a tank left
/// alone for an hour gets anything at all.
/// </summary>
internal static class DirectorTests
{
    public static void Run(Action<string, bool, string> check)
    {
        const double tick = 1.0 / 30;
        var rng = new Random(20261121);
        var nowhere = new Point(double.NaN, double.NaN);

        // ---- one at a time ----
        var s = new StageState();
        check("an empty stage lets something start", Director.MayStart(s), "");
        Director.Started(s, "roofdier");
        check("and then nothing else may", !Director.MayStart(s) && Director.Pace(s) == 0,
              $"pace {Director.Pace(s):F1}");

        // Clocks hold where they are rather than running out behind the curtain:
        // the pile-up is the thing this exists to prevent.
        double other = 30;
        for (int i = 0; i < 30 * 120; i++)
        {
            other -= tick * Director.Pace(s);
            Director.Tick(s, tick, nowhere, false);
        }
        check("another event's clock holds while the stage is busy",
              Math.Abs(other - 30) < 1e-9, $"{other:F2}s over");

        // ---- the quiet afterwards is required ----
        Director.Ended(s, "roofdier");
        check("the stage clears but the quiet holds it shut",
              s.OnStage == null && !Director.MayStart(s), $"{s.RestFor:F0}s rust");
        double waited = 0;
        while (!Director.MayStart(s) && waited < 600) { Director.Tick(s, tick, nowhere, false); waited += tick; }
        check("and it lasts about as long as it says",
              Math.Abs(waited - Director.Rest) < 1.0, $"{waited:F1}s tegen {Director.Rest:F0}s");

        // ---- a straggler must not clear somebody else's act ----
        var mix = new StageState();
        Director.Started(mix, "ammoniet");
        Director.Ended(mix, "roofdier");
        check("a late report from a different act clears nothing",
              mix.OnStage == "ammoniet", $"{mix.OnStage}");

        // ---- the watchdog: the one failure worse than no director ----
        var stuck = new StageState();
        Director.Started(stuck, "ritueel");
        string? freed = null;
        for (int i = 0; i < 30 * 400 && freed == null; i++)
            freed = Director.Tick(stuck, tick, nowhere, false);
        check("an act that never reports its end is taken off anyway",
              freed == "ritueel" && stuck.OnStage == null,
              $"na {Director.MaxAct:F0}s losgetrokken: {freed}");

        // ---- the heartbeat ----
        var dead = new StageState();
        double quiet = 0;
        while (!Director.Overdue(dead) && quiet < 2000) { Director.Tick(dead, tick, nowhere, false); quiet += tick; }
        check("a tank where nothing happens starts hurrying its clocks",
              Director.Overdue(dead) && Math.Abs(quiet - Director.Heartbeat) < 2 &&
              Math.Abs(Director.Pace(dead) - Director.Hurry) < 0.05,
              $"na {quiet:F0}s, tempo x{Director.Pace(dead):F1}");
        for (int i = 0; i < 30 * 1800; i++) Director.Tick(dead, tick, nowhere, false);
        check("and keeps hurrying harder, up to a ceiling",
              Math.Abs(Director.Pace(dead) - Director.MaxHurry) < 1e-9,
              $"na een half uur stilte x{Director.Pace(dead):F1}");
        Director.Noted(dead);
        check("and a passing school counts as something happening",
              !Director.Overdue(dead) && Director.Pace(dead) == 1, "");

        // ---- a day of it: the tank is neither crowded nor dead ----
        // Two clocks of the kind the tank really has, driven through the director.
        var day = new StageState();
        double hunterIn = 600, ritualIn = 1500;
        int acts = 0, overlaps = 0;
        double longestGap = 0, sinceLast = 0;
        for (int i = 0; i < 30 * 3600 * 6; i++)   // six hours
        {
            double pace = Director.Pace(day);
            hunterIn -= tick * pace;
            ritualIn -= tick * pace;
            sinceLast += tick;
            if (hunterIn <= 0 && Director.MayStart(day))
            {
                if (day.OnStage != null) overlaps++;
                Director.Started(day, "roofdier");
                hunterIn = 480 + rng.NextDouble() * 720;
                acts++; longestGap = Math.Max(longestGap, sinceLast); sinceLast = 0;
            }
            if (ritualIn <= 0 && Director.MayStart(day))
            {
                if (day.OnStage != null) overlaps++;
                Director.Started(day, "ritueel");
                ritualIn = 1500 + rng.NextDouble() * 1800;
                acts++; longestGap = Math.Max(longestGap, sinceLast); sinceLast = 0;
            }
            // Acts run 30 to 70 seconds and report their end.
            if (day.OnStage != null && day.Running > 45) Director.Ended(day, day.OnStage);
            Director.Tick(day, tick, nowhere, false);
        }
        check("six hours: things happen, never two at once, never ten minutes dead",
              overlaps == 0 && acts > 20 && longestGap < 660,
              $"{acts} gebeurtenissen, langste gat {longestGap / 60:F1} min, {overlaps} overlappingen");

        // ---- where you are looking ----
        var eyes = new StageState();
        for (int i = 0; i < 30 * 60; i++) Director.Tick(eyes, tick, new Point(900, 800), true);
        check("the attention point follows the cursor",
              eyes.Watching && Math.Abs(eyes.Attention.Y - 800) < 20, $"{eyes.Attention.Y:F0}");
        for (int i = 0; i < 30 * 60; i++) Director.Tick(eyes, tick, new Point(900, 100), false);
        check("and ignores a pointer nobody is touching",
              Math.Abs(eyes.Attention.Y - 800) < 20, $"{eyes.Attention.Y:F0}");

        double lowest = 0, highest = 2000, worstOff = 0;
        for (int i = 0; i < 400; i++)
        {
            double y = Director.StageY(eyes, rng, 160, 940);
            lowest = Math.Max(lowest, y); highest = Math.Min(highest, y);
            worstOff = Math.Max(worstOff, Math.Abs(y - eyes.Attention.Y));
        }
        check("events are staged near it, and always inside the tank",
              highest >= 160 && lowest <= 940 && worstOff <= Director.StageWithin + 1,
              $"{highest:F0}..{lowest:F0}, hoogstens {worstOff:F0}px van het oog");

        var blind = new StageState();
        bool spread = false;
        double first = Director.StageY(blind, rng, 160, 940);
        for (int i = 0; i < 200; i++)
            if (Math.Abs(Director.StageY(blind, rng, 160, 940) - first) > 300) spread = true;
        check("with no cursor to go on it uses the whole tank", spread, "");

        // ---- rubbish numbers ----
        var junk = new StageState { RestFor = double.NaN, Quiet = double.NaN, Running = double.NaN };
        Director.Started(junk, "roofdier");
        Director.Tick(junk, double.NaN, nowhere, true);
        Director.Tick(junk, -5, new Point(double.PositiveInfinity, 3), true);
        double y2 = Director.StageY(junk, rng, double.NaN, double.NaN);
        check("rubbish numbers are put back rather than acted on",
              double.IsFinite(junk.RestFor) && double.IsFinite(junk.Quiet) &&
              double.IsFinite(junk.Running) && double.IsFinite(y2) &&
              double.IsFinite(junk.Attention.X) && double.IsFinite(junk.Attention.Y),
              $"rust {junk.RestFor:F2}, stil {junk.Quiet:F2}, oog {junk.Attention.X:F0},{junk.Attention.Y:F0}");

        // ---- a summons never waits ----
        var busy = new StageState();
        Director.Started(busy, "roofdier");
        Director.Ended(busy, "roofdier");
        Director.Clear(busy);
        check("a debug summons never waits for the stage", Director.MayStart(busy), "");
    }
}