using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
                    services.AddHttpClient<IUjpService, UjpService>();
                    services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
                    services.AddHttpClient<IUsageService, UsageService>();
                    services.AddSingleton<ISessionContext, SessionContext>();
                    services.AddSingleton<IUserSettingsService, UserSettingsService>();
                    services.AddSingleton<IDatabaseService, DatabaseService>();
                    services.AddSingleton<IInvoicePdfService, InvoicePdfService>();

                    // NEW: login window/viewmodel, shown before Settings/MainWindow.
                    services.AddSingleton<LoginWindow>();
                    services.AddSingleton<LoginViewModel>();
                    services.AddSingleton<PlanPickerViewModel>();

                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<InvoiceViewModel>();
                    services.AddSingleton<HistoryViewModel>();
                    services.AddSingleton<ClientsViewModel>();
                    services.AddSingleton<PurchaseInvoicesViewModel>();
                    services.AddSingleton<PdfSettingsViewModel>();

                    // SettingsWindow/SettingsViewModel are deliberately NOT registered
                    // here: both are constructed fresh with `new` every time they're
                    // needed (see ShowSettingsOrMainWindow and MainWindow's
                    // OpenSettings_Click) so a stale account's cert/EDB never lingers
                    // in a cached singleton after a logout -> different-account login.
                })
                .Build();

            await AppHost.StartAsync();

            var sessionContext = AppHost.Services.GetRequiredService<ISessionContext>();
            var databaseService = AppHost.Services.GetRequiredService<IDatabaseService>();
            var settingsService = AppHost.Services.GetRequiredService<IUserSettingsService>();

            // Don't close the app just because a window closed, until we've decided
            // what the real "main" window is going to be.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            // Extracted from your original logic unchanged — decides whether the
            // signed-in account still needs the first-run Settings screen or can go
            // straight to the invoicing UI. Now called *after* a successful login
            // (and after that account's local data has been switched in) instead of
            // being the very first thing shown.
            async Task ShowSettingsOrMainWindow()
            {
                if (settingsService.IsConfigured())
                {
                    var invoiceVm = AppHost.Services.GetRequiredService<InvoiceViewModel>();
                    invoiceVm.LogoutRequested = DoLogout;
                    await invoiceVm.RefreshForUserAsync();

                    var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                    mainWindow.DataContext = invoiceVm;
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();

                    desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                }
                else
                {
                    // Fresh instances every time — never a cached singleton — so a
                    // different account never sees the previous one's cert/EDB here.
                    var settingsVm = new SettingsViewModel(
                        settingsService,
                        AppHost.Services.GetRequiredService<IUjpService>(),
                        AppHost.Services.GetRequiredService<IUsageService>());
                    var settingsWindow = new SettingsWindow { DataContext = settingsVm };
                    settingsVm.CloseAction = () => settingsWindow.Close();
                    settingsVm.BrowseFileAction = () => SettingsWindow.BrowsePfxAsync(settingsWindow);

                    desktop.MainWindow = settingsWindow;
                    settingsWindow.Show();

                    settingsWindow.Closed += async (s, args) =>
                    {
                        if (settingsService.IsConfigured())
                        {
                            var invoiceVm = AppHost.Services.GetRequiredService<InvoiceViewModel>();
                            invoiceVm.LogoutRequested = DoLogout;
                            await invoiceVm.RefreshForUserAsync();

                            var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                            mainWindow.DataContext = invoiceVm;

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

            bool loginWindowShown = false;

            // Signs the current account out (locally — the cached session file is
            // deleted, and the in-memory session/database/settings are dropped) and
            // returns to the login screen so a different account can sign in and get
            // its own invoices/clients/plan. Reachable from the sidebar's "Одјава"
            // button via InvoiceViewModel.LogoutRequested.
            void DoLogout()
            {
                var googleAuth = AppHost.Services.GetRequiredService<IGoogleAuthService>();
                googleAuth.Logout();
                sessionContext.Clear();

                // Hide rather than Close: both MainWindow and LoginWindow are
                // long-lived singletons meant to be shown again, and closing the
                // window Avalonia currently treats as "the" MainWindow under
                // OnMainWindowClose would shut the whole app down.
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
                desktop.MainWindow?.Hide();

                loginVm.Reset();
                loginWindowShown = true;
                desktop.MainWindow = loginWindow;
                loginWindow.Show();
            }

            // Fires for BOTH paths below — a silently restored cached session,
            // and a fresh interactive login — so the session only needs to be
            // captured in one place. Also fires again after a logout, once the
            // next account (or the same one) signs back in.
            loginVm.LoginSucceeded = async session =>
            {
                sessionContext.SetCurrent(session);

                // Falls back to Email in the unlikely case the Worker ever omits Id —
                // either way, this is the key that keeps one account's local data
                // (invoices, clients, cert/EDB) from leaking into another's.
                string userKey = !string.IsNullOrWhiteSpace(session.Id) ? session.Id : session.Email;

                // Point local storage at THIS account's own data before anything
                // reads from it — first login ever inherits the old single-account
                // data as a starting point; every account after that starts empty.
                await databaseService.SwitchUserAsync(userKey);
                settingsService.SwitchUser(userKey);

                if (loginWindowShown)
                    loginWindow.Hide();

                await ShowSettingsOrMainWindow();
            };

            // If a valid session is already cached (DPAPI-encrypted on disk),
            // this fires LoginSucceeded synchronously and skips the login screen.
            bool restoredExistingSession = await loginVm.TryAutoLoginAsync();

            if (!restoredExistingSession)
            {
                loginWindowShown = true;
                desktop.MainWindow = loginWindow;
                loginWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
