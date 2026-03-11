namespace FinOps.Models;

public class SpendAnomaly
{
    public DateTime Date { get; set; }
    public decimal ActualCost { get; set; }
    public decimal ExpectedCost { get; set; }
    public decimal StandardDeviations { get; set; }
    public string Severity { get; set; } = "";  // "High" (>3σ), "Medium" (>2σ), "Low" (>1.5σ)
    public string Currency { get; set; } = "USD";
}
