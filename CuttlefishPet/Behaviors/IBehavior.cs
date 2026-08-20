using CuttlefishPet.Audio;
using CuttlefishPet.Core;
using CuttlefishPet.Interop;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>Named so call sites read as intent, not as a bare boolean.</summary>
public delegate void SpawnPet(System.Windows.Point at, bool hatchling);

public sealed class BehaviorContext
{
    public required Pet Pet { get; init; }
    public required WorldState World { get; init; }
    public required GlobalInput Input { get; init; }
    public required SoundService Sound { get; init; }
    public required SpriteRenderer Renderer { get; init; }
    public required Random Rng { get; init; }

    /// <summary>
    /// Put another cuttlefish in the tank. Hatchlings arrive tiny and grow;
    /// anything else turns up as a young adult.
    /// </summary>
    public required SpawnPet SpawnPet { get; init; }
    /// <summary>Leave something behind: ink blots, egg clutches.</summary>
    public required Action<Prop> AddProp { get; init; }
    /// <summary>Take this pet out of the tank (the ghost swims off for good).</summary>
    public required Action<Pet> RemovePet { get; init; }
}

public abstract class BehaviorBase
{
    public abstract string Name { get; }
    /// <summary>True while this behavior moves the pet itself (physics engine stands down).</summary>
    public virtual bool OverridesPhysics => false;
    /// <summary>Can ambient events (flee, typing react) preempt this behavior?</summary>
    public virtual bool Interruptible => true;
    public bool Done { get; protected set; }
    /// <summary>Explicit successor; when null the machine picks a weighted-random next.</summary>
    public BehaviorBase? Next { get; protected set; }

    public virtual void Enter(BehaviorContext c) { }
    public virtual void Tick(BehaviorContext c, double dt) { }
    public virtual void Exit(BehaviorContext c) { }
}
