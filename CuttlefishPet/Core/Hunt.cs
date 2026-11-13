namespace CuttlefishPet.Core;

/// <summary>
/// The shape of one attack: cross, stalk, coil, burst, recover.
///
/// Before this the hunt had one gear and a 0.8s run-up, and the whole thing read
/// as a result rather than as an event -- something slid in from the side and a
/// cuttlefish was gone. Two separate faults were doing that.
///
/// The first is that fast is not the same as tense. A run at 470px/s that starts
/// 500px out is a second and a half of a fish getting closer at a constant rate,
/// which is nothing to watch. Tension is a *held* thing: the predator is already
/// near, the animal does not know it yet, and nothing is happening. So the speed
/// now goes the other way round -- it slows down to close the last stretch, stops
/// almost dead for a third of a second, and only then goes, at 760px/s, over
/// about 170px. Call it three and a half seconds of event, of which the strike is
/// a quarter. The strike is nearly twice as fast as the old one; everything in
/// front of it is new.
///
/// The second fault was in PetManager, not here, and it was the worse of the two:
/// the quarry was told to flee the instant the hunter fixed on it. There was
/// therefore no moment in the entire hunt where the predator was close and the
/// prey was oblivious -- which is the only moment that is actually frightening.
/// The alarm radius now collapses while stalking and opens on the burst.
///
/// Arithmetic over one little object with no window and no visual in it, for the
/// same reason as <see cref="Depths"/>: the pacing can then be driven for an hour
/// in DriveTests, and a bad number in here can never reach a position or a
/// behaviour. Every value that leaves is clamped and finite.
/// </summary>
public enum HuntPhase
{
    /// <summary>Passing through. Nothing has caught its eye.</summary>
    Crossing,
    /// <summary>Fixed on something and closing slowly, hoping not to be noticed.</summary>
    Stalk,
    /// <summary>Near enough. Almost stopped, tail still. The held breath.</summary>
    Coil,
    /// <summary>Committed. The only phase a bite can land in.</summary>
    Burst,
    /// <summary>Overshot or snapped at water: coasting, and not allowed to re-lock.</summary>
    Recover,
}

/// <summary>One attack's pacing, and the tail-beat and bank that are drawn from it.</summary>
public sealed class HuntState
{
    public HuntPhase Phase = HuntPhase.Crossing;

    /// <summary>Seconds spent in the current phase.</summary>
    public double In;

    /// <summary>
    /// The clock the swimming strip is sampled on. Advanced by speed rather than
    /// by wall time, which is the whole of the animation fix: the sprite was fed
    /// <c>Age</c>, so a shark stalking at 150px/s beat its tail at exactly the
    /// rate it did at 470 -- the one tell that it was a picture being slid about.
    /// </summary>
    public double Beat;

    /// <summary>Degrees of nose-down tilt being drawn, eased toward the heading.</summary>
    public double Bank;
}

public static class Hunt
{
    /// <summary>
    /// Fraction of cruising speed while stalking. Slower than cruising on purpose:
    /// a predator that closes in faster than it crosses reads as a charge, and a
    /// charge has no suspense in it. This looks like it does not want to be seen.
    /// </summary>
    public const double StalkSpeed = 0.46;

    /// <summary>Barely moving while it is coiled -- enough that it is not a frozen sprite.</summary>
    public const double CoilSpeed = 0.10;

    /// <summary>
    /// Where it settles before committing, in pixels. Far enough that the strike
    /// is a visible distance covered and the quarry has somewhere to go; close
    /// enough that it is unmistakably about to happen.
    /// </summary>
    public const double HoldGap = 170;

    /// <summary>
    /// Shortest and longest stalk. The minimum is there because a hunter that
    /// happens to enter already at the hold distance would otherwise skip the
    /// whole build-up; the maximum because a quarry that keeps drifting away
    /// would otherwise stall the visit forever at half speed.
    /// </summary>
    public const double StalkMin = 1.1, StalkMax = 3.4;

    /// <summary>
    /// How far past the hold mark a quarry may be when the stalk times out and
    /// still be worth going for, as a multiple of <see cref="HoldGap"/>.
    ///
    /// This is a guard rather than a feature. One live visit ended with a burst
    /// launched from 816px -- five times the hold distance -- after a mid-stalk
    /// change of quarry, and four attempts to reproduce it came out clean, so I
    /// could neither derive the path nor trigger it. What is certain is that a
    /// lunge from that far away is not a hunt however it got there: it is an
    /// animal sprinting at nothing. Beyond this the stalk is abandoned and it
    /// goes back to crossing, which is also the better behaviour on its own
    /// merits -- a predator whose quarry has outrun the approach has lost it.
    /// </summary>
    public const double GiveUpAt = 2.0;

