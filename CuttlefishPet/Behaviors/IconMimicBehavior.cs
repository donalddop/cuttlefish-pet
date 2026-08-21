using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Interop;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Settle into an empty slot in the desktop icon grid and sit there squared off,
/// wearing the wallpaper, passing for one more shortcut you never look at twice.
///
/// Which slots are taken cannot be asked of Explorer without reading its memory, so
/// the pet does what it would do anyway: it looks. A candidate slot is sampled, and
/// only a flat, uninteresting patch counts as empty.
/// </summary>
public sealed class IconMimicBehavior : BehaviorBase
{
    public override string Name => "icon";
    public override bool OverridesPhysics => true;

    private enum Phase { Approach, Posing, Bolting }
    private Phase _phase = Phase.Approach;
    private Point _slot;
    private readonly Point[] _candidates;
    private Rect _cell;
    /// <summary>Size to shrink to while posing, or 0 to stay as it is.</summary>
    private readonly double _poseScale;
    private double _t;

    // Written by the background check, read on the UI thread. _checked is volatile,
    // so the slot it settled on is guaranteed visible to the reader once the flag is.
    private volatile bool _checked;
    private bool _slotIsFree;
    private Prop? _label;

    private IconMimicBehavior(Point slot, Rect cell, Point[] candidates, double poseScale = 0)
    {
        _slot = slot;
        _cell = cell;
        _candidates = candidates;
        _poseScale = poseScale;
    }

    /// <summary>
    /// Find somewhere to pose. Two places work: an empty slot in the desktop icon
    /// grid, and an empty stretch of the taskbar.
    ///
    /// The desktop version only happens when the desktop is actually showing, which
    /// on a screen with a maximised window over it — that is, most screens, most of
    /// the time — is never. The taskbar is the row of icons that is always on show,
    /// so that is where anyone is realistically going to catch one at it.
    /// </summary>
    public static IconMimicBehavior? Find(BehaviorContext c) =>
        (c.Rng.NextDouble() < 0.65 ? OnTaskbar(c) : null) ?? OnDesktop(c) ?? OnTaskbar(c);

    /// <summary>
    /// A gap along the taskbar, posing at the size of the buttons around it. Kept
    /// clear of both ends: the Start button and the clock are not somewhere a pinned
    /// icon would plausibly turn up.
    /// </summary>
    private static IconMimicBehavior? OnTaskbar(BehaviorContext c)
    {
        Surface? bar = null;
        foreach (var s in c.World.Horizontal())
            if (s.Kind == SurfaceKind.TaskbarTop) { bar = s; break; }
        if (bar == null) return null;

        double height = c.World.VirtualScreen.Bottom - bar.Y;
        double span = bar.X2 - bar.X1;
        if (height < 28 || span < 400) return null;

        // Spots spread along the whole bar rather than clustered around the pet. A
        // taskbar with labelled buttons on it is occupied nearly end to end, and the
        // one gap is wherever it happens to be — so the gap has to be reachable.
        var candidates = new List<Point>();
        double usable = span - 280;
        foreach (double f in new[] { 0.5, 0.72, 0.28, 0.86, 0.14, 0.62, 0.38, 0.95 })
        {
            var p = new Point(bar.X1 + 90 + usable * f, bar.Y + height * 0.5);
            if ((p - c.Pet.Pos).Length <= 1400) candidates.Add(p);
        }
        if (candidates.Count == 0) return null;
        var centre = candidates[0];

        // Strictly inside the bar. A cell even slightly taller than the taskbar
        // samples the window above it as well, and the contrast at that edge reads
        // as "this slot is occupied" every single time.
        double side = Math.Max(16, Math.Min(38, height - 10));
        var cell = new Rect(centre.X - side / 2, centre.Y - side / 2, side, side);

        // Shrink to the size of the buttons it is hiding among. A full-grown
        // cuttlefish parked on the taskbar fools precisely nobody.
        double pose = Math.Min(0.55, side / (Pet.RenderScale * 44));

        // The sprite hangs off an anchor near its foot, so the pose point drops by
        // the distance from that anchor to the middle of the body — otherwise it
        // stands on the bar instead of sitting in it.
        var slot = new Point(centre.X, centre.Y + 23 * Pet.RenderScale * pose);
        return new IconMimicBehavior(slot, cell, candidates.ToArray(), pose);
    }

