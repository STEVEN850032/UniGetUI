using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using Avalonia.Threading;
using UniGetUI.Core.Data;
using UniGetUI.Core.Logging;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.Core.Tools;
using UniGetUI.Interface;
using UniGetUI.Interface.Enums;
using UniGetUI.Interface.Telemetry;
using UniGetUI.PackageEngine;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.Operations;
using UniGetUI.PackageEngine.PackageClasses;
using UniGetUI.PackageEngine.PackageLoader;
using UniGetUI.Tui.Infrastructure;

namespace UniGetUI.Tui.ViewModels;

public enum TuiPage
{
    Dashboard,
    Discover,
    Updates,
    Installed,
    Bundles,
    Settings,
    Logs,
    Help,
    Backup,
}

public sealed class MainWindowViewModel : ViewModelBase
{
    private TuiPage _currentPage = TuiPage.Dashboard;
    private string _statusText = CoreTools.Translate("Starting...");
    private string _packageSubtitle = "";
    private string _searchQuery = "";
    private string _detailsText = "";
    private string _bundlePath = "";
    private string _selectedLogText = "";
    private bool _isBusy = true;
    private PackageRowViewModel? _selectedPackage;
    private OperationRowViewModel? _selectedOperation;

    public MainWindowViewModel()
    {
        Operations = TuiOperationRegistry.Operations;
        RefreshCommand = new AsyncCommand(InitializeOrRefreshAsync, () => !IsBusy);
        ReloadCommand = new AsyncCommand(ReloadCurrentPageAsync, () => !IsBusy);
        SearchCommand = new AsyncCommand(SearchAsync, () => !IsBusy);
        DetailsCommand = new AsyncCommand(ShowSelectedDetailsAsync, () => SelectedPackage is not null);
        InstallCommand = new AsyncCommand(() => LaunchOperationAsync(OperationType.Install), () => CanOperate);
        UpdateCommand = new AsyncCommand(() => LaunchOperationAsync(OperationType.Update), () => CanOperate);
        UninstallCommand = new AsyncCommand(() => LaunchOperationAsync(OperationType.Uninstall), () => CanOperate);
        AddToBundleCommand = new AsyncCommand(AddSelectionToBundleAsync, () => SelectedPackages.Any());
        RemoveFromBundleCommand = new AsyncCommand(RemoveSelectionFromBundleAsync, () => CurrentPage == TuiPage.Bundles && SelectedPackages.Any());
        ImportBundleCommand = new AsyncCommand(ImportBundleAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(BundlePath));
        ExportBundleCommand = new AsyncCommand(ExportBundleAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(BundlePath));
        InstallBundleCommand = new AsyncCommand(InstallBundleAsync, () => !IsBusy && CurrentPage == TuiPage.Bundles && BundlePackageCount > 0);
        SelectAllCommand = new RelayCommand(SelectAllPackages);
        ClearSelectionCommand = new RelayCommand(ClearPackageSelection);
        CancelOperationsCommand = new RelayCommand(TuiOperationRegistry.CancelAll);
        ClearFinishedOperationsCommand = new RelayCommand(TuiOperationRegistry.ClearFinished);
        RefreshLogsCommand = new RelayCommand(RefreshLogs);

        NavigateDashboardCommand = new RelayCommand(() => Navigate(TuiPage.Dashboard));
        NavigateDiscoverCommand = new RelayCommand(() => Navigate(TuiPage.Discover));
        NavigateUpdatesCommand = new RelayCommand(() => Navigate(TuiPage.Updates));
        NavigateInstalledCommand = new RelayCommand(() => Navigate(TuiPage.Installed));
        NavigateBundlesCommand = new RelayCommand(() => Navigate(TuiPage.Bundles));
        NavigateSettingsCommand = new RelayCommand(() => Navigate(TuiPage.Settings));
        NavigateLogsCommand = new RelayCommand(() => Navigate(TuiPage.Logs));
        NavigateHelpCommand = new RelayCommand(() => Navigate(TuiPage.Help));
        NavigateBackupCommand = new RelayCommand(() => Navigate(TuiPage.Backup));

        BuildSettingsRows();
    }

    public ObservableCollection<ManagerStatusItem> Managers { get; } = [];
    public ObservableCollection<PackageRowViewModel> PackageRows { get; } = [];
    public ObservableCollection<SettingRowViewModel> SettingRows { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];
    public ObservableCollection<OperationRowViewModel> Operations { get; }

