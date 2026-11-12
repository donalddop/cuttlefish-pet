using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

// The questions this tool answers, one image each:
//   water-dark.png   -- is there anything there at all, and does it read as depth?
//   water-text.png   -- does it spoil a page of text? (the hard requirement)
//   water-trails.png -- does the current actually carry things, and do near motes
//                       travel further than far ones? Each mote's last 40 seconds
//                       are drawn as a tail, which is the only way to see minutes
//                       of drift in a still image.
//   water-shafts.png -- the light on both surfaces at once: deep water on the left
//                       half, a page of text on the right. The whole design of a
//                       shaft rests on the beam carrying it on the dark side and
//                       the shade beside it carrying it on the light side, and the
//                       only way to check that is to see one shaft cross the seam.
//                       The waterline along the top edge is in this one too, and
//                       the top right corner is the case that matters for it: a
//                       bright band laid over the top of a document.
//   water-shafts-loud.png -- the same at four times strength. Not what it looks
//                       like; what shape it is. Lean, spacing and how far down it
//                       reaches are all invisible at real strength in a still.

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
// The light alone, with the dust left out: two subtle things on one image means
// neither can be judged. Covered, because the right half is a document.
Render("water-shafts.png", Background.Split, warmUp: 120, trails: 0, covered: true,
       dust: false);
Render("water-shafts-loud.png", Background.Split, warmUp: 120, trails: 0, covered: true,
       dust: false, light: 4);
Zoom("water-zoom.png");
// The depth pass. A still cannot show speed, so the one thing that cannot be read
// off this image is drawn as a ruler underneath each animal.
Depths5("depth.png");
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

// Five animals at five depths, the same animal five times, over deep water and
// over a page of text. The questions: does the size difference read as distance
// rather than as five different animals, and does the haze take contrast away on
// both surfaces instead of adding a blue smear to the light one?
//
// Offscreen rather than on the desktop for a reason learned the hard way today: a
// live screenshot catches whatever Donald happens to have open -- a cartoon at
// full screen, in the event -- and the animals camouflage themselves against it,
// so the one thing you can see is the one thing you were not measuring.
void Depths5(string name)
{
    // Pet.RenderScale and a grown adult. Copied rather than referenced: pulling
    // Pet.cs in here would drag the behaviour machine and the sprite library with
    // it, and this tool exists to stay small enough to run in a second.
    const double Render = 1.7, Grown = 0.9;
    const int Rows = 5, Cell = 300;
    int w = Cell * Rows, h = 760;

    var sheet = new BitmapImage();
    sheet.BeginInit();
    sheet.UriSource = new Uri(Path.GetFullPath(Path.Combine(
        "CuttlefishPet", "Assets", "sprites", "swim.png")));
    sheet.CacheOption = BitmapCacheOption.OnLoad;
    sheet.EndInit();
    var frame = new CroppedBitmap(sheet, new Int32Rect(2 * 64, 0, 64, 64));

    var dv = new DrawingVisual();
    using (var dc = dv.RenderOpen())
    {
        Background.Deep.Draw(dc, w, h / 2);
        dc.PushTransform(new TranslateTransform(0, h / 2.0));
        Background.Page.Draw(dc, w, h / 2);
        dc.Pop();

        for (int i = 0; i < Rows; i++)
        {
            double d = i / (double)(Rows - 1);
            double s = Render * Grown * Depths.ScaleAt(d);
            for (int band = 0; band < 2; band++)
            {
                // The colour the camouflage sampler would come back with on this
                // background, which is what the renderer washes the body toward.
                var behind = band == 0 ? Color.FromRgb(7, 18, 28) : Color.FromRgb(252, 251, 249);
                var c = new Point(Cell * i + Cell / 2.0, h / 4.0 + band * h / 2.0);
                var box = new Rect(c.X - 32 * s, c.Y - 32 * s, 64 * s, 64 * s);

                dc.PushOpacity(0.62);          // Pet.BodyOpacity at rest
                dc.PushOpacity(Depths.OpacityAt(d));
                dc.DrawImage(frame, box);
                dc.PushOpacityMask(new ImageBrush(frame));
                dc.PushOpacity(Depths.HazeAt(d));
                dc.DrawRectangle(new SolidColorBrush(behind), null, box);
                dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();

                // One second of swimming at 90px/s, the pace of an unhurried
                // cuttlefish. This is the only way a still shows that a far animal
                // crosses less screen than a near one at the same real speed.
                var rule = new Pen(new SolidColorBrush(
                    band == 0 ? Color.FromArgb(150, 220, 120, 120)
                              : Color.FromArgb(150, 170, 40, 40)), 2);
                double run = 90 * Depths.SpeedAt(d);
                double y = box.Bottom + 26;
                dc.DrawLine(rule, new Point(c.X - run / 2, y), new Point(c.X + run / 2, y));
                dc.DrawLine(rule, new Point(c.X - run / 2, y - 5), new Point(c.X - run / 2, y + 5));
                dc.DrawLine(rule, new Point(c.X + run / 2, y - 5), new Point(c.X + run / 2, y + 5));
            }
        }
    }

    var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(dv);
    var enc = new PngBitmapEncoder();
    enc.Frames.Add(BitmapFrame.Create(rtb));
    using var fs = File.Create(Path.Combine(outDir, name));
    enc.Save(fs);
    Console.WriteLine($"{name}: depth 0..1 over deep water (top) and a page (bottom); " +
                      $"size x{Depths.ScaleAt(0):F2}..x{Depths.ScaleAt(1):F2}, " +
                      $"one second of swimming drawn under each");
}

