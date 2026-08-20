using System.IO;
using System.Text.Json;
using CuttlefishPet.Core;
using CuttlefishPet.Interop;

namespace CuttlefishPet.Behaviors;

public sealed class BehaviorMachine
{
    private static Dictionary<string, double> _weights = new()
    {
        ["idle"] = 20, ["swim"] = 26, ["sit"] = 13, ["sleep"] = 6,
        ["camouflage"] = 9, ["climb"] = 11, ["jump"] = 9, ["chase"] = 6,
        ["hunt"] = 16, ["peek"] = 9,
    };

    public static void LoadWeights(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var w = new Dictionary<string, double>();
            foreach (var p in doc.RootElement.GetProperty("weights").EnumerateObject())
                w[p.Name] = p.Value.GetDouble();
            _weights = w;
        }
        catch { /* keep defaults */ }
    }

    private readonly BehaviorContext _ctx;
    private double _fleeCooldown;

    public BehaviorBase Current { get; private set; }

    public BehaviorMachine(BehaviorContext ctx)
    {
        _ctx = ctx;
        Current = new FallBehavior(); // pets spawn mid-air and drop in
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

    private void AmbientChecks(double dt)
    {
        var pet = _ctx.Pet;
        var world = _ctx.World;

        // Lost the ground and nobody is handling it → fall.
        if (pet.Surface == null && !Current.OverridesPhysics &&
            Current is not (FallBehavior or JumpToWindowBehavior or FleeBehavior))
        {
            Force(new FallBehavior());
            return;
        }

        if (!Current.Interruptible) return;

        // User actually walked away (not just reading) → settle down until they're back.
        if (world.IdleSeconds > 240 && Current is not SleepBehavior)
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
        if (world.TypingRate > 3 && Current is IdleBehavior or SitBehavior &&
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

        // A shrimp on the desktop trumps everything else.
        if (pet.Surface != null && _ctx.World.NearestTreat(pet) is { } treat)
            return new HuntTreatBehavior(treat);

        Add("idle", () => new IdleBehavior());
        Add("swim", () => new SwimBehavior());

        if (pet.Surface != null)
        {
            Add("sit", () => new SitBehavior());
            Add("sleep", () => new SleepBehavior());
            Add("camouflage", () => new CamouflageBehavior());
            if (HuntCursorBehavior.Possible(_ctx)) Add("hunt", () => new HuntCursorBehavior());
            if (PeekBehavior.Possible(_ctx)) Add("peek", () => new PeekBehavior());

            var edge = ClimbBehavior.FindTarget(_ctx);
            if (edge != null) Add("climb", () => new ClimbBehavior(edge));

            var jumpTarget = JumpToWindowBehavior.FindTarget(_ctx);
            if (jumpTarget != null) Add("jump", () => new JumpToWindowBehavior(jumpTarget));

            if (Math.Abs(_ctx.World.Cursor.X - pet.Pos.X) < 700)
                Add("chase", () => new ChaseCursorBehavior());
        }

        double total = 0;
        foreach (var (_, w) in candidates) total += w;
        double roll = _ctx.Rng.NextDouble() * total;
        foreach (var (b, w) in candidates)
        {
            roll -= w;
            if (roll <= 0) return b;
        }
        return new IdleBehavior();
    }
}
