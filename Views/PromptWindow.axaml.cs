using Avalonia.Controls;
using TaskLoggerApp.ViewModels;

namespace TaskLoggerApp.Views;

public partial class PromptWindow : Window
{
    // Parameterless constructor required by Avalonia's AXAML runtime loader.
    public PromptWindow() => InitializeComponent();

    public PromptWindow(PromptViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
    }
}
