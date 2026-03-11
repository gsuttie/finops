namespace FinOps.Models;

public class ReservationAnalysis
{
    public string SubscriptionId { get; set; } = "";
    public string SubscriptionName { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public decimal TotalMonthlyPayGoCost { get; set; }
    public decimal CurrentReservationCoveragePercent { get; set; }
    public decimal TotalPotentialAnnualSavingsOneYear { get; set; }
    public decimal TotalPotentialAnnualSavingsThreeYear { get; set; }
    public IReadOnlyList<ReservationCandidate> Candidates { get; set; } = [];
    public IReadOnlyList<CostBreakdownItem> AllServiceItems { get; set; } = [];
}
