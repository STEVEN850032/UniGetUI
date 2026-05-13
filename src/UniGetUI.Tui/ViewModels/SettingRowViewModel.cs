using UniGetUI.Core.SettingsEngine;

namespace UniGetUI.Tui.ViewModels;

public sealed class SettingRowViewModel : ViewModelBase
{
    private bool _isEnabled;
    private string _value;

    public SettingRowViewModel(Settings.K key, string title, string description, bool isBoolean)
    {
        Key = key;
        Title = title;
        Description = description;
        IsBoolean = isBoolean;
        _isEnabled = Settings.Get(key);
        _value = Settings.GetValue(key);
    }

    public Settings.K Key { get; }
    public string Title { get; }
    public string Description { get; }
    public bool IsBoolean { get; }
    public bool IsStringValue => !IsBoolean;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
                Settings.Set(Key, value);
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
                Settings.SetValue(Key, value);
        }
    }
}
