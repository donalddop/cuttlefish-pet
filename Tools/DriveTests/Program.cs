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

// ---------- what the body wears ----------
Check("a hatchling has made up its mind about nothing", new Memory().Conviction == 0);

var sure = new Memory();
foreach (var key in new[] { "hunt", "pile", "settle", "play", "icon" }) sure.Relearn(key, 0.9);
Check("five firm views is a full mind", sure.Conviction > 0.85, $"{sure.Conviction:F2}");

var ruledOut = new Memory();
foreach (var key in new[] { "hunt", "pile", "settle", "play", "icon" }) ruledOut.Relearn(key, -0.9);
Check("having ruled things out counts just as much", ruledOut.Conviction > 0.85, $"{ruledOut.Conviction:F2}");
Check("conviction is bounded", ((Func<bool>)(() => {
    var m = new Memory();
    foreach (var key in Appetites.Known) m.Relearn(key, 1);
    return m.Conviction <= 1.0 + 1e-9; }))());

var half = new Memory();
half.Relearn("hunt", 0.5); half.Relearn("pile", 0.5);
Check("a half-formed mind lands in between", half.Conviction is > 0.1 and < 0.35, $"{half.Conviction:F2}");

// The inherited head start should show on the body straight away, which is the
// whole point of passing it on.
var born = new Memory();
foreach (var (key, w) in Memory.PassedOn(new Dictionary<string, double> {
    ["hunt"] = 0.9, ["settle"] = 0.8, ["pile"] = -0.7 }, rng)) born.Relearn(key, w);
Check("a hatchling of experienced stock is born partly sure", born.Conviction > 0.15, $"{born.Conviction:F2}");
Check("and still less sure than its mother", born.Conviction < sure.Conviction);

// Getting away from a hunter settles no need, so Payoff cannot score it and it
// is credited by hand instead. A hand-credited number still has to behave.
var learned = new Memory();
for (int i = 0; i < 40; i++) learned.Worked("decoy");
Check("a trick that keeps working approaches certainty without passing it",
      learned.Worth("decoy") is > 0.9 and <= 1.0, $"{learned.Worth("decoy"):F3}");
for (int i = 0; i < 40; i++) learned.Failed("decoy");
Check("and the same in reverse", learned.Worth("decoy") is >= -1.0 and < -0.9,
      $"{learned.Worth("decoy"):F3}");
Check("one escape is a start, not a conviction", ((Func<bool>)(() => {
    var m = new Memory(); m.Worked("decoy", 0.34);
    return m.Worth("decoy") is > 0.3 and < 0.4; }))());

// And it has to travel, because the point is that a brood starts where its
// mother left off rather than working the whole thing out again.
var handed = Memory.PassedOn(new Dictionary<string, double> { ["decoy"] = 0.8 }, rng);
Check("the trick is handed on, halved",
      handed.TryGetValue("decoy", out var passed) && passed is > 0.3 and < 0.5,
      $"{handed.GetValueOrDefault("decoy"):F2}");

// ---------- breeding: sexes + variant (c) ----------
// Only females lay, so courtship comes round 0.7x as often and a clutch is 1.4x
// bigger. These hold the numbers to what was chosen on 1 November.
var sexes = Enumerable.Range(0, 4000).Select(_ => Breeding.RollFemale(rng)).ToList();
double femaleShare = sexes.Count(f => f) / (double)sexes.Count;
Check("about half are female", femaleShare is > 0.46 and < 0.54, $"{femaleShare:P1}");
Check("only a male and a female court",
      Breeding.CanCourt(true, false) && Breeding.CanCourt(false, true) &&
      !Breeding.CanCourt(true, true) && !Breeding.CanCourt(false, false));

Check("courtship cooldown is 0.7x the old one, empty tank",
      Math.Abs(Breeding.CourtCooldown(false, 0, 10) - 42) < 1e-9, $"{Breeding.CourtCooldown(false, 0, 10):F1}s");
Check("courtship cooldown is 0.7x the old one, full tank",
      Math.Abs(Breeding.CourtCooldown(false, 10, 10) - 105) < 1e-9, $"{Breeding.CourtCooldown(false, 10, 10):F1}s");
Check("a bloom keeps its quick courtship", Breeding.CourtCooldown(true, 10, 10) == 10);
Check("a zero target cannot divide by zero", double.IsFinite(Breeding.CourtCooldown(false, 3, 0)));

double MeanClutch(int target, int count, bool bloom) =>
    Enumerable.Range(0, 20000).Average(_ => Breeding.ClutchSize(target, count, bloom, rng));
