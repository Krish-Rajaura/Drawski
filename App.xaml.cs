using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Drawski;

public partial class App : Application
{
    Mutex __instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        __instanceMutex = new Mutex(true, "Drawski.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        // GPU mode costs 100-300 MB of driver memory, software is plenty for ink and page images. --gpu for slow cpus / 4K
        if (!e.Args.Contains("--gpu")) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        new OverlayWindow(e.Args.FirstOrDefault(arg => !arg.StartsWith("--"))).Show();
    }
}
