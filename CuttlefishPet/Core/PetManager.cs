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
    private double _preySpawnIn = 12, _courtCooldown = 30, _shrimpSpawnIn = 25;
    private double _immigrationIn = 60, _bloomIn = 240, _fightCooldown = 120;
    private double _sampleMs, _binCheckIn;
    private int _sampleCount;
    private int _lastHourChimed = -1;
    private Pet? _lastDownPet;
    private int _tick;

    public int Count => _pets.Count;

    /// <summary>
    /// How readily two cuttlefish that meet in open water end up courting. Rises
    /// sharply when the tank is nearly empty and collapses when it is full, which is
    /// most of what pulls the population back from either extreme.
    /// </summary>
    private double CourtChance => _world.Bloom > 0
        ? 0.85
        : Math.Clamp(0.55 - _pets.Count * 0.07, 0.08, 0.55);

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
        pet.BirthScale = hatchling ? 0.30 : 0.80;
        // A clutch is one cohort: hatchlings draw from a short, tight range, so the
        // whole brood reaches the end of its life within a few minutes of itself.
        // That is what turns a swarm into a crash rather than a slow fade.
        pet.Lifespan = hatchling ? (18 + _rng.NextDouble() * 10) * 60
                                 : (26 + _rng.NextDouble() * 24) * 60;
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

    /// <summary>
    /// Keeps the tank alive without keeping it steady. Three forces, all of them
    /// visible to anyone watching:
    ///
    /// A tank can never empty. Below two cuttlefish one drifts in from off-screen,
    /// which also means a lone survivor eventually gets a mate.
    ///
    /// Blooms are what make a swarm. Every few minutes, if there is room, food turns
    /// plentiful for a minute or so and breeding goes into overdrive — several large
    /// clutches in quick succession.
    ///
    /// The crash needs no mechanism of its own: crowding speeds up ageing (see
    /// AgeAndRetire), a clutch hatches as one cohort with near-identical lifespans,
    /// and spawning kills the parent. So a swarm ages fast and dies together, and the
    /// survivors inherit an empty tank and breed freely again.
    /// </summary>
    private void PopulationTick(double dt)
    {
        _world.Bloom = Math.Max(0, _world.Bloom - dt);

        // The floor. Only counts down while the tank is nearly empty, so a healthy
        // population never accumulates arrivals.
        if (_pets.Count < 2)
        {
            // An empty tank is the one state that must not last. Any long wait
            // already ticking gets cut short the moment the last one dies.
            if (_pets.Count == 0)
                _immigrationIn = Math.Min(_immigrationIn, 8 + _rng.NextDouble() * 12);
            _immigrationIn -= dt;
            if (_immigrationIn <= 0)
            {
                _immigrationIn = 45 + _rng.NextDouble() * 45;
                SpawnDrifter();
            }
        }
        else _immigrationIn = 45 + _rng.NextDouble() * 45;

        // The boom. Held off while the tank is already full — a bloom there would
        // only push against the crowding that is about to kill everyone anyway.
        _bloomIn -= dt;
        if (_bloomIn <= 0)
        {
            _bloomIn = 1200 + _rng.NextDouble() * 1200;
            if (_pets.Count is >= 2 and <= 5)
            {
                _world.Bloom = 75;
                _courtCooldown = 0;
                Log($"bloom begint bij {_pets.Count} zeekatten");
            }
        }
    }

    /// <summary>One swims in from off the edge of the screen and joins the tank.</summary>
    private void SpawnDrifter()
    {
        var tank = _world.VirtualScreen;
        bool fromLeft = _rng.NextDouble() < 0.5;
        var at = new Point(fromLeft ? tank.Left + 70 : tank.Right - 70,
                           tank.Top + 150 + _rng.NextDouble() * (tank.Height - 300));
        Spawn(at);
        var arrival = _pets[^1];
        arrival.Vel = new Vector(fromLeft ? 150 : -150, 0);
        arrival.FacingRight = fromLeft;
        Log($"nieuwkomer drijft binnen bij {_pets.Count - 1} zeekatten");
    }

    /// <summary>Drop a shrimp at the cursor for the pets to chase down.</summary>
    public void TossTreat() => AddTreat(_input.Cursor);

    private void AddTreat(Point at)
    {
        _world.Treats.Add(new Treat
        {
            Pos = at,
            Vel = new Vector(_rng.Next(-90, 91), 40),
            Visual = _renderer.CreateProp("shrimp"),
        });
    }

    private BehaviorContext NewContext(Pet pet) => new()
    {
        Pet = pet, World = _world, Input = _input,
        Sound = _sound, Renderer = _renderer, Rng = _rng,
        // Queued, never applied mid-tick: the pet list is being iterated.
        SpawnPet = (p, hatchling) => _hatching.Add((p, hatchling)),
        AddProp = prop => { prop.Visual = _renderer.CreateProp(prop.Anim); _props.Add(prop); },
        AddBone = at => _world.Bones.Add(new Bone { Pos = at, Visual = _renderer.CreateProp("bone") }),
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
            // Immediate-mode: whoever is striking re-asserts it every tick, so a
            // behaviour that ends mid-strike can never leave tentacles hanging.
            pet.Striking = false;
            ReactToNewWindows(pet);
            pet.Machine.Tick(dt);
            PhysicsEngine.Tick(pet, _world, dt);
            // Environmental, so it applies even while a behaviour is steering.
            if (pet.Surface == null) PhysicsEngine.ApplyScrollCurrent(pet, _world, dt);
            AvoidRecycleBin(pet, dt);
            pet.Anim.Tick(dt);
            AgeAndRetire(pet, dt);
            UpdateExploration(pet, dt);
            ColourMimicry.Apply(pet, _world, _rng, dt);
            UpdateCamoSkin(pet, dt);
            UpdateColour(pet, dt);
            UpdateEyes(pet, dt);
            MaybeBubble(pet, dt);
            _renderer.Update(pet);
            if (pet.Machine.Current is DragBehavior || pet.Bounds.Contains(_world.Cursor))
                wantClicks = true;
        }

        SpreadAlarm();
        RideMinimisedWindows();
        ChimeOnTheHour();
        ApplyArrivalsAndDepartures();
        TickPrey(dt);
        TickBones(dt);
        TickTreats(dt);
        TickProps(dt);
        CheckSocial(dt);
        PopulationTick(dt);
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
                if (hit == null) { ReactToClick(new Point(e.X, e.Y)); continue; }

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

    /// <summary>On the hour, everyone goes to look at the clock.</summary>
    private void ChimeOnTheHour()
    {
        var now = DateTime.Now;
        if (now.Minute != 0 || now.Hour == _lastHourChimed) return;
        _lastHourChimed = now.Hour;

        foreach (var pet in _pets)
            if (pet.Machine.Current.Interruptible &&
                CheckClockBehavior.Find(NewContext(pet)) is { } look)
                pet.Machine.Force(look);
    }

    /// <summary>A pet perched on a window that just got minimised rides it down.</summary>
    private void RideMinimisedWindows()
    {
        if (_world.MinimisedWindows.Count == 0) return;
        var taskbar = TaskbarLocator.GetSurface();

        foreach (var pet in _pets)
        {
            if (pet.Surface == null || pet.Surface.Hwnd == IntPtr.Zero) continue;
            if (!_world.MinimisedWindows.Contains(pet.Surface.Hwnd)) continue;

            var spot = taskbar != null
                ? new Point(Math.Clamp(pet.Pos.X, taskbar.X1, taskbar.X2), taskbar.Y + 16)
                : new Point(pet.Pos.X, _world.VirtualScreen.Bottom - 20);
            pet.Machine.Force(new RideMinimiseBehavior(spot));
        }
    }

    /// <summary>An open Recycle Bin is something to stay well clear of.</summary>
    private void AvoidRecycleBin(Pet pet, double dt)
    {
        if (_world.RecycleBin is not { } bin || pet.Surface != null) return;

        var centre = new Point(bin.X + bin.Width / 2, bin.Y + bin.Height / 2);
        var away = pet.Pos - centre;
        double reach = Math.Max(bin.Width, bin.Height) / 2 + 120;
        if (away.Length > reach || away.Length < 1) return;

        double push = (1 - away.Length / reach) * 260;
        pet.Pos += away / away.Length * push * dt;
        PhysicsEngine.ClampToTank(pet, _world);
    }

    /// <summary>
    /// A click anywhere on screen turns heads: close by it is a fright, further off
    /// it is just something worth looking at.
    /// </summary>
    private void ReactToClick(Point at)
    {
        // Flick a drifting cuttlebone about.
        foreach (var b in _world.Bones)
        {
            var away = b.Pos - at;
            if (away.Length > 55) continue;
            if (away.Length < 1) away = new Vector(0, -1);
            away.Normalize();
            b.Nudge(away * 320);
            return;
        }

        foreach (var pet in _pets)
        {
            double d = (at - pet.Pos).Length;
            if (d < 230 && pet.Machine.Current.Interruptible)
            {
                pet.Machine.Force(new StartleBehavior());
            }
            else if (d < 950)
            {
                pet.GlanceTarget = at;
                pet.GlanceFor = 1.6;
            }
        }
    }

    /// <summary>
    /// Fear travels. One cuttlefish bolting sets off the ones near it, which is why
    /// a whole group scatters at once.
    /// </summary>
    private void SpreadAlarm()
    {
        for (int i = 0; i < _pets.Count; i++)
        {
            if (!_pets[i].Alarmed) continue;
            _pets[i].Alarmed = false;

            foreach (var other in _pets)
            {
                if (ReferenceEquals(other, _pets[i]) || other.Alarmed) continue;
                if (!other.Machine.Current.Interruptible) continue;
                if ((other.Pos - _pets[i].Pos).Length > 320) continue;
                other.Machine.Force(new StartleBehavior());
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
        // Whatever it last read bleeds into place over a few seconds.
        pet.CamoBlend = Math.Min(1, pet.CamoBlend + dt / 4.0);

        pet.CamoResampleIn -= dt;
        var moved = (pet.Pos - pet.LastSampleAt).Length;

        // Swimming somewhere quite different is normally reason enough to look again,
        // but not while it is deliberately hanging on to a reading.
        bool wandered = !pet.CamoHolding && moved > 620;
        if (pet.Sampling || (pet.CamoResampleIn > 0 && !wandered)) return;

        // Usually a fresh reading every so often, but now and then it keeps one it
        // has taken a liking to — which is how a pet ends up carrying a whole icon
        // around on its back for a minute.
        pet.CamoHolding = _rng.NextDouble() < 0.3;
        pet.CamoResampleIn = pet.CamoHolding
            ? 45 + _rng.NextDouble() * 45
            : 14 + _rng.NextDouble() * 12;
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
                if (skin != null)
                {
                    pet.CamoPrev = pet.Camo;
                    pet.Camo = skin;
                    pet.CamoBlend = pet.CamoPrev == null ? 1 : 0;
                }
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

        // Crowding wears them out, and faster than linearly: a tank of four is a bit
        // busy, a tank of eleven is a die-off waiting to happen. This is the entire
        // bust half of the cycle — no separate crash logic anywhere.
        int crowd = Math.Max(0, _pets.Count - 3);
        double pressure = 1 + crowd * 0.34 + crowd * crowd * 0.022;
        pet.Age += dt * pressure;

        // Size is age plus whatever eating has added, with a brief puff on top of a
        // fresh meal. Eased rather than snapped, so it reads as swelling.
        pet.Swell = Math.Max(0, pet.Swell - dt * 0.26);
        pet.ScaleTarget = pet.GrownScale + pet.Nourishment + pet.Swell;
        pet.Scale += (pet.ScaleTarget - pet.Scale) * Math.Min(1, dt * 1.9);

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
            "bone" => ("pearl", 0.75),                          // blanched over a corpse
            "boneRide" => ("coral", 1.0),                        // flustered, and stuck
            "dying" => ("pearl", 0.55),                          // the colour goes first
            "court" => (null, 1.0),                             // its own colour, full blast
            "imitate" => (null, 0.9),                           // wearing whatever it copied
            "fight" => ("ink", 1.0),
            "race" or "school" or "jet" => (null, 0.8),
            "colourShow" => (null, 1.0),                        // drives itself
            "camouflage" => ("glass", 0.0),
            _ => ("glass", 0.0),                                // resting: barely there
        };

        int want = display == null ? pet.HomePalette : Palettes.IndexOf(display);
        if (pet.Machine.Current.Name != "colourShow") pet.ShiftTo(want, 3);

        // Juveniles have not mastered camouflage yet, so the young stay visible in
        // their own colour — which is also the only way you get to watch one grow up.
        // Keyed off actual age, not drawn size: a well-fed adult is bigger than its
        // years and a pet posing as a taskbar icon is deliberately tiny, and neither
        // of those is a juvenile.
        double youth = Math.Clamp(1 - pet.Age / Math.Max(1, pet.GrowUpSeconds), 0, 1);
        if (youth > 0.25 && display == "glass") display = null;
        vivid = Math.Max(vivid, youth * 0.7);

        // Flaring up is sudden — that is the point of a display. Settling back into
        // hiding is not: the colour drains away over a few seconds.
        double ease = vivid > pet.Vividness ? 2.4 : 0.45;
        pet.Vividness += (vivid - pet.Vividness) * Math.Min(1, dt * ease);
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
        {
            // Slower on the way back to glass than on the way into a colour.
            double seconds = pet.Palette == Palettes.Glass ? 3.5 : 1.2;
            pet.PaletteBlend = Math.Min(1, pet.PaletteBlend + dt / seconds);
        }
        else
        {
            pet.FromPalette = pet.Palette;
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
            var to = pet.PupilTarget
                     ?? (pet.GlanceFor > 0 ? pet.GlanceTarget : null)
                     ?? (pet.Machine.Current is HuntTreatBehavior or EatTreatBehavior &&
                         _world.NearestTreat(pet) is { } t ? t.Pos : _world.Cursor);
            var d = to - eye;
            double len = d.Length;
            var aim = len < 1 ? new Vector(0, 0) : d / len * Math.Min(1, len / 180);
            if (!pet.FacingRight) aim.X = -aim.X;
            pet.PupilOffset = aim;
        }

        pet.GlanceFor = Math.Max(0, pet.GlanceFor - dt);
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
            // Right on top of you it is a fright; from a distance it is a curiosity.
            if (Rect.Inflate(r, 70, 70).Contains(pet.Pos))
            {
                pet.Machine.Force(new StartleBehavior());
                return;
            }
            double d = (new Point(r.X, r.Y) - pet.Pos).Length;
            if (d < 800 && _rng.NextDouble() < 0.5)
            {
                pet.Machine.Force(new InspectBehavior(r));
                return;
            }
        }
    }

    /// <summary>
    /// Things that need two cuttlefish: squaring up to a rival, pairing off to swim
    /// in formation, or racing across the tank.
    /// </summary>
    /// <summary>
    /// A shrimp with one cuttlefish already on it is an invitation to a second.
    /// </summary>
    private bool StartTugOfWar()
    {
        foreach (var treat in _world.Treats)
        {
            if (treat.Expired || treat.ClaimedBy == null) continue;
            var holder = treat.ClaimedBy;
            if (!holder.Machine.Current.Interruptible) continue;

            foreach (var other in _pets)
            {
                if (ReferenceEquals(other, holder)) continue;
                if (!other.Machine.Current.Interruptible) continue;
                if ((other.Pos - treat.Pos).Length > 260) continue;

                bool holderWins = _rng.NextDouble() < 0.5;
                holder.Machine.Force(new TugOfWarBehavior(treat, -1, holderWins));
                other.Machine.Force(new TugOfWarBehavior(treat, 1, !holderWins));
                return true;
            }
        }
        return false;
    }

    private void CheckSocial(double dt)
    {
        _rivalCooldown -= dt;
        _fightCooldown -= dt;
        _courtCooldown -= dt;
        if (_pets.Count < 2 || _rivalCooldown > 0) return;

        // A contested shrimp beats anything else two cuttlefish might do together.
        if (StartTugOfWar()) { _rivalCooldown = 14; return; }

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

                // Two grown cuttlefish meeting face to face. Usually it stays a
                // colour argument; now and then neither backs down.
                if (a.Mature && b.Mature && _fightCooldown <= 0 &&
                    _rng.NextDouble() < 0.3 && _pets.Count > 2)
                {
                    _fightCooldown = 900 + _rng.NextDouble() * 1500;
                    _rivalCooldown = 20;
                    bool aWins = _rng.NextDouble() < 0.5;
                    // A death here has to leave a tank that still works, so it needs
                    // company left behind and cannot be the last straw.
                    bool fatal = _pets.Count > 3 && _rng.NextDouble() < 0.22;
                    a.Machine.Force(new FightBehavior(b, aWins, fatal && !aWins));
                    b.Machine.Force(new FightBehavior(a, !aWins, fatal && aWins));
                    Log($"gevecht bij {_pets.Count} zeekatten, dodelijk={fatal}");
                }
                else if (perched && Math.Abs(a.Pos.Y - b.Pos.Y) < 40)
                {
                    bool aRetreats = _rng.NextDouble() < 0.5;
                    a.Machine.Force(new RivalDisplayBehavior(b, aRetreats));
                    b.Machine.Force(new RivalDisplayBehavior(a, !aRetreats));
                }
                else if (a.Mature && b.Mature &&
                         a.Surface == null && b.Surface == null && roll < CourtChance &&
                         _courtCooldown <= 0)
                {
                    // One puts on a display; the other decides how it lands.
                    bool welcome = _rng.NextDouble() < 0.55;
                    a.Machine.Force(new CourtshipBehavior(b, suitor: true, welcome));
                    b.Machine.Force(new CourtshipBehavior(a, suitor: false, welcome));
                    _courtCooldown = _world.Bloom > 0 ? 10 : 60 + _pets.Count * 30;
                }
                else if (a.Surface == null && b.Surface == null && roll < 0.42)
                {
                    // Copy each other move for move.
                    a.Machine.Force(new MirrorBehavior(b, leads: true));
                    b.Machine.Force(new MirrorBehavior(a, leads: false));
                }
                else if (a.Surface == null && b.Surface == null && roll < 0.52)
                {
                    a.Machine.Force(new InkTagBehavior(b));   // you're it
                }
                else if (a.Surface == null && b.Surface == null && roll < 0.72)
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
        // simply does not make it, which is how a real tank behaves too. The cap is
        // high enough to allow a proper swarm — crowding, not this line, is what
        // ends one.
        foreach (var (pos, hatchling) in _hatching)
            if (_pets.Count < 12) Spawn(pos, hatchling);
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

    private void TickBones(double dt)
    {
        for (int i = _world.Bones.Count - 1; i >= 0; i--)
        {
            var b = _world.Bones[i];
            b.Tick(dt, _world);
            if (b.Expired)
            {
                _renderer.RemoveProp(b.Visual);
                _world.Bones.RemoveAt(i);
                continue;
            }
            _renderer.UpdateProp(b.Visual, "bone", b.Pos, b.Age);
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
        // Shrimp turn up on their own, so there is usually something to hunt without
        // you having to throw one in.
        _shrimpSpawnIn -= dt;
        if (_shrimpSpawnIn <= 0 && _world.Treats.Count < 2 && _pets.Count > 0)
        {
            // A bloom shows as shrimp arriving the moment the last one is eaten,
            // rather than as a crowded tank — two at a time is plenty to look at.
            _shrimpSpawnIn = _world.Bloom > 0 ? 5 + _rng.NextDouble() * 5
                                              : 60 + _rng.NextDouble() * 90;
            var tank = _world.VirtualScreen;
            AddTreat(new Point(tank.Left + 100 + _rng.NextDouble() * (tank.Width - 200),
                               tank.Top + tank.Height * 0.5 + _rng.NextDouble() * (tank.Height * 0.4)));
        }

        for (int i = _world.Treats.Count - 1; i >= 0; i--)
        {
            var t = _world.Treats[i];
            if (t.Expired)
            {
                _renderer.RemoveProp(t.Visual);
                _world.Treats.RemoveAt(i);
                continue;
            }

            t.Tick(dt, _world, _rng);
            _renderer.UpdateProp(t.Visual, "shrimp", t.Pos, t.Age, t.FacingRight);
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

        _world.WindowRects.Clear();
        foreach (var w in _tracker.Windows)
            _world.WindowRects.Add(new Rect(w.Rect.Left, w.Rect.Top, w.Rect.Width, w.Rect.Height));

        _world.MinimisedWindows.Clear();
        _world.MinimisedWindows.AddRange(_tracker.TakeMinimised());

        // Scanning every window for the bin is not something to do 30 times a second.
        _binCheckIn -= dt;
        if (_binCheckIn <= 0)
        {
            _binCheckIn = 2.0;
            _world.RecycleBin = SystemProbes.RecycleBinWindow();
        }
        foreach (var r in _world.AppearedWindows)
            Log($"window appeared {r}");
    }
}
