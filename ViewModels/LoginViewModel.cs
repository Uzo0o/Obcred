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
    /// </summary>
    public Action<GoogleAuthResult>? LoginSucceeded { get; set; }

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

        LoginSucceeded?.Invoke(cached);
        return true;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        HasError = false;
        StatusMessage = "Opening your browser...";
        _loginCts = new CancellationTokenSource();

        try
        {
            var result = await _authService.LoginWithGoogleAsync(_loginCts.Token);
            StatusMessage = $"Welcome, {result.Name}!";
            LoginSucceeded?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Sign-in cancelled.";
            HasError = true;
        }
        catch (TimeoutException)
        {
            StatusMessage = "Sign-in timed out. Please try again.";
            HasError = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sign-in failed: {ex.Message}";
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
}