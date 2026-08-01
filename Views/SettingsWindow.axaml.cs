using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Obcred.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens a native file picker for a .pfx/.p12 certificate. Returns the chosen
    /// path, or null if cancelled. Used to wire SettingsViewModel.BrowseFileAction.
    /// </summary>
    public static async Task<string?> BrowsePfxAsync(Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select your .pfx certificate",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Certificate (*.pfx, *.p12)")
                {
                    Patterns = new[] { "*.pfx", "*.p12" }
                }
            }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
