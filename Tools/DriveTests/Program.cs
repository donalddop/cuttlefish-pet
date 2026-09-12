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
for (int i = 0; i < 30 * 260; i++) d.Tick(1.0 / 30, avg, false, 80, 0, 0);
Check("hunger fills within a few minutes", d.Hunger > 0.95, $"after 260s: {d.Hunger:F2}");
d.Fed(0.12);
Check("a meal settles hunger", d.Hunger < 0.55, $"{d.Hunger:F2}");

var rest = new Drives();
for (int i = 0; i < 30 * 200; i++) rest.Tick(1.0 / 30, avg, false, 90, 0, 0);
double tired = rest.Fatigue;
for (int i = 0; i < 30 * 120; i++) rest.Tick(1.0 / 30, avg, true, 0, 1, 0);
Check("swimming tires, perching restores", tired > 0.4 && rest.Fatigue < tired * 0.5, $"{tired:F2} -> {rest.Fatigue:F2}");
Check("company settles loneliness", rest.Loneliness < 0.05, $"{rest.Loneliness:F2}");

var scared = new Drives();
scared.Tick(1.0 / 30, avg, false, 0, 0, 1);
Check("a fright lands immediately", scared.Fear > 0.95, $"{scared.Fear:F2}");
for (int i = 0; i < 30 * 30; i++) scared.Tick(1.0 / 30, avg, false, 0, 0, 0);
Check("fear bleeds off", scared.Fear < 0.05, $"{scared.Fear:F2}");

var restless = new Drives(); var placid = new Drives();
var rG = avg with { Restlessness = 0.95 }; var pG = avg with { Restlessness = 0.05 };
for (int i = 0; i < 30 * 100; i++) { restless.Tick(1.0/30, rG, false, 60, 0, 0); placid.Tick(1.0/30, pG, false, 60, 0, 0); }
Check("temperament sets the pace of boredom", restless.Boredom > placid.Boredom * 1.6, $"{restless.Boredom:F2} vs {placid.Boredom:F2}");

var repeat = new Drives();
for (int i = 0; i < 30 * 100; i++) repeat.Tick(1.0/30, avg, false, 60, 0, 0);
double b1 = repeat.Boredom; repeat.Started("swimFree");
double dropNew = b1 - repeat.Boredom;
for (int i = 0; i < 30 * 100; i++) repeat.Tick(1.0/30, avg, false, 60, 0, 0);
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

// ---------- learning (S3) ----------
var green = new Memory();
Check("no experience means no opinion", Math.Abs(green.Appeal("hunt") - 1) < 1e-9);
Check("fear-only behaviours are never judged",
      Appetites.Payoff("inkBomb", new DriveState(0.5, 0.5, 0.5, 0.5, 1), new DriveState(0.5, 0.5, 0.5, 0.5, 0)) is null);
Check("behaviours outside the table are never judged",
      Appetites.Payoff("eggs", new DriveState(1, 1, 1, 1, 1), new DriveState(0, 0, 0, 0, 0)) is null);

// A hunt that lands a meal, over and over.
var lucky = new Memory();
for (int i = 0; i < 12; i++)
{
    lucky.Began("hunt", new DriveState(0.9, 0.3, 0.3, 0.5, 0));
    lucky.Ended(new DriveState(0.4, 0.3, 0.3, 0.5, 0));
    lucky.Tick(400);                       // let the habituation wear off
}
Check("a hunt that feeds it becomes worth doing", lucky.Worth("hunt") > 0.6, $"worth={lucky.Worth("hunt"):F2}");
Check("and that lifts its odds", lucky.Appeal("hunt") > 1.4, $"appeal={lucky.Appeal("hunt"):F2}");

// The same hunt, on a desktop where there is never anything to catch.
var barren = new Memory();
for (int i = 0; i < 12; i++)
{
    barren.Began("hunt", new DriveState(0.5, 0.3, 0.3, 0.5, 0));
    barren.Ended(new DriveState(0.62, 0.3, 0.3, 0.5, 0));   // hunger climbed throughout
    barren.Tick(400);
}
Check("a hunt that never feeds it is dropped", barren.Worth("hunt") < -0.2, $"worth={barren.Worth("hunt"):F2}");
Check("and that lowers its odds", barren.Appeal("hunt") < 0.85, $"appeal={barren.Appeal("hunt"):F2}");
Check("two desktops, two different animals", lucky.Appeal("hunt") > barren.Appeal("hunt") * 1.8);

