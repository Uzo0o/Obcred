using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Obcred.ViewModels;

namespace Obcred.Views;

public partial class InvoiceHistoryView : UserControl
{
    public InvoiceHistoryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is HistoryViewModel vm)
            vm.SavePdfFileAction = PromptSavePdfAsync;
    }

    private async Task<string?> PromptSavePdfAsync(string defaultFileName)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
            return null;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save invoice PDF",
            SuggestedFileName = defaultFileName,
            DefaultExtension = "pdf",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } }
            }
        });

        return file?.Path.LocalPath;
    }
}