    public ICommand RefreshCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand DetailsCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand AddToBundleCommand { get; }
    public ICommand RemoveFromBundleCommand { get; }
    public ICommand ImportBundleCommand { get; }
    public ICommand ExportBundleCommand { get; }
    public ICommand InstallBundleCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand CancelOperationsCommand { get; }
    public ICommand ClearFinishedOperationsCommand { get; }
    public ICommand RefreshLogsCommand { get; }
    public ICommand NavigateDashboardCommand { get; }
    public ICommand NavigateDiscoverCommand { get; }
    public ICommand NavigateUpdatesCommand { get; }
    public ICommand NavigateInstalledCommand { get; }
    public ICommand NavigateBundlesCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand NavigateLogsCommand { get; }
    public ICommand NavigateHelpCommand { get; }
    public ICommand NavigateBackupCommand { get; }

    public TuiPage CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(IsDashboardVisible));
                OnPropertyChanged(nameof(IsPackagesVisible));
                OnPropertyChanged(nameof(IsSettingsVisible));
                OnPropertyChanged(nameof(IsLogsVisible));
                OnPropertyChanged(nameof(IsHelpVisible));
                OnPropertyChanged(nameof(IsBackupVisible));
                RefreshCommandStates();
            }
        }
    }

    public string PageTitle => CurrentPage switch
    {
        TuiPage.Discover => CoreTools.Translate("Discover Packages"),
        TuiPage.Updates => CoreTools.Translate("Software Updates"),
        TuiPage.Installed => CoreTools.Translate("Installed Packages"),
        TuiPage.Bundles => CoreTools.Translate("Package Bundles"),
        TuiPage.Settings => CoreTools.Translate("Settings"),
        TuiPage.Logs => CoreTools.Translate("Logs"),
        TuiPage.Help => CoreTools.Translate("Help"),
        TuiPage.Backup => CoreTools.Translate("Backup and authentication"),
        _ => CoreTools.Translate("Dashboard"),
    };

    public bool IsDashboardVisible => CurrentPage == TuiPage.Dashboard;
    public bool IsPackagesVisible => CurrentPage is TuiPage.Discover or TuiPage.Updates or TuiPage.Installed or TuiPage.Bundles;
    public bool IsSettingsVisible => CurrentPage == TuiPage.Settings;
    public bool IsLogsVisible => CurrentPage == TuiPage.Logs;
    public bool IsHelpVisible => CurrentPage == TuiPage.Help;
    public bool IsBackupVisible => CurrentPage == TuiPage.Backup;
    public bool CanOperate => SelectedPackages.Any();
    public int InstalledPackageCount => InstalledPackagesLoader.Instance?.Packages.Count ?? 0;
    public int UpgradablePackageCount => UpgradablePackagesLoader.Instance?.Packages.Count ?? 0;
    public int BundlePackageCount => PackageBundlesLoader.Instance?.Packages.Count ?? 0;
    public string BackupDirectory => string.IsNullOrWhiteSpace(Settings.GetValue(Settings.K.ChangeBackupOutputDirectory))
        ? CoreData.UniGetUI_DefaultBackupDirectory
        : Settings.GetValue(Settings.K.ChangeBackupOutputDirectory);
    public string GitHubLogin => string.IsNullOrWhiteSpace(Settings.GetValue(Settings.K.GitHubUserLogin))
        ? CoreTools.Translate("Not logged in")
        : Settings.GetValue(Settings.K.GitHubUserLogin);

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string PackageSubtitle
    {
        get => _packageSubtitle;
        private set => SetProperty(ref _packageSubtitle, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string BundlePath
    {
        get => _bundlePath;
        set
        {
            if (SetProperty(ref _bundlePath, value))
                RefreshCommandStates();
        }
    }

    public string DetailsText
    {
        get => _detailsText;
        private set => SetProperty(ref _detailsText, value);
    }

    public string SelectedLogText
    {
        get => _selectedLogText;
        private set => SetProperty(ref _selectedLogText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                RefreshCommandStates();
        }
    }

    public PackageRowViewModel? SelectedPackage
    {
        get => _selectedPackage;
        set
        {
            if (SetProperty(ref _selectedPackage, value))
            {
                RefreshCommandStates();
                if (value is not null)
                    DetailsText = BuildPackageSummary(value.Package);
            }
        }
    }

    public OperationRowViewModel? SelectedOperation
    {
        get => _selectedOperation;
        set
        {
            if (SetProperty(ref _selectedOperation, value))
                SelectedLogText = value?.Output ?? "";
        }
    }

    private IEnumerable<IPackage> SelectedPackages =>
        PackageRows.Where(row => row.IsChecked).Select(row => row.Package)
            .DefaultIfEmpty(SelectedPackage?.Package)
            .Where(package => package is not null)!;

    public async Task InitializeOrRefreshAsync()
    {
        IsBusy = true;
        try
        {
            var progress = new Progress<string>(message => StatusText = CoreTools.Translate(message));
            await TuiBootstrapper.InitializeAsync(progress);
            RefreshDashboard();
            Navigate(CurrentPage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Navigate(TuiPage page)
    {
        CurrentPage = page;
        DetailsText = "";
        if (page is TuiPage.Dashboard)
            RefreshDashboard();
        else if (page is TuiPage.Settings)
            BuildSettingsRows();
        else if (page is TuiPage.Logs)
            RefreshLogs();
        else if (page is TuiPage.Backup)
            RefreshBackupStatus();
        else if (IsPackagesVisible)
            _ = ReloadCurrentPageAsync();
    }

    private async Task SearchAsync()
    {
        if (CurrentPage == TuiPage.Discover)
        {
            await ReloadCurrentPageAsync();
            return;
        }

        ApplyPackageQuery(GetCurrentLoaderPackages(), isLoading: false);
    }

    private async Task ReloadCurrentPageAsync()
    {
        if (!IsPackagesVisible)
            return;

        IsBusy = true;
        try
        {
            StatusText = CoreTools.Translate("Loading packages...");

            if (CurrentPage == TuiPage.Discover)
            {
                DiscoverablePackagesLoader.Instance.ClearPackages(emitFinishSignal: false);
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                    await DiscoverablePackagesLoader.Instance.ReloadPackages(SearchQuery.Trim());
            }
            else if (CurrentPage == TuiPage.Updates)
            {
                await UpgradablePackagesLoader.Instance.ReloadPackages();
            }
            else if (CurrentPage == TuiPage.Installed)
            {
                await InstalledPackagesLoader.Instance.ReloadPackages();
            }

            ApplyPackageQuery(GetCurrentLoaderPackages(), isLoading: false);
            RefreshDashboard();
            StatusText = CoreTools.Translate("Ready");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private IReadOnlyList<IPackage> GetCurrentLoaderPackages()
    {
        return CurrentPage switch
        {
            TuiPage.Discover => DiscoverablePackagesLoader.Instance.Packages,
            TuiPage.Updates => UpgradablePackagesLoader.Instance.Packages,
            TuiPage.Installed => InstalledPackagesLoader.Instance.Packages,
            TuiPage.Bundles => PackageBundlesLoader.Instance.Packages,
            _ => [],
        };
    }

    private void ApplyPackageQuery(IReadOnlyList<IPackage> packages, bool isLoading)
    {
        var query = PackageListQuery.Apply(packages, new PackageListQueryOptions
        {
            Query = CurrentPage == TuiPage.Discover ? "" : SearchQuery,
            SortField = CurrentPage == TuiPage.Updates
                ? PackageListSortField.NewVersion
                : PackageListSortField.Name,
            SortAscending = true,
        });

        PackageRows.Clear();
        foreach (IPackage package in query.Packages)
            PackageRows.Add(new PackageRowViewModel(package, OnPackageSelectionChanged));

        PackageSubtitle = PackageListQuery.BuildSubtitle(
            query.Packages.Count,
            query.TotalCount,
            query.SelectedCount,
            isLoading
        );
        OnPackageSelectionChanged();
    }

    private async Task ShowSelectedDetailsAsync()
    {
        if (SelectedPackage is not { Package: { } package })
            return;

        StatusText = CoreTools.Translate("Loading package details...");
        await package.Details.Load();
        DetailsText = BuildPackageDetails(package);
        StatusText = CoreTools.Translate("Ready");
    }

    private async Task LaunchOperationAsync(OperationType operationType)
    {
        var packages = SelectedPackages.Where(package => package is not null).ToList();
        if (packages.Count == 0)
        {
            StatusText = CoreTools.Translate("No packages selected");
            return;
        }

        foreach (IPackage package in packages)
        {
            var options = await InstallOptionsFactory.LoadApplicableAsync(package);
            PackageOperation operation = operationType switch
            {
                OperationType.Update => new UpdatePackageOperation(package, options),
                OperationType.Uninstall => new UninstallPackageOperation(package, options),
                _ => new InstallPackageOperation(package, options),
            };

            operation.OperationSucceeded += (_, _) => RegisterTelemetry(package, operationType, TEL_OP_RESULT.SUCCESS);
            operation.OperationFailed += (_, _) => RegisterTelemetry(package, operationType, TEL_OP_RESULT.FAILED);
            TuiOperationRegistry.Add(operation);
            _ = operation.MainThread();
        }

        StatusText = CoreTools.Translate("{0} operations queued", packages.Count);
    }

    private async Task AddSelectionToBundleAsync()
    {
        var packages = SelectedPackages.ToList();
        if (packages.Count == 0)
            return;

        await PackageBundlesLoader.Instance.AddPackagesAsync(packages);
        StatusText = CoreTools.Translate("{0} packages added to bundle", packages.Count);
        RefreshDashboard();
    }

    private async Task ImportBundleAsync()
    {
        IsBusy = true;
        try
        {
            var result = await IpcBundleApi.ImportBundleAsync(new IpcBundleImportRequest
            {
                Path = BundlePath,
                Append = true,
            });
            StatusText = CoreTools.Translate(
                "{0} packages loaded from bundle",
                result.Bundle.PackageCount
            );
            CurrentPage = TuiPage.Bundles;
            ApplyPackageQuery(PackageBundlesLoader.Instance.Packages, isLoading: false);
            RefreshDashboard();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportBundleAsync()
    {
        IsBusy = true;
        try
        {
            var result = await IpcBundleApi.ExportBundleAsync(new IpcBundleExportRequest
            {
                Path = BundlePath,
            });
            TelemetryHandler.ExportBundle(BundleFormatType.UBUNDLE);
            StatusText = CoreTools.Translate(
                "{0} packages exported to {1}",
                result.Bundle.PackageCount,
                result.Path ?? BundlePath
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InstallBundleAsync()
    {
        IsBusy = true;
        try
        {
            var result = await IpcBundleApi.InstallBundleAsync(new IpcBundleInstallRequest());
            StatusText = CoreTools.Translate(
                "Bundle install queued: {0} requested, {1} skipped",
                result.RequestedCount,
                result.SkippedCount
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task RemoveSelectionFromBundleAsync()
    {
        var packages = SelectedPackages.ToList();
        if (packages.Count == 0)
            return Task.CompletedTask;

        PackageBundlesLoader.Instance.RemoveRange(packages);
        ApplyPackageQuery(PackageBundlesLoader.Instance.Packages, isLoading: false);
        RefreshDashboard();
        return Task.CompletedTask;
    }

    private void SelectAllPackages()
    {
        foreach (PackageRowViewModel row in PackageRows)
            row.IsChecked = true;
        OnPackageSelectionChanged();
    }

    private void ClearPackageSelection()
    {
        foreach (PackageRowViewModel row in PackageRows)
            row.IsChecked = false;
        OnPackageSelectionChanged();
    }

    private void OnPackageSelectionChanged()
    {
        var query = PackageListQuery.Apply(PackageRows.Select(row => row.Package), new PackageListQueryOptions());
        PackageSubtitle = PackageListQuery.BuildSubtitle(
            PackageRows.Count,
            query.TotalCount,
            query.SelectedCount,
            isLoading: false
        );
        RefreshCommandStates();
    }

    private void RefreshDashboard()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshDashboard);
            return;
        }

        Managers.Clear();
        foreach (IPackageManager manager in PEInterface.Managers)
        {
            string state = manager.IsReady()
                ? CoreTools.Translate("Ready")
                : manager.IsEnabled()
                    ? CoreTools.Translate("Unavailable")
                    : CoreTools.Translate("Disabled");

            Managers.Add(new ManagerStatusItem(
                manager.DisplayName,
                state,
                manager.Status.ExecutablePath
            ));
        }

        OnPropertyChanged(nameof(InstalledPackageCount));
        OnPropertyChanged(nameof(UpgradablePackageCount));
        OnPropertyChanged(nameof(BundlePackageCount));
    }

    private void RefreshLogs()
    {
        LogLines.Clear();
        foreach (LogEntry entry in Logger.GetLogs().TakeLast(250))
        {
            if (!string.IsNullOrWhiteSpace(entry.Content))
                LogLines.Add($"[{entry.Time}] {entry.Severity}: {entry.Content}");
        }

        string history = Settings.GetValue(Settings.K.OperationHistory);
        if (!string.IsNullOrWhiteSpace(history))
        {
            LogLines.Add("---- Operation history ----");
            foreach (string line in history.Split('\n').Take(250))
                if (!string.IsNullOrWhiteSpace(line))
                    LogLines.Add(line);
        }
    }

    private void BuildSettingsRows()
    {
        SettingRows.Clear();
        SettingRows.Add(new(Settings.K.DisableNotifications,
            CoreTools.Translate("Disable notifications"),
            CoreTools.Translate("Suppress desktop-style notifications where applicable."),
            isBoolean: true));
        SettingRows.Add(new(Settings.K.DisableApi,
            CoreTools.Translate("Disable API"),
            CoreTools.Translate("Disable the local IPC/API server."),
            isBoolean: true));
        SettingRows.Add(new(Settings.K.ParallelOperationCount,
            CoreTools.Translate("Parallel operations"),
            CoreTools.Translate("Maximum number of package operations to run concurrently."),
            isBoolean: false));
        SettingRows.Add(new(Settings.K.StartupPage,
            CoreTools.Translate("Startup page"),
            CoreTools.Translate("Preferred startup page shared with the desktop UI."),
            isBoolean: false));
        SettingRows.Add(new(Settings.K.ChangeBackupOutputDirectory,
            CoreTools.Translate("Backup directory"),
            CoreTools.Translate("Directory used for local bundle backups."),
            isBoolean: false));
        SettingRows.Add(new(Settings.K.EnablePackageBackup_LOCAL,
            CoreTools.Translate("Enable local package backup"),
            CoreTools.Translate("Create package bundle backups locally."),
            isBoolean: true));
        SettingRows.Add(new(Settings.K.EnablePackageBackup_CLOUD,
            CoreTools.Translate("Enable cloud package backup"),
            CoreTools.Translate("Upload package bundle backups when authenticated."),
            isBoolean: true));
    }

    private void RefreshBackupStatus()
    {
        OnPropertyChanged(nameof(BackupDirectory));
        OnPropertyChanged(nameof(GitHubLogin));
    }

    private void RefreshCommandStates()
    {
        (RefreshCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (ReloadCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (SearchCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (DetailsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (InstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (UpdateCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (UninstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (AddToBundleCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RemoveFromBundleCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (ImportBundleCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (ExportBundleCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (InstallBundleCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }

    private static string BuildPackageSummary(IPackage package)
    {
        return $"{package.Name} ({package.Id}){Environment.NewLine}"
            + $"{CoreTools.Translate("Version")}: {package.VersionString}{Environment.NewLine}"
            + $"{CoreTools.Translate("New version")}: {package.NewVersionString}{Environment.NewLine}"
            + $"{CoreTools.Translate("Manager")}: {package.Manager.DisplayName}{Environment.NewLine}"
            + $"{CoreTools.Translate("Source")}: {package.Source.AsString_DisplayName}";
    }

    private static string BuildPackageDetails(IPackage package)
    {
        var details = package.Details;
        var sb = new StringBuilder();
        sb.AppendLine(BuildPackageSummary(package));
        sb.AppendLine();
        sb.AppendLine($"{CoreTools.Translate("Description")}: {details.Description}");
        sb.AppendLine($"{CoreTools.Translate("Publisher")}: {details.Publisher}");
        sb.AppendLine($"{CoreTools.Translate("Author")}: {details.Author}");
        sb.AppendLine($"{CoreTools.Translate("License")}: {details.License}");
        sb.AppendLine($"{CoreTools.Translate("Homepage")}: {details.HomepageUrl}");
        sb.AppendLine($"{CoreTools.Translate("Installer")}: {details.InstallerUrl}");
        sb.AppendLine($"{CoreTools.Translate("Installer type")}: {details.InstallerType}");
        sb.AppendLine($"{CoreTools.Translate("Release notes")}: {details.ReleaseNotesUrl}");
        if (!string.IsNullOrWhiteSpace(details.ReleaseNotes))
        {
            sb.AppendLine();
            sb.AppendLine(details.ReleaseNotes);
        }
        return sb.ToString();
    }

    private static void RegisterTelemetry(IPackage package, OperationType operationType, TEL_OP_RESULT result)
    {
        if (operationType == OperationType.Install)
            TelemetryHandler.InstallPackage(package, result, TEL_InstallReferral.DIRECT_SEARCH);
        else if (operationType == OperationType.Update)
            TelemetryHandler.UpdatePackage(package, result);
        else if (operationType == OperationType.Uninstall)
            TelemetryHandler.UninstallPackage(package, result);
    }
}
