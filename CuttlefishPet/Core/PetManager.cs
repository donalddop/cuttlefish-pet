using System.Windows;
using CuttlefishPet.Audio;
using CuttlefishPet.Behaviors;
using CuttlefishPet.Interop;
using CuttlefishPet.Rendering;

namespace CuttlefishPet.Core;

/// <summary>
/// Owns all pets and the shared world sensing. One tick: sense → route input →
/// behave → physics → render.
/// </summary>
public sealed class PetManager
{
    private const double DoubleClickSeconds = 0.4;
    private const double RivalDistance = 110;

    private readonly OverlayWindow _overlay;
    private readonly SpriteRenderer _renderer;
    private readonly Dictionary<string, SpriteAnim> _library;
    private readonly GlobalInput _input;
    private readonly WindowTracker _tracker = new();
    private readonly SoundService _sound;
    private readonly WorldState _world = new();
    private readonly Random _rng = new();
    private readonly List<Pet> _pets = new();
    private readonly List<Prop> _props = new();
    private readonly List<Pet> _leaving = new();
    private readonly List<Point> _hatching = new();
    private double _clock, _lastDownAt = double.NegativeInfinity, _rivalCooldown;
    private Pet? _lastDownPet;
    private int _tick;

    public int Count => _pets.Count;

    public PetManager(OverlayWindow overlay, SpriteRenderer renderer,
        Dictionary<string, SpriteAnim> library, GlobalInput input, SoundService sound)
    {
        _overlay = overlay;
        _renderer = renderer;
        _library = library;
        _input = input;
        _sound = sound;
    }

    public void Spawn() => Spawn(null);

    public void Spawn(Point? at)
    {
        var wa = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea; // physical px
        var pet = new Pet
        {
            Anim = new AnimationPlayer(_library),
            // Swim in from somewhere in open water rather than dropping from the sky.
            Pos = at ?? new Point(wa.Left + 120 + _rng.NextDouble() * (wa.Width - 240),
                                  wa.Top + 120 + _rng.NextDouble() * (wa.Height - 300)),
        };
        pet.Palette = pet.FromPalette = Palettes.PickRandom(_rng);
        pet.PaletteChangeIn = 4 + _rng.NextDouble() * 20;
        pet.SkinPattern = _rng.Next(5);
        pet.SkinStrength = 0.45 + _rng.NextDouble() * 0.30;
        pet.SheenStrength = 0.10 + _rng.NextDouble() * 0.14;
        pet.Visual = _renderer.CreateVisual();
        pet.Machine = new BehaviorMachine(NewContext(pet));
        _pets.Add(pet);
    }

    public void RemoveOne()
    {
        if (_pets.Count == 0) return;
        var pet = _pets[^1];
        _pets.RemoveAt(_pets.Count - 1);
        _renderer.RemoveVisual(pet.Visual);
    }

    /// <summary>Drop a shrimp at the cursor for the pets to chase down.</summary>
    public void TossTreat()
    {
        var treat = new Treat
        {
            Pos = _input.Cursor,
            Vel = new Vector(_rng.Next(-120, 121), -180),
            Visual = _renderer.CreateProp("shrimp"),
        };
        _world.Treats.Add(treat);
    }

    private BehaviorContext NewContext(Pet pet) => new()
    {
        Pet = pet, World = _world, Input = _input,
        Sound = _sound, Renderer = _renderer, Rng = _rng,
        // Queued, never applied mid-tick: the pet list is being iterated.
        SpawnPet = p => _hatching.Add(p),
        AddProp = prop => { prop.Visual = _renderer.CreateProp(prop.Anim); _props.Add(prop); },
        RemovePet = p => _leaving.Add(p),
    };

    public void Tick(double dt)
    {
        _clock += dt;
        _input.Tick(dt);
        _tracker.Tick();
        RebuildWorld(dt);
        RouteMouse();

        bool wantClicks = false;
        foreach (var pet in _pets)
        {
            ReactToNewWindows(pet);
            pet.Machine.Tick(dt);
            PhysicsEngine.Tick(pet, _world, dt);
            pet.Anim.Tick(dt);
            UpdateColour(pet, dt);
            UpdateEyes(pet, dt);
            MaybeBubble(pet, dt);
            _renderer.Update(pet);
            if (pet.Machine.Current is DragBehavior || pet.Bounds.Contains(_world.Cursor))
                wantClicks = true;
        }

        ApplyArrivalsAndDepartures();
        TickTreats(dt);
        TickProps(dt);
        CheckRivalry(dt);
        _renderer.TickEffects(dt);

        _overlay.SetClickThrough(!wantClicks);
        if (++_tick % 120 == 0)
        {
            _overlay.EnsureTopmost();
            LogDebug();
        }
    }

