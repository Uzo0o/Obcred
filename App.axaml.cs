using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obcred.Services;
using Obcred.ViewModels;
using Obcred.Views;

namespace Obcred;

public partial class App : Application
{
    // Restored the Host property from your WPF configuration
    public static IHost? AppHost { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Rebuild the Dependency Injection container from the WPF project
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, builder) => 
                {
                    builder.AddJsonFile("appsettings.json", optional: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<SettingsWindow>();
                    services.AddSingleton<SettingsViewModel>();
                    
                    services.AddHttpClient<IUjpService, UjpService>();
                    services.AddSingleton<IUserSettingsService, UserSettingsService>();
                    services.AddSingleton<IDatabaseService, DatabaseService>();
                    services.AddSingleton<IInvoicePdfService, InvoicePdfService>();
                    
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<InvoiceViewModel>();
                    services.AddSingleton<HistoryViewModel>();
                    services.AddSingleton<ClientsViewModel>();
                    services.AddSingleton<PurchaseInvoicesViewModel>();
                    services.AddSingleton<PdfSettingsViewModel>();
                })
                .Build();

            await AppHost.StartAsync();

            var settingsService = AppHost.Services.GetRequiredService<IUserSettingsService>();

            // 1. Tell Avalonia: "Do not close the app just because a window closed!"
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            if (settingsService.IsConfigured())
            {
                var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                
                // Inject the ViewModel directly into the Window's DataContext
                mainWindow.DataContext = AppHost.Services.GetRequiredService<InvoiceViewModel>();
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                
                // 2. Now that the main window is up, return to normal behavior
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose; 
            }
            else
            {
                var settingsWindow = AppHost.Services.GetRequiredService<SettingsWindow>();

                // Inject the ViewModel directly into the Settings Window
                var settingsVm = AppHost.Services.GetRequiredService<SettingsViewModel>();
                settingsWindow.DataContext = settingsVm;
                settingsVm.CloseAction = () => settingsWindow.Close();
                settingsVm.BrowseFileAction = () => SettingsWindow.BrowsePfxAsync(settingsWindow);

                desktop.MainWindow = settingsWindow;
                settingsWindow.Show();
            
                settingsWindow.Closed += (s, args) => 
                {
                    if (settingsService.IsConfigured())
                    {
                        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                        mainWindow.DataContext = AppHost.Services.GetRequiredService<InvoiceViewModel>();
                        
                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();
                        
                        // Return to normal behavior
                        desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                    }
                    else
                    {
                        desktop.Shutdown(); // They exited without finishing setup
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}