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
        ["hunt"] = 12, ["settle"] = 34, ["huntTreat"] = 34, ["stalk"] = 26,
        // perched on something
        ["patrol"] = 20, ["idle"] = 14, ["sit"] = 12,
        ["camouflage"] = 26, ["peek"] = 8, ["hang"] = 8, ["swing"] = 6,
        ["climb"] = 8, ["climbDown"] = 5, ["slide"] = 5, ["leave"] = 12,
        // rare set pieces — kept low so they stay surprises
        ["burrow"] = 5, ["eggs"] = 16, ["blot"] = 4, ["nibble"] = 6,
        ["inkBomb"] = 2, ["balloon"] = 3, ["ghost"] = 1, ["shock"] = 2,
        // meddling with your desktop
        ["push"] = 6, ["tease"] = 5, ["clock"] = 4, ["caret"] = 12,
        ["ride"] = 6, ["jet"] = 7,
        // social and flourishes
        ["pile"] = 10, ["colourShow"] = 6, ["icon"] = 12, ["play"] = 9,
        ["cross"] = 7, ["read"] = 16, ["bigBubble"] = 9,
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

        // Nobody watching → melt into the background rather than nod off.
        if (world.IdleSeconds > 240 && Current is not (LurkBehavior or CamouflageBehavior))
        {
            Force(_ctx.Rng.NextDouble() < 0.6 && pet.Surface != null
                ? new CamouflageBehavior()
                : new LurkBehavior());
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

        // Food competes on the same footing as everything else rather than short-
        // circuiting the choice. As early returns these crowded out the whole rest
        // of the list whenever anything edible was in the tank — which, now that
        // shrimp turn up by themselves, is most of the time.
        var treat = _ctx.World.NearestTreat(pet);
        if (treat != null && (treat.Pos - pet.Pos).Length > 1100) treat = null;
        var prey = _ctx.World.NearestPrey(pet);

        // Nesting after a successful courtship. If she is not on a ledge yet, go and
        // find one — otherwise the clutch never happens and courtship leads nowhere.
        if (pet.WantsToNest)
        {
            if (pet.Surface is { IsLandable: true })
            {
                pet.WantsToNest = false;
                return new LayEggsBehavior();
            }
            if (SettleBehavior.Find(_ctx, landableOnly: true) is { } nest) return nest;
        }

        if (pet.Surface == null)
        {
            // Open water.
            if (treat != null) Add("huntTreat", () => new HuntTreatBehavior(treat));
            if (prey != null) Add("stalk", () => new StalkPreyBehavior(prey));
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

            // Meddling with the desktop itself.
            if (PushWindowBehavior.Find(_ctx) is { } push) Add("push", () => push);
            if (TeaseCloseBehavior.Find(_ctx) is { } tease) Add("tease", () => tease);
            if (CheckClockBehavior.Find(_ctx) is { } clock) Add("clock", () => clock);
            if (CaretChaseBehavior.Possible(_ctx)) Add("caret", () => new CaretChaseBehavior());
            if (RideCursorBehavior.Possible(_ctx)) Add("ride", () => new RideCursorBehavior());
            if (WaterJetBehavior.Possible(_ctx)) Add("jet", () => new WaterJetBehavior());

            Add("colourShow", () => new ColourShowBehavior());
            Add("play", () => new BubblePlayBehavior());
            Add("bigBubble", () => new BigBubbleBehavior());
            Add("cross", () => new EdgeCrossBehavior(_ctx.Rng.NextDouble() < 0.5 ? -1 : 1));
            if (ReadAlongBehavior.Possible(_ctx)) Add("read", () => new ReadAlongBehavior());
            if (SleepPileBehavior.Find(_ctx) is { } pile) Add("pile", () => pile);
            if (IconMimicBehavior.Find(_ctx) is { } icon) Add("icon", () => icon);
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
            // Worth leaving a perch for.
            if (treat != null) Add("huntTreat", () => new HuntTreatBehavior(treat));
            if (prey != null) Add("stalk", () => new StalkPreyBehavior(prey));
            Add("patrol", () => new SwimBehavior());
            Add("idle", () => new IdleBehavior());
            Add("sit", () => new SitBehavior());
            Add("camouflage", () => new CamouflageBehavior());
            Add("leave", () => new LeavePerchBehavior());
            if (PeekBehavior.Possible(_ctx)) Add("peek", () => new PeekBehavior());
            if (HuntCursorBehavior.Possible(_ctx)) Add("hunt", () => new HuntCursorBehavior());
            if (BurrowBehavior.Possible(_ctx)) Add("burrow", () => new BurrowBehavior());
            if (InkBlotBehavior.Possible(_ctx)) Add("blot", () => new InkBlotBehavior());
            if (NibbleBehavior.Possible(_ctx)) Add("nibble", () => new NibbleBehavior());
            if (LayEggsBehavior.Possible(_ctx) && _ctx.World.PetCount < 6)
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
