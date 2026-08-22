using System.Windows;
using CuttlefishPet.Core;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Behaviors;

/// <summary>
/// Sees something worth copying and has a go at being it.
///
/// Cuttlefish are mimics: they do not only match a background, they take on the
/// shape and carriage of other animals outright. This is that, in three parts,
/// and it is deliberately imperfect — the interest is in watching one try.
///
/// Shape: it holds the pose that suits the subject.
/// Colour: the subject's own pixels are sampled off the screen and matched to the
/// nearest chromatophore state, so it really does end up the colour of the thing.
/// Behaviour: it shadows the subject's movement at a fixed offset, so a copied
/// fish darts when the fish darts.
/// </summary>
public sealed class ImitateBehavior : BehaviorBase
{
    public override string Name => "imitate";
    public override bool OverridesPhysics => true;
    public override bool NeedsPerch => false;

    /// <summary>Whatever is being copied, reduced to what the copying needs.</summary>
    private sealed class Subject
    {
        /// <summary>Where it is now, or null once it is gone.</summary>
        public required Func<Point?> Where { get; init; }
        /// <summary>Animation that best stands in for its shape.</summary>
        public required string Pose { get; init; }
        /// <summary>Side of the box to sample its colour from, in physical pixels.</summary>
        public required double Size { get; init; }
        /// <summary>Set when copying a tankmate, whose colours can be read directly.</summary>
        public Pet? Tankmate { get; init; }
    }

    private enum Phase { Approach, Study, Wearing }

    private readonly Subject _it;
    private Phase _phase = Phase.Approach;
    private Point _last;
    private Vector _offset;
    private double _t, _hold;
    private bool _sampling;

    private ImitateBehavior(Subject it) => _it = it;

    /// <summary>
    /// Pick something to copy. Another cuttlefish is the most fun and the most
    /// legible, so it goes in the hat twice; failing that, anything alive in the
    /// tank, and failing that the cuttlebone of something that used to be.
    /// </summary>
    public static ImitateBehavior? Find(BehaviorContext c)
    {
        var pet = c.Pet;
        var options = new List<Subject>();

        foreach (var other in c.World.Pets)
        {
            if (ReferenceEquals(other, pet) || other.Dying) continue;
            if ((other.Pos - pet.Pos).Length > 620) continue;
            var mate = new Subject
            {
                Where = () => other.Dying ? null : other.Pos,
                Pose = "swim",
                Size = 70,
                Tankmate = other,
            };
            options.Add(mate);
            options.Add(mate);
        }

        if (c.World.NearestPrey(pet) is { } fish && (fish.Pos - pet.Pos).Length < 620)
            options.Add(new Subject
            {
                Where = () => fish.Expired ? null : fish.Pos,
                Pose = "hunt",
                Size = 46,
            });

        var treat = c.World.NearestTreat(pet);
        if (treat != null && (treat.Pos - pet.Pos).Length < 620)
            options.Add(new Subject
            {
                Where = () => treat.Expired ? null : treat.Pos,
                Pose = "hunt",
                Size = 38,
            });

        if (c.World.NearestBone(pet) is { } bone)
            options.Add(new Subject
            {
                Where = () => bone.Expired ? null : bone.Pos,
                Pose = "mimic_icon",
                Size = 44,
            });

        if (options.Count == 0) return null;
        return new ImitateBehavior(options[c.Rng.Next(options.Count)]);
    }

    public override void Enter(BehaviorContext c)
    {
        c.Pet.Anim.Play("swim");
        c.Pet.Surface = null;
        _last = _it.Where() ?? c.Pet.Pos;
    }

