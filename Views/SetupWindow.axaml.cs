using Avalonia.Controls;
using TaskLoggerApp.ViewModels;

namespace TaskLoggerApp.Views;

public partial class SetupWindow : Window
{
    // Parameterless constructor required by Avalonia's AXAML runtime loader.
    public SetupWindow() => InitializeComponent();

    public SetupWindow(SetupViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Let the VM request its own window to close without needing a
        // direct reference to the View.
        viewModel.RequestClose += (_, _) => Close();
    }
}
