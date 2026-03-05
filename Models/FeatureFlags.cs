namespace FinOps.Models;

public class FeatureFlags
{
    public bool Budgets { get; set; } = true;
    public bool CostManagement { get; set; } = true;
    public bool Tagging { get; set; } = true;
    public bool Policy { get; set; } = true;
    public bool Advisor { get; set; } = true;
    public bool OrphanedResources { get; set; } = true;
    public bool Security { get; set; } = true;
    public bool ServiceRetirements { get; set; } = true;
    public bool LoggingUsage { get; set; } = true;
    public bool PrivateEndpoints { get; set; } = true;
    public bool Rightsizing { get; set; } = true;
    public bool Maturity { get; set; } = true;
    public bool Carbon { get; set; } = true;
    public bool Themes { get; set; } = true;
    public bool CostQuestions { get; set; } = true;
}
