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

        TitleText.Text = $"Го надминавте вашиот {planDisplayName} план";
        IntroText.Text = $"На пат сте да ги надминете {limit} фактури овој месец, вклучени во вашиот {planDisplayName} план.";
        CostText.Text = $"Секоја наредна фактура ќе чини {overagePerInvoice} МКД, автоматски додадени на вашата сметка на крајот на месецот.";
        ChoiceText.Text = $"Можете да продолжите на {planDisplayName} и да плаќате по фактура, или да преминете на план со повисок вклучен лимит.";
        StayOnPlanButton.Content = $"Да, наплатете ми {overagePerInvoice} МКД по фактура";

        StayOnPlanButton.Click += (_, _) => Close(OverageWarningResult.StayOnPlan);
        ChoosePlanButton.Click += (_, _) => Close(OverageWarningResult.ChoosePlan);
    }
}