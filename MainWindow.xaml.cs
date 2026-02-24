using System.Windows;
using GuitarToolkit.Pages;

namespace GuitarToolkit
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadPages();
        }

        private void LoadPages()
        {
            TunerFrame.Navigate(new TunerPage());
            MetronomeFrame.Navigate(new MetronomePage());
            DroneFrame.Navigate(new DronePage());
        }
    }
}

//GuitarToolkit /
//├── Pages /
//│   ├── TunerPage.xaml
//│   ├── TunerPage.xaml.cs
//│   ├── MetronomePage.xaml
//│   ├── MetronomePage.xaml.cs
//│   ├── DronePage.xaml
//│   └── DronePage.xaml.cs
//├── Services /
//│   ├── AudioService.cs       ← захват микрофона, FFT
//│   ├── MetronomeService.cs   ← логика метронома
//│   └── DroneService.cs       ← генерация тона
//├── MainWindow.xaml
//└── MainWindow.xaml.cs