    /// <summary>
    /// The held beat. A third of a second: long enough to read as a decision,
    /// short enough that it is not a fish stopping for a think. At 0.8s in the
    /// first cut it read as a bug.
    /// </summary>
    public const double CoilLen = 0.34;

    /// <summary>
    /// How long a burst may last before it counts as having gone past. 170px at
    /// burst speed is under a quarter of a second, so this is mostly slack for a
    /// quarry that bolts -- a short chase, not a second lunge.
    /// </summary>
    public const double BurstLen = 0.8;

    /// <summary>Seconds of coasting after a burst, during which it cannot fix on anything.</summary>
    public const double RecoverLen = 1.2;

    /// <summary>
    /// Tail beats per second at a standstill and at cruising speed. Not
    /// proportional: a drifting fish still sculls, and one at four times cruise
    /// does not beat four times as fast -- it is already at its stroke limit and
    /// the extra is in the body, not the rate. Square-rooted, so the difference
    /// between stalk and burst is obvious without the burst becoming a blur.
    /// </summary>
    public const double BeatStill = 0.45, BeatCruise = 1.55;

    /// <summary>Most degrees of tilt that will ever be drawn, and how much of the heading is taken.</summary>
    public const double BankMax = 38, BankOf = 0.8;

    /// <summary>Seconds for the drawn tilt to catch up with the heading. A body has mass.</summary>
    public const double BankEase = 0.18;

    private static double Fin(double v, double fallback = 0) =>
        double.IsFinite(v) ? v : fallback;

    /// <summary>
    /// Speed this phase wants, as a fraction of cruising. Burst is handed back
    /// separately because it is not a multiple of cruising in spirit -- it is the
    /// animal's top speed and the one number that should feel out of scale.
    /// </summary>
    public static double CruiseFactor(HuntState h, double gap)
    {
        gap = Math.Max(0, Fin(gap, HoldGap));
        return h.Phase switch
        {
            // Easing down over the last stretch rather than braking at the mark:
            // a constant-speed slide to a dead stop looked mechanical, and the
            // slow-down is itself part of what makes it read as deliberate.
            // Easing on distance rather than on a timer, and down to almost
            // nothing: it has to *arrive* and wait, not creep through the hold
            // mark and end up nose to nose with the animal before it commits.
            // At 0.34 as the floor it was still closing at 50px/s through the
            // whole hold, and a burst that starts 30px out has nowhere to go --
            // which is the exact bug that put a shark on top of a cuttlefish
            // with no run at all on the second live run.
            HuntPhase.Stalk => StalkSpeed * Math.Clamp(gap / (HoldGap * 1.6), 0.08, 1.15),
            HuntPhase.Coil => CoilSpeed,
            HuntPhase.Recover => 1.0,
            _ => 1.0,
        };
    }

    /// <summary>Whether a bite may resolve at all. Only a committed run counts.</summary>
    public static bool CanBite(HuntState h) => h.Phase == HuntPhase.Burst;

    /// <summary>
    /// Whether it is allowed to fix on something new. It may change its mind
    /// early in an approach, but not late in one: a predator that re-aims at the
    /// last moment is a predator that never misses, and the second half of the
    /// stalk is the part that is supposed to be committed to somebody.
    /// </summary>
    public static bool CanLock(HuntState h) =>
        h.Phase == HuntPhase.Crossing ||
        (h.Phase == HuntPhase.Stalk && h.In < StalkMin);

    /// <summary>
    /// How far off a cuttlefish notices it, in pixels. A stalking predator is
    /// trying not to be seen and the whole tank bolting while it sneaks up undoes
    /// the point; the burst is what everything reacts to.
    /// </summary>
    public static double AlarmRadius(HuntState h) =>
        h.Phase is HuntPhase.Stalk or HuntPhase.Coil ? 95 : 300;

