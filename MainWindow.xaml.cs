using System.Windows;

namespace GalaxyClient;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void InstallVersion_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "The launcher interface is ready. Minecraft version discovery and installation are the next implementation step.",
            "Galaxy Client", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
