using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace Fifawc10Launcher;

public partial class App : Application
{
    static Mutex? single;

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

    void App_Startup(object sender, StartupEventArgs e)
    {
        // One launcher at a time: a second start brings the first one forward.
        single = new Mutex(true, "Fifawc10Recompiled.Launcher", out bool first);
        if (!first)
        {
            var me = System.Diagnostics.Process.GetCurrentProcess();
            foreach (var p in System.Diagnostics.Process.GetProcessesByName(me.ProcessName))
            {
                if (p.Id == me.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                ShowWindow(p.MainWindowHandle, 9);
                SetForegroundWindow(p.MainWindowHandle);
            }
            Shutdown();
            return;
        }
        new MainWindow().Show();
    }
}
