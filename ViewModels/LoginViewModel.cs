using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Obcred.Models;
using Obcred.Services;

namespace Obcred.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IGoogleAuthService _authService;
    private CancellationTokenSource? _loginCts;

    /// <summary>
    /// Set by App.axaml.cs. Invoked once sign-in succeeds (either via the button,
    /// or silently via TryAutoLoginAsync) so the app can move on to the next window.
    /// Async because it has to switch the signed-in account's local database/settings
    /// over before the next window is shown.
    /// </summary>
    public Func<GoogleAuthResult, Task>? LoginSucceeded { get; set; }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasError;

    public LoginViewModel(IGoogleAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Call once on startup, before showing the window, to skip straight past
    /// the login screen if a valid cached session already exists.
    /// </summary>
    public async Task<bool> TryAutoLoginAsync()
    {
        var cached = await _authService.TryRestoreSessionAsync();
        if (cached is null)
            return false;

        if (LoginSucceeded is not null)
            await LoginSucceeded(cached);
        return true;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        HasError = false;
        StatusMessage = "Се отвора прелистувачот...";
        _loginCts = new CancellationTokenSource();

        try
        {
            var result = await _authService.LoginWithGoogleAsync(_loginCts.Token);
            StatusMessage = $"Добредојдовте, {result.Name}!";
            if (LoginSucceeded is not null)
                await LoginSucceeded(result);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Најавата е откажана.";
            HasError = true;
        }
        catch (TimeoutException)
        {
            StatusMessage = "Времето за најава истече. Обидете се повторно.";
            HasError = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Најавата не успеа: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelLogin()
    {
        _loginCts?.Cancel();
    }

    /// <summary>Called by App.axaml.cs before re-showing this screen after a logout.</summary>
    public void Reset()
    {
        StatusMessage = string.Empty;
        HasError = false;
    }
}