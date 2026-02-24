using System.Windows;
using GuitarToolkit.Pages;

namespace GuitarToolkit
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            AllocConsole();

            [System.Runtime.InteropServices.DllImport("kernel32.dll")]
            static extern bool AllocConsole();
            InitializeComponent();
            LoadPages();
        }

        private void LoadPages()
        {
            TunerFrame.Navigate(new TunerPage());
            MetronomeFrame.Navigate(new MetronomePage());
            DroneFrame.Navigate(new DronePage());
            ScalesFrame.Navigate(new ScalesPage());
            RecorderFrame.Navigate(new RecorderPage());
        }
    }
}