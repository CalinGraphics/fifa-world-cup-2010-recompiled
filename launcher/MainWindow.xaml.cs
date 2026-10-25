using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Fifawc10Launcher;

public partial class MainWindow : Window
{
    Settings settings = Settings.Load();
    GameCheck? check;
    bool loading = true;
    const string GameExe = "fifawc10.exe";

    public MainWindow()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";
        AboutVersion.Text = $"Version {v}  -  launcher and game for Windows 10 / 11 (x64)";
        Root.SizeChanged += (_, _) =>
            Root.Clip = new RectangleGeometry(new Rect(0, 0, Root.ActualWidth, Root.ActualHeight), 15, 15);
        BuildKeyMap();
        FillWindowSizes();
        ApplyToUi();
        loading = false;
        UpdateSummary();
        UpdateGame(settings.GamePath);
    }

    // ---------------------------------------------------------------- window chrome

    void Drag_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (PagePlay is null) return;   // during InitializeComponent
        PagePlay.Visibility = TabPlay.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageDisplay.Visibility = TabDisplay.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageGraphics.Visibility = TabGraphics.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageControls.Visibility = TabControls.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = TabAbout.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        var page = new[] { PagePlay, PageDisplay, PageGraphics, (FrameworkElement)PageControls, PageAbout }
            .First(p => p.Visibility == Visibility.Visible);
        page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    // ---------------------------------------------------------------- settings <-> UI

    void FillWindowSizes()
    {
        var (sw, sh) = Settings.Screen;
        var sizes = new List<(int, int)> { (1280, 720), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160) }
            .Where(s => s.Item1 <= sw && s.Item2 <= sh).ToList();
        if (!sizes.Contains((sw, sh))) sizes.Add((sw, sh));
        if (!sizes.Contains((settings.WindowWidth, settings.WindowHeight)))
            sizes.Add((settings.WindowWidth, settings.WindowHeight));
        foreach (var (w, h) in sizes.OrderBy(s => s.Item1 * s.Item2))
            WindowBox.Items.Add(new ComboBoxItem
            {
                Content = w == sw && h == sh ? $"{w} x {h}  (your screen)" : $"{w} x {h}",
                Tag = $"{w}x{h}",
            });
    }

    void ApplyToUi()
    {
        (settings.Display switch { "Window" => ModeWindow, "Fullscreen" => ModeFullscreen, _ => ModeBorderless }).IsChecked = true;
        foreach (ComboBoxItem item in WindowBox.Items)
            if ((string)item.Tag == $"{settings.WindowWidth}x{settings.WindowHeight}") WindowBox.SelectedItem = item;
        VSyncBox.IsChecked = settings.VSync;
        (settings.RenderScale switch { 1 => Scale1, 3 => Scale3, _ => Scale2 }).IsChecked = true;
        (settings.Anisotropic switch { -1 => AnisoOff, 3 => Aniso4, 4 => Aniso8, _ => Aniso16 }).IsChecked = true;
        (settings.Controls == "Keyboard" ? CtlKeys : CtlPad).IsChecked = true;
    }

    void ReadFromUi()
    {
        settings.Display = ModeWindow.IsChecked == true ? "Window" : ModeFullscreen.IsChecked == true ? "Fullscreen" : "Borderless";
        if (WindowBox.SelectedItem is ComboBoxItem { Tag: string size })
        {
            var p = size.Split('x');
            settings.WindowWidth = int.Parse(p[0]);
            settings.WindowHeight = int.Parse(p[1]);
        }
        settings.VSync = VSyncBox.IsChecked == true;
        settings.RenderScale = Scale1.IsChecked == true ? 1 : Scale3.IsChecked == true ? 3 : 2;
        settings.Anisotropic = AnisoOff.IsChecked == true ? -1 : Aniso4.IsChecked == true ? 3 : Aniso8.IsChecked == true ? 4 : 5;
        settings.Controls = CtlKeys.IsChecked == true ? "Keyboard" : "Controller";
    }

    void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        ReadFromUi();
        UpdateSummary();
        SaveQuietly();
    }

    void SaveQuietly()
    {
        try { settings.Save(); } catch { /* saved again on Play */ }
    }

    void UpdateSummary()
    {
        SumRes.Text = settings.RenderScale switch { 1 => "720p", 3 => "4K", _ => "1440p" };
        SumResHint.Text = settings.Display switch
        {
            "Window" => $"Window {settings.WindowWidth} x {settings.WindowHeight}",
            "Fullscreen" => "Exclusive fullscreen",
            _ => "Borderless fullscreen",
        };
        SumCtl.Text = settings.Controls == "Keyboard" ? "Keyboard" : "Controller";
        SumCtlHint.Text = settings.Controls == "Keyboard" ? "WASD + Space" : "Xbox / PlayStation";
        SumFpsHint.Text = settings.VSync ? "In matches, V-Sync on" : "In matches, V-Sync off";
    }

    // ---------------------------------------------------------------- game copy

    void UpdateGame(string path)
    {
        settings.GamePath = path;
        GamePathText.Text = path;
        PlayButton.IsEnabled = false;
        if (string.IsNullOrEmpty(path)) { ShowCheck(GameSource.Check(path)); return; }
        SetBusy(true);
        StatusTitle.Text = "Checking your copy...";
        StatusText.Text = "Making sure it is the version this port was built from.";
        Task.Run(() => GameSource.Check(path)).ContinueWith(t => Dispatcher.Invoke(() =>
        {
            if (settings.GamePath != path) return;
            SetBusy(false);
            ShowCheck(t.Result);
            if (t.Result.Playable) SaveQuietly();
        }));
    }

    void SetBusy(bool busy)
    {
        StatusBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        StatusIcon.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        BusySpin.BeginAnimation(RotateTransform.AngleProperty, busy
            ? new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever }
            : null);
    }

    void ShowCheck(GameCheck c)
    {
        check = c;
        var good = (Brush)FindResource("Good");
        var bad = (Brush)FindResource("Bad");
        var dim = (Brush)FindResource("Dim");
        if (c.Playable)
        {
            StatusTitle.Text = c.Kind == SourceKind.Xbox360Iso ? "Xbox 360 disc image - ready" : "Xbox 360 game folder - ready";
            StatusText.Text = "Verified: this is the copy the port was built from.";
            StatusIcon.Data = Geometry.Parse("M0,8 L7,15 L22,0");
            StatusIcon.Stroke = Brushes.White;
            StatusRing.Fill = good;
            ReadyDot.Fill = good;
            ReadyText.Text = "Ready to play";
        }
        else if (c.Kind == SourceKind.None)
        {
            StatusTitle.Text = "No game selected";
            StatusText.Text = "Choose your Xbox 360 disc image, or drop it on this window.";
            StatusIcon.Data = Geometry.Parse("M11,0 L11,22 M0,11 L22,11");
            StatusIcon.Stroke = (Brush)FindResource("Muted");
            StatusRing.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0x34, 0x2B));
            ReadyDot.Fill = dim;
            ReadyText.Text = "Choose your game to play";
        }
        else
        {
            StatusTitle.Text = c.Kind == SourceKind.Ps3Iso ? "PS3 disc image - not supported yet" : "This copy can't be used";
            StatusText.Text = c.Message;
            StatusIcon.Data = Geometry.Parse("M0,0 L16,16 M16,0 L0,16");
            StatusIcon.Stroke = Brushes.White;
            StatusRing.Fill = bad;
            ReadyDot.Fill = bad;
            ReadyText.Text = "Choose another copy";
        }
        PlayButton.IsEnabled = c.Playable;
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

    static string? DroppedPath(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } f
            ? f[0] : null;

    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedPath(e) is null ? DragDropEffects.None : DragDropEffects.Copy;
        DropOverlay.Visibility = e.Effects == DragDropEffects.None ? Visibility.Collapsed : Visibility.Visible;
        e.Handled = true;
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (DroppedPath(e) is { } path)
        {
            TabPlay.IsChecked = true;
            UpdateGame(path);
        }
    }

    // ---------------------------------------------------------------- controls page

    void BuildKeyMap()
    {
        (string, string)[] left =
        {
            ("Move", "W  A  S  D"), ("Short pass (A)", "Space"), ("Shoot (B)", "Backspace"),
            ("Lob / cross (X)", "L"), ("Through ball (Y)", "P"), ("Sprint (RT)", "E"),
        };
        (string, string)[] right =
        {
            ("Pace control (LT)", "Q"), ("Player run / finesse (LB / RB)", "1  /  3"), ("Skill moves (right stick)", "Arrows"),
            ("D-pad", "Shift + Arrows"), ("Pause / Back", "Enter  /  Tab"), ("Performance overlay", "F3"),
        };
        foreach (var (a, k) in left) KeyMapLeft.Children.Add(KeyRow(a, k));
        foreach (var (a, k) in right) KeyMapRight.Children.Add(KeyRow(a, k));
    }

    Grid KeyRow(string action, string keys)
    {
        var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        g.Children.Add(new TextBlock { Text = action, Foreground = (Brush)FindResource("Muted"), FontSize = 13 });
        var chip = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x18, 0x14)),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock { Text = keys, FontFamily = (FontFamily)FindResource("DisplayWide"), FontSize = 12.5 },
        };
        g.Children.Add(chip);
        return g;
    }

    // ---------------------------------------------------------------- about page

    void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(Settings.DataFolder) { UseShellExecute = true });

    void Shortcut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                    "2010 FIFA World Cup Recompiled.lnk");
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Launcher.exe");
            var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic sc = shell.CreateShortcut(link);
            sc.TargetPath = exe;
            sc.WorkingDirectory = AppContext.BaseDirectory;
            sc.IconLocation = exe + ",0";
            sc.Description = "2010 FIFA World Cup South Africa: Recompiled";
            sc.Save();
            Toast("Shortcut added to your desktop.");
        }
        catch (Exception ex) { Toast($"Could not create the shortcut: {ex.Message}"); }
    }

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        var game = settings.GamePath;
        settings = Settings.ForThisPc();
        settings.GamePath = game;
        loading = true;
        ApplyToUi();
        loading = false;
        UpdateSummary();
        SaveQuietly();
        Toast("Settings reset to the defaults for this PC.");
    }

    void Toast(string text)
    {
        ToastText.Text = text;
        ToastText.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(4))),
                new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(5))),
            },
        });
    }

    // ---------------------------------------------------------------- play

    void Play_Click(object sender, RoutedEventArgs e)
    {
        if (check is not { Playable: true }) return;
        ReadFromUi();
        var exe = Path.Combine(AppContext.BaseDirectory, GameExe);
        if (!File.Exists(exe))
        {
            Toast($"{GameExe} was not found next to the launcher.");
            return;
        }
        if (Process.GetProcessesByName("fifawc10").Length > 0)
        {
            Toast("The game is already running.");
            return;
        }
        try
        {
            settings.Save();
            // The data folder is the working folder, so logs land next to saves and settings
            // (never in Program Files).
            Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = Settings.DataFolder,
                UseShellExecute = false,
            });
            Close();
        }
        catch (Exception ex)
        {
            Toast($"Could not start the game: {ex.Message}");
        }
    }
}