double emptyTank = MeanClutch(10, 2, false), fullTank = MeanClutch(10, 10, false), bloomTank = MeanClutch(10, 2, true);
Check("an empty tank lays 1.4x three", Math.Abs(emptyTank - 4.2) < 0.03, $"{emptyTank:F2}");
Check("a full tank lays 1.4x a token egg", Math.Abs(fullTank - 1.4) < 0.02, $"{fullTank:F2}");
Check("a bloom lays 1.4x six", Math.Abs(bloomTank - 8.4) < 0.03, $"{bloomTank:F2}");
Check("a clutch is never empty and never wild",
      Enumerable.Range(0, 5000).All(i => Breeding.ClutchSize(10, i % 30, i % 3 == 0, rng) is >= 1 and <= 9));

Check("a lone female gets a male drifter", !Breeding.DrifterFemale(new[] { true }, rng));
Check("a lone male gets a female drifter", Breeding.DrifterFemale(new[] { false }, rng));
var emptyDrifters = Enumerable.Range(0, 2000).Count(_ => Breeding.DrifterFemale(Array.Empty<bool>(), rng));
Check("into an empty tank either sex drifts", emptyDrifters is > 900 and < 1100, $"{emptyDrifters}/2000 female");
Check("three males get a female drifter", Breeding.DrifterFemale(new[] { false, false, false }, rng));
Check("four females get a male drifter", !Breeding.DrifterFemale(new[] { true, true, true, true }, rng));
var mixedDrifters = Enumerable.Range(0, 2000).Count(_ => Breeding.DrifterFemale(new[] { true, false, false }, rng));
Check("into a mixed tank either sex drifts", mixedDrifters is > 900 and < 1100, $"{mixedDrifters}/2000 female");
Check("one-sex: all males or all females, not mixed or empty",
      Breeding.OneSex(new[] { false, false }) && Breeding.OneSex(new[] { true }) &&
      !Breeding.OneSex(new[] { true, false }) && !Breeding.OneSex(Array.Empty<bool>()));
Check("a one-sex tank waits a few minutes, not forever",
      Enumerable.Range(0, 2000).All(_ => Breeding.OneSexWait(rng) is >= 150 and <= 270));
Check("the census reads naturally",
      Breeding.Census(3, 1) == "3 mannetjes, 1 vrouwtje" && Breeding.Census(1, 0) == "1 mannetje, 0 vrouwtjes",
      Breeding.Census(3, 1));
Check("the eggs line reads naturally",
      Breeding.EggsLine(0) == "Geen eieren onderweg" && Breeding.EggsLine(1) == "1 ei onderweg" &&
      Breeding.EggsLine(4) == "4 eieren onderweg", Breeding.EggsLine(4));

// The breeding numbers can be overridden from settings.json. Without a block the
// defaults must be exactly what variant (c) was built with.
Breeding.Apply(null);
Check("no overrides: variant (c) defaults stand",
      Breeding.CooldownFactor == 0.7 && Breeding.ClutchFactor == 1.4 &&
      Breeding.OneSexWaitMin == 150 && Breeding.OneSexWaitMax == 270);
Check("no overrides: the cooldown is unchanged",
      Math.Abs(Breeding.CourtCooldown(false, 0, 10) - 42) < 1e-9);
var tuned = System.Text.Json.JsonSerializer.Deserialize<Settings>(
    "{\"TargetPopulation\":8,\"Breeding\":{\"CooldownFactor\":0.5,\"ClutchFactor\":2,\"OneSexWaitMin\":60,\"OneSexWaitMax\":120}}")!;
Breeding.Apply(tuned.BreedingOverrides);
Check("overrides from settings.json are used",
      Breeding.CooldownFactor == 0.5 && Breeding.ClutchFactor == 2 &&
      Math.Abs(Breeding.CourtCooldown(false, 0, 10) - 30) < 1e-9 &&
      Enumerable.Range(0, 500).All(_ => Breeding.OneSexWait(rng) is >= 60 and <= 120));
Check("a clutch follows the overridden factor (2x of 3 = 6)", Breeding.ClutchSize(10, 0, false, rng) == 6);
var partial = System.Text.Json.JsonSerializer.Deserialize<Settings>("{\"Breeding\":{\"ClutchFactor\":1.0}}")!;
Breeding.Apply(partial.BreedingOverrides);
Check("a partial block only changes what it names",
      Breeding.ClutchFactor == 1.0 && Breeding.CooldownFactor == 0.7 && Breeding.OneSexWaitMax == 270);
