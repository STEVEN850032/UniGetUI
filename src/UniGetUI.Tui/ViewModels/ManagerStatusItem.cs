namespace UniGetUI.Tui.ViewModels;

public sealed class ManagerStatusItem
{
    public ManagerStatusItem(string name, string state, string executable)
    {
        Name = name;
        State = state;
        Executable = executable;
    }

    public string Name { get; }
    public string State { get; }
    public string Executable { get; }
}
