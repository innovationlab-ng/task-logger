using Avalonia.Controls;
using TaskLoggerApp.ViewModels;

namespace TaskLoggerApp.Views;

public partial class StartMissionWindow : Window
{
    public StartMissionWindow() => InitializeComponent();

    public StartMissionWindow(StartMissionViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose += (_, _) => Close();
    }
}
