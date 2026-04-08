using Avalonia.Controls;
using Avalonia.Input;
using TaskLoggerApp.ViewModels;

namespace TaskLoggerApp.Views;

/// <summary>
/// A small always-on-top overlay that floats above all windows.
/// Left area: drag grip — clicking and dragging repositions the window.
/// Right area: "📋 Log" button — opens the task-prompt window.
/// </summary>
public partial class FloatingIconWindow : Window
{
    public FloatingIconWindow() => InitializeComponent();

    public FloatingIconWindow(FloatingIconViewModel vm) : this()
    {
        DataContext = vm;
    }

    /// <summary>
    /// Called when the user presses on the drag-grip area (left border).
    /// <see cref="BeginMoveDrag"/> hands control to the OS to move the window.
    /// </summary>
    private void OnDragPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }
}
