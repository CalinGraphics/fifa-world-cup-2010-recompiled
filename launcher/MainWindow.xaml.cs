using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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
        TagLine.Text = $"NATIVE PC PORT   V{v}";
        AboutVersion.Text = $"Version {v}. Windows 10 and 11, 64-bit.";
        Root.SizeChanged += (_, _) =>
        {
            Root.Clip = new RectangleGeometry(new Rect(0, 0, Root.ActualWidth, Root.ActualHeight), 10, 10);
            BuildStadium();
        };
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
        var pages = new (RadioButton Tab, FrameworkElement Page)[]
        {
            (TabPlay, PagePlay), (TabDisplay, PageDisplay), (TabGraphics, PageGraphics),
            (TabControls, PageControls), (TabAbout, PageAbout),
        };
        foreach (var (tab, page) in pages)
        {
            bool on = tab.IsChecked == true;
            page.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (!on) continue;
            // a short slide in from the right, like a broadcast graphic
            var move = new TranslateTransform(18, 0);
            page.RenderTransform = move;
            var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        }
    }

    void SheetRow_Click(object sender, RoutedEventArgs e)
    {
        var target = (string)((Button)sender).Tag switch
        {
            "graphics" => TabGraphics,
            "controls" => TabControls,
            _ => TabDisplay,
        };
        target.IsChecked = true;
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
                Content = w == sw && h == sh ? $"{w} × {h}   YOUR SCREEN" : $"{w} × {h}",
                Tag = $"{w}x{h}",
            });
    }

    void ApplyToUi()
    {
        (settings.Display switch { "Window" => ModeWindow, "Fullscreen" => ModeFullscreen, _ => ModeBorderless }).IsChecked = true;
        foreach (ComboBoxItem item in WindowBox.Items)
            if ((string)item.Tag == $"{settings.WindowWidth}x{settings.WindowHeight}") WindowBox.SelectedItem = item;
        VSyncBox.IsChecked = settings.VSync;
        (settings.FrameRateLimit == 30 ? Fps30 : Fps60).IsChecked = true;
        ShowFpsBox.IsChecked = settings.ShowFps;
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
        settings.FrameRateLimit = Fps30.IsChecked == true ? 30 : 60;
        settings.ShowFps = ShowFpsBox.IsChecked == true;
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
        try { settings.Save(); } catch { /* saved again on Kick off */ }
    }

    void UpdateSummary()
    {
        SumRes.Text = settings.RenderScale switch { 1 => "720P", 3 => "4K", _ => "1440P" };
        SumDisplay.Text = settings.Display switch
        {
            "Window" => $"WINDOW  {settings.WindowWidth} × {settings.WindowHeight}",
            "Fullscreen" => "FULLSCREEN",
            _ => "BORDERLESS",
        };
        SumCtl.Text = settings.Controls == "Keyboard" ? "KEYBOARD" : "CONTROLLER";
        SumFps.Text = (settings.FrameRateLimit == 30 ? "30" : "60") + (settings.VSync ? "  ·  V-SYNC" : "  ·  NO V-SYNC")
                      + (settings.ShowFps ? "  ·  COUNTER" : "");
        CtlHint.Text = settings.Controls == "Keyboard"
            ? "The keyboard plays as a controller. The keys:"
            : "Xbox, PlayStation and most other gamepads. Connect it before Kick off. Keyboard keys, if you need them:";
    }

    // ---------------------------------------------------------------- game copy

    void UpdateGame(string path)
    {
        settings.GamePath = path;
        GamePathText.Text = path;
        PlayButton.IsEnabled = false;
        if (string.IsNullOrEmpty(path)) { ShowCheck(GameSource.Check(path)); return; }
        SetBusy(true);
        StatusTitle.Text = "Checking your copy";
        StatusText.Text = "Making sure it's the disc this port was built from.";
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
        Busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy) Stamp.Visibility = Visibility.Collapsed;
        Busy.BeginAnimation(OpacityProperty, busy && Motion
            ? new DoubleAnimation(1, 0.3, TimeSpan.FromMilliseconds(500)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }
            : null);
    }

    void ShowCheck(GameCheck c)
    {
        check = c;
        var good = (Brush)FindResource("Green");
        var bad = (Brush)FindResource("Red");
        if (c.Playable)
        {
            StatusTitle.Text = c.Kind == SourceKind.Xbox360Iso ? "Xbox 360 disc image" : "Xbox 360 game folder";
            StatusText.Text = "The exact disc this port was built from. Ready when you are.";
            ShowStamp("VERIFIED", good);
            ReadyDot.Fill = good;
            ReadyText.Text = "READY";
        }
        else if (c.Kind == SourceKind.None)
        {
            StatusTitle.Text = "No game selected";
            StatusText.Text = "Choose your Xbox 360 disc image, or drop it on this window.";
            Stamp.Visibility = Visibility.Collapsed;
            ReadyDot.Fill = (Brush)FindResource("Dim");
            ReadyText.Text = "CHOOSE YOUR GAME";
        }
        else
        {
            StatusTitle.Text = c.Kind == SourceKind.Ps3Iso ? "PS3 disc image" : "Can't use this copy";
            StatusText.Text = c.Message;
            ShowStamp(c.Kind == SourceKind.Ps3Iso ? "PS3" : "REJECTED", bad);
            ReadyDot.Fill = bad;
            ReadyText.Text = "CHOOSE ANOTHER COPY";
        }
        PlayButton.IsEnabled = c.Playable;
    }

    void ShowStamp(string text, Brush brush)
    {
        StampText.Text = text;
        StampText.Foreground = brush;
        Stamp.BorderBrush = brush;
        Stamp.Visibility = Visibility.Visible;
        if (!Motion) return;
        // stamped down: a quick drop from a little larger
        var scale = new ScaleTransform(1.35, 1.35);
        Stamp.RenderTransform = new TransformGroup { Children = { scale, new RotateTransform(-7) } };
        var ease = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.35, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.35, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        Stamp.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
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
            ("Move", "W A S D"), ("Short pass", "SPACE"), ("Shoot", "BACKSPACE"),
            ("Lob, cross", "L"), ("Through ball", "P"), ("Sprint", "E"),
        };
        (string, string)[] right =
        {
            ("Pace control", "Q"), ("Player run, finesse", "1  3"), ("Skill moves", "ARROWS"),
            ("D-pad", "SHIFT + ARROWS"), ("Pause, back", "ENTER  TAB"), ("Frame rate", "F3"),
        };
        foreach (var (a, k) in left) KeyMapLeft.Children.Add(KeyRow(a, k));
        foreach (var (a, k) in right) KeyMapRight.Children.Add(KeyRow(a, k));
    }

    Border KeyRow(string action, string keys)
    {
        var g = new Grid();
        g.Children.Add(new TextBlock
        {
            Text = action, Foreground = (Brush)FindResource("Muted"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
        });
        var chip = new Border
        {
            BorderBrush = (Brush)FindResource("HairStrong"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 1, 8, 1),
            HorizontalAlignment = HorizontalAlignment.Right,
            RenderTransform = new SkewTransform(-14, 0),
            Child = new TextBlock { Text = keys, FontFamily = (FontFamily)FindResource("Caps"), FontSize = 12.5 },
        };
        g.Children.Add(chip);
        return new Border
        {
            BorderBrush = (Brush)FindResource("Hair"), BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 8, 4, 8), Child = g,
        };
    }

    // ---------------------------------------------------------------- about page

    void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(Settings.DataFolder) { UseShellExecute = true });

    void Shortcut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var link = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                              "2010 FIFA World Cup Recompiled.lnk");
            var exe = Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "Launcher.exe");
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
        catch (Exception ex) { Toast($"Couldn't create the shortcut: {ex.Message}"); }
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
        Toast("Settings are back to the defaults for this PC.");
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

    // ---------------------------------------------------------------- kick off

    void Play_Click(object sender, RoutedEventArgs e)
    {
        if (check is not { Playable: true }) return;
        ReadFromUi();
        var exe = System.IO.Path.Combine(AppContext.BaseDirectory, GameExe);
        if (!File.Exists(exe))
        {
            Toast($"{GameExe} isn't next to the launcher.");
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
            // The data folder is the working folder, so logs land next to saves and settings.
            Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = Settings.DataFolder,
                UseShellExecute = false,
            });
            Close();
        }
        catch (Exception ex)
        {
            Toast($"Couldn't start the game: {ex.Message}");
        }
    }
}
