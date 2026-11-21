using System.Windows;
using CuttlefishPet.Core;

/// <summary>
/// Animals in the current: carried by it, and set against it. The whole thing is
/// 8.5px/s at its strongest, so nothing here can be judged by looking -- these
/// are the numbers that say whether it is doing anything at all.
/// </summary>
internal static class CrabbingTests
{
    public static void Run(Action<string, bool, string> check)
    {
        const double tick = 1.0 / 30;
        var water = new Current();

        // ---- the carry is real over the time a current lasts ----
        // An idle animal holds a heading of its own (LurkBehavior drifts at 15 to
        // 31px/s); the question is whether the water adds up to anything beside it.
        var pos = new Point(0, 0);
        var ownStep = new Vector(0, 0);
        for (int i = 0; i < 30 * 60; i++)
        {
            water.Tick(tick);
            pos += Crabbing.Carry(water.Flow, tick);
        }
        check("a minute in the water moves an animal a visible distance",
              Math.Abs(pos.X) > 60 && Math.Abs(pos.X) < 520 && ownStep.Length == 0,
              $"{pos.X:F0},{pos.Y:F0} px na een minuut");

        // ---- and it is the same water for everybody ----
        var a = Crabbing.Carry(new Vector(8.5, -2), tick);
        var b = Crabbing.Carry(new Vector(8.5, -2), tick);
        check("two animals in the same water are carried the same way",
              a == b && a.X > 0 && a.Y < 0, $"{a.X:F3},{a.Y:F3} px per tick");

        // ---- the lean answers the flow, and only while the animal is slow ----
        double still = Crabbing.WantLean(new Vector(0, 0), new Vector(0, 2.8), true);
        double swimming = Crabbing.WantLean(new Vector(220, 0), new Vector(0, 2.8), true);
        check("water pushing it down tips the nose up, and only when it is drifting",
              still < -4 && Math.Abs(swimming) < 0.01,
              $"stil {still:F1}gr, zwemmend {swimming:F1}gr");

        double up = Crabbing.WantLean(new Vector(0, 0), new Vector(0, -2.8), true);
        check("and it turns over with the flow", up > 4 && Math.Abs(up + still) < 1e-9,
              $"omhoog {up:F1}gr tegen omlaag {still:F1}gr");

        double facingLeft = Crabbing.WantLean(new Vector(0, 0), new Vector(0, 2.8), false);
        check("an animal facing the other way leans the other way on the screen",
              Math.Abs(facingLeft + still) < 1e-9, $"{facingLeft:F1}gr tegen {still:F1}gr");

        // A sideways current is deliberately not answered: a side-on body cannot
        // point upstream by tilting, and turning it round is the behaviour's call.
        double sideways = Crabbing.WantLean(new Vector(0, 0), new Vector(8.5, 0), true);
        check("a sideways current is not answered with a tilt",
              Math.Abs(sideways) < 0.01, $"{sideways:F2}gr");

        // ---- it is a pose, so it arrives slowly and stays inside its limit ----
        double lean = 0, worst = 0;
        for (int i = 0; i < 30 * 30; i++)
        {
            water.Tick(tick);
            double want = Crabbing.WantLean(new Vector(6, 0), water.Flow, true);
            double before = lean;
            lean = Crabbing.Step(lean, want, tick);
            worst = Math.Max(worst, Math.Abs(lean - before) / tick);
        }
        check("the body comes round to it rather than snapping",
              worst < Crabbing.MaxLean * 1.2 && Math.Abs(lean) <= Crabbing.MaxLean,
              $"snelst {worst:F1}gr/s, nu {lean:F2}gr");

        double pegged = 0;
        for (int i = 0; i < 30 * 20; i++)
            pegged = Crabbing.Step(pegged, 900, tick);
        check("and never past its limit, whatever it is asked for",
              Math.Abs(pegged - Crabbing.MaxLean) < 1e-6, $"{pegged:F2}gr");

        // ---- influence runs out before the animals get fast ----
        check("the water stops mattering once it is really swimming",
              Crabbing.Influence(new Vector(0, 0), 8.5) > 0.9 &&
              Crabbing.Influence(new Vector(40, 0), 8.5) is > 0.3 and < 0.8 &&
              Crabbing.Influence(new Vector(Crabbing.Yields + 1, 0), 8.5) == 0,
              $"0px/s {Crabbing.Influence(new Vector(0, 0), 8.5):F2}, " +
              $"40px/s {Crabbing.Influence(new Vector(40, 0), 8.5):F2}, " +
              $"{Crabbing.Yields + 1:F0}px/s {Crabbing.Influence(new Vector(Crabbing.Yields + 1, 0), 8.5):F2}");

        check("still water leans nobody",
              Crabbing.Influence(new Vector(0, 0), 0) == 0 &&
              Crabbing.WantLean(new Vector(0, 0), new Vector(0, 0), true) == 0, "");

        // ---- rubbish numbers are put back rather than drawn ----
        // Lean reaches a RotateTransform on the UI thread.
        var junkFlow = new Vector(double.NaN, double.PositiveInfinity);
        double junkLean = Crabbing.WantLean(new Vector(double.NaN, 0), junkFlow, true);
        double stepped = Crabbing.Step(double.NaN, double.NaN, double.NaN);
        var carried = Crabbing.Carry(junkFlow, double.NaN);
        check("rubbish numbers are put back rather than drawn",
              double.IsFinite(junkLean) && Math.Abs(junkLean) <= Crabbing.MaxLean &&
              double.IsFinite(stepped) && Math.Abs(stepped) <= Crabbing.MaxLean &&
              double.IsFinite(carried.X) && double.IsFinite(carried.Y),
              $"wil {junkLean:F2}, stap {stepped:F2}, dragen {carried.X:F2},{carried.Y:F2}");

        // ---- a stalled frame must not shove an animal across the screen ----
        var hiccup = Crabbing.Carry(new Vector(8.5, 2.8), 5.0);
        check("one long frame does not shove it sideways",
              hiccup.Length < 3, $"{hiccup.X:F2},{hiccup.Y:F2} px na een frame van 5s");

        // ---- the debug strength is a debug thing only ----
        var plain = new Current();
        var loud = new Current { Strength = 6 };
        plain.Tick(tick); loud.Tick(tick);
        check("the showcase current is stronger and the real one is untouched",
              Math.Abs(loud.Flow.X) > Math.Abs(plain.Flow.X) * 5 &&
              Math.Abs(plain.Flow.X) <= Current.MaxSideways + 1e-9,
              $"{plain.Flow.X:F2} tegen {loud.Flow.X:F2} px/s");
    }
}