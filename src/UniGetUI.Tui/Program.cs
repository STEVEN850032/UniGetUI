using Avalonia;
using Consolonia;
using UniGetUI.Tui.Infrastructure;

namespace UniGetUI.Tui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (TuiCliHandler.HandlePreUiArgs(args) is { } exitCode)
        {
            Environment.Exit(exitCode);
            return;
        }

        BuildAvaloniaApp()
            .StartWithConsoleLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseConsolonia()
            .UseAutoDetectedConsole()
            .LogToException();
}
