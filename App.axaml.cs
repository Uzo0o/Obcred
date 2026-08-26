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
                    services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
                    services.AddSingleton<IUserSettingsService, UserSettingsService>();
                    services.AddSingleton<IDatabaseService, DatabaseService>();
                    services.AddSingleton<IInvoicePdfService, InvoicePdfService>();

                    // NEW: login window/viewmodel, shown before Settings/MainWindow.
                    services.AddSingleton<LoginWindow>();
                    services.AddSingleton<LoginViewModel>();

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

            // Don't close the app just because a window closed, until we've decided
            // what the real "main" window is going to be.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            // Extracted from your original logic unchanged — decides whether the
            // user still needs the first-run Settings screen or can go straight
            // to the invoicing UI. Now called *after* a successful login instead
            // of being the very first thing shown.
            void ShowSettingsOrMainWindow()
            {
                if (settingsService.IsConfigured())
                {
                    var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                    mainWindow.DataContext = AppHost.Services.GetRequiredService<InvoiceViewModel>();
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();

                    desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                }
                else
                {
                    var settingsWindow = AppHost.Services.GetRequiredService<SettingsWindow>();

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

                            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                        }
                        else
                        {
                            desktop.Shutdown(); // They exited without finishing setup
                        }
                    };
                }
            }

            // NEW: gate everything behind Google sign-in.
            var loginWindow = AppHost.Services.GetRequiredService<LoginWindow>();
            var loginVm = AppHost.Services.GetRequiredService<LoginViewModel>();
            loginWindow.DataContext = loginVm;

            // If a valid session is already cached (DPAPI-encrypted on disk),
            // skip the login screen entirely.
            bool restoredExistingSession = await loginVm.TryAutoLoginAsync();

            if (restoredExistingSession)
            {
                ShowSettingsOrMainWindow();
            }
            else
            {
                loginVm.LoginSucceeded = _ =>
                {
                    loginWindow.Close();
                    ShowSettingsOrMainWindow();
                };

                desktop.MainWindow = loginWindow;
                loginWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}