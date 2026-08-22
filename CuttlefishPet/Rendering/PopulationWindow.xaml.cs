using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CuttlefishPet.Core;

namespace CuttlefishPet.Rendering;

/// <summary>
/// One slider, for how full the tank should feel. Built in code rather than XAML
/// because it is the only ordinary window in the app and a markup file for six
/// controls earns nothing.
///
/// The number is a resting level, not a quota: it sets what counts as crowded, how
/// big a clutch is and how readily two of them court, so the tank still swings
/// above and below it on its own.
/// </summary>
public sealed class PopulationWindow : Window
{
    private readonly PetManager _manager;
    private readonly TextBlock _readout;
    private readonly Slider _slider;
    /// <summary>
    /// WPF raises ValueChanged while the control template is being applied, before
    /// anyone has touched anything. Writing that through would have the window
    /// silently reset the setting the moment it opened — which it did, to the
    /// slider maximum. Only changes after the window is up count.
    /// </summary>
    private bool _ready;

    public PopulationWindow(PetManager manager)
    {
        _manager = manager;

        Title = "Cuttlefish Pet";
        Width = 380;
        Height = 230;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(24, 26, 32));
        Foreground = Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

        panel.Children.Add(new TextBlock
        {
            Text = "Hoeveel zeekatten?",
            FontSize = 15,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 2),
        });

        _readout = new TextBlock
        {
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 190, 210)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        };
        panel.Children.Add(_readout);

        _slider = new Slider
        {
            Minimum = Settings.MinPopulation,
            Maximum = Settings.MaxPopulation,
            Value = manager.TargetPopulation,
            IsSnapToTickEnabled = true,
            TickFrequency = 1,
            TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
        };
        _slider.ValueChanged += (_, e) =>
        {
            if (!_ready) return;
            _manager.TargetPopulation = (int)e.NewValue;
            UpdateReadout();
        };
        panel.Children.Add(_slider);

        var fill = new Button
        {
            Content = "Nu bijvullen",
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        fill.Click += (_, _) => _manager.StockTank();
        panel.Children.Add(fill);

        Content = panel;
        Loaded += (_, _) =>
        {
            _slider.Value = _manager.TargetPopulation;
            _ready = true;
            UpdateReadout();
        };
        UpdateReadout();
    }

    private void UpdateReadout()
    {
        int n = _manager.TargetPopulation;
        _readout.Text = $"Rustniveau {n}. Ze zwemmen hier omheen — soms een zwerm, " +
                        $"daarna weer weinig. Nooit meer dan {_manager.PopulationCeiling}.";
    }
}
