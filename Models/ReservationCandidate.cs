namespace FinOps.Models;

public class ReservationCandidate
{
    public string ServiceName { get; set; } = "";
    public decimal MonthlyCost { get; set; }
    public decimal AnnualizedCost { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal OneYearSavingPercent { get; set; }
    public decimal ThreeYearSavingPercent { get; set; }
    public decimal EstimatedMonthlySavingsOneYear { get; set; }
    public decimal EstimatedMonthlySavingsThreeYear { get; set; }
    public decimal EstimatedAnnualSavingsOneYear { get; set; }
    public decimal EstimatedAnnualSavingsThreeYear { get; set; }
    public string Recommendation { get; set; } = ""; // "Buy 3-Year" | "Buy 1-Year" | "Evaluate"
}
