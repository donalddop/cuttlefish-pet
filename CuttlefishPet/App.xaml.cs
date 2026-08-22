using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CuttlefishPet.Audio;
using CuttlefishPet.Behaviors;
using CuttlefishPet.Core;
using CuttlefishPet.Interop;
using CuttlefishPet.Rendering;

namespace CuttlefishPet;

public partial class App : Application
{
    private OverlayWindow _overlay = null!;
    private PetManager _manager = null!;
    private GlobalInput _input = null!;
    private SoundService _sound = null!;
    private System.Windows.Forms.NotifyIcon? _tray;
    private DispatcherTimer _loop = null!;
    private CommandServer? _commands;
    private Mutex? _instanceLock;
    private System.Windows.Forms.ToolStripMenuItem? _muteItem;
    private readonly Stopwatch _clock = new();
    private double _lastT;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Second launch: hand our arguments to the running pet and get out of the way.
        _instanceLock = new Mutex(true, @"Local\CuttlefishPet.instance", out bool isFirst);
        if (!isFirst)
        {
            if (e.Args.Length > 0) CommandServer.TrySend(string.Join(' ', e.Args));
            Shutdown();
            return;
        }

        string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        var library = SpriteLibrary.Load(Path.Combine(assets, "sprites"));
        BehaviorMachine.LoadWeights(Path.Combine(assets, "behaviors.json"));
        _sound = new SoundService(Path.Combine(assets, "sounds")) { Muted = true };

        _overlay = new OverlayWindow();
        _overlay.Show();

        var skins = SkinLibrary.Load(Path.Combine(assets, "sprites"));
        var renderer = new SpriteRenderer(_overlay, library, skins);
        _input = new GlobalInput();
        _input.Install();

        _manager = new PetManager(_overlay, renderer, library, _input, _sound);
        _manager.StockTank();

        SetupTray();
        _commands = new CommandServer(Dispatcher, RunCommand);
        if (e.Args.Length > 0) RunCommand(string.Join(' ', e.Args).ToLowerInvariant());

        _clock.Start();
        // 30fps, not 60. The whole transparent overlay is recomposited every tick,
        // which costs far more than the simulation does, and at these deliberately
        // slow animation speeds nobody can tell the difference.
        _loop = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _loop.Tick += (_, _) =>
        {
            double t = _clock.Elapsed.TotalSeconds;
            double dt = Math.Min(t - _lastT, 0.05); // clamp hiccups so physics can't tunnel
            _lastT = t;
            _manager.Tick(dt);
        };
        _loop.Start();
    }

    /// <summary>One command name from the tray, the CLI or the pipe.</summary>
    private void RunCommand(string command)
    {
        switch (command)
        {
            case "add": _manager.Spawn(); break;
            case "remove": _manager.RemoveOne(); break;
            case "cull": _manager.CullTo(1); break;
            case "population": ShowPopulationWindow(); break;
            case "shrimp": _manager.TossTreat(); break;
            case "mute": if (_muteItem != null) _muteItem.Checked = !_muteItem.Checked; break;
            case "exit": Shutdown(); break;
        }
    }


    private PopulationWindow? _popWindow;

    /// <summary>
    /// One window, reused: a second click on the tray item brings the existing one
    /// forward rather than stacking another copy behind it.
    /// </summary>
    private void ShowPopulationWindow()
    {
        if (_popWindow is { IsLoaded: true })
        {
            _popWindow.Activate();
            return;
        }
        _popWindow = new PopulationWindow(_manager);
        _popWindow.Closed += (_, _) => _popWindow = null;
        _popWindow.Show();
    }
    private void SetupTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Add cuttlefish", null, (_, _) => RunCommand("add"));
        menu.Items.Add("Remove one", null, (_, _) => RunCommand("remove"));
        menu.Items.Add("Thin them out", null, (_, _) => RunCommand("cull"));
        menu.Items.Add("Toss a shrimp", null, (_, _) => RunCommand("shrimp"));
        menu.Items.Add("Population…", null, (_, _) => RunCommand("population"));
        var mute = new System.Windows.Forms.ToolStripMenuItem("Mute sounds") { CheckOnClick = true, Checked = true };
        mute.CheckedChanged += (_, _) => _sound.Muted = mute.Checked;
        _muteItem = mute;
        menu.Items.Add(mute);

        var startup = new System.Windows.Forms.ToolStripMenuItem("Start with Windows")
        { CheckOnClick = true, Checked = Autostart.Enabled };
        startup.CheckedChanged += (_, _) => Autostart.Set(startup.Checked);
        menu.Items.Add(startup);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = File.Exists(iconPath)
                ? new System.Drawing.Icon(iconPath)
                : System.Drawing.SystemIcons.Application,
            Text = "Cuttlefish Pet",
            Visible = true,
            ContextMenuStrip = menu,
        };
        // Left-click opens the menu too — no hunting for the right mouse button.
        _tray.MouseUp += (_, e) =>
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;
            typeof(System.Windows.Forms.NotifyIcon)
                .GetMethod("ShowContextMenu", System.Reflection.BindingFlags.Instance |
                                              System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(_tray, null);
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _loop?.Stop();
        _commands?.Dispose();
        _input?.Dispose();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _instanceLock?.Dispose();
        base.OnExit(e);
    }
}
