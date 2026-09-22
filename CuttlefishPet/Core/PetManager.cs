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
    private readonly Settings _settings;
    private readonly Random _rng = new();
    private readonly List<Pet> _pets = new();
    private readonly List<Prop> _props = new();
    private readonly List<Pet> _leaving = new();
    private readonly List<(Point Pos, bool Hatchling, Heritage? Inherit)> _hatching = new();

    /// <summary>Hands out the identity every pet keeps for life.</summary>
    private int _nextId;
    private double _clock, _lastDownAt = double.NegativeInfinity, _rivalCooldown;
    private double _preySpawnIn = 12, _courtCooldown = 30, _shrimpSpawnIn = 25;
    private double _immigrationIn = 60, _bloomIn = 240, _fightCooldown = 120;
    private double _ritualIn = 600;

    /// <summary>Seconds to the next autosave. A kill or a crash costs a minute, not a tank.</summary>
    private double _saveIn = 60;

    /// <summary>So a failing save says so once, not once a minute forever.</summary>
    /// <summary>Whoever is being looked at, so the log says it once and not every frame.</summary>
    private int _inspecting = -1;
    private bool _saveFailing;
    private bool _saveConfirmed;
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
        : Math.Clamp(0.55 - 0.21 * _pets.Count / _settings.TargetPopulation, 0.08, 0.55);

    public PetManager(OverlayWindow overlay, SpriteRenderer renderer,
        Dictionary<string, SpriteAnim> library, GlobalInput input, SoundService sound)
    {
        _overlay = overlay;
        _renderer = renderer;
        _library = library;
        _input = input;
        _sound = sound;
        _settings = Settings.Load();
        _world.Settings = _settings;
    }

    /// <summary>How full the tank is meant to be; the slider writes this.</summary>
    public int TargetPopulation
    {
        get => _settings.TargetPopulation;
        set
        {
            _settings.TargetPopulation = Math.Clamp(value, Settings.MinPopulation,
                                                    Settings.MaxPopulation);
            _settings.Save();
        }
    }

    /// <summary>
    /// Top the tank up to its resting level. Only ever adds: there is no upper
    /// limit on the population, and thinning out is what the tray menu is for.
    /// </summary>
    public void StockTank()
    {
        while (_pets.Count < _settings.TargetPopulation) Spawn();
    }

    /// <summary>
    /// Put one in the ground. A living tank only ever shows you the survivors, so
    /// the animals that did badly are exactly the ones you cannot see -- this is
    /// where they go, and it is the only place selection is legible.
    /// </summary>
    private void Bury(Pet pet)
    {
        var stone = new Epitaph
        {
            Name = pet.Name,
            Id = pet.Id,
            Genome = pet.Genome,
            Fate = pet.Fate,
            Born = pet.BornAt.ToString("o"),
            Died = DateTime.Now.ToString("o"),
            Age = Math.Round(pet.Age, 1),
            Lifespan = Math.Round(pet.Lifespan, 1),
            Size = Math.Round(pet.GrownScale, 3),
            Meals = pet.Meals,
            Offspring = pet.Offspring,
        };
        foreach (var (behavior, worth) in pet.Memory.Learned)
            stone.Learned[behavior] = Math.Round(worth, 3);

        if (Graveyard.Record(stone) is string why)
        {
            if (!_graveFailing) Log($"graveyard schrijven MISLUKT: {why}");
            _graveFailing = true;
            return;
        }
        _graveFailing = false;
        Log($"#{pet.Id} {pet.Name} {pet.Fate} na {pet.Age / 60:F0}m, " +
            $"{pet.Meals} maaltijden, {pet.Offspring} eieren");

        // A canary, written after an edit once left the old-age check with an empty
        // body and the dying itself outside it, so every animal in the tank died on
        // every tick. Nothing in the app noticed; the graveyard was the only reason
        // it surfaced at all. Old age cannot arrive early, so if it does, say so.
        if (pet.Fate == "ouderdom" && pet.Age < pet.Lifespan * 0.9)
            Log($"!! #{pet.Id} {pet.Name} ging aan ouderdom op {pet.Age / 60:F1}m " +
                $"van {pet.Lifespan / 60:F1}m -- dat hoort niet te kunnen");
    }

    private bool _graveFailing;

    /// <summary>Write the tank out as it stands.</summary>
    public void SaveTank()
    {
        var state = new TankState { NextId = _nextId };
        foreach (var pet in _pets)
        {
            var d = pet.Drives;
            var saved = new SavedPet
            {
                Id = pet.Id,
                Name = pet.Name,
                Genome = pet.Genome,
                Born = pet.BornAt.ToString("o"),
                Offspring = pet.Offspring,
                Meals = pet.Meals,
                Age = pet.Age,
                Lifespan = pet.Lifespan,
                BirthScale = pet.BirthScale,
                GrowUpSeconds = pet.GrowUpSeconds,
                Nourishment = pet.Nourishment,
                X = pet.Pos.X,
                Y = pet.Pos.Y,
                Hunger = d.Hunger, Fatigue = d.Fatigue, Loneliness = d.Loneliness,
                Boredom = d.Boredom, Fear = d.Fear,
            };
            foreach (var (behavior, worth) in pet.Memory.Learned) saved.Learned[behavior] = worth;
            foreach (var (id, bond) in pet.Relations.Everyone) saved.Bonds[id] = bond;
            state.Pets.Add(saved);
        }

        if (state.Save() is string why)
        {
            if (!_saveFailing) Log($"tank opslaan MISLUKT: {why}");
            _saveFailing = true;
            return;
        }

        if (_saveFailing) Log("tank opslaan werkt weer");
        else if (!_saveConfirmed) Log($"tank bewaard: {state.Pets.Count} zeekatten");
        _saveFailing = false;
        _saveConfirmed = true;
    }

    /// <summary>
    /// Put back the tank from the last session. Returns how many came back, so the
    /// caller can top up from there; nothing saved simply means nothing to put back.
    /// </summary>
    public int RestoreTank()
    {
        var state = TankState.Load();
        if (state == null)
        {
            if (TankState.LastLoadError is string bad) Log($"tank laden mislukt: {bad}");
            return 0;
        }
        if (state.Pets.Count == 0) return 0;

        _nextId = Math.Max(_nextId, state.NextId);
        var wa = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;

        foreach (var saved in state.Pets)
        {
            // A screen that has changed size since last time must not strand
            // anybody off the edge of it.
            var pos = new Point(
                Math.Clamp(saved.X, wa.Left + 40, wa.Right - 40),
                Math.Clamp(saved.Y, wa.Top + 40, wa.Bottom - 40));

            var pet = new Pet { Anim = new AnimationPlayer(_library), Pos = pos };
            pet.Id = saved.Id;
            pet.Genome = saved.Genome;
            pet.Name = string.IsNullOrEmpty(saved.Name)
                ? Names.Pick(_rng, _pets.ConvertAll(p => p.Name))
                : saved.Name;
            if (DateTime.TryParse(saved.Born, out var born)) pet.BornAt = born;
            pet.Offspring = saved.Offspring;
            pet.Meals = saved.Meals;
            pet.Age = saved.Age;
            pet.Lifespan = saved.Lifespan;
            pet.BirthScale = saved.BirthScale;
            pet.GrowUpSeconds = saved.GrowUpSeconds;
            pet.Nourishment = saved.Nourishment;
            pet.Scale = pet.GrownScale;
            pet.ScaleTarget = pet.GrownScale;
            pet.HomePalette = pet.Genome.Chroma;
            pet.SkinPattern = pet.Genome.Pattern;
            pet.Palette = pet.FromPalette = Palettes.Glass;
            pet.PaletteChangeIn = 20 + _rng.NextDouble() * 40;
            pet.SkinBase = pet.SkinStrength = 0.45 + _rng.NextDouble() * 0.30;

            var d = pet.Drives;
            d.Hunger = saved.Hunger; d.Fatigue = saved.Fatigue;
            d.Loneliness = saved.Loneliness; d.Boredom = saved.Boredom; d.Fear = saved.Fear;
            foreach (var (behavior, worth) in saved.Learned) pet.Memory.Relearn(behavior, worth);
            foreach (var (id, bond) in saved.Bonds) pet.Relations.Remember(id, bond);

            pet.Visual = _renderer.CreateVisual();
            pet.Machine = new BehaviorMachine(NewContext(pet));
            _pets.Add(pet);
            _nextId = Math.Max(_nextId, pet.Id);
        }

        Log($"tank hervat: {_pets.Count} zeekatten terug van {state.SavedAt}");
        return _pets.Count;
    }

    public void Spawn() => Spawn(null);

    /// <param name="hatchling">
    /// Just out of the egg: starts tiny and grows. Anyone you add by hand turns up
    /// as a young adult instead, because waiting ten minutes for a pet to become
    /// visible is nobody's idea of fun.
    /// </param>
    /// <param name="inherit">
    /// What the parents handed on when this one came out of an egg: traits, and a
    /// head start on what its mother had learned. Null for anything that swam in
    /// from outside, which gets a fresh roll and no opinions at all.
    /// </param>
    public void Spawn(Point? at, bool hatchling = false, Heritage? inherit = null)
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
        pet.Id = ++_nextId;
        pet.Name = Names.Pick(_rng, _pets.ConvertAll(p => p.Name));
        pet.Genome = inherit?.Genome ?? Genome.Random(_rng);
        if (inherit != null)
            foreach (var (behavior, worth) in inherit.Lore) pet.Memory.Relearn(behavior, worth);
        // Colour and pattern are inherited rather than rolled fresh every so often,
        // so a brood looks like its parents and you can follow one animal around.
        pet.HomePalette = pet.Genome.Chroma;
        pet.SkinPattern = pet.Genome.Pattern;
        pet.Drives.Stagger(_rng);
        pet.Palette = pet.FromPalette = Palettes.Glass;   // arrives near-invisible
        pet.PaletteChangeIn = 20 + _rng.NextDouble() * 40;
        pet.SkinBase = pet.SkinStrength = 0.45 + _rng.NextDouble() * 0.30;
        pet.SheenStrength = 0.10 + _rng.NextDouble() * 0.14;
        pet.Visual = _renderer.CreateVisual();
        pet.Machine = new BehaviorMachine(NewContext(pet));
        _pets.Add(pet);

        var g = pet.Genome;
        Log($"#{pet.Id} {pet.Name} {(inherit is null ? "nieuw" : $"uit ei (erft {inherit.Lore.Count} inzichten)")} " +
            $"{Palettes.All[g.Chroma].Name}/{g.Pattern} " +
            $"lef={g.Boldness:F2} sociaal={g.Sociability:F2} nieuwsgierig={g.Curiosity:F2} " +
            $"stofwisseling={g.Metabolism:F2} onrustig={g.Restlessness:F2}");
    }

    /// <summary>
    /// Feed the needs the sensory facts they run on. The scan for company is the
    /// only cost here, and at a tankful of pets it is nothing.
    /// </summary>
    private void UpdateDrives(Pet pet, double dt)
    {
        double company = 0;
        foreach (var other in _pets)
        {
            if (ReferenceEquals(other, pet)) continue;
            if ((other.Pos - pet.Pos).LengthSquared > Drives.CompanyRange * Drives.CompanyRange)
                continue;

            // Time alongside is how a stranger becomes somebody it recognises. It
            // stops at familiar: going further than that takes an occasion.
            pet.Relations.Warm(other.Id, dt / 420, Relations.Familiar);
            company = Math.Max(company, 1 + Math.Max(0, pet.Relations.With(other.Id)));
        }

        double fright = pet.Alarmed ? 1 : Math.Min(0.6, pet.Pestered / 4);
        pet.Drives.Tick(dt, pet.Genome, pet.Surface != null, pet.Vel.Length, company, fright);

        // How it carries itself: nerve and settled experience lift it, being liked
        // lifts it a little more, and fear folds it down faster than anything else
        // raises it. Eased so a scare reads as the animal shrinking.
        double upright = Math.Clamp(
            (pet.Genome.Boldness - 0.5) * 1.2
            + pet.Memory.Conviction * 0.8
            + pet.Relations.With(Relations.You) * 0.3
            - pet.Drives.Fear * 1.5, -1, 1);
        pet.Carriage += (upright - pet.Carriage) * Math.Min(1, dt * 1.6);
    }

    /// <summary>
    /// The animal under the cursor, laid out plainly. Everything here is a number
    /// the tank is already running on -- nothing is computed for the display.
    /// </summary>
    private static string Describe(Pet pet)
    {
        static string Bar(string label, double v) =>
            $"{label,-9}{new string('\u2588', (int)Math.Round(Math.Clamp(v, 0, 1) * 10)).PadRight(10, '\u2591')} {v:F2}";

        var d = pet.Drives;
        var g = pet.Genome;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{pet.Name}  #{pet.Id}  {Palettes.All[pet.HomePalette].Name}/{pet.SkinPattern}");
        sb.AppendLine($"{pet.Age / 60:F0}m van {pet.Lifespan / 60:F0}m  " +
                      $"{pet.Meals} maaltijden  {pet.Offspring} eieren");
        sb.AppendLine($"nu: {pet.Machine.Current.Name}");
        sb.AppendLine();
        sb.AppendLine(Bar("honger", d.Hunger));
        sb.AppendLine(Bar("moe", d.Fatigue));
        sb.AppendLine(Bar("alleen", d.Loneliness));
        sb.AppendLine(Bar("verveeld", d.Boredom));
        sb.AppendLine(Bar("bang", d.Fear));
        sb.AppendLine();
        sb.AppendLine($"lef {g.Boldness:F2}  sociaal {g.Sociability:F2}  " +
                      $"nieuwsgierig {g.Curiosity:F2}");
        sb.AppendLine($"rusteloos {g.Restlessness:F2}  stofwisseling {g.Metabolism:F2}");
        sb.AppendLine($"overtuiging {pet.Memory.Conviction:F2}  houding {pet.Carriage:+0.00;-0.00; 0.00}");

        var views = pet.Memory.Learned.Where(kv => Math.Abs(kv.Value) > 0.05)
                       .OrderByDescending(kv => Math.Abs(kv.Value)).Take(3).ToList();
        sb.AppendLine();
        sb.AppendLine(views.Count == 0
            ? "weet nog niets"
            : "weet: " + string.Join("  ", views.Select(kv => $"{kv.Key} {kv.Value:+0.00;-0.00}")));

        var known = pet.Relations.Everyone.Where(kv => Math.Abs(kv.Value) > 0.05)
                       .OrderByDescending(kv => Math.Abs(kv.Value)).Take(3).ToList();
        sb.Append(known.Count == 0
            ? "kent niemand"
            : "kent: " + string.Join("  ", known.Select(kv =>
                $"{(kv.Key == Relations.You ? "jou" : "#" + kv.Key)} {kv.Value:+0.00;-0.00}")));
        return sb.ToString();
    }

    private void UpdateInspector()
    {
        // Held down, not switched on. Everything below is the honest view of an
        // animal and it breaks the illusion on purpose, so it belongs to whoever
        // goes looking for it rather than being pushed at everyone.
        if (!Keys.Inspecting) { _renderer.HideInspector(); _inspecting = -1; return; }

        Pet? under = null;
        for (int i = _pets.Count - 1; i >= 0; i--)
            if (_pets[i].Bounds.Contains(_world.Cursor)) { under = _pets[i]; break; }

        if (under == null) { _renderer.HideInspector(); return; }
        _renderer.ShowInspector(under, Describe(under));
        if (_inspecting != under.Id)
        {
            _inspecting = under.Id;
            Log($"bekeken: #{under.Id}");
        }
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
        pet.Fate = "weggehaald";
        Bury(pet);
    }

    /// <summary>
    /// Keeps the tank alive without keeping it steady. Three forces, all of them

    /// <summary>
    /// The rare gathering. Needs at least three of them free at the same moment and
    /// somewhere in the tank with room for a ring; the long cooldown is what keeps
    /// it an event rather than a habit.
    /// </summary>
    private void MaybeRitual(double dt)
    {
        _ritualIn -= dt;
        if (_ritualIn > 0 || _pets.Count < 3) return;

        var willing = new List<Pet>();
        foreach (var pet in _pets)
        {
            if (!pet.Machine.Current.Interruptible || pet.Dying) continue;
            willing.Add(pet);
        }
        if (willing.Count < 3) return;

        // Between three and seven, whoever happens to be free.
        int want = Math.Min(willing.Count, 3 + _rng.Next(5));
        while (willing.Count > want) willing.RemoveAt(_rng.Next(willing.Count));

        var tank = _world.VirtualScreen;
        double radius = 120 + willing.Count * 16;
        var centre = new Point(
            tank.Left + radius + 90 + _rng.NextDouble() * Math.Max(1, tank.Width - 2 * radius - 180),
            tank.Top + radius + 140 + _rng.NextDouble() * Math.Max(1, tank.Height - 2 * radius - 300));

        // One colour for the whole ring: that is most of what makes it read as a
        // gathering rather than several cuttlefish that happen to be near each other.
        int robe = Palettes.PickRandom(_rng);
        double spin = (_rng.NextDouble() < 0.5 ? -1 : 1) * (0.42 + _rng.NextDouble() * 0.22);

        for (int i = 0; i < willing.Count; i++)
        {
            willing[i].Borrow(robe, willing[i].SkinPattern, 80);
            willing[i].Machine.Force(new RitualBehavior(
                centre, radius, i * Math.Tau / willing.Count, spin));
        }

        _ritualIn = 1500 + _rng.NextDouble() * 1800;
        Log($"ritueel met {willing.Count} zeekatten op ({centre.X:F0},{centre.Y:F0})");
    }
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
            if (_pets.Count >= 2 && _pets.Count <= _settings.TargetPopulation + 2)
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
        SpawnPet = (p, hatchling, inherit) => _hatching.Add((p, hatchling, inherit)),
        AddProp = prop => { prop.Visual = _renderer.CreateProp(prop.Anim); _props.Add(prop); },
        AddBone = (at, size) => _world.Bones.Add(
            new Bone { Pos = at, Size = size, Visual = _renderer.CreateProp("bone") }),
        RemovePet = p => _leaving.Add(p),
    };

    public void Tick(double dt)
    {
        _clock += dt;

        _saveIn -= dt;
        if (_saveIn <= 0) { _saveIn = 60; SaveTank(); }
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
            UpdateDrives(pet, dt);
            pet.Memory.Tick(dt);
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
        TickShrimp(dt);
        TickPredator(dt);
        TickBones(dt);
        TickTreats(dt);
        TickProps(dt);
        CheckSocial(dt);
        PopulationTick(dt);
        MaybeRitual(dt);
        _renderer.TickEffects(dt);

        UpdateInspector();

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
        // Each animal reads the desktop behind it on its own clock. At a dozen that
        // is a few grabs a minute; at a hundred it would be a steady stream of them,
        // so the interval stretches with the crowd and the total rate stays put.
        double crowding = Math.Max(1, _pets.Count / 12.0);
        pet.CamoResampleIn = (pet.CamoHolding
            ? 45 + _rng.NextDouble() * 45
            : 14 + _rng.NextDouble() * 12) * crowding;
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
        int crowd = Math.Max(0, _pets.Count - _settings.TargetPopulation);
        double pressure = 1 + crowd * 0.34 + crowd * crowd * 0.022;
        pet.Age += dt * pressure;

        // Size is age plus whatever eating has added, with a brief puff on top of a
        // fresh meal. Eased rather than snapped, so it reads as swelling.
        pet.Swell = Math.Max(0, pet.Swell - dt * 0.26);
        pet.ScaleTarget = pet.GrownScale + pet.Swell;
        pet.Scale += (pet.ScaleTarget - pet.Scale) * Math.Min(1, dt * 1.9);

        if (pet.Age >= pet.Lifespan && pet.Machine.Current.Interruptible)
        {
            pet.Fate = "ouderdom";
            pet.Machine.Force(new DyingBehavior());
        }
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
            "ritual" => (null, 1.0),                            // the whole ring in one colour
            "fight" => ("ink", 1.0),
            "race" or "school" or "jet" => (null, 0.8),
            "colourShow" => (null, 1.0),                        // drives itself
            "camouflage" => ("glass", 0.0),
            _ => ("glass", 0.0),                                // resting: barely there
        };

        int want = display == null ? pet.HomePalette : Palettes.IndexOf(display);
        if (pet.Machine.Current.Name != "colourShow") pet.ShiftTo(want, 3);

        // Small ones have not mastered camouflage yet, so they stay visible in their
        // own colour — which is also the only way you get to watch one grow up. Keyed
        // off GrownScale, which is size from food alone: it ignores both the swell
        // after a meal and the deliberate shrink of a pet posing as a taskbar icon.
        double youth = Math.Clamp((0.60 - pet.GrownScale) / 0.30, 0, 1);
        if (youth > 0.25 && display == "glass") display = null;
        vivid = Math.Max(vivid, youth * 0.7);

        // And nerve decides how much it bothers hiding at all. A timid, unsure
        // animal spends its life as glass; a bold one that knows what it is doing
        // just wears its colour. The tank gets brighter as it settles in.
        vivid = Math.Max(vivid, (pet.Genome.Boldness * 0.4 + pet.Memory.Conviction * 0.6) * 0.42);

        // Flaring up is sudden — that is the point of a display. Settling back into
        // hiding is not: the colour drains away over a few seconds.
        double ease = vivid > pet.Vividness ? 2.4 : 0.45;
        pet.Vividness += (vivid - pet.Vividness) * Math.Min(1, dt * ease);
        pet.BodyOpacity = 0.52 + 0.48 * pet.Vividness;
        pet.SheenStrength = 0.10 + 0.14 * (1 - pet.Vividness);   // glassier = more shimmer

        // Experience shows on the body. An animal with no opinions yet wears its
        // markings faintly; one that has worked this desktop out wears them
        // plainly. Convictions are inherited, so what you are watching over a week
        // is not one animal ageing but a line of them getting surer of itself.
        double settled = pet.Memory.Conviction;
        pet.SkinStrength = pet.SkinBase * (0.55 + 0.75 * settled);

        // A borrowed colour is given back. The personal colour itself is inherited
        // and never re-rolled: it is the only thing that says this is the same
        // cuttlefish you were watching a minute ago.
        if (pet.BorrowedFor > 0)
        {
            pet.BorrowedFor -= dt;
            if (pet.BorrowedFor <= 0)
            {
                pet.HomePalette = pet.Genome.Chroma;
                pet.SkinPattern = pet.Genome.Pattern;
            }
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
        // Where the attention is. Body-local, so the sprite flip does not invert it.
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

            // Full deflection whatever the distance. Scaling it down for anything
            // close by — which is what this used to do — made a pet staring at the
            // shrimp under its nose look like it was staring into space.
            var aim = len < 1 ? new Vector(0, 0) : d / len;
            if (!pet.FacingRight) aim.X = -aim.X;

            // Eyes move in jumps, not sweeps. Fast enough to read as a flick, slow
            // enough that you see which way it went.
            pet.PupilOffset += (aim - pet.PupilOffset) * Math.Min(1, dt * 17);
        }

        // Wide when something has its attention, narrow while it is hiding. This is
        // the other half of reading a cuttlefish: pupil size says how interested.
        double want = pet.Machine.Current.Name switch
        {
            "hunt" or "stalk" or "huntTreat" or "eat" or "strike" => 1.30,
            "startle" or "flee" or "shock" or "fight" => 1.34,
            "imitate" or "bone" or "inspect" or "icon" => 1.18,
            "camouflage" or "lurk" or "sit" or "idle" => 0.84,
            _ => 1.0,
        };
        pet.PupilScale += (want - pet.PupilScale) * Math.Min(1, dt * 6);

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
    /// <summary>
    /// Two cuttlefish after the same mouthful. Usually that is a tug of war, which
    /// nobody gets hurt in; once in a while it escalates into a real fight, and the
    /// winner eats. Food is the only thing they fight over — a cuttlefish that is
    /// simply passing another has no quarrel with it.
    /// </summary>
    private bool ContestFood()
    {
        foreach (var treat in _world.Treats)
        {
            if (treat.Expired || treat.ClaimedBy == null) continue;
            var holder = treat.ClaimedBy;
            if (!holder.Machine.Current.Interruptible) continue;
            // The holder has to be closing on it, and the two of them have to be in
            // the same part of the tank — otherwise two of them square up from
            // opposite corners over a shrimp neither has reached.
            if ((holder.Pos - treat.Pos).Length > 420) continue;

            foreach (var other in _pets)
            {
                if (ReferenceEquals(other, holder)) continue;
                if (!other.Machine.Current.Interruptible) continue;
                if ((other.Pos - treat.Pos).Length > 260) continue;
                if ((other.Pos - holder.Pos).Length > 340) continue;

                if (Escalates(holder, other))
                {
                    StartFight(holder, other, winner =>
                    {
                        if (treat.Expired) return;
                        treat.Eaten = true;
                        winner.Feed();
                    });
                    return true;
                }

                bool holderWins = _rng.NextDouble() < 0.5;
                holder.Machine.Force(new TugOfWarBehavior(treat, -1, holderWins));
                other.Machine.Force(new TugOfWarBehavior(treat, 1, !holderWins));
                return true;
            }
        }

        // A stalked fish is worth arguing over too, and there is no tug-of-war
        // version of that — a fish either gets eaten or gets away.
        foreach (var fish in _world.Prey)
        {
            if (fish.Expired || fish.StalkedBy == null) continue;
            var hunter = fish.StalkedBy;
            if (!hunter.Machine.Current.Interruptible) continue;
            if ((hunter.Pos - fish.Pos).Length > 420) continue;

            foreach (var other in _pets)
            {
                if (ReferenceEquals(other, hunter)) continue;
                if (!other.Machine.Current.Interruptible) continue;
                if ((other.Pos - fish.Pos).Length > 240) continue;
                if ((other.Pos - hunter.Pos).Length > 340) continue;
                if (!Escalates(hunter, other)) continue;

                StartFight(hunter, other, winner =>
                {
                    if (fish.Expired) return;
                    fish.Eaten = true;
                    winner.Feed(0.16);
                });
                return true;
            }
        }
        return false;
    }

    /// <summary>Is this squabble going to turn into a real fight?</summary>
    private bool Escalates(Pet a, Pet b) =>
        a.Mature && b.Mature && _fightCooldown <= 0 && _pets.Count > 2 &&
        _rng.NextDouble() < 0.28;

    /// <summary>
    /// What the pair make of each other, as something to multiply odds by. Two that
    /// have spent the evening in the same corner court readily; two that have
    /// already fallen out mostly do not.
    /// </summary>
    private static double Familiarity(Pet a, Pet b) =>
        Math.Clamp(1 + (a.Relations.With(b.Id) + b.Relations.With(a.Id)) / 2, 0.25, 2.0);

    private void StartFight(Pet a, Pet b, Action<Pet> prize)
    {
        _fightCooldown = 600 + _rng.NextDouble() * 1200;
        _rivalCooldown = 20;
        bool aWins = _rng.NextDouble() < 0.5;
        // A death has to leave a tank that still works, so it needs company left
        // behind and can never be the last straw.
        bool fatal = _pets.Count > 3 && _rng.NextDouble() < 0.22;
        a.Machine.Force(new FightBehavior(b, aWins, fatal && !aWins, prize));
        b.Machine.Force(new FightBehavior(a, !aWins, fatal && aWins, prize));
        // Neither of them comes out of this thinking better of the other, and the
        // one that lost carries it furthest.
        a.Relations.Cool(b.Id, aWins ? 0.25 : 0.55);
        b.Relations.Cool(a.Id, aWins ? 0.55 : 0.25);
        Log($"gevecht om eten bij {_pets.Count} zeekatten, dodelijk={fatal}");
    }

    private void CheckSocial(double dt)
    {
        _rivalCooldown -= dt;
        _fightCooldown -= dt;
        _courtCooldown -= dt;
        if (_pets.Count < 2 || _rivalCooldown > 0) return;

        // A contested shrimp beats anything else two cuttlefish might do together.
        if (ContestFood()) { _rivalCooldown = 14; return; }

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
                else if (a.Mature && b.Mature &&
                         a.Surface == null && b.Surface == null &&
                         roll < CourtChance * Familiarity(a, b) &&
                         _courtCooldown <= 0)
                {
                    // One puts on a display; the other decides how it lands.
                    bool welcome = _rng.NextDouble() < 0.55;
                    a.Machine.Force(new CourtshipBehavior(b, suitor: true, welcome));
                    b.Machine.Force(new CourtshipBehavior(a, suitor: false, welcome));
                    _courtCooldown = _world.Bloom > 0
                        ? 10
                        : 60 + 90.0 * _pets.Count / _settings.TargetPopulation;
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
            foreach (var survivor in _pets) survivor.Relations.Forget(pet.Id);
            Bury(pet);
        }
        _leaving.Clear();

        // Eggs only come to anything when there is room. Above the cap the clutch
        // simply does not make it, which is how a real tank behaves too. The cap is
        // high enough to allow a proper swarm — crowding, not this line, is what
        // ends one.
        foreach (var (pos, hatchling, inherit) in _hatching)
            Spawn(pos, hatchling, inherit);
        _hatching.Clear();
    }

    /// <summary>
    /// Keep a fish or two swimming through the tank. They come and go on their own,
    /// which gives the pets something real to hunt.
    /// </summary>
    private void TickPrey(double dt)
    {
        _preySpawnIn -= dt;
        // A tank of a hundred cannot live off two fish. Everything edible scales
        // with how full the tank is meant to be, or the slider just makes a famine.
        int fishRoom = Math.Clamp(1 + _settings.TargetPopulation / 4, 2, 26);
        if (_preySpawnIn <= 0 && _world.Prey.Count < fishRoom && _pets.Count > 0)
        {
            _preySpawnIn = (35 + _rng.NextDouble() * 65) * 5.0 / _settings.TargetPopulation;
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

    /// <summary>Bring the next visit forward to now. For the command line, and for
    /// anyone who would rather not wait a quarter of an hour to see one.</summary>
    /// <summary>
    /// Debug: three cuttlebones side by side at the smallest, middling and
    /// largest size a life can leave behind. There is no other way to see the
    /// three together -- in the tank they arrive minutes apart.
    /// </summary>
    public void DropTestBones()
    {
        var tank = _world.VirtualScreen;
        double y = tank.Top + tank.Height * 0.55;
        double x = tank.Left + tank.Width * 0.32;
        foreach (double size in new[] { 0.4, 0.85, 1.3 })
        {
            _world.Bones.Add(new Bone
            {
                Pos = new Point(x, y),
                Size = size,
                Visual = _renderer.CreateProp("bone"),
            });
            x += 190;
        }
        Log($"drie testschelpen neergelegd op y={y:F0}");
    }

    /// <summary>
    /// Debug: bring the next visit forward. It also aims -- the summoned one
    /// arrives at the height of an actual cuttlefish instead of a random band,
    /// because a hunter that crosses an empty stretch of screen shows you
    /// nothing, and that is most of them.
    /// </summary>
    /// <param name="sure">
    /// Debug: the next strike cannot miss. A catch is meant to be rare, which
    /// is right for the tank and hopeless for checking the swallow animation --
    /// fifteen summoned strikes in a row came away empty, which is exactly the
    /// odds working as designed and no help at all.
    /// </param>
    /// <summary>Debug: the three biggest hold their tentacles out for a few seconds.</summary>
    public void ReachOut()
    {
        var chosen = _pets.OrderByDescending(p => p.Scale).Take(3).ToList();
        foreach (var pet in chosen) pet.Machine.Force(new ReachBehavior());
        foreach (var pet in chosen)
            Log($"tentakel uit bij #{pet.Id} op ({pet.Pos.X:F0},{pet.Pos.Y:F0}) schaal={pet.Scale:F2}");
    }

    public void SummonHunter(bool sure = false)
    {
        if (_world.Hunter != null) return;
        _hunterIn = 0;
        _summonAimed = true;
        _summonSure = sure;
    }

    private bool _summonAimed;
    private bool _summonSure;

    /// <summary>Seconds to the next visit, and which of the two comes next.</summary>
    private double _hunterIn = 240 + 420 * 0.5;
    private bool _dolphinNext;

    /// <summary>
    /// A hunter passes through now and then, notices whatever is making itself
    /// obvious, and has a go at it. It usually misses. What it is for is not the
    /// kill -- it is that hiding finally beats not hiding.
    /// </summary>
    private void TickPredator(double dt)
    {
        var hunter = _world.Hunter;
        if (hunter == null)
        {
            _hunterIn -= dt;
            if (_hunterIn > 0 || _pets.Count < 3) return;

            // Eight to twenty minutes apart. Often enough to matter over a day,
            // rare enough that it is an event rather than weather.
            _hunterIn = 480 + _rng.NextDouble() * 720;
            _dolphinNext = !_dolphinNext;

            var tank = _world.VirtualScreen;
            bool fromLeft = _rng.NextDouble() < 0.5;
            string kind = _dolphinNext ? "dolphin" : "shark";
            double entryY = _summonAimed && _pets.Count > 0
                ? _pets[_rng.Next(_pets.Count)].Pos.Y
                : tank.Top + 160 + _rng.NextDouble() * (tank.Height - 420);
            entryY = Math.Clamp(entryY, tank.Top + 120, tank.Bottom - 160);
            _summonAimed = false;
            hunter = new Predator
            {
                Kind = kind,
                Pos = new Point(fromLeft ? tank.Left - 120 : tank.Right + 120, entryY),
                Vel = new Vector(fromLeft ? Predator.Cruise : -Predator.Cruise, 0),
                FacingRight = fromLeft,
                Visual = _renderer.CreateProp(kind),
            };
            _world.Hunter = hunter;
            _sound.Play("squirt", 0.5);
            Log($"{kind} komt langs bij {_pets.Count} zeekatten op ({hunter.Pos.X:F0},{hunter.Pos.Y:F0})");
            return;
        }

        hunter.Age += dt;
        hunter.LookIn -= dt;
        if (hunter.BiteT >= 0)
        {
            hunter.BiteT += dt;
            if (hunter.BiteT >= Predator.BiteLen) hunter.BiteT = -1;
        }

        // Pick something to go for. Conspicuousness decides, which is the point:
        // a pet flaring at a rival is advertising, and one sitting still wearing
        // the desktop is very nearly invisible.
        if (!hunter.Leaving && hunter.LookIn <= 0)
        {
            hunter.LookIn = 0.5;
            Pet? best = null;
            double bestScore = 0.55;          // below this it simply does not register
            foreach (var pet in _pets)
            {
                if (pet.Dying) continue;
                double d = (pet.Pos - hunter.Pos).Length;
                if (d > 520) continue;
                double score = Predator.Conspicuousness(pet) * (1 - d / 900);
                if (score > bestScore) { bestScore = score; best = pet; }
            }
            if (!ReferenceEquals(best, hunter.Target))
            {
                hunter.Target = best;
                hunter.Lock = 0;
                // A chase only reads as a chase if the quarry runs. Everything
                // nearby is already alarmed; the one actually being aimed at is
                // told outright, so the two of them move across the screen as a
                // pair instead of the hunter closing on something oblivious.
                if (best != null && best.Machine.Current.Interruptible
                    && best.CamoOpacity <= 0.5
                    && best.Machine.Current is not (ThreatBehavior or FleeBehavior))
                    best.Machine.Force(new FleeBehavior());
            }
        }

        var tankRect = _world.VirtualScreen;
        if (hunter.Target is { } quarry && !quarry.Dying && !hunter.Leaving)
        {
            hunter.Lock += dt;
            var to = quarry.Pos - hunter.Pos;
            double gap = to.Length;

            // A run-up, then the burst. Coming straight in at full speed meant
            // the strike landed the moment anything was noticed, which is not
            // a hunt -- it is a result. The first second is a pursuit you can
            // follow, and only then does it commit.
            double speed = hunter.Lock < 0.8 ? Predator.Cruise * 1.15 : Predator.Lunge;
            var want = gap < 1 ? hunter.Vel : to / gap * speed;
            hunter.Vel += (want - hunter.Vel) * Math.Min(1, dt * 1.8);

            if (gap < 62 && hunter.Lock > 0.6 && hunter.BiteT < 0)
            {
                // The jaw opens whatever happens next. A snap at empty water
                // is worth as much to watch as a hit, and it is most of what
                // you will ever see one do.
                hunter.BiteT = 0;
                _sound.Play("splat", 0.55);
                Log($"{hunter.Kind} hapt naar #{quarry.Id} {quarry.Name}");
                // One in ten for something still making itself obvious, one in a
                // hundred for something that took cover -- and at two lunges a visit
                // that comes to roughly one visit in five ending badly for somebody,
                // nearly always somebody who was easy to see. That ratio is the
                // entire point of the animal.
                double slip = _summonSure
                    ? -1
                    : 0.90 + (1 - Math.Min(1, Predator.Conspicuousness(quarry))) * 0.09;
                hunter.Tries++;
                if (_rng.NextDouble() < slip)
                {
                    hunter.Target = null;
                    hunter.LookIn = 3.5;
                    if (hunter.Tries >= 2) hunter.Leaving = true;
                }
                else
                {
                    _summonSure = false;
                    quarry.Fate = "opgegeten";
                    quarry.Machine.Force(new EatenBehavior());
                    hunter.Leaving = true;
                    Log($"{hunter.Kind} pakt #{quarry.Id} {quarry.Name} na {hunter.Lock:F1}s jacht");
                }
            }
        }
        else
        {
            // Nothing worth chasing: level out and carry on across.
            double straight = hunter.FacingRight ? Predator.Cruise : -Predator.Cruise;
            hunter.Vel += (new Vector(straight, Math.Sin(hunter.Age * 0.7) * 26) - hunter.Vel)
                          * Math.Min(1, dt * 1.2);
        }

        hunter.Pos += hunter.Vel * dt;
        if (Math.Abs(hunter.Vel.X) > 12) hunter.FacingRight = hunter.Vel.X > 0;

        // Everything close by knows it is there, and each reacts in its own way.
        AlarmAt(hunter);

        bool offScreen = hunter.Pos.X < tankRect.Left - 240 || hunter.Pos.X > tankRect.Right + 240;
        if (hunter.Expired || (offScreen && hunter.Age > 6))
        {
            _renderer.RemoveProp(hunter.Visual);
            _world.Hunter = null;
            return;
        }
        // The bite strip runs on its own clock so it plays once through
        // rather than being sampled at whatever point the visit has reached.
        _renderer.UpdateProp(hunter.Visual, hunter.Anim, hunter.Pos,
                             hunter.Biting ? hunter.BiteT : hunter.Age, hunter.FacingRight);
    }

    /// <summary>
    /// What a cuttlefish does about a hunter is not one thing. Wikipedia again: they
    /// "use the flamboyant display towards larger, more dangerous fish" -- so the
    /// bold ones make themselves enormous and flash their false eyes, and the rest
    /// do the sensible thing and leave, ink first.
    /// </summary>
    private void AlarmAt(Predator hunter)
    {
        foreach (var pet in _pets)
        {
            if (pet.Dying || !pet.Machine.Current.Interruptible) continue;
            double d = (pet.Pos - hunter.Pos).Length;
            if (d > 300) continue;

            pet.Drives.Fear = 1;
            pet.Alarmed = true;

            // Already hidden and holding still? Then holding still is the answer.
            if (pet.CamoOpacity > 0.5) continue;
            if (pet.Machine.Current is ThreatBehavior or FleeBehavior or InkBombBehavior) continue;

            if (ThreatBehavior.Possible(NewContext(pet)) && _rng.NextDouble() < 0.5)
                pet.Machine.Force(new ThreatBehavior());
            else if (_rng.NextDouble() < 0.4)
                pet.Machine.Force(new InkBombBehavior());
            else
                pet.Machine.Force(new FleeBehavior());
        }
    }

    /// <summary>
    /// Shrimp turn up by themselves, at a rate the tank's size sets. Before this the
    /// only ones were the ones you threw in by hand, which was fine for five animals
    /// and starvation for fifty.
    /// </summary>
    private void TickShrimp(double dt)
    {
        _shrimpIn -= dt;
        if (_shrimpIn > 0 || _pets.Count == 0) return;

        int room = Math.Clamp(_settings.TargetPopulation / 6, 1, 14);
        _shrimpIn = (26 + _rng.NextDouble() * 34) * 6.0 / _settings.TargetPopulation;
        if (_world.Treats.Count >= room) return;

        var t = _world.VirtualScreen;
        AddTreat(new Point(t.Left + 120 + _rng.NextDouble() * (t.Width - 240),
                           t.Top + 90 + _rng.NextDouble() * (t.Height * 0.4)));
    }

    private double _shrimpIn = 12;

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
            _renderer.UpdateProp(b.Visual, "bone", b.Pos, b.Age, scale: b.Size);
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
            // Scaled to the target: a fuller tank needs feeding faster, or everyone
            // in it stays a hatchling. Still never more than two on screen at once.
            _shrimpSpawnIn = (_world.Bloom > 0 ? 5 + _rng.NextDouble() * 5
                                               : 40 + _rng.NextDouble() * 50)
                             * 5.0 / _settings.TargetPopulation;
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
                $"{DateTime.Now:HH:mm:ss} pet#{p.Id} pos=({p.Pos.X:F0},{p.Pos.Y:F0}) muis=({_world.Cursor.X:F0},{_world.Cursor.Y:F0}) vel=({p.Vel.X:F0},{p.Vel.Y:F0}) " +
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