// Habituation: the counterweight that stops a good behaviour eating the tank.
var keen = new Memory();
for (int i = 0; i < 12; i++) { keen.Began("hunt", new DriveState(0.9, 0.3, 0.3, 0.5, 0)); keen.Ended(new DriveState(0.4, 0.3, 0.3, 0.5, 0)); keen.Tick(400); }
double rested = keen.Appeal("hunt");
for (int i = 0; i < 4; i++) { keen.Began("hunt", new DriveState(0.9, 0.3, 0.3, 0.5, 0)); keen.Ended(new DriveState(0.4, 0.3, 0.3, 0.5, 0)); }
Check("having just done it makes it less tempting", keen.Appeal("hunt") < rested * 0.65, $"{rested:F2} -> {keen.Appeal("hunt"):F2}");
keen.Tick(600);
Check("and the appetite comes back", keen.Appeal("hunt") > rested * 0.95, $"{keen.Appeal("hunt"):F2}");

// Nothing runs away, however lopsided the experience.
double worst = double.MaxValue, best = 0;
foreach (var key in Appetites.Known)
{
    var m = new Memory();
    for (int i = 0; i < 200; i++) { m.Began(key, new DriveState(1, 1, 1, 1, 1)); m.Ended(new DriveState(0, 0, 0, 0, 0)); m.Tick(400); }
    best = Math.Max(best, m.Appeal(key));
    var n = new Memory();
    for (int i = 0; i < 200; i++) { n.Began(key, new DriveState(0, 0, 0, 0, 0)); n.Ended(new DriveState(1, 1, 1, 1, 1)); n.Tick(400); }
    worst = Math.Min(worst, n.Appeal(key));
}
Check("learning stays bounded", worst > 0.25 && best > 1.4 && best < 2.1, $"range {worst:F2}..{best:F2}");

// ---------- relations (S4) ----------
var knows = new Relations();
Check("a stranger is nobody", Math.Abs(knows.With(7)) < 1e-9);
for (int i = 0; i < 30 * 600; i++) knows.Warm(7, (1.0 / 30) / 420, Relations.Familiar);
Check("time alongside makes them familiar", knows.With(7) > 0.45, $"{knows.With(7):F2}");
Check("but only as far as familiar", knows.With(7) <= Relations.Familiar + 1e-9, $"{knows.With(7):F2}");
knows.Warm(7, 0.45);
Check("an occasion takes it further", knows.With(7) > 0.9, $"{knows.With(7):F2}");
knows.Warm(7, 5);
Check("fondness is bounded", knows.With(7) <= 1 + 1e-9);

var fell = new Relations();
fell.Warm(9, 0.4); fell.Cool(9, 0.55);
Check("a fight costs more than the acquaintance was worth", fell.With(9) < 0, $"{fell.With(9):F2}");
fell.Cool(9, 5);
Check("dislike is bounded", fell.With(9) >= -1 - 1e-9);
Check("proximity can carry a bond back to familiar, never past it", ((Func<bool>)(() => {
    var r = new Relations(); r.Cool(3, 0.8);
    for (int i = 0; i < 30 * 600; i++) r.Warm(3, (1.0 / 30) / 420, Relations.Familiar);
    return r.With(3) <= Relations.Familiar + 1e-9; }))());

var crowd = new Relations();
crowd.Warm(1, 0.2); crowd.Warm(2, 0.8); crowd.Warm(3, 0.5);
Check("it knows who it thinks most of", crowd.Dearest() == 2);
Check("and nobody, if it thinks little of anyone", new Relations().Dearest() is null);
crowd.Forget(2);
Check("the dead are forgotten", crowd.Dearest() == 3 && !crowd.Everyone.ContainsKey(2));

var alone = new Drives { Loneliness = 1 };
var amongStrangers = new Drives { Loneliness = 1 };
var amongFriends = new Drives { Loneliness = 1 };
for (int i = 0; i < 30 * 20; i++)
{
    alone.Tick(1.0 / 30, avg, false, 40, 0, 0);
    amongStrangers.Tick(1.0 / 30, avg, false, 40, 1, 0);
    amongFriends.Tick(1.0 / 30, avg, false, 40, 2, 0);
}
Check("company settles loneliness, solitude does not", amongStrangers.Loneliness < alone.Loneliness);
Check("the company of a friend counts double",
      amongFriends.Loneliness < amongStrangers.Loneliness * 0.75,
      $"friends={amongFriends.Loneliness:F2} strangers={amongStrangers.Loneliness:F2}");

// ---------- persistence (S5) ----------
// A record struct with nothing but a primary constructor is exactly where a
// round trip quietly turns into a tank of default-valued blanks.
var before5 = new Genome(0.73, 0.21, 0.64, 1.19, 0.38, Palettes.IndexOf("azure"), 3);
var after5 = System.Text.Json.JsonSerializer.Deserialize<Genome>(
    System.Text.Json.JsonSerializer.Serialize(before5));
Check("a genome survives the trip to disk", after5 == before5, $"{after5}");