void Render(string name, Background bg, double warmUp, double trails, bool covered,
            bool dust = true, double light = 1)
{
    var snow = new MarineSnow(new Random(1904));
    snow.Fit(tank);
    var flow = new Current();
    var shafts = new LightShafts(new Random(5150)) { Brightness = light };
    shafts.Fit(tank, flow.Flow);
    var shimmer = new SurfaceShimmer(new Random(8803)) { Brightness = light };
    shimmer.Fit(tank);
    var covers = covered ? new List<Rect> { tank } : null;

    // Settle first: straight after Fit the motes are scattered but have never
    // moved, so their weave phases have not spread out yet -- and Clarity has not
    // eased down to where a covered screen would hold it.
    for (double t = 0; t < warmUp; t += Dt)
    {
        flow.Tick(Dt);
        snow.Tick(Dt, flow.Flow, 0, covers);
        shafts.Tick(Dt, flow.Flow);
        shimmer.Tick(Dt, flow.Flow, shafts.Shafts);
    }

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
        DrawShafts(dc, shafts);
        DrawShimmer(dc, shimmer);

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

        if (dust)
        {
            foreach (var f in snow.Flakes)
            {
                double a = Math.Min(1, f.Alpha * f.Clarity * snow.Brightness);
                if (a < 0.012) continue;
                dc.PushOpacity(a);
                dc.DrawEllipse(brush, null, new Point(f.X, f.Y), f.Size / 2, f.Size / 2);
                dc.Pop();
            }
        }
    }

    var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(dv);
    var enc = new PngBitmapEncoder();
    enc.Frames.Add(BitmapFrame.Create(rtb));
    using var fs = File.Create(Path.Combine(outDir, name));
    enc.Save(fs);
    Console.WriteLine($"{name}: {snow.Flakes.Count} motes, {shafts.Shafts.Count} shafts, " +
                      $"{shimmer.Glints.Count} glints, flow {flow.Flow.X:F2},{flow.Flow.Y:F2} px/s");
}

// The waterline, hung from the top of the frame exactly as the overlay hangs it
// from the top of the virtual screen.
void DrawShimmer(DrawingContext dc, SurfaceShimmer shimmer)
{
    var glintBrush = WaterLook.Glint(Math.Max(1, shimmer.Brightness));
    foreach (var g in shimmer.Glints)
    {
        double a = g.Strength * Math.Min(1, shimmer.Brightness);
        if (a < 0.012) continue;
        dc.PushOpacity(a);
        dc.DrawRectangle(glintBrush, null,
            new Rect(g.X - g.Width / 2, 0, g.Width, shimmer.Band));
        dc.Pop();
    }
}

// The same geometry the overlay builds out of a Rectangle and a SkewTransform:
// the band hangs from the surface at its own x, leans by `lean` px sideways per px
// down, and the bitmap brush carries the rest. Written as a matrix here rather
// than reusing LightShaftLayer because that one needs a live overlay window to
// know its DPI, and the whole point of this tool is not needing one.
void DrawShafts(DrawingContext dc, LightShafts shafts)
{
    var shaftBrush = WaterLook.Shaft(Math.Max(1, shafts.Brightness));
    foreach (var s in shafts.Shafts)
    {
        double a = s.Strength * Math.Min(1, shafts.Brightness);
        if (a < 0.012) continue;
        // (x,y) -> (x + lean*y, y), with the top of the tank pinned so the band
        // starts where the simulation put it instead of where y=0 happens to be.
        dc.PushTransform(new MatrixTransform(new Matrix(1, 0, s.Lean, 1, 0, 0)));
        dc.PushOpacity(a);
        dc.DrawRectangle(shaftBrush, null,
            new Rect(s.X - s.Width / 2, 0, s.Width, s.Reach * H));
        dc.Pop();
        dc.Pop();
    }
}

/// <summary>The two surfaces that matter: deep water, and something to read.</summary>
abstract class Background
{
    public static readonly Background Deep = new DeepWater();
    public static readonly Background Page = new TextPage();
    /// <summary>Both at once, split down the middle, for anything that has to work on either.</summary>
    public static readonly Background Split = new SplitScreen();
    public abstract void Draw(DrawingContext dc, int w, int h);

    private sealed class SplitScreen : Background
    {
        public override void Draw(DrawingContext dc, int w, int h)
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, w / 2.0, h)));
            Deep.Draw(dc, w, h);
            dc.Pop();
            dc.PushClip(new RectangleGeometry(new Rect(w / 2.0, 0, w / 2.0, h)));
            Page.Draw(dc, w, h);
            dc.Pop();
        }
    }

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