    private void RouteMouse()
    {
        while (_input.TryDequeue(out var e))
        {
            if (e.Kind == MouseEventKind.Down)
            {
                Pet? hit = null;
                for (int i = _pets.Count - 1; i >= 0; i--)
                {
                    if (_pets[i].Bounds.Contains(new Point(e.X, e.Y))) { hit = _pets[i]; break; }
                }
                if (hit == null) continue;

                // Second click on the same pet in quick succession = a friendly pet,
                // not another drag.
                if (hit == _lastDownPet && _clock - _lastDownAt < DoubleClickSeconds)
                {
                    hit.Machine.Force(new HappyBehavior(1.8));
                    _lastDownPet = null;
                    _lastDownAt = double.NegativeInfinity;
                    continue;
                }
                _lastDownPet = hit;
                _lastDownAt = _clock;
                hit.Machine.HandleMouse(e);
            }
            else
            {
                foreach (var p in _pets) p.Machine.HandleMouse(e);
            }
        }
    }

    /// <summary>Chromatophores never sit still: colours drift, and moods override.</summary>
    private void UpdateColour(Pet pet, double dt)
    {
        // The sheen never stops crawling over the skin.
        pet.SheenPhase = (pet.SheenPhase + dt * 0.075) % 1.0;

        if (pet.PaletteBlend < 1)
            pet.PaletteBlend = Math.Min(1, pet.PaletteBlend + dt / 1.8);
        else
            pet.FromPalette = pet.Palette;

        pet.PaletteChangeIn -= dt;
        if (pet.PaletteChangeIn > 0) return;

        // A colour change usually comes with a change of pattern too.
        if (_rng.NextDouble() < 0.5) pet.SkinPattern = _rng.Next(5);

        // Mood first, otherwise wander to a neighbouring colour.
        string? mood = pet.Machine.Current.Name switch
        {
            "startle" or "flee" => "pearl",       // cuttlefish blanch when spooked
            "rival" or "angry" => "ink",
            "happy" or "eat" => "magenta",
            "hunt" => "crimson",
            "sleep" => "sand",
            _ => null,
        };
        pet.ShiftTo(mood != null ? Palettes.IndexOf(mood) : Palettes.PickRandom(_rng),
                    12 + _rng.NextDouble() * 30);
    }

    private void UpdateEyes(Pet pet, double dt)
    {
        // Pupils track the cursor; body-local so the sprite flip doesn't invert them.
        var anim = pet.Anim.Current;
        if (anim.EyeCenter is Point ec)
        {
            var b = pet.Bounds;
            var eye = new Point(
                b.X + (pet.FacingRight ? ec.X : anim.FrameW - ec.X) * Pet.RenderScale,
                b.Y + ec.Y * Pet.RenderScale);
            var to = pet.Machine.Current is HuntTreatBehavior or EatTreatBehavior &&
                     _world.NearestTreat(pet) is { } t ? t.Pos : _world.Cursor;
            var d = to - eye;
            double len = d.Length;
            var aim = len < 1 ? new Vector(0, 0) : d / len * Math.Min(1, len / 180);
            if (!pet.FacingRight) aim.X = -aim.X;
            pet.PupilOffset = aim;
        }

        pet.BlinkLeft -= dt;
        pet.BlinkIn -= dt;
        if (pet.BlinkIn <= 0)
        {
            pet.BlinkLeft = 0.13;
            pet.BlinkIn = 2.5 + _rng.NextDouble() * 5;
        }
    }

    private void MaybeBubble(Pet pet, double dt)
    {
        if (pet.Machine.Current.Name is not ("idle" or "sit" or "sleep")) return;
        if (_rng.NextDouble() > dt * 0.22) return;
        _renderer.SpawnBubble(pet.Pos + new Vector(
            (pet.FacingRight ? 12 : -12) + _rng.Next(-6, 7), -44));
    }

    private void ReactToNewWindows(Pet pet)
    {
        if (_world.AppearedWindows.Count == 0 || !pet.Machine.Current.Interruptible) return;
        foreach (var r in _world.AppearedWindows)
        {
            var near = Rect.Inflate(r, 70, 70);
            if (near.Contains(pet.Pos))
            {
                pet.Machine.Force(new StartleBehavior());
                return;
            }
        }
    }

    private void CheckRivalry(double dt)
    {
        _rivalCooldown -= dt;
        if (_pets.Count < 2 || _rivalCooldown > 0) return;

        for (int i = 0; i < _pets.Count; i++)
        {
            for (int j = i + 1; j < _pets.Count; j++)
            {
                var a = _pets[i];
                var b = _pets[j];
                if (a.Surface == null || b.Surface == null) continue;
                if (!a.Machine.Current.Interruptible || !b.Machine.Current.Interruptible) continue;
                if (Math.Abs(a.Pos.Y - b.Pos.Y) > 40) continue;
                if (Math.Abs(a.Pos.X - b.Pos.X) > RivalDistance) continue;

                bool aRetreats = _rng.NextDouble() < 0.5;
                a.Machine.Force(new RivalDisplayBehavior(b, aRetreats));
                b.Machine.Force(new RivalDisplayBehavior(a, !aRetreats));
                _rivalCooldown = 20;
                return;
            }
        }
    }

