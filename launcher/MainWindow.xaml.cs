using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Fifawc10Launcher;

public partial class MainWindow : Window
{
    readonly Settings settings = Settings.Load();
    const string GameExe = "fifawc10.exe";

    public MainWindow()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Version {v?.ToString(3)} - unofficial fan project, not affiliated with EA or FIFA";

        Select(DisplayBox, settings.Display);
        Select(WindowBox, $"{settings.WindowWidth}x{settings.WindowHeight}");
        Select(ScaleBox, settings.RenderScale.ToString());
        Select(AnisoBox, settings.Anisotropic.ToString());
        Select(ControlsBox, settings.Controls);
        VSyncBox.IsChecked = settings.VSync;
        UpdateGame(settings.GamePath);
    }

    static void Select(ComboBox box, string tag)
    {
        foreach (ComboBoxItem item in box.Items)
            if ((string)item.Tag == tag) { box.SelectedItem = item; return; }
        box.SelectedIndex = 0;
    }

    static string TagOf(ComboBox box) => (string)((ComboBoxItem)box.SelectedItem).Tag;

    void UpdateGame(string path)
    {
        settings.GamePath = path;
        GamePathText.Text = string.IsNullOrEmpty(path) ? "No game selected" : path;
        StatusText.Text = "Checking...";
        PlayButton.IsEnabled = false;
        if (string.IsNullOrEmpty(path)) { ShowCheck(GameSource.Check(path)); return; }
        // Hashing a disc image takes a moment; keep the window responsive.
        Task.Run(() => GameSource.Check(path)).ContinueWith(t => Dispatcher.Invoke(() =>
        {
            if (settings.GamePath == path) ShowCheck(t.Result);
        }));
    }

    void ShowCheck(GameCheck check)
    {
        StatusText.Text = (check.Playable ? "✔  " : "") + check.Message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(
            check.Playable ? "Good" : check.Kind == SourceKind.None ? "Muted" : "Bad");
        PlayButton.IsEnabled = check.Playable;
    }

    void BrowseIso_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose your Xbox 360 disc image",
            Filter = "Disc images (*.iso)|*.iso|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == true) UpdateGame(dlg.FileName);
    }

    void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose the folder that holds default.xex" };
        if (dlg.ShowDialog(this) == true) UpdateGame(dlg.FolderName);
    }

    void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(Settings.DataFolder) { UseShellExecute = true });

    void Play_Click(object sender, RoutedEventArgs e)
    {
        settings.Display = TagOf(DisplayBox);
        var size = TagOf(WindowBox).Split('x');
        settings.WindowWidth = int.Parse(size[0]);
        settings.WindowHeight = int.Parse(size[1]);
        settings.RenderScale = int.Parse(TagOf(ScaleBox));
        settings.Anisotropic = int.Parse(TagOf(AnisoBox));
        settings.Controls = TagOf(ControlsBox);
        settings.VSync = VSyncBox.IsChecked == true;

        var exe = Path.Combine(AppContext.BaseDirectory, GameExe);
        if (!File.Exists(exe))
        {
            MessageBox.Show(this, $"{GameExe} was not found next to the launcher.", Title,
                            MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        try
        {
            settings.Save();
            Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
            });
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not start the game: {ex.Message}", Title,
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