    private static IconMimicBehavior? OnDesktop(BehaviorContext c)
    {
        var grid = SystemProbes.DesktopIcons();
        if (grid == null) return null;
        var g = grid.Value;

        int cols = Math.Max(1, (int)(g.Area.Width / g.CellW));
        int rows = Math.Max(1, (int)(g.Area.Height / g.CellH));

        // A few slots to try, all of them somewhere the desktop is actually showing.
        // Maximised windows offer no ledges, so they are easy to forget — and they
        // are exactly what would be covering the grid.
        var candidates = new List<Point>();
        for (int attempt = 0; attempt < 12 && candidates.Count < 5; attempt++)
        {
            int col = c.Rng.Next(Math.Min(cols, 6));        // icons cluster on the left
            int row = c.Rng.Next(rows);
            var p = new Point(g.Area.X + (col + 0.5) * g.CellW,
                              g.Area.Y + (row + 0.5) * g.CellH);
            if ((p - c.Pet.Pos).Length > 900) continue;
            if (c.World.IsCovered(p)) continue;
            candidates.Add(p);
        }
        if (candidates.Count == 0) return null;

        var centre = candidates[0];
        var cell = new Rect(centre.X - g.CellW * 0.35, centre.Y - g.CellH * 0.35,
                            g.CellW * 0.7, g.CellH * 0.7);
        cell.Intersect(c.World.VirtualScreen);
        if (cell.Width < 20 || cell.Height < 20) return null;

        return new IconMimicBehavior(centre, cell, candidates.ToArray());
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;

        var candidates = _candidates;
        var size = _cell.Size;
        double drop = _slot.Y - _cell.Y - _cell.Height / 2;   // pose offset, kept intact
        Task.Run(() =>
        {
            // A slot with something in it is full of contrast; bare wallpaper or an
            // empty stretch of taskbar is not. Several spots get tried rather than
            // one: a taskbar with labelled buttons on it is mostly occupied, and a
            // single unlucky guess would mean the whole thing never happens.
            foreach (var centre in candidates)
            {
                var cell = new Rect(centre.X - size.Width / 2, centre.Y - size.Height / 2,
                                    size.Width, size.Height);
                var look = CamoSampler.Sample(cell);
                if (look == null || look.Busyness >= 0.35) continue;
                _cell = cell;
                _slot = new Point(centre.X, centre.Y + drop);
                _slotIsFree = true;
                break;
            }
            _checked = true;
        });
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        switch (_phase)
        {
            case Phase.Approach:
            {
                // The slot turned out to have a real icon in it — leave it alone.
                if (_checked && !_slotIsFree)
                {
                    Next = new SwimFreeBehavior();
                    Done = true;
                    return;
                }

                var to = _slot - pet.Pos;
                if (to.Length < 12 || _t > 12)
                {
                    _phase = Phase.Posing;
                    _t = 0;
                    pet.Pos = _slot;
                    pet.Vel = new Vector(0, 0);
                    pet.FacingRight = true;
                    pet.Anim.Play("mimic_icon", restart: true);
                    // A shortcut without a name under it fools nobody — but a
                    // taskbar button does not have one, so only the desktop
                    // version gets a label.
                    if (_poseScale <= 0)
                    {
                        _label = new Prop
                        {
                            Anim = "label",
                            Pos = new Point(_slot.X, _slot.Y + 46),
                            Life = 60,
                        };
                        c.AddProp(_label);
                    }
                    break;
                }
                var desired = to / to.Length * Math.Min(150, to.Length * 2.4);
                pet.Vel += (desired - pet.Vel) * Math.Min(1, 3.5 * dt);
                pet.Pos += pet.Vel * dt;
                if (Math.Abs(pet.Vel.X) > 12) pet.FacingRight = pet.Vel.X > 0;
                break;
            }

            case Phase.Posing:
                // Dead still. Anything else would give the game away.
                pet.Pos = _slot;
                pet.Vel = new Vector(0, 0);
                pet.VisualBob = 0;
                // Held down every tick: ageing eases the size back otherwise.
                if (_poseScale > 0) pet.Scale = _poseScale;

                // Cursor closing in, a window sliding over the slot, or simply bored.
                if ((c.World.Cursor - pet.Pos).Length < 90 ||
                    (_poseScale <= 0 && c.World.IsCovered(_slot)) ||
                    _t > 45 + c.Rng.NextDouble() * 45)
                {
                    _phase = Phase.Bolting;
                    _t = 0;
                    pet.Anim.Play("startle", restart: true);
                    c.Sound.Play("blip", 0.3);
                    var away = pet.Pos - c.World.Cursor;
                    if (away.Length < 1) away = new Vector(0, -1);
                    away.Normalize();
                    pet.Vel = away * 620;
                }
                break;

            case Phase.Bolting:
                pet.Vel *= Math.Exp(-2 * dt);
                pet.Pos += pet.Vel * dt;
                PhysicsEngine.ClampToTank(pet, c.World);
                if (_t > 0.8)
                {
                    Next = new SwimFreeBehavior();
                    Done = true;
                }
                break;
        }
    }

    /// <summary>The name tag goes with the disguise, however it ends.</summary>
    public override void Exit(BehaviorContext c)
    {
        if (_label != null) _label.Age = _label.Life;
    }
}
