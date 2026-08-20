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
    private readonly Point _slot;
    private readonly Rect _cell;
    private double _t;

    // Written by the background check, read on the UI thread. Both are plain bools
    // so the write is atomic; a frame's delay either way does not matter.
    private bool _checked;
    private bool _slotIsFree;
    private Prop? _label;

    private IconMimicBehavior(Point slot, Rect cell)
    {
        _slot = slot;
        _cell = cell;
    }

    /// <summary>
    /// Pick a grid slot to try. Whether it is actually free is decided later, off the
    /// UI thread — a screen grab takes longer than a frame is allowed to.
    /// </summary>
    public static IconMimicBehavior? Find(BehaviorContext c)
    {
        var grid = SystemProbes.DesktopIcons();
        if (grid == null) return null;
        var g = grid.Value;

        int cols = Math.Max(1, (int)(g.Area.Width / g.CellW));
        int rows = Math.Max(1, (int)(g.Area.Height / g.CellH));

        int col = c.Rng.Next(Math.Min(cols, 6));            // icons cluster on the left
        int row = c.Rng.Next(rows);
        var centre = new Point(g.Area.X + (col + 0.5) * g.CellW,
                               g.Area.Y + (row + 0.5) * g.CellH);
        if ((centre - c.Pet.Pos).Length > 900) return null;

        // Posing as a desktop icon only works where the desktop is actually showing.
        // Maximised windows offer no ledges, so they are easy to forget — and they
        // are exactly what would be covering the slot.
        if (c.World.IsCovered(centre)) return null;

        var cell = new Rect(centre.X - g.CellW * 0.35, centre.Y - g.CellH * 0.35,
                            g.CellW * 0.7, g.CellH * 0.7);
        cell.Intersect(c.World.VirtualScreen);
        if (cell.Width < 20 || cell.Height < 20) return null;

        return new IconMimicBehavior(centre, cell);
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;

        var cell = _cell;
        Task.Run(() =>
        {
            // A slot with an icon in it is full of contrast; bare wallpaper is not.
            var look = CamoSampler.Sample(cell);
            _slotIsFree = look != null && look.Busyness < 0.35;
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
                    // A shortcut without a name under it fools nobody.
                    _label = new Prop
                    {
                        Anim = "label",
                        Pos = new Point(_slot.X, _slot.Y + 46),
                        Life = 60,
                    };
                    c.AddProp(_label);
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

                // Cursor closing in, a window sliding over the slot, or simply bored.
                if ((c.World.Cursor - pet.Pos).Length < 90 || c.World.IsCovered(_slot) ||
                    _t > 45 + c.Rng.NextDouble() * 45)
                {
                    _phase = Phase.Bolting;
                    _t = 0;
                    pet.Anim.Play("startle", restart: true);
                    pet.ShiftTo(Palettes.IndexOf("pearl"), 5);
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
