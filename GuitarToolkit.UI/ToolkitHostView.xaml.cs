using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GuitarToolkit.Core.Services;

namespace GuitarToolkit.UI;

public partial class ToolkitHostView : UserControl
{
    private readonly UserSettings _settings;
    private readonly TabPlayerView? _tabPlayerTab;

    public ToolkitHostView(TunerEngine tuner, MetronomeEngine metronome, IAudioPlayback audioHost, bool enableTabs = true)
    {
        InitializeComponent();

        _settings = UserSettings.Load();

        TunerTab.Initialize(tuner, _settings);
        MetronomeTab.Initialize(metronome, _settings);
        ChordTab.Initialize(audioHost, _settings);
        IntervalTab.Initialize(audioHost);
        ProgressionTab.Initialize(audioHost, _settings);
        InspirationTab.Initialize(audioHost);
        MelodyIdeasTab.Initialize(audioHost);
        CircleTab.Initialize(audioHost);
        FretboardTab.Initialize(audioHost, _settings);
        SettingsTab.Initialize(_settings);
        SettingsTab.ThemeChanged += SettingsTab_ThemeChanged;

        if (enableTabs)
        {
            _tabPlayerTab = new TabPlayerView(_settings);
            TabPlayerTabItem.Content = _tabPlayerTab;
        }
        else
        {
            ToolkitTabs.Items.Remove(TabPlayerTabItem);
        }

        // Горячие клавиши
        Focusable = true;
        Loaded += (s, e) => Focus();
        Unloaded += (s, e) => SaveSettings();
        PreviewKeyDown += OnKeyDown;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ThemeManager.Parse(_settings.ThemeName));
    }

    private void SettingsTab_ThemeChanged(object? sender, ToolkitTheme theme)
    {
        ApplyTheme(theme);
    }

    private void ApplyTheme(ToolkitTheme theme)
    {
        if (Window.GetWindow(this) is Window window)
            ThemeManager.Apply(window, theme);
        else
            ThemeManager.Apply(this, theme);
    }

    public event EventHandler<int> InputDeviceSelected
    {
        add => TunerTab.InputDeviceSelected += value;
        remove => TunerTab.InputDeviceSelected -= value;
    }

    public int SelectedInputDeviceIndex => TunerTab.SelectedInputDeviceIndex;

    public void SetInputDevices(IReadOnlyList<string> devices, int selectedIndex)
    {
        TunerTab.SetInputDevices(devices, selectedIndex);
    }

    public void SetInputStatus(string text, bool isActive)
    {
        TunerTab.SetInputStatus(text, isActive);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            if (ToolkitTabs.SelectedContent is TabPlayerView && _tabPlayerTab?.HandleShortcut(e) == true)
                return;

            MetronomeTab.ToggleStartStop();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Вызывается при закрытии — сохраняет настройки.
    /// </summary>
    public void SaveSettings()
    {
        TunerTab.SaveTo(_settings);
        MetronomeTab.SaveTo(_settings);
        ChordTab.SaveTo(_settings);
        ProgressionTab.SaveTo(_settings);
        FretboardTab.SaveTo(_settings);
        _tabPlayerTab?.SaveTo(_settings);
        _settings.Save();
    }
}
