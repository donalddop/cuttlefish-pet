using System.Windows;

namespace CuttlefishPet.Core;

/// <summary>
/// Who decides when something happens, and where you are looking when it does.
///
/// Until now every event fired on its own timer with no idea the others existed,
/// so the tank gave you three things at once and then ten minutes of nothing.
/// This is the layer above them: at most one big thing on stage, a required quiet
/// afterwards, and a slow heartbeat so a tank nobody has looked at for seven
/// minutes starts hurrying its clocks rather than staying dead.
///
/// The gates are deliberately all one-liners at the call sites -- a multiplier on
/// a countdown and a question before firing -- because the alternative is moving
/// eighty behaviours and five event loops under a scheduler, and that is a
/// rewrite rather than a Saturday.
///
/// What it does NOT hold the reins on, on purpose: everything two animals do
/// between themselves (rivalry, courtship, mirroring, fights over a shrimp).
/// Those are the tank's weather rather than its events, several of them decide
/// who breeds and who dies, and the house rule is that population is changed on
/// a day when population is the thing being changed and Tools/graveyard.py is
/// run before and after.
///
/// Arithmetic and a few strings, no window and no animal, so a day of staging can
/// be driven in DriveTests in a second.
/// </summary>
public sealed class StageState
{
    /// <summary>What is playing, or null when the stage is clear.</summary>
    public string? OnStage;

    /// <summary>Seconds this act has been running, for the watchdog.</summary>
    public double Running;

    /// <summary>Seconds of required quiet left after the last act.</summary>
    public double RestFor;

    /// <summary>Seconds since anything at all happened.</summary>
    public double Quiet;

    /// <summary>Where the cursor has been lately, if it has been anywhere.</summary>
    public Point Attention;
    public bool Watching;
}

public static class Director
{
    /// <summary>Seconds of quiet after a big thing. Nothing may start inside it.</summary>
    /// A bit over a minute: long enough that two events never read as one long
    /// event, short enough that the rest does not itself become the dead air it
    /// was put there to prevent.
    public const double Rest = 75;

    /// <summary>Seconds of nothing after which the clocks start to hurry.</summary>
    public const double Heartbeat = 360;

    /// <summary>How much faster the clocks run as soon as it has been quiet too long.</summary>
    public const double Hurry = 3.0;

    /// <summary>
    /// The ceiling on the hurry. The quiet keeps growing and so does the pace: a
    /// flat 3x was not enough on its own, because an event clock that happened to
    /// roll 1200 seconds still left a twelve-minute hole in a six-hour run, which
    /// is exactly the dead air the heartbeat is for. Every further Heartbeat of
    /// silence adds another 3x, to a hard 9x -- past that the tank would start
    /// emptying its whole repertoire at somebody who had just sat down.
    /// </summary>
    public const double MaxHurry = 9.0;

    /// <summary>
    /// Seconds after which an act that never reported its end is taken off the
    /// stage anyway.
    ///
    /// The one failure that would be worse than no director at all: if something
    /// claims the stage and then returns by a path that forgets to clear it, every
    /// clock in the tank is stopped for good and the aquarium is dead for as long
    /// as the app runs. A predator visit expires at 75s, an ammonite crosses in
    /// about two minutes, a ritual is over in one -- 300 is far outside all three
    /// and far inside "nobody noticed for an hour".
    /// </summary>
    public const double MaxAct = 300;

    /// <summary>Seconds of ease on the attention point. Where the cursor has *been*.</summary>
    public const double AttentionEase = 26;

    /// <summary>How far from the attention point an event may still be staged.</summary>
    public const double StageWithin = 420;

    private static double Fin(double v, double fallback = 0) => double.IsFinite(v) ? v : fallback;

    /// <summary>Is the stage clear enough for something big to start?</summary>
    public static bool MayStart(StageState s) => s.OnStage == null && s.RestFor <= 0;

    /// <summary>Something big is starting.</summary>
    public static void Started(StageState s, string what)
    {
        s.OnStage = what;
        s.Running = 0;
        s.Quiet = 0;
    }

