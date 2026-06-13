using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using GuitarToolkit.Core.Services;

namespace GuitarToolkit.UI;

public partial class AboutView : UserControl
{
    private const string GitHubUrl = "https://github.com/LuTiK1984/GuitarToolkitVST";
    private const string LicenseUrl = "https://github.com/LuTiK1984/GuitarToolkitVST/blob/master/LICENSE";

    public AboutView()
    {
        InitializeComponent();

        string version = typeof(AboutView).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "dev";

        VersionText.Text = $"Version {version}";
        LogPathText.Text = AppLogger.LogDirectory;
    }

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Open(GitHubUrl);
        }
        catch (Exception ex)
        {
            AppLogger.Warning("Failed to open GitHub page.", ex);
            MessageBox.Show(
                GitHubUrl,
                "GuitarToolkit GitHub",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppLogger.LogDirectory);
            Open(AppLogger.LogDirectory);
        }
        catch (Exception ex)
        {
            AppLogger.Warning("Failed to open log directory.", ex);
            MessageBox.Show(
                AppLogger.LogDirectory,
                "GuitarToolkit logs",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void License_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string licensePath = Path.Combine(AppContext.BaseDirectory, "LICENSE");
            Open(File.Exists(licensePath) ? licensePath : LicenseUrl);
        }
        catch (Exception ex)
        {
            AppLogger.Warning("Failed to open license.", ex);
            MessageBox.Show(
                "GuitarToolkit is released under the MIT License. The LICENSE file is included with every release package.",
                "GuitarToolkit license",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private static void Open(string target)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
    }
}
