using System.IO;
using System.Text.Json;
using CuttlefishPet.Core;
using CuttlefishPet.Interop;

namespace CuttlefishPet.Behaviors;

public sealed class BehaviorMachine
{
    private static Dictionary<string, double> _weights = new()
    {
        // open water
        ["swimFree"] = 40, ["hover"] = 14, ["dart"] = 9, ["chase"] = 8,
        ["hunt"] = 12, ["settle"] = 22,
        // perched on something
        ["patrol"] = 20, ["idle"] = 14, ["sit"] = 12, ["sleep"] = 6,
        ["camouflage"] = 11, ["peek"] = 8, ["hang"] = 8, ["swing"] = 6,
        ["climb"] = 8, ["climbDown"] = 5, ["slide"] = 5, ["leave"] = 26,
        // rare set pieces — kept low so they stay surprises
        ["burrow"] = 5, ["eggs"] = 2, ["blot"] = 4, ["nibble"] = 6,
        ["inkBomb"] = 2, ["balloon"] = 3, ["ghost"] = 1, ["shock"] = 2,
    };

    /// <summary>
    /// Overlay tuning from behaviors.json onto the defaults. Merging, not replacing:
    /// a file written for an older build must not silently switch off new behaviour.
    /// </summary>
    public static void LoadWeights(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var p in doc.RootElement.GetProperty("weights").EnumerateObject())
                _weights[p.Name] = p.Value.GetDouble();
        }
        catch { /* keep defaults */ }
    }

    private readonly BehaviorContext _ctx;
    private double _fleeCooldown;

    public BehaviorBase Current { get; private set; }

    public BehaviorMachine(BehaviorContext ctx)
    {
        _ctx = ctx;
        Current = new SwimFreeBehavior(); // pets simply swim in
        Current.Enter(ctx);
    }

    public void Force(BehaviorBase next)
    {
        Log($"{Current.Name} -> {next.Name} pos=({_ctx.Pet.Pos.X:F0},{_ctx.Pet.Pos.Y:F0}) vel=({_ctx.Pet.Vel.X:F0},{_ctx.Pet.Vel.Y:F0})");
        Current.Exit(_ctx);
        _ctx.Pet.VisualBob = 0; // never carry a hover-bob into the next behavior
        Current = next;
        Current.Enter(_ctx);
    }

    private static void Log(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cuttlefishpet-debug.log"),
                $"{DateTime.Now:HH:mm:ss.f} {msg}{Environment.NewLine}");
        }
        catch { }
    }

    public void HandleMouse(MouseEvent e)
    {
        if (e.Kind == MouseEventKind.Down)
        {
            if (Current is not DragBehavior && _ctx.Pet.Bounds.Contains(new System.Windows.Point(e.X, e.Y)))
                Force(new DragBehavior());
        }
        else if (Current is DragBehavior drag)
        {
            drag.Release(_ctx);
        }
    }

    public void Tick(double dt)
    {
        _fleeCooldown = Math.Max(0, _fleeCooldown - dt);
        AmbientChecks(dt);

        if (Current.Done)
        {
            var next = Current.Next ?? PickNext();
            Force(next);
        }
        Current.Tick(_ctx, dt);
    }

    /// <summary>Hanging under the ceiling: poses anchored at the feet would float.</summary>
    private bool OnCeiling => _ctx.Pet.Surface is { Kind: SurfaceKind.Ceiling };

    private void AmbientChecks(double dt)
    {
        var pet = _ctx.Pet;
        var world = _ctx.World;

        // Let go of a perch while doing something that assumed one → start swimming.
        if (pet.Surface == null && !Current.OverridesPhysics && Current is not DriftBehavior)
        {
            Force(new SwimFreeBehavior());
            return;
        }

        if (!Current.Interruptible) return;

        // User actually walked away (not just reading) → settle down until they're back.
        if (world.IdleSeconds > 240 && Current is not SleepBehavior && !OnCeiling)
        {
            Force(new SleepBehavior(away: true));
            return;
        }

        // Cursor rushing at the pet → startled dash.
        var toPet = pet.Pos - world.Cursor;
        if (_fleeCooldown <= 0 && toPet.Length < 140 &&
            world.CursorVelocity.Length > 1000 &&
            System.Windows.Vector.Multiply(world.CursorVelocity, toPet) > 0)
        {
            _fleeCooldown = 4;
            Force(new FleeBehavior());
            return;
        }

        // Heavy typing → occasional excited wiggle.
        if (world.TypingRate > 3 && !OnCeiling && Current is IdleBehavior or SitBehavior &&
            _ctx.Rng.NextDouble() < dt * 0.4)
            Force(new TypingReactBehavior());
    }

    private BehaviorBase PickNext()
    {
        var pet = _ctx.Pet;
        var candidates = new List<(BehaviorBase b, double w)>();

        void Add(string key, Func<BehaviorBase> make)
        {
            if (_weights.TryGetValue(key, out var w) && w > 0)
                candidates.Add((make(), w));
        }

        // A shrimp in the tank trumps everything else, wherever the pet is.
        if (_ctx.World.NearestTreat(pet) is { } treat)
            return new HuntTreatBehavior(treat);

        if (pet.Surface == null)
        {
            // Open water.
            Add("swimFree", () => new SwimFreeBehavior());
            Add("hover", () => new HoverBehavior());
            Add("dart", () => new DartBehavior());
            if (HuntCursorBehavior.Possible(_ctx)) Add("hunt", () => new HuntCursorBehavior());
            if ((_ctx.World.Cursor - pet.Pos).Length < 900)
                Add("chase", () => new ChaseCursorBehavior());
            if (SettleBehavior.Find(_ctx) is { } settle) Add("settle", () => settle);
            Add("balloon", () => new BalloonBehavior());
            Add("inkBomb", () => new InkBombBehavior());
            Add("shock", () => new ShockBehavior());
            if (_ctx.World.PetCount > 1) Add("ghost", () => new GhostBehavior());
        }
        else if (pet.Surface.Kind == SurfaceKind.Ceiling)
        {
            // Hanging upside down: walk along it, or let go.
            Add("ceiling", () => new CeilingWalkBehavior());
            Add("leave", () => new LeavePerchBehavior());
        }
        else if (pet.Surface.IsVertical)
        {
            // Clinging to a wall: crawl along it, or push off.
            var up = ClimbBehavior.FindTarget(_ctx);
            if (up != null) Add("climb", () => new ClimbBehavior(up));
            var down = ClimbBehavior.FindTarget(_ctx, down: true);
            if (down != null)
            {
                Add("climbDown", () => new ClimbBehavior(down, down: true));
                Add("slide", () => new ClimbBehavior(down, down: true, slide: true));
            }
            Add("camouflage", () => new CamouflageBehavior());
            Add("leave", () => new LeavePerchBehavior());
        }
        else
        {
            // Settled on a ledge: the taskbar, a title bar, the desktop floor.
            Add("patrol", () => new SwimBehavior());
            Add("idle", () => new IdleBehavior());
            Add("sit", () => new SitBehavior());
            Add("sleep", () => new SleepBehavior());
            Add("camouflage", () => new CamouflageBehavior());
            Add("leave", () => new LeavePerchBehavior());
            if (PeekBehavior.Possible(_ctx)) Add("peek", () => new PeekBehavior());
            if (HuntCursorBehavior.Possible(_ctx)) Add("hunt", () => new HuntCursorBehavior());
            if (BurrowBehavior.Possible(_ctx)) Add("burrow", () => new BurrowBehavior());
            if (InkBlotBehavior.Possible(_ctx)) Add("blot", () => new InkBlotBehavior());
            if (NibbleBehavior.Possible(_ctx)) Add("nibble", () => new NibbleBehavior());
            if (LayEggsBehavior.Possible(_ctx) && _ctx.World.PetCount < 8)
                Add("eggs", () => new LayEggsBehavior());
            Add("shock", () => new ShockBehavior());
            if (HangBehavior.Possible(_ctx))
            {
                Add("hang", () => new HangBehavior());
                Add("swing", () => new HangBehavior(launch: true));
            }
            var edge = ClimbBehavior.FindTarget(_ctx);
            if (edge != null) Add("climb", () => new ClimbBehavior(edge));
        }

        double total = 0;
        foreach (var (_, w) in candidates) total += w;
        double roll = _ctx.Rng.NextDouble() * total;
        foreach (var (b, w) in candidates)
        {
            roll -= w;
            if (roll <= 0) return b;
        }
        return new SwimFreeBehavior();
    }
}
