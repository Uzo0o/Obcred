using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Obcred.Models;
using Obcred.Services;
using Obcred.ViewModels;

namespace Obcred.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Start on the invoice entry screen. It inherits the window's DataContext
        // (the shared InvoiceViewModel set up by the DI container).
        PageHost.Content = new InvoiceEntryView();

        // DataContext is assigned by App.axaml.cs AFTER this constructor runs,
        // so we hook the callback once it actually arrives rather than here.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is InvoiceViewModel invoiceViewModel)
                invoiceViewModel.OverageWarningRequested = status => _ = ShowOverageWarningAsync(status);
        };
    }

    private async Task ShowOverageWarningAsync(UsageStatus status)
    {
        string planDisplayName = char.ToUpper(status.Plan[0]) + status.Plan[1..];
        var warningWindow = new OverageWarningWindow(planDisplayName, status.Limit ?? 0, status.OveragePerInvoice);
        var result = await warningWindow.ShowDialog<OverageWarningResult?>(this);

        var usageService = App.AppHost!.Services.GetRequiredService<IUsageService>();

        if (result == OverageWarningResult.StayOnPlan)
        {
            usageService.AcknowledgeOverageThisMonth();
        }
        else if (result == OverageWarningResult.ChoosePlan)
        {
            await OpenPlanPickerAsync();
        }
        // A closed-without-choosing window (null) intentionally leaves nothing
        // acknowledged — the prompt will simply show again on the next submit.
    }

    private async Task OpenPlanPickerAsync()
    {
        var planPickerViewModel = App.AppHost!.Services.GetRequiredService<PlanPickerViewModel>();
        planPickerViewModel.PlanChanged = result =>
        {
            // Reflect the new plan in the sidebar badge immediately.
            _ = (DataContext as InvoiceViewModel)?.RefreshUsageAsync();
        };

        var window = new PlanPickerWindow { DataContext = planPickerViewModel };
        await planPickerViewModel.LoadAsync();
        await window.ShowDialog(this);
    }

    private async void UsageBadge_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        await OpenPlanPickerAsync();
    }

    // The window is frameless (SystemDecorations=None), so we drag it via the top bar.
    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void NavNewInvoice_Click(object? sender, RoutedEventArgs e)
    {
        PageHost.Content = new InvoiceEntryView();
    }

    private async void NavHistory_Click(object? sender, RoutedEventArgs e)
    {
        var historyViewModel = App.AppHost!.Services.GetRequiredService<HistoryViewModel>();
        await historyViewModel.LoadAsync();
        PageHost.Content = new InvoiceHistoryView { DataContext = historyViewModel };
    }

    private async void NavClientList_Click(object? sender, RoutedEventArgs e)
    {
        var clientsViewModel = App.AppHost!.Services.GetRequiredService<ClientsViewModel>();
        await clientsViewModel.LoadAsync();
        PageHost.Content = new ClientsView { DataContext = clientsViewModel };
    }

    private async void NavReceivedInvoices_Click(object? sender, RoutedEventArgs e)
    {
        var purchaseInvoicesViewModel = App.AppHost!.Services.GetRequiredService<PurchaseInvoicesViewModel>();
        await purchaseInvoicesViewModel.LoadFromCacheAsync();
        PageHost.Content = new PurchaseInvoicesView { DataContext = purchaseInvoicesViewModel };
    }

    private void NavPdfTemplate_Click(object? sender, RoutedEventArgs e)
    {
        var pdfSettingsViewModel = App.AppHost!.Services.GetRequiredService<PdfSettingsViewModel>();
        PageHost.Content = new PdfSettingsView { DataContext = pdfSettingsViewModel };
    }

    private async void OpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        var services = App.AppHost!.Services;
        var settingsViewModel = new SettingsViewModel(
            services.GetRequiredService<IUserSettingsService>(),
            services.GetRequiredService<IUjpService>(),
            services.GetRequiredService<IUsageService>());

        var window = new SettingsWindow { DataContext = settingsViewModel };
        settingsViewModel.CloseAction = () => window.Close();
        settingsViewModel.BrowseFileAction = () => SettingsWindow.BrowsePfxAsync(window);

        await window.ShowDialog(this);

        // Reflect any environment change in the banner without needing a restart.
        (DataContext as InvoiceViewModel)?.RefreshEnvironment();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}