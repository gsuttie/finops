namespace FinOps.Models;

public enum ImprovementPriority { High, Medium, Low }

public class MaturityImprovement
{
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required ImprovementPriority Priority { get; init; }
    public string? ActionLink { get; init; }
}

public class SubscriptionMaturityScore
{
    public required TenantSubscription Subscription { get; init; }

    // Raw data (for display)
    public int TaggedResourceGroupCount { get; init; }
    public int TotalResourceGroupCount { get; init; }
    public bool HasBudget { get; init; }
    public decimal MonthlyCost { get; init; }
    public decimal MonthlySavings { get; init; }

    // Pre-computed display strings
    public string TagCoverageDisplay { get; init; } = string.Empty;
    public string BudgetDisplay { get; init; } = string.Empty;
    public string WasteDisplay { get; init; } = string.Empty;
    public string ReservationDisplay { get; init; } = string.Empty;

    // Normalised 0–100 scores
    public decimal TagCoverageScore { get; init; }
    public decimal BudgetCoverageScore { get; init; }
    public decimal WasteScore { get; init; }
    public decimal ReservationCoverageScore { get; init; }

    // Computed
    public decimal OverallScore =>
        (TagCoverageScore + BudgetCoverageScore + WasteScore + ReservationCoverageScore) / 4m;

    public decimal IdleWastePercent => 100m - WasteScore;

    public string Tier => OverallScore switch
    {
        >= 80 => "Excellent",
        >= 60 => "Good",
        >= 40 => "Fair",
        _ => "Poor"
    };

    // Per-metric error strings (null = success)
    public string? TagCoverageError { get; init; }
    public string? BudgetCoverageError { get; init; }
    public string? WasteScoreError { get; init; }
    public string? ReservationCoverageError { get; init; }

    public required IReadOnlyList<MaturityImprovement> Improvements { get; init; }
}
