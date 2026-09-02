using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

int failed = 0;
void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (detail == "" ? "" : "  " + detail));
    if (!ok) failed++;
}
var rng = new Random(20260902);

// ---------- genome (S1) ----------
var founders = Enumerable.Range(0, 400).Select(_ => Genome.Random(rng)).ToList();
Check("founders vary", founders.Select(g => Math.Round(g.Boldness, 2)).Distinct().Count() > 50);
Check("founder ranges hold", founders.All(g => g.Metabolism is >= 0.72 and <= 1.38 && g.Boldness is >= 0 and <= 1));
var bold  = new Genome(0.92, 0.5, 0.5, 1.1, 0.5, Palettes.IndexOf("teal"), 2);
var timid = new Genome(0.08, 0.5, 0.5, 0.9, 0.5, Palettes.IndexOf("coral"), 4);
var kids = Enumerable.Range(0, 3000).Select(_ => Genome.Inherit(bold, timid, rng)).ToList();
Check("children land between parents", Math.Abs(kids.Average(g => g.Boldness) - 0.5) < 0.05, $"mean={kids.Average(g => g.Boldness):F3}");

// ---------- drives ----------
var d = new Drives();
var avg = new Genome(0.5, 0.5, 0.5, 1.0, 0.5, 1, 1);
for (int i = 0; i < 30 * 260; i++) d.Tick(1.0 / 30, avg, false, 80, false, 0);
Check("hunger fills within a few minutes", d.Hunger > 0.95, $"after 260s: {d.Hunger:F2}");
d.Fed(0.12);
Check("a meal settles hunger", d.Hunger < 0.55, $"{d.Hunger:F2}");

var rest = new Drives();
for (int i = 0; i < 30 * 200; i++) rest.Tick(1.0 / 30, avg, false, 90, false, 0);
double tired = rest.Fatigue;
for (int i = 0; i < 30 * 120; i++) rest.Tick(1.0 / 30, avg, true, 0, true, 0);
Check("swimming tires, perching restores", tired > 0.4 && rest.Fatigue < tired * 0.5, $"{tired:F2} -> {rest.Fatigue:F2}");
Check("company settles loneliness", rest.Loneliness < 0.05, $"{rest.Loneliness:F2}");

var scared = new Drives();
scared.Tick(1.0 / 30, avg, false, 0, false, 1);
Check("a fright lands immediately", scared.Fear > 0.95, $"{scared.Fear:F2}");
for (int i = 0; i < 30 * 30; i++) scared.Tick(1.0 / 30, avg, false, 0, false, 0);
Check("fear bleeds off", scared.Fear < 0.05, $"{scared.Fear:F2}");

var restless = new Drives(); var placid = new Drives();
var rG = avg with { Restlessness = 0.95 }; var pG = avg with { Restlessness = 0.05 };
for (int i = 0; i < 30 * 100; i++) { restless.Tick(1.0/30, rG, false, 60, false, 0); placid.Tick(1.0/30, pG, false, 60, false, 0); }
Check("temperament sets the pace of boredom", restless.Boredom > placid.Boredom * 1.6, $"{restless.Boredom:F2} vs {placid.Boredom:F2}");

var repeat = new Drives();
for (int i = 0; i < 30 * 100; i++) repeat.Tick(1.0/30, avg, false, 60, false, 0);
double b1 = repeat.Boredom; repeat.Started("swimFree");
double dropNew = b1 - repeat.Boredom;
for (int i = 0; i < 30 * 100; i++) repeat.Tick(1.0/30, avg, false, 60, false, 0);
double b2 = repeat.Boredom; repeat.Started("swimFree");
double dropRepeat = b2 - repeat.Boredom;
Check("a new trick settles boredom, the same one does not",
      dropNew > 0.4 && dropRepeat < 0.15 && dropNew > dropRepeat * 3,
      $"new={dropNew:F2} repeat={dropRepeat:F2}");

// ---------- arbitration ----------
var neutral = new Drives();
Check("unlisted behaviours are untouched", Math.Abs(Appetites.Weigh("eggs", neutral, avg) - 1) < 1e-9);

var hungry = new Drives { Hunger = 1 };
var fed = new Drives { Hunger = 0 };
Check("hunger lifts food", Appetites.Weigh("huntTreat", hungry, avg) > Appetites.Weigh("huntTreat", fed, avg) * 3,
      $"{Appetites.Weigh("huntTreat", hungry, avg):F2} vs {Appetites.Weigh("huntTreat", fed, avg):F2}");
var afraid = new Drives { Fear = 1 };
Check("fear lifts hiding", Appetites.Weigh("lurk", afraid, avg) > Appetites.Weigh("lurk", neutral, avg) * 2);
Check("fear does not lift meddling", Appetites.Weigh("tease", afraid, avg) <= Appetites.Weigh("tease", neutral, avg) + 1e-9);
var lonely = new Drives { Loneliness = 1 };
Check("loneliness lifts company", Appetites.Weigh("pile", lonely, avg) > Appetites.Weigh("pile", neutral, avg) * 2);

// temperament, same needs
var boldG = avg with { Boldness = 0.95 };
var shyG  = avg with { Boldness = 0.05 };
Check("bold animals meddle more", Appetites.Weigh("tease", neutral, boldG) > Appetites.Weigh("tease", neutral, shyG) * 2.5,
      $"{Appetites.Weigh("tease", neutral, boldG):F2} vs {Appetites.Weigh("tease", neutral, shyG):F2}");
Check("shy animals hide more", Appetites.Weigh("lurk", afraid, shyG) > Appetites.Weigh("lurk", afraid, boldG) * 2);

// ---------- no runaway, no collapse ----------
double lo = double.MaxValue, hi = 0;
foreach (var key in Appetites.Known)
    for (int i = 0; i < 4000; i++)
    {
        var dd = new Drives { Hunger = rng.NextDouble(), Fatigue = rng.NextDouble(), Loneliness = rng.NextDouble(), Boredom = rng.NextDouble(), Fear = rng.NextDouble() };
        double w = Appetites.Weigh(key, dd, Genome.Random(rng));
        lo = Math.Min(lo, w); hi = Math.Max(hi, w);
    }
Check("multipliers stay bounded", lo > 0.1 && hi < 6, $"range {lo:F2}..{hi:F2}");
Check("multipliers actually move", hi / lo > 4, $"ratio {hi / lo:F1}x");

Console.WriteLine(failed == 0 ? "\nALL PASS" : $"\n{failed} FAILED");
return failed;

