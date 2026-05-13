using UniGetUI.Shared;

namespace UniGetUI.Tui.Infrastructure;

internal static class TuiCliHandler
{
    public static int? HandlePreUiArgs(string[] args)
    {
        return SharedPreUiCommandDispatcher.TryHandle(
            args,
            SharedPreUiCommandDispatcher.AvaloniaExitCodes
        );
    }
}
