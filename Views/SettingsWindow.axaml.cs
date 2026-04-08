using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using TaskLoggerApp.ViewModels;

namespace TaskLoggerApp.Views;

public partial class SettingsWindow : Window
{
    // Parameterless constructor required by Avalonia's AXAML runtime loader.
    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Let the VM close the window without holding a direct View reference.
        viewModel.RequestClose += (_, _) => Close();

        // Provide the OS folder picker as a delegate so the VM stays View-free.
        viewModel.FolderPicker = PickFolderAsync;
    }

    private async Task<string?> PickFolderAsync()
    {
        // Bring the window to the front first.  On macOS a tray app's child
        // window may not have OS focus, which silently prevents the sheet from
        // appearing or returns empty results.
        Activate();

        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title         = "Choose report export folder",
                AllowMultiple = false,
            });

        if (folders.Count == 0)
            return null;

        // TryGetLocalPath() is the correct Avalonia 11 API for converting an
        // IStorageItem URI to a native filesystem path on all platforms.
        // Path.LocalPath can return a percent-encoded string on macOS.
        return folders[0].TryGetLocalPath();
    }
}
