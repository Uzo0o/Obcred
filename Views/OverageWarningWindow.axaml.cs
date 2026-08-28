using Avalonia.Controls;

namespace Obcred.Views;

public enum OverageWarningResult
{
    StayOnPlan,
    ChoosePlan
}

public partial class OverageWarningWindow : Window
{
    // Parameterless constructor kept for the XAML previewer/loader; real usage
    // should go through the constructor below.
    public OverageWarningWindow() : this("Free", 5, 14)
    {
    }

    public OverageWarningWindow(string planDisplayName, int limit, int overagePerInvoice)
    {
        InitializeComponent();

        TitleText.Text = $"You've gone over your {planDisplayName} plan";
        IntroText.Text = $"You're about to go over your {planDisplayName} plan's {limit} invoices this month.";
        CostText.Text = $"Every invoice after that will cost {overagePerInvoice} MKD, added to your bill automatically at the end of the month.";
        ChoiceText.Text = $"You can keep going on {planDisplayName} and pay per invoice, or switch to a plan with a higher included limit.";
        StayOnPlanButton.Content = $"Yes, bill me {overagePerInvoice} MKD per invoice";

        StayOnPlanButton.Click += (_, _) => Close(OverageWarningResult.StayOnPlan);
        ChoosePlanButton.Click += (_, _) => Close(OverageWarningResult.ChoosePlan);
    }
}