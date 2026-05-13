using System.Collections.ObjectModel;
using Avalonia.Threading;
using UniGetUI.Core.Logging;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageOperations;
using UniGetUI.Tui.ViewModels;

namespace UniGetUI.Tui.Infrastructure;

internal static class TuiOperationRegistry
{
    public static ObservableCollection<OperationRowViewModel> Operations { get; } = [];

    public static void Add(AbstractOperation operation)
    {
        var row = new OperationRowViewModel(operation);
        Dispatcher.UIThread.Post(() => Operations.Add(row));

        operation.LogLineAdded += (_, line) => row.AddLine(line.Item1, line.Item2);
        operation.StatusChanged += (_, status) => row.Status = status.ToString();

        operation.OperationSucceeded += (_, _) =>
        {
            row.Status = OperationStatus.Succeeded.ToString();
            _ = Task.Run(() => AppendOperationHistory(operation));
        };
        operation.OperationFailed += (_, _) =>
        {
            row.Status = OperationStatus.Failed.ToString();
            _ = Task.Run(() => AppendOperationHistory(operation));
        };
        operation.OperationFinished += (_, _) => row.RefreshOutput();
    }

    public static void CancelAll()
    {
        foreach (OperationRowViewModel row in Operations.ToList())
        {
            if (row.Operation.Status is OperationStatus.Running or OperationStatus.InQueue)
                row.Operation.Cancel();
        }
    }

    public static void ClearFinished()
    {
        foreach (OperationRowViewModel row in Operations
            .Where(row => row.Operation.Status is OperationStatus.Succeeded
                or OperationStatus.Failed
                or OperationStatus.Canceled)
            .ToList())
        {
            Operations.Remove(row);
        }
    }

    private static void AppendOperationHistory(AbstractOperation operation)
    {
        try
        {
            var rawOutput = new List<string>
            {
                "",
                "--------------------------------------------------------------------------------",
            };
            foreach (var (text, _) in operation.GetOutput())
                rawOutput.Add(text);

            string oldLines = Settings.GetValue(Settings.K.OperationHistory);
            Settings.SetValue(
                Settings.K.OperationHistory,
                string.Join('\n', rawOutput.Concat(oldLines.Split('\n').Take(300)))
            );
        }
        catch (Exception ex)
        {
            Logger.Warn("Failed to write TUI operation history");
            Logger.Warn(ex);
        }
    }
}