    /// <summary>Something big has finished. Clears the stage and starts the rest.</summary>
    public static void Ended(StageState s, string what)
    {
        if (s.OnStage != what) return;   // a straggler reporting late must not clear someone else
        s.OnStage = null;
        s.Running = 0;
        s.RestFor = Rest;
        s.Quiet = 0;
    }

    /// <summary>
    /// Something small happened -- a passing school, a thrown shrimp. It does not
    /// take the stage and nothing waits for it, but the tank is not dead either,
    /// so the heartbeat counts it.
    /// </summary>
    public static void Noted(StageState s) => s.Quiet = 0;

    /// <summary>
    /// Advance the clocks and follow the cursor. Returns the name of an act the
    /// watchdog had to take off the stage, or null -- the caller logs it, because
    /// it means a bug somewhere else and should never be silent.
    /// </summary>
    public static string? Tick(StageState s, double dt, Point cursor, bool moved)
    {
        dt = Math.Clamp(Fin(dt), 0, 0.25);
        s.RestFor = Math.Max(0, Fin(s.RestFor) - dt);
        s.Quiet = Math.Min(1e6, Fin(s.Quiet) + dt);

        if (moved && double.IsFinite(cursor.X) && double.IsFinite(cursor.Y))
        {
            if (!s.Watching) { s.Attention = cursor; s.Watching = true; }
            else
            {
                double k = Math.Min(1, dt / AttentionEase);
                s.Attention = new Point(s.Attention.X + (cursor.X - s.Attention.X) * k,
                                        s.Attention.Y + (cursor.Y - s.Attention.Y) * k);
            }
        }

        if (s.OnStage == null) return null;
        s.Running = Fin(s.Running) + dt;
        if (s.Running < MaxAct) return null;

        string stuck = s.OnStage;
        s.OnStage = null;
        s.Running = 0;
        s.RestFor = Rest;
        return stuck;
    }

    /// <summary>
    /// Clear the stage and the rest with it. Only for the debug summons: `hunter`,
    /// `stalk`, `rise`, `strike` and `ammonite` have to do the thing now, and a
    /// command that silently did nothing for the next seventy seconds because the
    /// tank happened to be resting would be a debug tool you cannot trust -- which
    /// is worse than none, because you would read the silence as the feature being
    /// broken.
    /// </summary>
    public static void Clear(StageState s)
    {
        s.OnStage = null;
        s.Running = 0;
        s.RestFor = 0;
    }

    /// <summary>Has nothing happened for long enough that the tank reads as dead?</summary>
    public static bool Overdue(StageState s) =>
        MayStart(s) && Fin(s.Quiet) > Heartbeat;

    /// <summary>
    /// What to multiply an event's countdown by this tick. Zero while the stage is
    /// busy or resting, so the other clocks hold where they are instead of all
    /// running out behind the curtain and firing together the moment it lifts --
    /// which is the pile-up this whole file exists to prevent.
    /// </summary>
    public static double Pace(StageState s)
    {
        if (!MayStart(s)) return 0;
        double over = Fin(s.Quiet) - Heartbeat;
        if (over <= 0) return 1;
        return Math.Clamp(Hurry * (1 + over / Heartbeat), Hurry, MaxHurry);
    }

    /// <summary>
    /// A height to stage something at: near where the cursor has been, when it has
    /// been anywhere, and anywhere in the tank when it has not. An event on the
    /// monitor you are not looking at is thrown away.
    /// </summary>
    public static double StageY(StageState s, Random rng, double top, double bottom)
    {
        top = Fin(top); bottom = Fin(bottom, top + 1);
        if (bottom - top < 2) return top;
        double anywhere = top + rng.NextDouble() * (bottom - top);
        if (!s.Watching || !double.IsFinite(s.Attention.Y)) return anywhere;

        // Not on the cursor: near it. On it is where the animals already go when
        // they are being curious, and a predator that always surfaces under the
        // mouse pointer stops being weather and starts being a jump scare.
        double near = s.Attention.Y + (rng.NextDouble() * 2 - 1) * StageWithin;
        return Math.Clamp(near, top, bottom);
    }
}