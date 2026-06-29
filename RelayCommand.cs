using System.Windows.Input;

namespace TapeSplitterWpf;

public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _)    => execute();
}