var silly = System.Text.Json.JsonSerializer.Deserialize<Settings>(
    "{\"Breeding\":{\"CooldownFactor\":0,\"ClutchFactor\":-3,\"OneSexWaitMin\":300,\"OneSexWaitMax\":100}}")!;
Breeding.Apply(silly.BreedingOverrides);
Check("nonsense falls back to the defaults, a swapped range is put right",
      Breeding.CooldownFactor == 0.7 && Breeding.ClutchFactor == 1.4 &&
      Breeding.OneSexWaitMin == 100 && Breeding.OneSexWaitMax == 300);
var plainJson = System.Text.Json.JsonSerializer.Serialize(new Settings { TargetPopulation = 6 });
var keptJson = System.Text.Json.JsonSerializer.Serialize(tuned);
Check("saving without overrides writes no Breeding block, with them keeps it",
      !plainJson.Contains("Breeding") && keptJson.Contains("\"Breeding\"") && !keptJson.Contains("null"),
      keptJson);
Breeding.Apply(null);

// An old tank file has no sexes in it. It has to load, and the field has to come
// out null so the manager knows to assign one rather than make everyone male.
var oldSave = System.Text.Json.JsonSerializer.Deserialize<SavedPet>("{\"Id\":7,\"Name\":\"Oud\",\"Offspring\":2}")!;
Check("a pre-sexes save still loads, with no sex in it", oldSave.Id == 7 && oldSave.Female is null);
var newSave = System.Text.Json.JsonSerializer.Deserialize<SavedPet>(
    System.Text.Json.JsonSerializer.Serialize(new SavedPet { Id = 8, Female = true }))!;
Check("and a new one keeps it", newSave.Female == true);

// ---------- the water: shared current ----------
// Both of these are scenery that runs all day, so what is tested is not "does it
// look nice" but the three ways scenery goes wrong: it leaves its range, it jumps,
// or it quietly stops moving.
var flow = new Current();
var seen = new List<System.Windows.Vector>();
for (int i = 0; i < 30 * 1800; i++) { flow.Tick(1.0 / 30); if (i % 30 == 0) seen.Add(flow.Flow); }
Check("the current stays within its range",
      seen.All(v => Math.Abs(v.X) <= Current.MaxSideways + 1e-9 &&
                    Math.Abs(v.Y) <= Current.MaxVertical + 1e-9),
      $"max |x|={seen.Max(v => Math.Abs(v.X)):F2} |y|={seen.Max(v => Math.Abs(v.Y)):F2}");
Check("the current reverses within half an hour",
      seen.Any(v => v.X > 1) && seen.Any(v => v.X < -1));
Check("the current uses most of its range",
      seen.Max(v => v.X) - seen.Min(v => v.X) > Current.MaxSideways,
      $"span={seen.Max(v => v.X) - seen.Min(v => v.X):F2}");
var step = new Current();
double worstStep = 0;
var prev = step.Flow;
for (int i = 0; i < 30 * 900; i++)
{
    step.Tick(1.0 / 30);
    worstStep = Math.Max(worstStep, (step.Flow - prev).Length);
    prev = step.Flow;
}
Check("the current never jumps", worstStep < 0.02, $"worst tick={worstStep:F4} px/s");
var jumped = new Current();
var before = jumped.Flow;
jumped.Advance(600);
Check("Advance moves the current on", (jumped.Flow - before).Length > 1);

// ---------- the water: marine snow ----------
var snow = new MarineSnow(new Random(7));
snow.Fit(new System.Windows.Rect(0, 0, 1920, 1080));
Check("a 1080p tank gets a sensible number of motes", snow.Flakes.Count is >= 25 and <= 50,
      $"{snow.Flakes.Count}");
Check("motes start scattered through the tank, not all at the top",
      snow.Flakes.Count(f => f.Y > 400) > 5);
Check("depth spreads across the tank", snow.Flakes.Any(f => f.Depth < 0.3) &&
                                       snow.Flakes.Any(f => f.Depth > 0.7));
var strong = new System.Windows.Vector(Current.MaxSideways, Current.MaxVertical);
for (int i = 0; i < 30 * 600; i++) snow.Tick(1.0 / 30, strong, 0, null);
Check("ten minutes of the strongest current leaves every mote in the tank",
      snow.Flakes.All(f => f.X >= -20 && f.X <= 1940 && f.Y >= -20 && f.Y <= 1100));
