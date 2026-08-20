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
    private readonly List<(Point Pos, bool Hatchling)> _hatching = new();
    private double _clock, _lastDownAt = double.NegativeInfinity, _rivalCooldown;
    private double _preySpawnIn = 12, _courtCooldown = 30;
    private double _sampleMs;
    private int _sampleCount;
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

    /// <param name="hatchling">
    /// Just out of the egg: starts tiny and grows. Anyone you add by hand turns up
    /// as a young adult instead, because waiting ten minutes for a pet to become
    /// visible is nobody's idea of fun.
    /// </param>
    public void Spawn(Point? at, bool hatchling = false)
    {
        var wa = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea; // physical px
        var pet = new Pet
        {
            Anim = new AnimationPlayer(_library),
            // Swim in from somewhere in open water rather than dropping from the sky.
            Pos = at ?? new Point(wa.Left + 120 + _rng.NextDouble() * (wa.Width - 240),
                                  wa.Top + 120 + _rng.NextDouble() * (wa.Height - 300)),
        };
        pet.Lifespan = (14 + _rng.NextDouble() * 16) * 60;   // 14–30 minutes undisturbed
        pet.BirthScale = hatchling ? 0.30 : 0.80;
        pet.GrowUpSeconds = pet.Lifespan * (hatchling ? 0.40 : 0.12);
        pet.Scale = pet.BirthScale;
        pet.HomePalette = Palettes.PickRandom(_rng);
        pet.Palette = pet.FromPalette = Palettes.Glass;   // arrives near-invisible
        pet.PaletteChangeIn = 20 + _rng.NextDouble() * 40;
        pet.SkinPattern = _rng.Next(5);
        pet.SkinStrength = 0.45 + _rng.NextDouble() * 0.30;
        pet.SheenStrength = 0.10 + _rng.NextDouble() * 0.14;
        pet.Visual = _renderer.CreateVisual();
        pet.Machine = new BehaviorMachine(NewContext(pet));
        _pets.Add(pet);
    }

    /// <summary>Send everyone but a handful drifting off — the panic button.</summary>
    public void CullTo(int keep)
    {
        while (_pets.Count > Math.Max(0, keep)) RemoveOne();
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
        SpawnPet = (p, hatchling) => _hatching.Add((p, hatchling)),
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
            // Environmental, so it applies even while a behaviour is steering.
            if (pet.Surface == null) PhysicsEngine.ApplyScrollCurrent(pet, _world, dt);
            pet.Anim.Tick(dt);
            AgeAndRetire(pet, dt);
            UpdateExploration(pet, dt);
            UpdateCamoSkin(pet, dt);
            UpdateColour(pet, dt);
            UpdateEyes(pet, dt);
            MaybeBubble(pet, dt);
            _renderer.Update(pet);
            if (pet.Machine.Current is DragBehavior || pet.Bounds.Contains(_world.Cursor))
                wantClicks = true;
        }

        ApplyArrivalsAndDepartures();
        TickPrey(dt);
        TickTreats(dt);
        TickProps(dt);
        CheckSocial(dt);
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

    /// <summary>
    /// Every few seconds a pet reads the desktop around it and rebuilds its skin from
    /// what it finds. Sampling a patch wider than the body means the pet's own
    /// (translucent) pixels barely register in the result.
    /// </summary>
    private void UpdateCamoSkin(Pet pet, double dt)
    {
        pet.CamoResampleIn -= dt;
        var moved = (pet.Pos - pet.LastSampleAt).Length;
        if (pet.Sampling || (pet.CamoResampleIn > 0 && moved < 220)) return;

        pet.CamoResampleIn = 3.5 + _rng.NextDouble() * 2.5;
        pet.LastSampleAt = pet.Pos;

        var b = pet.Bounds;
        var patch = new Rect(b.X - b.Width * 0.45, b.Y - b.Height * 0.35,
                             b.Width * 1.9, b.Height * 1.7);
        patch.Intersect(_world.VirtualScreen);
        if (patch.Width < 20 || patch.Height < 20) return;

        // Off the UI thread: a screen grab takes longer than a frame is allowed to.
        // The result is an immutable, frozen skin, so handing it back is just a
        // reference assignment.
        pet.Sampling = true;
        Task.Run(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var skin = CamoSampler.Sample(patch);
            double ms = sw.Elapsed.TotalMilliseconds;
            _overlay.Dispatcher.BeginInvoke(() =>
            {
                if (skin != null) pet.Camo = skin;
                pet.Sampling = false;
                _sampleMs += ms;
                _sampleCount++;
            });
        });
    }

    /// <summary>
    /// Age a pet and let it go when its time is up. Crowding shortens lives sharply,
    /// so a tank that fills up thins itself back out instead of growing without end.
    /// </summary>
    private void AgeAndRetire(Pet pet, double dt)
    {
        if (pet.Dying) return;

        int crowd = Math.Max(0, _pets.Count - 3);
        double pressure = 1 + crowd * 0.55;
        pet.Age += dt * pressure;

        pet.Scale = pet.BirthScale + (1 - pet.BirthScale) *
                    Math.Clamp(pet.Age / Math.Max(1, pet.GrowUpSeconds), 0, 1);

        if (pet.Age >= pet.Lifespan && pet.Machine.Current.Interruptible)
            pet.Machine.Force(new DyingBehavior());
    }

    private void UpdateExploration(Pet pet, double dt)
    {
        for (int i = 0; i < pet.RegionAge.Length; i++) pet.RegionAge[i] += dt;
        pet.RegionAge[Pet.RegionOf(pet.Pos, _world.VirtualScreen)] = 0;
    }

    /// <summary>
    /// Colour is a signal, not a coat of paint. At rest a cuttlefish is nearly
    /// transparent with speckles drifting over it; a display floods it with its own
    /// colour and makes it solid. This runs off the current behaviour every tick, so
    /// the skin can never get stuck in the wrong state.
    /// </summary>
    private void UpdateColour(Pet pet, double dt)
    {
        // Irritation cools off if you leave them alone for a minute or so.
        pet.Pestered = Math.Max(0, pet.Pestered - dt / 45);

        // The sheen and the speckles never stop moving.
        pet.SheenPhase = (pet.SheenPhase + dt * 0.075) % 1.0;
        pet.SkinPhase = (pet.SkinPhase + dt * 0.035) % 1.0;

        (string? display, double vivid) = pet.Machine.Current.Name switch
        {
            "startle" or "flee" or "dizzy" => ("pearl", 1.0),   // blanching with fright
            "rival" or "angry" => ("ink", 1.0),
            "happy" or "eat" => ("magenta", 1.0),
            "hunt" or "stalk" => ("crimson", 0.95),
            "court" => (null, 1.0),                             // its own colour, full blast
            "race" or "school" or "jet" => (null, 0.8),
            "colourShow" => (null, 1.0),                        // drives itself
            "camouflage" => ("glass", 0.0),
            _ => ("glass", 0.0),                                // resting: barely there
        };

        int want = display == null ? pet.HomePalette : Palettes.IndexOf(display);
        if (pet.Machine.Current.Name != "colourShow") pet.ShiftTo(want, 3);

        // Juveniles have not mastered camouflage yet, so the young stay visible in
        // their own colour — which is also the only way you get to watch one grow up.
        double youth = Math.Clamp((0.75 - pet.Scale) / 0.4, 0, 1);
        if (youth > 0.25 && display == "glass") display = null;
        vivid = Math.Max(vivid, youth * 0.7);

        // Ease toward the target look rather than snapping between states.
        pet.Vividness += (vivid - pet.Vividness) * Math.Min(1, dt * 2.2);
        pet.BodyOpacity = 0.52 + 0.48 * pet.Vividness;
        pet.SheenStrength = 0.10 + 0.14 * (1 - pet.Vividness);   // glassier = more shimmer

        // Every so often it settles on a different personal colour and pattern.
        pet.HomeChangeIn -= dt;
        if (pet.HomeChangeIn <= 0)
        {
            pet.HomeChangeIn = 25 + _rng.NextDouble() * 50;
            pet.HomePalette = Palettes.PickRandom(_rng);
            if (_rng.NextDouble() < 0.5) pet.SkinPattern = _rng.Next(5);
        }

        if (pet.PaletteBlend < 1)
            pet.PaletteBlend = Math.Min(1, pet.PaletteBlend + dt / 1.4);
        else
            pet.FromPalette = pet.Palette;
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
            var to = pet.PupilTarget
                     ?? (pet.Machine.Current is HuntTreatBehavior or EatTreatBehavior &&
                         _world.NearestTreat(pet) is { } t ? t.Pos : _world.Cursor);
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
        if (pet.Machine.Current.Name is not ("idle" or "sit" or "lurk" or "hover")) return;
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

    /// <summary>
    /// Things that need two cuttlefish: squaring up to a rival, pairing off to swim
    /// in formation, or racing across the tank.
    /// </summary>
    private void CheckSocial(double dt)
    {
        _rivalCooldown -= dt;
        _courtCooldown -= dt;
        if (_pets.Count < 2 || _rivalCooldown > 0) return;

        for (int i = 0; i < _pets.Count; i++)
        {
            for (int j = i + 1; j < _pets.Count; j++)
            {
                var a = _pets[i];
                var b = _pets[j];
                if (!a.Machine.Current.Interruptible || !b.Machine.Current.Interruptible) continue;
                if ((a.Pos - b.Pos).Length > RivalDistance) continue;

                bool perched = a.Surface != null && b.Surface != null;
                double roll = _rng.NextDouble();

                if (perched && Math.Abs(a.Pos.Y - b.Pos.Y) < 40)
                {
                    bool aRetreats = _rng.NextDouble() < 0.5;
                    a.Machine.Force(new RivalDisplayBehavior(b, aRetreats));
                    b.Machine.Force(new RivalDisplayBehavior(a, !aRetreats));
                }
                else if (a.Surface == null && b.Surface == null && roll < 0.3 &&
                         _courtCooldown <= 0)
                {
                    // One puts on a display; the other decides how it lands.
                    bool welcome = _rng.NextDouble() < 0.55;
                    a.Machine.Force(new CourtshipBehavior(b, suitor: true, welcome));
                    b.Machine.Force(new CourtshipBehavior(a, suitor: false, welcome));
                    _courtCooldown = 90;
                }
                else if (a.Surface == null && b.Surface == null && roll < 0.6)
                {
                    // Fall in beside each other and cruise as a pair.
                    b.Machine.Force(new FollowBehavior(a, new Vector(62, 34)));
                }
                else if (a.Surface == null && b.Surface == null)
                {
                    var t = _world.VirtualScreen;
                    int dir = a.Pos.X < t.Left + t.Width / 2 ? 1 : -1;
                    double finish = dir > 0 ? t.Right - 140 : t.Left + 140;
                    double lane = (a.Pos.Y + b.Pos.Y) / 2;
                    a.Machine.Force(new RaceBehavior(finish, dir, lane - 34));
                    b.Machine.Force(new RaceBehavior(finish, dir, lane + 34));
                }
                else
                {
                    continue;
                }

                _rivalCooldown = 18;
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

        // Eggs only come to anything when there is room. Above the cap the clutch
        // simply does not make it, which is how a real tank behaves too.
        foreach (var (pos, hatchling) in _hatching)
            if (_pets.Count < 7) Spawn(pos, hatchling);
        _hatching.Clear();
    }

    /// <summary>
    /// Keep a fish or two swimming through the tank. They come and go on their own,
    /// which gives the pets something real to hunt.
    /// </summary>
    private void TickPrey(double dt)
    {
        _preySpawnIn -= dt;
        if (_preySpawnIn <= 0 && _world.Prey.Count < 2 && _pets.Count > 0)
        {
            _preySpawnIn = 45 + _rng.NextDouble() * 90;
            var t = _world.VirtualScreen;
            bool fromLeft = _rng.NextDouble() < 0.5;
            var fish = new Prey
            {
                Pos = new Point(fromLeft ? t.Left + 60 : t.Right - 60,
                                t.Top + 150 + _rng.NextDouble() * (t.Height - 400)),
                Vel = new Vector(fromLeft ? 90 : -90, 0),
                Visual = _renderer.CreateProp("fish"),
            };
            _world.Prey.Add(fish);
        }

        for (int i = _world.Prey.Count - 1; i >= 0; i--)
        {
            var f = _world.Prey[i];
            if (f.Expired)
            {
                _renderer.RemoveProp(f.Visual);
                _world.Prey.RemoveAt(i);
                continue;
            }
            f.Tick(dt, _world, _rng);
            _renderer.UpdateProp(f.Visual, "fish", f.Pos, f.Age, f.FacingRight);
        }
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
                $"behavior={p.Machine.Current.Name} anim={p.Anim.Current.Name} surface={p.Surface?.Kind.ToString() ?? "none"} " +
                $"colour={Palettes.All[p.Palette].Name} vivid={p.Vividness:F2} " +
                $"age={p.Age:F0}/{p.Lifespan:F0}s scale={p.Scale:F2}");
            System.IO.File.AppendAllLines(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cuttlefishpet-debug.log"), lines);
            if (_sampleCount > 0)
            {
                Log($"camo-samples={_sampleCount} gemiddeld={_sampleMs / _sampleCount:F1}ms " +
                    $"totaal={_sampleMs:F0}ms");
                _sampleMs = 0;
                _sampleCount = 0;
            }
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
        _world.ScrollCurrent = _input.ScrollCurrent;
        _world.TypingRate = _input.TypingRate;
        _world.IdleSeconds = GlobalInput.IdleSeconds();
        _world.Pets.Clear();
        _world.Pets.AddRange(_pets);

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