var tank = new TankState { NextId = 42 };
var one = new SavedPet
{
    Id = 7, Genome = before5, Age = 120, Lifespan = 1800, BirthScale = 0.3,
    GrowUpSeconds = 700, Nourishment = 0.4, X = 1200, Y = 340,
    Hunger = 0.61, Fatigue = 0.22, Loneliness = 0.05, Boredom = 0.44, Fear = 0.0,
};
one.Learned["hunt"] = 0.8;
one.Learned["pile"] = -0.3;
one.Bonds[9] = 0.72;
one.Bonds[11] = -0.4;
tank.Pets.Add(one);

var back = System.Text.Json.JsonSerializer.Deserialize<TankState>(
    System.Text.Json.JsonSerializer.Serialize(tank))!;
Check("the tank survives the trip", back.Pets.Count == 1 && back.NextId == 42);
var b = back.Pets[0];
Check("an animal comes back whole",
      b.Id == 7 && b.Genome == before5 && Math.Abs(b.Age - 120) < 1e-9 && Math.Abs(b.Hunger - 0.61) < 1e-9);
Check("what it learned comes back", Math.Abs(b.Learned["hunt"] - 0.8) < 1e-9 && Math.Abs(b.Learned["pile"] + 0.3) < 1e-9);
Check("who it knows comes back", Math.Abs(b.Bonds[9] - 0.72) < 1e-9 && Math.Abs(b.Bonds[11] + 0.4) < 1e-9);

// And that the restored values actually land back in a live animal's head.
var revived = new Memory();
foreach (var (behavior, worth) in b.Learned) revived.Relearn(behavior, worth);
Check("a restored animal still knows what worked", revived.Appeal("hunt") > 1.4 && revived.Appeal("pile") < 0.85,
      $"hunt={revived.Appeal("hunt"):F2} pile={revived.Appeal("pile"):F2}");
var knownAgain = new Relations();
foreach (var (id, bond) in b.Bonds) knownAgain.Remember(id, bond);
Check("and who it liked", knownAgain.Dearest() == 9 && knownAgain.With(11) < 0);

// ---------- inherited learning ----------
var taught = new Dictionary<string, double> { ["hunt"] = 0.80, ["pile"] = -0.60, ["idle"] = 0.01 };
var broods = Enumerable.Range(0, 400).Select(_ => Memory.PassedOn(taught, rng)).ToList();
Check("a hatchling starts at half its mother's conviction",
      Math.Abs(broods.Average(b => b["hunt"]) - 0.40) < 0.03, $"mean={broods.Average(b => b["hunt"]):F3}");
Check("and inherits her doubts as well as her enthusiasms",
      broods.Average(b => b["pile"]) < -0.25, $"mean={broods.Average(b => b["pile"]):F3}");
Check("convictions that came to nothing are not passed on",
      broods.Count(b => b.ContainsKey("idle")) < broods.Count / 2);
Check("siblings do not inherit the same certainty",
      broods.Select(b => Math.Round(b["hunt"], 3)).Distinct().Count() > 50);
Check("an inexperienced mother teaches nothing",
      Memory.PassedOn(new Dictionary<string, double>(), rng).Count == 0);

// Down the generations: an opinion the tank keeps confirming should deepen rather
// than wash out, and one nothing confirms should fade away.
var lineage = new Dictionary<string, double> { ["hunt"] = 0.3 };
for (int gen = 0; gen < 8; gen++)
{
    var child = new Memory();
    foreach (var (behavior, worth) in Memory.PassedOn(lineage, rng)) child.Relearn(behavior, worth);
    // Each generation finds hunting works again, as it would on a desktop with food.
    for (int i = 0; i < 6; i++)
    {
        child.Began("hunt", new DriveState(0.9, 0.3, 0.3, 0.5, 0));
        child.Ended(new DriveState(0.45, 0.3, 0.3, 0.5, 0));
        child.Tick(400);
    }
    lineage = new Dictionary<string, double>(child.Learned);
}
Check("an opinion the tank keeps confirming deepens over generations", lineage["hunt"] > 0.75, $"worth={lineage["hunt"]:F2}");

var fading = new Dictionary<string, double> { ["hunt"] = 0.6 };
for (int gen = 0; gen < 8; gen++)
{
    var child = new Memory();
    foreach (var (behavior, worth) in Memory.PassedOn(fading, rng)) child.Relearn(behavior, worth);
    fading = new Dictionary<string, double>(child.Learned);   // nothing confirms it
}
Check("and one nothing confirms washes out", !fading.ContainsKey("hunt") || fading["hunt"] < 0.05,
      fading.ContainsKey("hunt") ? $"{fading["hunt"]:F3}" : "gone");

Console.WriteLine(failed == 0 ? "\nALL PASS" : $"\n{failed} FAILED");
return failed;

