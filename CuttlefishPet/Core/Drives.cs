namespace CuttlefishPet.Core;

/// <summary>
/// What one cuttlefish wants at this moment. Five needs that fill up by themselves
/// and are emptied by doing something about them, which is the whole of it: the
/// behaviour machine no longer rolls a die over a fixed table, it rolls one that is
/// weighted by whichever need has got loudest.
///
/// The time constants are set against the lifespan the tank already runs on --
/// 26 to 50 minutes -- so a need has to matter within minutes to matter at all.
/// </summary>
public sealed class Drives
{
    /// <summary>Wants food. Fills on its own, emptied by a meal.</summary>
    public double Hunger;
    /// <summary>Wants to stop swimming. Paid off by sitting on something.</summary>
    public double Fatigue;
    /// <summary>Wants company. Only falls while another pet is nearby.</summary>
    public double Loneliness;
    /// <summary>Wants something else to happen. The pace is set by temperament.</summary>
    public double Boredom;
    /// <summary>Wants to not be seen. Spikes on a fright, bleeds off over ~20s.</summary>
    public double Fear;

    private string _lastBehavior = "";

    /// <summary>Seconds from a full stomach to an empty one at an average metabolism.</summary>
    private const double HungerFill = 260;

    /// <summary>Nobody within this many pixels and it starts to feel alone.</summary>
    public const double CompanyRange = 320;

    /// <summary>
    /// Start each animal somewhere different, otherwise a tank stocked in one go
    /// gets hungry in one go and every cuttlefish on screen does the same thing at
    /// the same moment for the rest of the session.
    /// </summary>
    public void Stagger(Random rng)
    {
        Hunger = rng.NextDouble() * 0.6;
        Fatigue = rng.NextDouble() * 0.4;
        Loneliness = rng.NextDouble() * 0.4;
        Boredom = rng.NextDouble() * 0.6;
    }

    /// <param name="perched">On a surface rather than in open water.</param>
    /// <param name="speed">Current speed in px/s: what swimming actually costs.</param>
    /// <param name="company">Another cuttlefish within <see cref="CompanyRange"/>.</param>
    /// <param name="fright">0..1 from the outside world: alarm, or being grabbed.</param>
    public void Tick(double dt, Genome g, bool perched, double speed, bool company, double fright)
    {
        Hunger = Clamp(Hunger + dt / HungerFill * g.Metabolism);

        // Swimming costs, resting on a perch pays it back. Speed is what counts:
        // hovering in open water is not free, and neither is a long climb.
        Fatigue = Clamp(perched && speed < 30
            ? Fatigue - dt / 70
            : Fatigue + dt * speed / 14000);

        Loneliness = Clamp(company ? Loneliness - dt / 45 : Loneliness + dt / 330);

        // The one need whose pace is the animal's own: a restless one runs out of
        // patience with whatever it is doing in half the time a placid one takes.
        Boredom = Clamp(Boredom + dt / (270 - 160 * g.Restlessness));

        // Fright jumps straight to the top and then bleeds off, so a scare colours
        // what the animal does next without pinning it there for good.
        Fear = Clamp(Math.Max(Fear - dt / 22, fright));
    }

    /// <summary>A meal is worth most of a stomach; a nibble is not.</summary>
    public void Fed(double amount) => Hunger = Clamp(Hunger - amount * 4);

    /// <summary>
    /// Starting something new is what settles boredom. Falling into the same
    /// behaviour again barely counts, which is what stops a pet looping on one
    /// trick once that trick happens to score well.
    /// </summary>
    public void Started(string behavior)
    {
        Boredom = Clamp(Boredom - (behavior == _lastBehavior ? 0.10 : 0.55));
        _lastBehavior = behavior;
    }

    private static double Clamp(double v) => Math.Clamp(v, 0, 1);
}
