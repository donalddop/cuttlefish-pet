using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

// Three questions this tool answers, in three images:
//   water-dark.png   -- is there anything there at all, and does it read as depth?
//   water-text.png   -- does it spoil a page of text? (the hard requirement)
//   water-trails.png -- does the current actually carry things, and do near motes
//                       travel further than far ones? Each mote's last 40 seconds
//                       are drawn as a tail, which is the only way to see minutes
//                       of drift in a still image.

// Donald's actual screen. The mote count follows the area, so previewing at any
// other size shows a field of the wrong density.
const int W = 1920, H = 1080;
const double Dt = 1.0 / 30;
var tank = new Rect(0, 0, W, H);
string outDir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var brush = WaterLook.Flake();

Render("water-dark.png", Background.Deep, warmUp: 120, trails: 0, covered: false);
// Covered: a maximised document window is the real case for the readability rule,
// and it is also the dimmest the water ever gets. If it survives here it is there.
Render("water-text.png", Background.Page, warmUp: 120, trails: 0, covered: true);
Render("water-trails.png", Background.Deep, warmUp: 120, trails: 40, covered: false);
Zoom("water-zoom.png");
Console.WriteLine($"written to {outDir}");
return 0;

// One mote of each depth at 8x, half on deep water and half on a white page. The
// whole design rests on a mote being present on both, and at 1:1 that is a
// judgement call about three pixels -- which is not a judgement, it is a guess.
void Zoom(string name)
{
    const int Z = 8, Rows = 5, Cell = 110;
    int w = Cell * Rows, h = Cell * 2;
    var dv = new DrawingVisual();
    using (var dc = dv.RenderOpen())
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 20, 30)), null, new Rect(0, 0, w, h / 2));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(250, 249, 246)), null, new Rect(0, h / 2, w, h / 2));
        for (int i = 0; i < Rows; i++)
        {
            var f = new Flake { Depth = i / (double)(Rows - 1) };
            for (int band = 0; band < 2; band++)
            {
                var c = new Point(Cell * i + Cell / 2.0, h / 4.0 + band * h / 2.0);
                dc.PushOpacity(f.Alpha);
                dc.DrawEllipse(brush, null, c, f.Size * Z / 2, f.Size * Z / 2);
                dc.Pop();
            }
        }
    }
    var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(dv);
    var enc = new PngBitmapEncoder();
    enc.Frames.Add(BitmapFrame.Create(rtb));
    using var fs = File.Create(Path.Combine(outDir, name));
    enc.Save(fs);
    Console.WriteLine($"{name}: far..near at {Z}x on dark (top) and light (bottom)");
}

void Render(string name, Background bg, double warmUp, double trails, bool covered)
{
    var snow = new MarineSnow(new Random(1904));
    snow.Fit(tank);
    var flow = new Current();
    var covers = covered ? new List<Rect> { tank } : null;

    // Settle first: straight after Fit the motes are scattered but have never
    // moved, so their weave phases have not spread out yet -- and Clarity has not
    // eased down to where a covered screen would hold it.
    for (double t = 0; t < warmUp; t += Dt) { flow.Tick(Dt); snow.Tick(Dt, flow.Flow, 0, covers); }

    // A tail per mote, sampled backwards in time by re-running the field.
    var tails = new List<List<Point>>();
    if (trails > 0)
    {
        foreach (var f in snow.Flakes) tails.Add(new List<Point>());
        for (double t = 0; t < trails; t += Dt)
        {
            flow.Tick(Dt);
            snow.Tick(Dt, flow.Flow, 0, covers);
            if ((int)(t / Dt) % 6 != 0) continue;
            for (int i = 0; i < snow.Flakes.Count; i++)
                tails[i].Add(new Point(snow.Flakes[i].X, snow.Flakes[i].Y));
        }
    }

    var dv = new DrawingVisual();
    using (var dc = dv.RenderOpen())
    {
        bg.Draw(dc, W, H);

        for (int i = 0; i < tails.Count; i++)
        {
            var pts = tails[i];
            // Wrapping around the edge would otherwise draw a line straight back
            // across the screen; a long hop means it wrapped, so start a new tail.
            for (int j = 1; j < pts.Count; j++)
            {
                if ((pts[j] - pts[j - 1]).Length > 60) continue;
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(
                    (byte)(40 + 120 * snow.Flakes[i].Depth), 255, 120, 120)), 1);
                dc.DrawLine(pen, pts[j - 1], pts[j]);
            }
        }

        foreach (var f in snow.Flakes)
        {
            double a = Math.Min(1, f.Alpha * f.Clarity * snow.Brightness);
            if (a < 0.012) continue;
            dc.PushOpacity(a);
            dc.DrawEllipse(brush, null, new Point(f.X, f.Y), f.Size / 2, f.Size / 2);
            dc.Pop();
        }
    }

    var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(dv);
    var enc = new PngBitmapEncoder();
    enc.Frames.Add(BitmapFrame.Create(rtb));
    using var fs = File.Create(Path.Combine(outDir, name));
    enc.Save(fs);
    Console.WriteLine($"{name}: {snow.Flakes.Count} motes, flow {flow.Flow.X:F2},{flow.Flow.Y:F2} px/s");
}

/// <summary>The two surfaces that matter: deep water, and something to read.</summary>
abstract class Background
{
    public static readonly Background Deep = new DeepWater();
    public static readonly Background Page = new TextPage();
    public abstract void Draw(DrawingContext dc, int w, int h);

    private sealed class DeepWater : Background
    {
        public override void Draw(DrawingContext dc, int w, int h) =>
            dc.DrawRectangle(new LinearGradientBrush(
                Color.FromRgb(10, 26, 38), Color.FromRgb(4, 11, 18), 90), null,
                new Rect(0, 0, w, h));
    }

    /// <summary>
    /// Not real text: grey bars at body-text size and spacing. Enough to judge
    /// whether a mote drifting over a paragraph pulls the eye off the line, which
    /// is the only thing being asked here.
    /// </summary>
    private sealed class TextPage : Background
    {
        public override void Draw(DrawingContext dc, int w, int h)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(252, 251, 249)), null,
                new Rect(0, 0, w, h));
            var ink = new SolidColorBrush(Color.FromRgb(42, 40, 38));
            var rng = new Random(8);
            for (int y = 70; y < h - 60; y += 29)
            {
                double x = 110;
                while (x < w - 160)
                {
                    double word = 24 + rng.NextDouble() * 70;
                    dc.DrawRectangle(ink, null, new Rect(x, y, word, 9));
                    x += word + 11;
                }
            }
        }
    }
}
