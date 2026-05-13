using System.Runtime.InteropServices;
using UniGetUI.Core.Data;
using UniGetUI.Core.Logging;
using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine;

namespace UniGetUI.Tui.Infrastructure;

internal static class TuiBootstrapper
{
    private static bool _hasStarted;

    public static async Task InitializeAsync(IProgress<string>? progress = null)
    {
        if (_hasStarted)
            return;

        _hasStarted = true;
        progress?.Report("Initializing UniGetUI services...");
        CoreTools.ReloadLanguageEngineInstance();

        Logger.ImportantInfo($"Starting UniGetUI TUI {CoreData.VersionName}");
        Logger.ImportantInfo($"UI Framework: Consolonia");
        Logger.ImportantInfo($"Data directory {CoreData.UniGetUIDataDirectory}");
        Logger.ImportantInfo($"OS: {RuntimeInformation.OSDescription}");
        Logger.ImportantInfo($"Runtime: {RuntimeInformation.FrameworkDescription}");
        Logger.ImportantInfo($"Elevated: {CoreTools.IsAdministrator()}");

        progress?.Report("Loading package engine...");
        PEInterface.LoadLoaders();

        progress?.Report("Initializing package managers...");
        await Task.Run(PEInterface.LoadManagers).ConfigureAwait(false);

        progress?.Report("Ready");
        Logger.Info("UniGetUI TUI bootstrap completed");
    }
}
