using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Obcred.Models;
using Obcred.Services;

namespace Obcred.ViewModels;

public partial class PlanPickerViewModel : ViewModelBase
{
    private readonly IUsageService _usageService;
    private int _currentPlanRank;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _currentPlanLabel = string.Empty;

    // The plan the person has tapped but not yet confirmed. Non-null shows the
    // "are you sure" confirmation panel (this IS confirmation #2 — the switch
    // itself doesn't happen until Confirm is pressed).
    [ObservableProperty] private PlanInfo? _pendingPlan;

    public List<PlanOptionViewModel> Plans { get; } = new();

    /// <summary>Set by the caller. Invoked after a plan switch actually succeeds.</summary>
    public Action<PlanSelectResult>? PlanChanged { get; set; }

    public PlanPickerViewModel(IUsageService usageService)
    {
        _usageService = usageService;
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        HasError = false;
        StatusMessage = string.Empty;
        PendingPlan = null;

        var status = await _usageService.GetStatusAsync();
        var plans = await _usageService.GetPlansAsync();

        if (plans is null)
        {
            HasError = true;
            StatusMessage = "Couldn't reach the server to load plans. Check your connection and try again.";
            IsBusy = false;
            return;
        }

        _currentPlanRank = plans.FirstOrDefault(p => p.Id == status?.Plan)?.Rank ?? 0;
        CurrentPlanLabel = status is null ? "Free plan" : $"Currently on: {status.Plan[0].ToString().ToUpper()}{status.Plan[1..]}";

        Plans.Clear();
        foreach (var plan in plans.OrderBy(p => p.Rank))
        {
            Plans.Add(new PlanOptionViewModel(plan, isCurrent: plan.Id == status?.Plan,
                isDowngrade: plan.Rank < _currentPlanRank));
        }
        OnPropertyChanged(nameof(Plans));

        IsBusy = false;
    }

    [RelayCommand]
    private void SelectPlan(PlanOptionViewModel option)
    {
        if (option.IsCurrent || option.IsDowngrade)
            return; // downgrades aren't offered as a live option — see IsDowngrade below

        HasError = false;
        PendingPlan = option.Plan;
    }

    [RelayCommand]
    private void CancelPending()
    {
        PendingPlan = null;
    }

    [RelayCommand]
    private async Task ConfirmPendingAsync()
    {
        if (PendingPlan is null) return;

        IsBusy = true;
        HasError = false;

        var result = await _usageService.SelectPlanAsync(PendingPlan.Id);

        if (!result.Success)
        {
            HasError = true;
            StatusMessage = result.ErrorMessage ?? "Something went wrong — please try again.";
            IsBusy = false;
            // Refresh so the UI reflects reality (e.g. a downgrade race with another device).
            await LoadAsync();
            return;
        }

        PendingPlan = null;
        IsBusy = false;
        PlanChanged?.Invoke(result);
        await LoadAsync();
    }
}

/// <summary>Wraps a PlanInfo with selection state for display in the picker list.</summary>
public partial class PlanOptionViewModel : ObservableObject
{
    public PlanInfo Plan { get; }
    public bool IsCurrent { get; }
    public bool IsDowngrade { get; }

    public string DisplayName => Plan.DisplayName;
    public string LimitLabel => Plan.LimitLabel;
    public string PriceLabel => Plan.PriceLabel;

    public PlanOptionViewModel(PlanInfo plan, bool isCurrent, bool isDowngrade)
    {
        Plan = plan;
        IsCurrent = isCurrent;
        IsDowngrade = isDowngrade;
    }
}