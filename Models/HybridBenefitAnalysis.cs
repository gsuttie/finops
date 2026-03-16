namespace FinOps.Models;

public class HybridBenefitAnalysis
{
    public int TotalWindowsVms { get; set; }
    public int EnabledCount { get; set; }
    public int NotEnabledCount => TotalWindowsVms - EnabledCount;
    public decimal CoveragePercent { get; set; }
    public IReadOnlyList<VmHybridBenefitInfo> Vms { get; set; } = [];
}
