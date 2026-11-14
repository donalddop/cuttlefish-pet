using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

/// <summary>The squid school and the cost of a palette at the new sprite resolution.</summary>
static class SquidTests
{
    public static void Run(Action<string, bool, string> check)
    {
        var screen = new Rect(0, 0, 2560, 1440);
        int crossed = 0, flipped = 0, tailFirst = 0, trials = 40;
        double worstSpread = 0, worstBank = 0, worstCrowd = 0, slowest = 0;
        for (int trial = 0; trial < trials; trial++)
        {
            var rng = new Random(9000 + trial);
            var school = SquidSchool.Create(screen, rng);
            if (school.TailFirst) tailFirst++;
            var facing = school.Members.Select(m => m.FacingRight).ToArray();
            double crowdTime = 0;
            while (!school.Gone)
            {
                school.Step(1.0 / 60, rng);
                for (int i = 0; i < facing.Length; i++)
                    if (school.Members[i].FacingRight != facing[i]) flipped++;
                worstBank = Math.Max(worstBank, school.Members.Max(m => Math.Abs(m.Bank)));

                // Once the whole school is on screen, check it is still one school
                // and not a pile of squid sitting on top of each other.
                var on = school.Members.Where(m => screen.Contains(m.Pos)).ToList();
                if (on.Count != school.Members.Count) continue;
                double cx = on.Average(m => m.Pos.X), cy = on.Average(m => m.Pos.Y);
                worstSpread = Math.Max(worstSpread,
                    on.Max(m => Math.Max(Math.Abs(m.Pos.X - cx), Math.Abs(m.Pos.Y - cy))));
                bool crowded = false;
                for (int a = 0; a < on.Count && !crowded; a++)
                    for (int b = a + 1; b < on.Count; b++)
                        if ((on[a].Pos - on[b].Pos).Length < 14) { crowded = true; break; }
                if (crowded) crowdTime += 1.0 / 60;
            }
            worstCrowd = Math.Max(worstCrowd, crowdTime);
            slowest = Math.Max(slowest, school.Age);
            if (school.Age < SquidSchool.MaxAge) crossed++;
        }

        check("every squid school makes it across", crossed == trials, $"{crossed}/{trials}, slowest {slowest:F0}s");
        check("a crossing takes under a minute", slowest < 60, $"{slowest:F0}s");
        check("squid never turn round mid-crossing", flipped == 0, $"{flipped} flips");
        check("some schools come through tail first", tailFirst is > 3 and < 30, $"{tailFirst}/{trials}");
        check("a school stays a school", worstSpread < 700, $"widest {worstSpread:F0}px from the centre");
        check("squid do not pile up on each other", worstCrowd < 1.0, $"worst {worstCrowd:F2}s crowded");
        check("banking stays a tilt, not a dive", worstBank <= SquidSchool.MaxBank + 0.01, $"{worstBank:F1} deg");

        // The jet: speed should come in pulses, not as a steady glide.
        var pulse = SquidSchool.Create(screen, new Random(3), count: 1);
        var r = new Random(4);
        var speeds = new List<double>();
        for (int i = 0; i < 60 * 20; i++) { pulse.Step(1.0 / 60, r); speeds.Add(pulse.Members[0].Vel.Length); }
        double lo = speeds.Skip(120).Min(), hi = speeds.Skip(120).Max();
        check("a squid swims in pulses", hi > lo * 1.35, $"{lo:F0}..{hi:F0} px/s");

        // Palette work is now done lazily on the UI thread the first time a colour
        // is worn, at double resolution. One action's worth must fit in a frame or two.
        var px = new byte[128 * 128 * 4];
        var noise = new Random(1);
        for (int i = 0; i < px.Length; i += 4)
        {
            px[i] = (byte)noise.Next(60, 200); px[i + 1] = (byte)noise.Next(60, 200);
            px[i + 2] = (byte)noise.Next(120, 240); px[i + 3] = (byte)(i % 16 == 0 ? 0 : 255);
        }
        var frame = BitmapSource.Create(128, 128, 96, 96, PixelFormats.Bgra32, null, px, 128 * 4);
        frame.Freeze();
        Palettes.Apply(frame, Palettes.All[3]);   // warm the JIT
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 16; i++) Palettes.Apply(frame, Palettes.All[1 + i % 12]);
        double ms = sw.Elapsed.TotalMilliseconds;
        check("a 16-frame palette row at 128px costs under 40ms", ms < 40, $"{ms:F1}ms");
    }
}
