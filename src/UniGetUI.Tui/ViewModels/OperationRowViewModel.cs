using System.Collections.ObjectModel;
using Avalonia.Threading;
using UniGetUI.PackageOperations;

namespace UniGetUI.Tui.ViewModels;

public sealed class OperationRowViewModel : ViewModelBase
{
    private string _status;
    private string _output = "";

    public OperationRowViewModel(AbstractOperation operation)
    {
        Operation = operation;
        Title = operation.Metadata.Title;
        _status = operation.Status.ToString();
        RefreshOutput();
    }

    internal AbstractOperation Operation { get; }
    public string Title { get; }
    public ObservableCollection<string> Lines { get; } = [];

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Output
    {
        get => _output;
        private set => SetProperty(ref _output, value);
    }

    public void AddLine(string line, AbstractOperation.LineType type)
    {
        if (type is AbstractOperation.LineType.ProgressIndicator)
            Status = line;

        Dispatcher.UIThread.Post(() =>
        {
            Lines.Add(line);
            RefreshOutput();
        });
    }

    public void RefreshOutput()
    {
        Output = string.Join(Environment.NewLine, Operation.GetOutput().Select(line => line.Item1));
    }
}
