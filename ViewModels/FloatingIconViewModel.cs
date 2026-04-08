using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskLoggerApp.ViewModels;

/// <summary>
/// Drives the small always-on-top floating icon overlay window.
/// Holds the <see cref="ShowPromptCommand"/> passed in from <c>AppViewModel</c>
/// so the window's button can open the prompt without a direct View→ViewModel reference.
/// </summary>
public partial class FloatingIconViewModel : ViewModelBase
{
    /// <summary>Opens the log-task prompt window. Bound to the overlay button.</summary>
    public ICommand ShowPromptCommand { get; }

    public FloatingIconViewModel(ICommand showPromptCommand)
    {
        ShowPromptCommand = showPromptCommand;
    }
}
