using System.Windows;
using System.Windows.Controls;

namespace CuttlefishPet.Core;

/// <summary>
/// Something that eats cuttlefish, passing through.
///
/// Wikipedia puts it plainly: their predators are dolphins, larger fish including
/// sharks, seals, seabirds, and other cuttlefish. Two of those are worth drawing,
/// and they take turns so a visit is not the same picture every time.
///
/// What it is really for is selection. Until now everything in the tank died of old
/// age, which is a clock and not a pressure -- nothing an animal did made any
/// difference to whether it got to breed, so nothing could drift. A hunter that
/// notices the conspicuous ones and overlooks the hidden ones is the first thing in
/// here that an animal can be better or worse at surviving.
/// </summary>
public sealed class Predator
{
    /// <summary>"shark" or "dolphin" -- also the sprite it is drawn with.</summary>
    public required string Kind { get; init; }

    public Point Pos;
    public Vector Vel;
    public double Age;
    public bool FacingRight = true;
    public Image Visual = null!;

    /// <summary>Who it has fixed on, once something has caught its eye.</summary>
    public Pet? Target;

    /// <summary>Seconds until it looks around again; it does not re-decide every frame.</summary>
    public double LookIn;

    /// <summary>It leaves after a kill, and leaves anyway if the hunting is poor.</summary>
    public bool Leaving;

    /// <summary>
    /// Lunges taken this visit. A visit is worth two, hit or miss: by the time
    /// the first one has gone wrong the whole neighbourhood is ink and scatter,
    /// and a hunter that could keep trying would clear a crowded tank -- the
    /// odds per lunge stop meaning anything once it gets thirty of them.
    /// </summary>
    public int Tries;

    /// <summary>
    /// Seconds locked on to the current quarry. A strike cannot resolve before
    /// this passes a threshold, which is the whole difference between a chase
    /// and a teleport: the hunter used to go from cruising to top speed the
    /// instant something caught its eye, so the hunt was over in the time it
    /// takes to cross fifty pixels and there was nothing to watch.
    /// </summary>
    public double Lock;

    /// <summary>
    /// A pseudomorph it has committed to instead of the animal. While this is set
    /// it drives at the blot and ignores everything else, which is the entire
    /// value of the trick to whoever left it behind.
    /// </summary>
    public Point? Decoy;

    /// <summary>Whether this quarry has already had its one chance to try.</summary>
    public bool DecoyRolled;

    /// <summary>Seconds into the bite, or -1 when the mouth is shut.</summary>
    public double BiteT = -1;

    /// <summary>
    /// Half a second, jaw wide for barely a tenth of it. Long enough to see,
    /// short enough to be a snap rather than a yawn.
    /// </summary>
    public const double BiteLen = 0.5;

    public bool Biting => BiteT >= 0 && BiteT < BiteLen;

    /// <summary>The sprite it is drawn with right now, jaw open or shut.</summary>
    public string Anim => Biting ? Kind + "bite" : Kind;

    /// <summary>
    /// Where prey goes: the back of the open jaw, not the tip of the snout.
    /// Anything being swallowed heads for this point, so it has to sit inside
    /// the mouth or a cuttlefish would vanish into thin water just short of it.
    /// </summary>
    public Point Mouth => new(Pos.X + (FacingRight ? 74 : -74), Pos.Y + 12);

    public bool Expired => Age > 75;

    /// <summary>Cruising speed, and the speed of a committed run at something.</summary>
    /// <summary>
    /// Crossing speed. Fast enough that the whole visit is over in about seven
    /// seconds: long enough to see what it is and get a fright, short enough
    /// that it never becomes something living on your desktop. A slow one is
    /// far more unpleasant than a quick one.
    /// </summary>
    public const double Cruise = 305;
    public const double Lunge = 470;

    /// <summary>
    /// How much this animal stands out to something hunting it. Colour is most of
    /// it -- a cuttlefish flaring at a rival is advertising -- then movement, then
    /// size. Sitting still on a perch wearing the desktop is very nearly free.
    ///
    /// This is the whole selective mechanism: what gets eaten is what got noticed.
    /// </summary>
    public static double Conspicuousness(Pet pet)
    {
        double showing = pet.Vividness * 0.85;
        double moving = Math.Min(1, pet.Vel.Length / 200) * 0.55;
        double bulk = pet.GrownScale * 0.25;
        double hiding = pet.CamoOpacity * 0.75 + (pet.Surface != null ? 0.15 : 0);
        return Math.Max(0, showing + moving + bulk - hiding);
    }
}
