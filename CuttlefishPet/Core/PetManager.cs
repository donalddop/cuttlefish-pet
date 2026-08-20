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

    public void Spawn()
    {
        var wa = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea; // physical px
        var pet = new Pet
        {
            Anim = new AnimationPlayer(_library),
            Pos = new Point(wa.Left + 100 + _rng.NextDouble() * (wa.Width - 200), wa.Top + 60),
        };
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
            UpdateEyes(pet, dt);
            MaybeBubble(pet, dt);
            _renderer.Update(pet);
            if (pet.Machine.Current is DragBehavior || pet.Bounds.Contains(_world.Cursor))
                wantClicks = true;
        }

        TickTreats(dt);
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
                t.Vel.Y = Math.Min(t.Vel.Y + PhysicsEngine.Gravity * dt,
                                   PhysicsEngine.TerminalVelocity);
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

        _world.Surfaces.Clear();
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var wa = screen.WorkingArea;
            _world.Surfaces.Add(new Surface(SurfaceKind.Floor, IntPtr.Zero, wa.Left, wa.Right, wa.Bottom));
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
