using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GuitarToolkit.Core.Services;
using GuitarToolkit.UI;

namespace GuitarToolkit.Desktop;

public partial class MainWindow : Window
{
    private readonly AudioBridge _audio;
    private ToolkitHostView? _toolkitView;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;

        try
        {
            _audio = new AudioBridge();

            var settings = UserSettings.Load();
            _toolkitView = new ToolkitHostView(_audio.Tuner, _audio.Metronome, _audio);
            _toolkitView.InputDeviceSelected += ToolkitView_InputDeviceSelected;
            ContentArea.Content = _toolkitView;

            var devices = AudioBridge.GetInputDevices();
            if (devices.Count > 0)
            {
                int selectedIndex = Math.Clamp(settings.LastInputDevice, 0, devices.Count - 1);
                _toolkitView.SetInputDevices(devices, selectedIndex);
                StartInput(selectedIndex);
            }
            else
            {
                _toolkitView.SetInputDevices(Array.Empty<string>(), -1);
                _toolkitView.SetInputStatus("\u0423\u0441\u0442\u0440\u043E\u0439\u0441\u0442\u0432\u0430 \u043D\u0435 \u043D\u0430\u0439\u0434\u0435\u043D\u044B", false);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Desktop startup failed.", ex);
            MessageBox.Show(ex.ToString(), "\u041E\u0448\u0438\u0431\u043A\u0430 \u0437\u0430\u043F\u0443\u0441\u043A\u0430");
            _audio = new AudioBridge();
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WindowProc);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmGetMinMaxInfo = 0x0024;
        if (msg == wmGetMinMaxInfo)
        {
            AdjustMaximizedWindow(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void AdjustMaximizedWindow(IntPtr hwnd, IntPtr lParam)
    {
        IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return;

        var monitorInfo = new NativeMethods.MonitorInfo();
        if (!NativeMethods.GetMonitorInfo(monitor, monitorInfo)) return;

        var minMaxInfo = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(lParam);
        var workArea = monitorInfo.WorkArea;
        var monitorArea = monitorInfo.Monitor;

        minMaxInfo.MaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
        minMaxInfo.MaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
        minMaxInfo.MaxSize.X = Math.Abs(workArea.Right - workArea.Left);
        minMaxInfo.MaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);
        minMaxInfo.MinTrackSize.X = 1050;
        minMaxInfo.MinTrackSize.Y = 780;

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeButton();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
            return;
        }

        DragMove();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateMaximizeButton();
    }

    private void UpdateMaximizeButton()
    {
        if (MaximizeButton != null)
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "\u2750" : "\u25A1";
    }

    private void ToolkitView_InputDeviceSelected(object? sender, int deviceIndex)
    {
        StartInput(deviceIndex);
    }

    private void StartInput(int deviceIndex)
    {
        if (deviceIndex < 0) return;

        _audio.StartInput(deviceIndex);
        _toolkitView?.SetInputStatus("\u25CF \u0417\u0430\u043F\u0438\u0441\u044C", true);
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _toolkitView?.SaveSettings();

        var settings = UserSettings.Load();
        int selectedInputDeviceIndex = _toolkitView?.SelectedInputDeviceIndex ?? -1;
        if (selectedInputDeviceIndex >= 0)
            settings.LastInputDevice = selectedInputDeviceIndex;
        settings.Save();

        _audio.Dispose();
    }

    private static class NativeMethods
    {
        public const int MonitorDefaultToNearest = 2;

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, MonitorInfo lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MinMaxInfo
        {
            public Point Reserved;
            public Point MaxSize;
            public Point MaxPosition;
            public Point MinTrackSize;
            public Point MaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public sealed class MonitorInfo
        {
            public int Size = Marshal.SizeOf<MonitorInfo>();
            public Rect Monitor;
            public Rect WorkArea;
            public int Flags;
        }
    }
}
