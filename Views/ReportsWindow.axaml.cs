using Avalonia.Controls;
using TaskLoggerApp.ViewModels;

namespace TaskLoggerApp.Views;

public partial class ReportsWindow : Window
{
    // Parameterless constructor required by Avalonia's AXAML runtime loader.
    public ReportsWindow() => InitializeComponent();

    public ReportsWindow(ReportsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
    }
}
