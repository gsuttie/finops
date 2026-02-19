namespace FinOps.Models;

public class CarbonEstimate
{
    public string ResourceName { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public string ResourceGroup { get; set; } = "";
    public string Location { get; set; } = "";
    public string SubscriptionId { get; set; } = "";
    public string SubscriptionName { get; set; } = "";
    public string SkuName { get; set; } = "";
    public double CarbonIntensityGCo2PerKwh { get; set; }
    public double EstimatedWatts { get; set; }
    public double MonthlyKgCo2e { get; set; }
    public string RegionDisplayName { get; set; } = "";
}

public class RegionCarbonSummary
{
    public string Location { get; set; } = "";
    public string RegionDisplayName { get; set; } = "";
    public double CarbonIntensityGCo2PerKwh { get; set; }
    public decimal MonthlyCost { get; set; }
    public string Currency { get; set; } = "USD";
    public double MonthlyKgCo2e { get; set; }
    public int ResourceCount { get; set; }
}
