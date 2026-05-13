using UniGetUI.PackageEngine.Interfaces;

namespace UniGetUI.Tui.ViewModels;

public sealed class PackageRowViewModel : ViewModelBase
{
    private readonly Action _selectionChanged;

    public PackageRowViewModel(IPackage package, Action selectionChanged)
    {
        Package = package;
        _selectionChanged = selectionChanged;
    }

    public IPackage Package { get; }
    public string Name => Package.Name;
    public string Id => Package.Id;
    public string Version => Package.VersionString;
    public string NewVersion => Package.NewVersionString;
    public string Source => Package.Source.AsString_DisplayName;
    public string Manager => Package.Manager.DisplayName;
    public string Tag => Package.Tag.ToString();

    public bool IsChecked
    {
        get => Package.IsChecked;
        set
        {
            if (Package.IsChecked == value)
                return;

            Package.IsChecked = value;
            OnPropertyChanged();
            _selectionChanged();
        }
    }
}
