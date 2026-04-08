using Avalonia.Controls;
using TaskLoggerApp.ViewModels;

namespace TaskLoggerApp.Views;

public partial class FridayReportDialog : Window
{
    // Parameterless constructor required by Avalonia's AXAML runtime loader.
    public FridayReportDialog() => InitializeComponent();

    public FridayReportDialog(FridayReportDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
    }
}