    /// <summary>
    /// Advance the phase clock. <paramref name="hasQuarry"/> is whether something
    /// is currently fixed on, <paramref name="gap"/> the distance to it.
    /// </summary>
    /// <returns>True on the tick the burst begins -- the moment everything else hangs off.</returns>
    public static bool Advance(HuntState h, double dt, bool hasQuarry, double gap)
    {
        dt = Math.Clamp(Fin(dt), 0, 0.25);
        gap = Math.Max(0, Fin(gap, 1e4));
        h.In = Fin(h.In) + dt;

        switch (h.Phase)
        {
            case HuntPhase.Crossing:
                if (hasQuarry) Enter(h, HuntPhase.Stalk);
                break;

            case HuntPhase.Stalk:
                // Losing the quarry mid-stalk drops it straight back to crossing
                // rather than into recovery: nothing was spent, so nothing is owed.
                if (!hasQuarry) { Enter(h, HuntPhase.Crossing); break; }
                if (h.In >= StalkMin && gap <= HoldGap) { Enter(h, HuntPhase.Coil); break; }
                if (h.In >= StalkMax)
                    Enter(h, gap > HoldGap * GiveUpAt ? HuntPhase.Crossing : HuntPhase.Coil);
                break;

            case HuntPhase.Coil:
                if (!hasQuarry) { Enter(h, HuntPhase.Crossing); break; }
                if (h.In >= CoilLen) { Enter(h, HuntPhase.Burst); return true; }
                break;

            case HuntPhase.Burst:
                // A burst always ends in recovery, hit, miss or quarry gone. It is
                // the one phase that cannot be left early: a predator that could
                // re-aim mid-lunge never misses, and a hunt nobody survives stops
                // being a hunt.
                if (h.In >= BurstLen) Enter(h, HuntPhase.Recover);
                break;

            case HuntPhase.Recover:
                if (h.In >= RecoverLen) Enter(h, HuntPhase.Crossing);
                break;
        }
        return false;
    }

    /// <summary>Called when the quarry is gone for good: back to passing through.</summary>
    public static void Reset(HuntState h) => Enter(h, HuntPhase.Crossing);

    /// <summary>
    /// Start the approach over, because it is now approaching somebody else.
    ///
    /// A stalk is per-quarry, and the clock used to run straight through a swap.
    /// Live, that read as a shark gliding in on one animal, noticing a closer one
    /// a metre away, and lunging at it on the spot with no build-up at all --
    /// the one thing this whole change exists to get rid of. Switching costs it
    /// the seconds it had banked, which is also the right trade to give it.
    /// </summary>
    public static void Requarry(HuntState h)
    {
        if (h.Phase == HuntPhase.Stalk) h.In = 0;
    }

    /// <summary>Cut the current run short and coast. Used after a bite, hit or miss.</summary>
    public static void Spend(HuntState h)
    {
        if (h.Phase != HuntPhase.Recover) Enter(h, HuntPhase.Recover);
    }

    private static void Enter(HuntState h, HuntPhase phase)
    {
        h.Phase = phase;
        h.In = 0;
    }

    /// <summary>Tail beats per second at a given speed, taking cruising as the reference.</summary>
    public static double BeatRate(double speed, double cruise)
    {
        speed = Math.Max(0, Fin(speed));
        cruise = Fin(cruise, 1);
        if (cruise <= 1) cruise = 1;
        return BeatStill + (BeatCruise - BeatStill) * Math.Sqrt(Math.Min(4, speed / cruise));
    }

    /// <summary>
    /// Advance the beat clock and ease the bank toward the heading.
    ///
    /// The tail stops while coiled. That was not the plan -- the plan was a frame
    /// picked out of the strip as a cocked pose -- but holding the clock where it
    /// is turns out to read better and costs nothing, and it does not depend on
    /// which frame of a six-frame strip happens to have the tail over. A glide
    /// with a still tail is what a fish about to go actually looks like.
    /// </summary>
    public static void Animate(HuntState h, double dt, double speed, double cruise,
                               double vx, double vy)
    {
        dt = Math.Clamp(Fin(dt), 0, 0.25);
        if (h.Phase != HuntPhase.Coil)
            h.Beat = Fin(h.Beat) + dt * BeatRate(speed, cruise);
        // Wrapped, or after a day up the clock is large enough that single frames
        // start being skipped by floating-point error in the renderer's cast.
        if (h.Beat > 1e5) h.Beat -= 1e5;

        double want = Math.Clamp(
            Math.Atan2(Fin(vy), Math.Abs(Fin(vx))) * (180 / Math.PI) * BankOf,
            -BankMax, BankMax);
        h.Bank = Fin(h.Bank) + (want - Fin(h.Bank)) * Math.Min(1, dt / BankEase);
        h.Bank = Math.Clamp(Fin(h.Bank), -BankMax, BankMax);
    }
}
