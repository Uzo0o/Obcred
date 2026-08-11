using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Obcred.ViewModels;

namespace Obcred.Views;

public partial class PdfSettingsView : UserControl
{
    public PdfSettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is PdfSettingsViewModel vm)
            vm.BrowseLogoFileAction = PromptForLogoAsync;
    }

    private async Task<string?> PromptForLogoAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
            return null;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a company logo",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } }
            }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}