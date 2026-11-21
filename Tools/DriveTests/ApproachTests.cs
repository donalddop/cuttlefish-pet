using CuttlefishPet.Core;

/// <summary>
/// The hunter's arrival out of the haze. Worth its own file for the same reason
/// the squid and the arms have one: an arrival happens once every eight to twenty
/// minutes on a live desktop, so the only way to look at a hundred of them is
/// without a screen.
/// </summary>
internal static class ApproachTests
{
    public static void Run(Action<string, bool, string> check)
    {
        const double tick = 1.0 / 30;

        // ---- it arrives faint, small and far back ----
        var fresh = new ApproachState();
        check("a visit starts invisible and resolves out of the haze",
              Approach.Opacity(fresh) < 0.01, $"dekking {Approach.Opacity(fresh):F3} op t=0");
        for (int i = 0; i < (int)(Approach.AppearLen * 30) + 2; i++)
            Approach.Tick(fresh, tick, HuntPhase.Crossing, false, false, 1);
        double arrived = Approach.Opacity(fresh);
        check("and settles well behind and well under anything an animal can be",
              arrived < Depths.OpacityAt(Approach.AnimalFloor) * 0.62 &&
              Approach.ScaleAt(fresh.Depth) < Depths.ScaleAt(Approach.AnimalFloor) * 0.86,
              $"dekking {arrived:F3} tegen {Depths.OpacityAt(Approach.AnimalFloor):F2} " +
              $"voor een dier, x{Approach.ScaleAt(fresh.Depth):F2} tegen " +
              $"x{Depths.ScaleAt(Approach.AnimalFloor):F2}, d={fresh.Depth:F3}");

        // The flaw the first live run showed: a hunter stalking an animal that was
        // loafing at depth 0.29 sat in its plane at half its cover. Inside the
        // range the animals use, the two have to obey one rule.
        bool sameRules = true;
        string worst = "";
        for (double d = Approach.AnimalFloor; d <= 1.0001; d += 0.02)
        {
            double ds = Math.Min(d, 1);
            if (Math.Abs(Approach.ScaleAt(ds) - Depths.ScaleAt(ds)) > 1e-9 ||
                Math.Abs(Approach.FadeAt(ds) - Depths.OpacityAt(ds)) > 1e-9 ||
                Math.Abs(Approach.SpeedAt(ds) - Depths.SpeedAt(ds)) > 1e-9)
            {
                sameRules = false;
                worst = $"d={ds:F2}: x{Approach.ScaleAt(ds):F3} vs x{Depths.ScaleAt(ds):F3}, " +
                        $"dekking {Approach.FadeAt(ds):F3} vs {Depths.OpacityAt(ds):F3}";
                break;
            }
        }
        check("in the water the animals use, a hunter is drawn by their rules",
              sameRules, worst == "" ? "gelijk van 0,18 tot 1,00" : worst);
        check("and behind their floor it keeps going where they cannot",
              Approach.FadeAt(Approach.Far) < Depths.OpacityAt(Approach.AnimalFloor) &&
              Approach.ScaleAt(Approach.Far) < Depths.ScaleAt(Approach.AnimalFloor),
              $"op d={Approach.Far:F2}: dekking {Approach.FadeAt(Approach.Far):F2}, " +
              $"x{Approach.ScaleAt(Approach.Far):F2}");

        // ---- nothing to chase: it crosses deep and stays deep ----
        var idle = new ApproachState();
        for (int i = 0; i < 30 * 60; i++)
            Approach.Tick(idle, tick, HuntPhase.Crossing, false, false, 1);
        check("a hunter with nothing to chase never comes forward",
              Math.Abs(idle.Depth - Approach.Far) < 0.01 && idle.Vanish == 0,
              $"na een minuut d={idle.Depth:F3}");

        // ---- the real thing: a whole hunt, phases driven by Hunt itself ----
        // Both ends of the stalk, because the short one is what the first tuning
        // got wrong: a rise that looks fine over 3.4s strikes from the back of the
        // tank when the quarry happens to be close enough for a 1.5s stalk.
        foreach (double gap0 in new[] { 260.0, 620.0 })
        {
            var a = new ApproachState();
            var h = new HuntState();
            double gap = gap0, quarry = 0.92, atBurst = -1, burstGap = -1;
            for (int i = 0; i < 30 * 12; i++)
            {
                bool committing = Hunt.Advance(h, tick, true, gap);
                if (committing) { atBurst = a.Depth; burstGap = gap; }
                // Closing at the speed the phase asks for, scaled by depth exactly
                // as PetManager scales the step.
                double speed = (h.Phase == HuntPhase.Burst ? 760 : 305 * Hunt.CruiseFactor(h, gap))
                               * Approach.SpeedAt(a.Depth);
                gap = Math.Max(0, gap - speed * tick);
                Approach.Tick(a, tick, h.Phase, true, false, quarry);
                if (atBurst >= 0 && h.Phase == HuntPhase.Recover) break;
            }
            check($"it strikes from the quarry's own plane (stalk from {gap0:F0}px)",
                  atBurst > quarry * 0.86,
                  $"d={atBurst:F3} van quarry {quarry:F2} op {burstGap:F0}px, " +
                  $"x{Approach.ScaleAt(atBurst):F2} dekking={Approach.FadeAt(atBurst):F2}");
        }

        // ---- a quarry at the very back must not make it reverse ----
        check("it never retreats to reach something deeper than itself",
              Approach.Want(HuntPhase.Stalk, true, false, 0.0) >= Approach.Far - 1e-9 &&
              Approach.Want(HuntPhase.Stalk, true, false, -5) >= Approach.Far - 1e-9,
              $"wil {Approach.Want(HuntPhase.Stalk, true, false, 0.0):F3}, bodem {Approach.Far:F2}");

        // ---- leaving: sinks away, then dissolves, and only once it is back there ----
        var going = new ApproachState { Depth = 0.95, Appear = 1 };
        bool fadedInYourFace = false;
        int ticks = 0;
        while (!Approach.Dissolved(going) && ticks < 30 * 40)
        {
            Approach.Tick(going, tick, HuntPhase.Recover, false, true, 1);
            if (going.Vanish > 0 && going.Depth > Approach.VanishBelow + 0.02) fadedInYourFace = true;
            ticks++;
        }
        check("a hunter that is done sinks back and dissolves",
              Approach.Dissolved(going) && ticks < 30 * 30,
              $"weg na {ticks / 30.0:F1}s");
        check("and never dissolves while it is still up against the glass",
              !fadedInYourFace, "begon te verdwijnen boven de drempel");

        // ---- distance costs screen speed, same direction as the animals ----
        check("it covers fewer pixels per second at the back",
              Approach.SpeedAt(0) < Approach.SpeedAt(1) && Approach.SpeedAt(0) > 0.5,
              $"{Approach.SpeedAt(0):F2}x achterin, {Approach.SpeedAt(1):F2}x tegen het glas");

        // ---- a stalled frame must not teleport it forward ----
        var hiccup = new ApproachState();
        Approach.Tick(hiccup, 5.0, HuntPhase.Burst, true, false, 1);
        check("one long frame does not jump it to the glass",
              hiccup.Depth < 0.30, $"d={hiccup.Depth:F3} na een frame van 5s");

        // The two rates are chosen against the shortest window there is; if either
        // end of it is retuned this is the test that should fail.
        var fromTheBack = new ApproachState { Appear = 1 };
        double climbed = 0;
        while (fromTheBack.Depth < 0.9199 && climbed < 10)
        {
            Approach.Tick(fromTheBack, tick, HuntPhase.Stalk, true, false, 0.92);
            climbed += tick;
        }
        check("the rise crosses the whole tank inside the shortest lock-to-burst",
              climbed < Hunt.StalkMin + Hunt.CoilLen,
              $"{climbed:F2}s nodig van d={Approach.Far:F2} tot 0,92, " +
              $"{Hunt.StalkMin + Hunt.CoilLen:F2}s beschikbaar");

        // And the half of it that carries the change in cover is slow enough to
        // read as a fade rather than as a cut. 0.19s was a pop on the capture.
        var outOfTheDark = new ApproachState { Appear = 1 };
        double inTheDark = 0;
        while (outOfTheDark.Depth < Approach.AnimalFloor && inTheDark < 10)
        {
            Approach.Tick(outOfTheDark, tick, HuntPhase.Stalk, true, false, 0.92);
            inTheDark += tick;
        }
        check("coming out of the haze takes long enough to see",
              inTheDark > 0.40,
              $"{inTheDark:F2}s voor dekking {Approach.FadeAt(Approach.Far):F2} -> " +
              $"{Approach.FadeAt(Approach.AnimalFloor):F2}");

        // ---- rubbish numbers are put back rather than drawn ----
        // A hunter is drawn on the UI thread; a NaN reaching a ScaleTransform or an
        // Opacity is exactly the kind of thing that has taken the tank down before.
        var junk = new ApproachState { Depth = double.NaN, Appear = double.NaN, Vanish = double.NaN };
        Approach.Tick(junk, double.NaN, HuntPhase.Stalk, true, false, double.NaN);
        Approach.Tick(junk, -1, HuntPhase.Stalk, true, false, double.PositiveInfinity);
        double op = Approach.Opacity(junk);
        double sc = Approach.ScaleAt(junk.Depth);
        check("rubbish numbers are put back rather than drawn",
              double.IsFinite(junk.Depth) && double.IsFinite(op) && op is >= 0 and <= 1 &&
              double.IsFinite(sc) && sc > 0 &&
              double.IsFinite(Approach.SpeedAt(double.NaN)),
              $"d={junk.Depth:F2} dekking={op:F2} x{sc:F2}");
    }
}