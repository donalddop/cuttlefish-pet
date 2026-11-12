using System.Text.Json;
using System.Windows;
using CuttlefishPet.Behaviors;

namespace CuttlefishPet.Core;

/// <summary>
/// What Desktop Dude can do to the tank over the link: carry and throw, ride, pet, hit
/// and -- rarely, and never the last few -- eat. Everything goes through the same
/// behaviours and the same graveyard as the tank's own events, so a cuttlefish the dude
/// ate is buried like any other, with its name and its fate on the stone.
/// </summary>
public sealed partial class PetManager
{
    private readonly List<object> _linkEvents = new();
    private Point? _dude;
    private double _dudeRage, _dudeSeenAt = double.NegativeInfinity;

    /// <summary>Never let him eat below this many cuttlefish.</summary>
    private const int DudeMayEatAbove = 2;

    public string HandleLink(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("dude", out var d) && d.ValueKind == JsonValueKind.Array && d.GetArrayLength() >= 3)
            {
                _dude = new Point(d[0].GetDouble(), d[1].GetDouble());
                _dudeRage = d[2].GetDouble();
                _dudeSeenAt = _clock;
            }
            if (root.TryGetProperty("ops", out var ops) && ops.ValueKind == JsonValueKind.Array)
                foreach (var op in ops.EnumerateArray()) LinkOp(op);
            ReactToDude();
        }
        catch (Exception ex)
        {
            Log($"link: {ex.Message}");
        }

        var fish = _pets.Where(p => !p.Dying).Select(p =>
        {
            var b = p.Bounds;
            var a = p.Anim.Current;
            double s = p.DrawScale;
            // Where a rider's seat is: the top of the mantle, not of the sprite frame.
            double back = p.Pos.Y - (a.Anchor.Y - a.BackTop(p.Anim.FrameIndex)) * s + p.VisualBob;
            return new
            {
                id = p.Id, name = p.Name, x = p.Pos.X, y = p.Pos.Y,
                l = b.Left, t = b.Top, r = b.Right, bo = b.Bottom, back,
                vx = p.Vel.X, vy = p.Vel.Y, right = p.FacingRight,
                beh = p.Machine.Current.Name, scale = p.Scale,
            };
        }).ToArray();
        string reply = JsonSerializer.Serialize(new { v = 1, fish, events = _linkEvents.ToArray() });
        _linkEvents.Clear();
        return reply;
    }

    private void LinkOp(JsonElement op)
    {
        string kind = op.TryGetProperty("op", out var k) ? k.GetString() ?? "" : "";
        int id = op.TryGetProperty("id", out var i) ? i.GetInt32() : -1;
        var pet = _pets.FirstOrDefault(p => p.Id == id && !p.Dying);
        if (pet == null)
        {
            _linkEvents.Add(new { ev = "gone", id });
            return;
        }
        double D(string key, double fallback = 0) =>
            op.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;

        switch (kind)
        {
            case "hold":
            {
                var at = new Point(D("x", pet.Pos.X), D("y", pet.Pos.Y));
                bool right = D("f", 1) > 0;
                if (pet.Machine.Current is DudeHeldBehavior held) held.Update(at, right);
                else
                {
                    pet.Machine.Force(new DudeHeldBehavior(at, right));
                    Log($"de dude pakt #{pet.Id} {pet.Name}");
                }
                break;
            }
            case "release":
                if (pet.Machine.Current is DudeHeldBehavior h) h.Throw(new Vector(D("vx"), D("vy")));
                break;
            case "ride":
                if (pet.Machine.Current is not DudeRideBehavior)
                {
                    pet.Machine.Force(new DudeRideBehavior(D("secs", 10)));
                    Log($"de dude rijdt op #{pet.Id} {pet.Name}");
                }
                break;
            case "pet":
                pet.Pestered = Math.Max(0, pet.Pestered - 1);
                pet.Machine.Force(new HappyBehavior(2.5));
                break;
            case "hit":
                pet.Pestered += 1.2;
                pet.Surface = null;
                pet.Machine.Force(pet.Pestered >= 4 ? new DizzyBehavior() : new StartleBehavior());
                pet.Vel = new Vector(D("vx"), D("vy"));
                if (_rng.NextDouble() < 0.4)
                {
                    _renderer.SpawnInk(pet.Pos);
                    _sound.Play("squirt", 0.4);
                    _linkEvents.Add(new { ev = "ink", id = pet.Id, x = pet.Pos.X, y = pet.Pos.Y });
                }
                break;
            case "eat":
                if (_pets.Count(p => !p.Dying) <= DudeMayEatAbove)
                {
                    _linkEvents.Add(new { ev = "refused", id = pet.Id });
                    break;
                }
                pet.Fate = "opgegeten door de dude";
                pet.Machine.Force(new EatenBehavior());
                _linkEvents.Add(new { ev = "eaten", id = pet.Id, name = pet.Name });
                Log($"de dude eet #{pet.Id} {pet.Name} op");
                break;
        }
    }

    /// <summary>A furious dude close by is something to get away from.</summary>
    private void ReactToDude()
    {
        if (_dude is not { } dude || _clock - _dudeSeenAt > 1 || _dudeRage < 0.7) return;
        foreach (var pet in _pets)
        {
            if (pet.Dying || !pet.Machine.Current.Interruptible) continue;
            if (pet.Machine.Current is DudeHeldBehavior or DudeRideBehavior or StartleBehavior or FleeBehavior) continue;
            if ((pet.Pos - dude).Length < 150 && _rng.NextDouble() < 0.02)
                pet.Machine.Force(new StartleBehavior());
        }
    }
}