for (int i = 0; i < 30 * 600; i++) snow.Tick(1.0 / 30, -strong, 0.9, null);
Check("so does ten minutes of it reversed, with the wheel stirring",
      snow.Flakes.All(f => f.X >= -20 && f.X <= 1940 && f.Y >= -20 && f.Y <= 1100));
Check("nothing turns into a NaN along the way",
      snow.Flakes.All(f => double.IsFinite(f.X) && double.IsFinite(f.Y) &&
                           double.IsFinite(f.Clarity)));

// Depth is the whole illusion, so the three things it drives are each pinned down.
var pair = new MarineSnow(new Random(11));
pair.Fit(new System.Windows.Rect(0, 0, 4000, 4000));
pair.Flakes.Clear();
var far = new Flake { X = 2000, Y = 100, Depth = 0.05 };
var near = new Flake { X = 2000, Y = 100, Depth = 0.95 };
pair.Flakes.Add(far);
pair.Flakes.Add(near);
for (int i = 0; i < 30 * 20; i++) pair.Tick(1.0 / 30, new System.Windows.Vector(6, 0), 0, null);
// Distance fallen, not final Y: they both start at 100, and counting from zero
// quietly flattered the ratio.
Check("a near mote sinks faster than a far one", near.Y - 100 > (far.Y - 100) * 1.8,
      $"far fell {far.Y - 100:F0}px, near {near.Y - 100:F0}px");
Check("a near mote is carried further sideways", near.X - 2000 > (far.X - 2000) * 2,
      $"far={far.X - 2000:F1} near={near.X - 2000:F1}");
Check("a near mote is bigger and brighter", near.Size > far.Size * 1.8 && near.Alpha > far.Alpha * 2,
      $"size {far.Size:F2}->{near.Size:F2}, alpha {far.Alpha:F3}->{near.Alpha:F3}");

// Readability: a mote over a window has to fade back, and come up again after.
var reading = new MarineSnow(new Random(3));
reading.Fit(new System.Windows.Rect(0, 0, 4000, 4000));
reading.Flakes.Clear();
var overText = new Flake { X = 500, Y = 500, Depth = 0.8 };
reading.Flakes.Add(overText);
var window = new List<System.Windows.Rect> { new(0, 0, 1000, 1000) };
for (int i = 0; i < 30 * 3; i++) reading.Tick(1.0 / 30, default, 0, window);
Check("a mote over a window steps back but does not vanish",
      overText.Clarity is > 0.4 and < 0.7, $"{overText.Clarity:F2}");
double dimmed = overText.Clarity;
for (int i = 0; i < 30 * 3; i++) reading.Tick(1.0 / 30, default, 0, null);
Check("and comes up again over bare desktop", overText.Clarity > 0.9,
      $"{dimmed:F2} -> {overText.Clarity:F2}");

var resized = new MarineSnow(new Random(5));
resized.Fit(new System.Windows.Rect(0, 0, 1920, 1080));
int wide = resized.Flakes.Count;
resized.Fit(new System.Windows.Rect(0, 0, 3840, 2160));
Check("a bigger tank gets more motes, up to the cap",
      resized.Flakes.Count > wide && resized.Flakes.Count <= 70,
      $"{wide} -> {resized.Flakes.Count}");
resized.Fit(new System.Windows.Rect(0, 0, 800, 600));
Check("a small tank still gets the minimum", resized.Flakes.Count >= 18, $"{resized.Flakes.Count}");
var empty = new MarineSnow(new Random(1));
empty.Tick(1.0 / 30, strong, 0, null);
Check("ticking an unfitted field does nothing rather than throwing", empty.Flakes.Count == 0);

// ---------- the water: light shafts ----------
// Same three failure modes as the current, plus two that are specific to light:
// the shafts all have to lean the same way (one sun), and none of them may ever go
// fully dark, because a shaft that reaches zero pops back into existence.
var tide = new Current();
var light = new LightShafts(new Random(31));
light.Fit(new System.Windows.Rect(0, 0, 1920, 1080), tide.Flow);
Check("a 1080p tank gets a handful of shafts", light.Shafts.Count is >= 3 and <= 5,
      $"{light.Shafts.Count}");
Check("shafts spread across the width rather than clumping",
      light.Shafts.Select(s => s.Home).OrderBy(x => x).Zip(
          light.Shafts.Select(s => s.Home).OrderBy(x => x).Skip(1),
          (a, b) => b - a).All(gap => gap > 200),
      string.Join(",", light.Shafts.Select(s => $"{s.Home:F0}")));
