using Avalonia;

namespace SpiritCreator.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        BuildApp().StartWithClassicDesktopLifetime(args);
    }

    internal static AppBuilder BuildApp() => AppBuilder.Configure<App>().UseWin32().UseSkia();
}