    /// <summary>Apply pet arrivals/removals queued by behaviors during the tick.</summary>
    private void ApplyArrivalsAndDepartures()
    {
        foreach (var pet in _leaving)
        {
            if (!_pets.Remove(pet)) continue;
            _renderer.RemoveVisual(pet.Visual);
        }
        _leaving.Clear();

        foreach (var p in _hatching)
            if (_pets.Count < 24) Spawn(p);   // sanity cap: eggs must not run away with it
        _hatching.Clear();
    }

    private void TickProps(double dt)
    {
        for (int i = _props.Count - 1; i >= 0; i--)
        {
            var p = _props[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                p.OnExpire?.Invoke(p.Pos);
                _renderer.RemoveProp(p.Visual);
                _props.RemoveAt(i);
                continue;
            }
            p.Visual.Opacity = p.Opacity;
            _renderer.UpdateProp(p.Visual, p.Anim, p.Pos, p.Age);
        }
    }

    private void TickTreats(double dt)
    {
        for (int i = _world.Treats.Count - 1; i >= 0; i--)
        {
            var t = _world.Treats[i];
            t.Age += dt;

            if (t.Expired)
            {
                _renderer.RemoveProp(t.Visual);
                _world.Treats.RemoveAt(i);
                continue;
            }

            if (t.Surface == null)
            {
                // Food sinks slowly through water and wafts sideways on the way down.
                t.Vel = new Vector(t.Vel.X * Math.Exp(-1.5 * dt) + Math.Sin(t.Age * 2.2) * 14 * dt,
                                   Math.Min(t.Vel.Y * Math.Exp(-1.2 * dt) + 190 * dt, 130));
                double nx = t.Pos.X + t.Vel.X * dt;
                double ny = t.Pos.Y + t.Vel.Y * dt;
                if (t.Vel.Y > 0)
                {
                    var landing = PhysicsEngine.FindLanding(nx, t.Pos.Y, ny, _world);
                    if (landing != null)
                    {
                        t.Surface = landing;
                        ny = landing.Y;
                        t.Vel = new Vector(0, 0);
                    }
                    else if (ny > _world.VirtualScreen.Bottom - 4)
                    {
                        ny = _world.VirtualScreen.Bottom - 4;
                        t.Surface = new Surface(SurfaceKind.Floor, IntPtr.Zero,
                            _world.VirtualScreen.Left, _world.VirtualScreen.Right, ny);
                        t.Vel = new Vector(0, 0);
                    }
                }
                t.Pos = new Point(Math.Clamp(nx, _world.VirtualScreen.Left + 10,
                                             _world.VirtualScreen.Right - 10), ny);
            }
            else
            {
                var s = _world.Find(t.Surface, t.Pos.X);
                if (s == null) t.Surface = null;           // window moved out from under it
                else { t.Pos = new Point(t.Pos.X + s.X1 - t.Surface.X1, s.Y); t.Surface = s; }
            }

            _renderer.UpdateProp(t.Visual, "shrimp", t.Pos, t.Age);
        }
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

    private void LogDebug()
    {
        try
        {
            var lines = _pets.Select((p, i) =>
                $"{DateTime.Now:HH:mm:ss} pet{i} pos=({p.Pos.X:F0},{p.Pos.Y:F0}) vel=({p.Vel.X:F0},{p.Vel.Y:F0}) " +
                $"behavior={p.Machine.Current.Name} anim={p.Anim.Current.Name} surface={p.Surface?.Kind.ToString() ?? "none"}");
            System.IO.File.AppendAllLines(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cuttlefishpet-debug.log"), lines);
        }
        catch { }
    }

    private void RebuildWorld(double dt)
    {
        var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
        _world.VirtualScreen = new Rect(vs.Left, vs.Top, vs.Width, vs.Height);
        _world.Cursor = _input.Cursor;
        _world.CursorVelocity = _input.CursorVelocity;
        _world.CursorStill = _input.CursorStill;
        _world.TypingRate = _input.TypingRate;
        _world.IdleSeconds = GlobalInput.IdleSeconds();
        _world.PetCount = _pets.Count;

        _world.Surfaces.Clear();
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var wa = screen.WorkingArea;
            _world.Surfaces.Add(new Surface(SurfaceKind.Floor, IntPtr.Zero, wa.Left, wa.Right, wa.Bottom));
            // The screen itself is climbable: both side edges plus a ceiling to hang from.
            _world.Surfaces.Add(new Surface(SurfaceKind.Ceiling, IntPtr.Zero,
                wa.Left + 20, wa.Right - 20, wa.Top + 2));
            _world.Surfaces.Add(new Surface(SurfaceKind.ScreenLeft, IntPtr.Zero,
                wa.Left, wa.Left, wa.Top + 2, wa.Bottom));
            _world.Surfaces.Add(new Surface(SurfaceKind.ScreenRight, IntPtr.Zero,
                wa.Right, wa.Right, wa.Top + 2, wa.Bottom));
        }
        if (TaskbarLocator.GetSurface() is { } taskbar)
            _world.Surfaces.Add(taskbar);
        _tracker.AddSurfaces(_world.Surfaces);

        _world.AppearedWindows.Clear();
        _world.AppearedWindows.AddRange(_tracker.TakeAppeared());
        foreach (var r in _world.AppearedWindows)
            Log($"window appeared {r}");
    }
}
