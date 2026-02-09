namespace FinOps.Models;

public class BudgetAlertThreshold
{
    public decimal? Percentage { get; set; }
    public string ThresholdType { get; set; } = "Actual";
}
