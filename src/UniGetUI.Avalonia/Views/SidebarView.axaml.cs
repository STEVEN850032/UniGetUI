using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using UniGetUI.Avalonia.ViewModels;

namespace UniGetUI.Avalonia.Views;

public partial class SidebarView : BaseView<SidebarViewModel>
{
    private bool _lastNavItemSelectionWasAuto;

    /// <summary>
    /// Whether the nav item text labels are shown. False renders an icon-only rail; true renders the
    /// full labeled pane. Decoupled from the view-model's pane state so the same view can be used both
    /// as the always-visible rail and as the sliding flyout simultaneously.
    /// </summary>
    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<SidebarView, bool>(nameof(ShowLabels), defaultValue: true);

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public SidebarView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SidebarViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SidebarViewModel.SelectedPageType))
                    SyncListBoxSelection(vm.SelectedPageType);
            };
            // The startup page may already be set before this view subscribes, so apply it now.
            SyncListBoxSelection(vm.SelectedPageType);
        }
    }

    private void SyncListBoxSelection(PageType page)
    {
        // Selection lives in two ListBoxes (main + footer); only one may hold a selection at a time.
        _lastNavItemSelectionWasAuto = true;
        NavListBox.SelectedItem = page switch
        {
            PageType.Discover => DiscoverNavBtn,
            PageType.Updates => UpdatesNavBtn,
            PageType.Installed => InstalledNavBtn,
            PageType.Bundles => BundlesNavBtn,
            _ => null,
        };
        FooterNavListBox.SelectedItem = page switch
        {
            PageType.Settings => SettingsNavBtn,
            PageType.Managers => ManagersNavBtn,
            _ => null,
        };
        _lastNavItemSelectionWasAuto = false;
    }

    private void NavListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => HandleNavSelectionChanged(NavListBox.SelectedItem);

    private void FooterNavListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => HandleNavSelectionChanged(FooterNavListBox.SelectedItem);

    private void HandleNavSelectionChanged(object? selectedItem)
    {
        if (_lastNavItemSelectionWasAuto) return;
        if (selectedItem is not ListBoxItem item || item.Tag is not string tag) return;

        if (tag == "More")
        {
            // Not a page: re-sync selection so "More" doesn't stay highlighted, then open its menu.
            SyncListBoxSelection(ViewModel?.SelectedPageType ?? PageType.Null);
            FlyoutBase.ShowAttachedFlyout(item);
            return;
        }

        if (Enum.TryParse<PageType>(tag, out var pageType))
            ViewModel?.RequestNavigation(pageType.ToString());
    }

    public void FocusSelectedItem()
    {
        if ((NavListBox.SelectedItem ?? FooterNavListBox.SelectedItem) is InputElement item)
            item.Focus();
        else
            NavListBox.Focus();
    }
}