    public override void Tick(BehaviorContext c, double dt)
    {
        var pet = c.Pet;
        _t += dt;

        var here = _it.Where();
        if (here == null)
        {
            // It left. Whatever was half-copied drains away on its own.
            Next = new SwimFreeBehavior();
            Done = true;
            return;
        }
        var target = here.Value;
        pet.PupilTarget = target;

        switch (_phase)
        {
            case Phase.Approach:
            {
                // Draw up alongside — close enough to study, not so close as to be
                // in the way of whatever it is doing.
                var want = target + new Vector(target.X > pet.Pos.X ? -95 : 95, 8);
                var to = want - pet.Pos;
                if (to.Length < 40 || _t > 9)
                {
                    _phase = Phase.Study;
                    _t = 0;
                    _offset = pet.Pos - target;
                    pet.Anim.Play("idle");
                    break;
                }
                var desired = to / Math.Max(1, to.Length) * Math.Min(160, to.Length * 2.4);
                pet.Vel += (desired - pet.Vel) * Math.Min(1, 3.2 * dt);
                pet.Pos += pet.Vel * dt;
                PhysicsEngine.ClampToTank(pet, c.World);
                if (Math.Abs(pet.Vel.X) > 12) pet.FacingRight = pet.Vel.X > 0;
                break;
            }

            case Phase.Study:
            {
                // Hangs there taking it in, tilting the way an animal does when it
                // is working something out.
                pet.Vel *= Math.Exp(-3 * dt);
                pet.Pos += pet.Vel * dt;
                pet.FacingRight = target.X > pet.Pos.X;
                pet.VisualBob = Math.Sin(_t * 2.6) * 3;
                pet.Rotation = Math.Sin(_t * 1.3) * 7;

                if (!_sampling)
                {
                    _sampling = true;
                    TakeColour(pet, target);
                }
                if (_t > 1.6)
                {
                    _phase = Phase.Wearing;
                    _t = 0;
                    _hold = 6 + c.Rng.NextDouble() * 6;
                    pet.Rotation = 0;
                    pet.Anim.Play(_it.Pose, restart: true);
                }
                break;
            }

            case Phase.Wearing:
            {
                // Shadowing it: same offset, same moves, a beat behind.
                var want = target + _offset;
                var to = want - pet.Pos;
                var desired = to.Length < 1
                    ? default
                    : to / to.Length * Math.Min(220, to.Length * 3.0);
                pet.Vel += (desired - pet.Vel) * Math.Min(1, 2.6 * dt);
                pet.Pos += pet.Vel * dt;
                PhysicsEngine.ClampToTank(pet, c.World);

                var moved = target - _last;
                if (Math.Abs(moved.X) > 0.6) pet.FacingRight = moved.X > 0;
                _last = target;

                // A tankmate is copied move for move, including whatever it happens
                // to be doing with its own body.
                if (_it.Tankmate is { } mate && pet.Anim.Current.Name != mate.Anim.Current.Name)
                    pet.Anim.Play(mate.Anim.Current.Name);
                else if (pet.Anim.Finished)
                    pet.Anim.Play(_it.Pose, restart: true);

                pet.VisualBob = Math.Sin(_t * 3.4) * 2;

                if (_t > _hold)
                {
                    Next = new SwimFreeBehavior();
                    Done = true;
                }
                break;
            }
        }
    }

    /// <summary>
    /// Read the subject's actual colour off the screen and wear the nearest match.
    /// A tankmate is copied directly instead — its palette is already known, and a
    /// screen grab of a half-transparent cuttlefish would mostly return desktop.
    /// </summary>
    private void TakeColour(Pet pet, Point target)
    {
        if (_it.Tankmate is { } mate)
        {
            pet.HomePalette = mate.HomePalette;
            pet.SkinPattern = mate.SkinPattern;
            return;
        }

        double half = _it.Size / 2;
        var box = new Rect(target.X - half, target.Y - half, _it.Size, _it.Size);
        Task.Run(() =>
        {
            // Best effort: a failed grab just means it keeps its own colour, which
            // is a perfectly good outcome for an animal trying to be a shrimp.
            var look = CamoSampler.Sample(box);
            if (look != null) pet.HomePalette = Palettes.NearestTo(look.Dominant);
        });
    }

    public override void Exit(BehaviorContext c)
    {
        c.Pet.PupilTarget = null;
        c.Pet.Rotation = 0;
    }
}