Check("every shaft leans the same way", light.Shafts.All(s => s.Lean > 0.05),
      string.Join(",", light.Shafts.Select(s => $"{s.Lean:F3}")));
Check("a shaft fades out before the bottom of the screen",
      light.Shafts.All(s => s.Reach is > 0.3 and < 0.85));

var homes = light.Shafts.Select(s => s.Home).ToList();
var strengths = light.Shafts.Select(_ => (Min: 2.0, Max: -1.0)).ToList();
var leans = (Min: 9.0, Max: -9.0);
double worstLeanStep = 0, worstXStep = 0;
var lastLean = light.Shafts.Select(s => s.Lean).ToList();
var lastX = light.Shafts.Select(s => s.X).ToList();
for (int i = 0; i < 30 * 3600; i++)
{
    tide.Tick(1.0 / 30);
    light.Tick(1.0 / 30, tide.Flow);
    for (int j = 0; j < light.Shafts.Count; j++)
    {
        var s = light.Shafts[j];
        strengths[j] = (Math.Min(strengths[j].Min, s.Strength), Math.Max(strengths[j].Max, s.Strength));
        leans = (Math.Min(leans.Min, s.Lean), Math.Max(leans.Max, s.Lean));
        worstLeanStep = Math.Max(worstLeanStep, Math.Abs(s.Lean - lastLean[j]));
        worstXStep = Math.Max(worstXStep, Math.Abs(s.X - lastX[j]));
        lastLean[j] = s.Lean;
        lastX[j] = s.X;
    }
}
Check("an hour of light leaves every strength in range",
      light.Shafts.All(s => s.Strength is >= 0 and <= 1) &&
      strengths.All(r => r.Min >= 0.4 && r.Max <= 1.0),
      $"{strengths.Min(r => r.Min):F2}..{strengths.Max(r => r.Max):F2}");
Check("every shaft breathes over that hour, and none of them goes dark",
      strengths.All(r => r.Min < 0.55 && r.Max > 0.95),
      string.Join(" ", strengths.Select(r => $"{r.Min:F2}-{r.Max:F2}")));
Check("the lean stays a lean and never flips", leans.Min > 0.02 && leans.Max < 0.5,
      $"{leans.Min:F3}..{leans.Max:F3}");
Check("nothing about the light jumps", worstLeanStep < 0.001 && worstXStep < 0.1,
      $"lean {worstLeanStep:F5}/tick, x {worstXStep:F3}px/tick");
Check("a shaft stays near the slot it was given",
      light.Shafts.Select((s, j) => Math.Abs(s.X - homes[j])).All(d => d < 70),
      $"worst {light.Shafts.Select((s, j) => Math.Abs(s.X - homes[j])).Max():F0}px");
Check("no NaN anywhere in the light",
      light.Shafts.All(s => double.IsFinite(s.X) && double.IsFinite(s.Lean) &&
                            double.IsFinite(s.Strength) && double.IsFinite(s.Reach)));

var jumpedLight = new LightShafts(new Random(31));
jumpedLight.Fit(new System.Windows.Rect(0, 0, 1920, 1080), tide.Flow);
double lightBefore = jumpedLight.Shafts[0].Strength;
double xBefore = jumpedLight.Shafts[0].X;
jumpedLight.Advance(180);
Check("Advance moves the light on",
      Math.Abs(jumpedLight.Shafts[0].Strength - lightBefore) > 0.05 ||
      Math.Abs(jumpedLight.Shafts[0].X - xBefore) > 5);

var narrow = new LightShafts(new Random(2));
narrow.Fit(new System.Windows.Rect(0, 0, 800, 600));
Check("a narrow tank still gets two shafts", narrow.Shafts.Count == 2, $"{narrow.Shafts.Count}");
narrow.Fit(new System.Windows.Rect(0, 0, 7680, 2160));
Check("a wall of monitors is capped", narrow.Shafts.Count == 6, $"{narrow.Shafts.Count}");
Check("and they are spread over the whole wall",
      narrow.Shafts.Max(s => s.Home) - narrow.Shafts.Min(s => s.Home) > 5000);
var unfitted = new LightShafts(new Random(1));
unfitted.Tick(1.0 / 30, strong);
Check("ticking unfitted light does nothing rather than throwing", unfitted.Shafts.Count == 0);

Console.WriteLine(failed == 0 ? "\nALL PASS" : $"\n{failed} FAILED");
return failed;

