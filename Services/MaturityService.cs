using FinOps.Models;

namespace FinOps.Services;

public class MaturityService(
    IResourceTaggingService taggingService,
    IBudgetService budgetService,
    ICostAnalysisService costService,
    IAdvisorService advisorService,
    ILogger<MaturityService> logger) : IMaturityService
{
    public async Task<IReadOnlyList<SubscriptionMaturityScore>> ScoreSubscriptionsAsync(
        IEnumerable<TenantSubscription> subscriptions,
        IProgress<int>? progress = null)
    {
        var subList = subscriptions.ToList();
        var results = new List<SubscriptionMaturityScore>(subList.Count);
        var completed = 0;

        for (var i = 0; i < subList.Count; i++)
        {
            results.Add(await ScoreOneAsync(subList[i]));
            progress?.Report(++completed);
            if (i < subList.Count - 1)
                await Task.Delay(300);
        }

        return results;
    }

    private async Task<SubscriptionMaturityScore> ScoreOneAsync(TenantSubscription sub)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Metric 1: Tag coverage
        decimal tagScore = 100m;
        int totalRgs = 0, taggedRgs = 0;
        string tagDisplay = string.Empty;
        string? tagError = null;

        try
        {
            var rgs = await taggingService.GetResourceGroupsAsync(sub);
            totalRgs = rgs.Count;
            taggedRgs = rgs.Count(rg => rg.Tags.Count > 0);
            tagScore = totalRgs > 0 ? Math.Round((decimal)taggedRgs / totalRgs * 100m, 1) : 100m;
            tagDisplay = $"{taggedRgs} of {totalRgs} resource groups tagged";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get tag coverage for {Sub}", sub.SubscriptionId);
            tagError = ex.Message;
        }

        // Metric 2: Budget coverage
        decimal budgetScore = 0m;
        bool hasBudget = false;
        string budgetDisplay = string.Empty;
        string? budgetError = null;

        try
        {
            var budgets = await budgetService.GetBudgetsAsync(sub);
            hasBudget = budgets.Count > 0;
            budgetScore = hasBudget ? 100m : 0m;
            budgetDisplay = hasBudget ? "Budget exists" : "No budget";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get budget coverage for {Sub}", sub.SubscriptionId);
            budgetError = ex.Message;
        }

        // Metric 3: Waste score
        decimal wasteScore = 100m;
        decimal monthlyCost = 0m, monthlySavings = 0m, wastePercent = 0m;
        string wasteDisplay = string.Empty;
        string? wasteError = null;

        try
        {
            var costResult = await costService.GetCostsAsync(sub.SubscriptionId, sub.TenantId, monthStart, now);
            var annualSavings = await advisorService.GetPotentialCostSavingsAsync(new[] { sub });
            monthlyCost = costResult.TotalCost;
            monthlySavings = annualSavings / 12m;
            wastePercent = monthlyCost > 0
                ? Math.Min(monthlySavings / monthlyCost * 100m, 100m)
                : 0m;
            wasteScore = Math.Round(100m - wastePercent, 1);
            wasteDisplay = monthlySavings > 0
                ? $"Identified ${monthlySavings:F0}/mo in savings ({wastePercent:F1}% of spend)"
                : "No savings identified";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get waste score for {Sub}", sub.SubscriptionId);
            wasteError = ex.Message;
            wasteScore = 100m;
        }

        // Metric 4: Reservation coverage
        decimal reservationScore = 0m;
        string reservationDisplay = string.Empty;
        string? reservationError = null;

        try
        {
            var pricing = await costService.GetCostsByDimensionAsync(
                sub.SubscriptionId, sub.TenantId, monthStart, now, "PricingModel", 10);
            var total = pricing.Sum(x => x.Cost);
            var reserved = pricing.FirstOrDefault(x =>
                x.Name.Contains("Reservation", StringComparison.OrdinalIgnoreCase))?.Cost ?? 0m;
            reservationScore = total > 0 ? Math.Round(reserved / total * 100m, 1) : 0m;
            reservationDisplay = $"{reservationScore:F1}% of costs on reserved pricing";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get reservation coverage for {Sub}", sub.SubscriptionId);
            reservationError = ex.Message;
        }

        var improvements = BuildImprovements(tagScore, hasBudget, wastePercent, monthlySavings, reservationScore);

        return new SubscriptionMaturityScore
        {
            Subscription = sub,
            TaggedResourceGroupCount = taggedRgs,
            TotalResourceGroupCount = totalRgs,
            HasBudget = hasBudget,
            MonthlyCost = monthlyCost,
            MonthlySavings = monthlySavings,
            TagCoverageDisplay = tagDisplay,
            BudgetDisplay = budgetDisplay,
            WasteDisplay = wasteDisplay,
            ReservationDisplay = reservationDisplay,
            TagCoverageScore = tagScore,
            BudgetCoverageScore = budgetScore,
            WasteScore = wasteScore,
            ReservationCoverageScore = reservationScore,
            TagCoverageError = tagError,
            BudgetCoverageError = budgetError,
            WasteScoreError = wasteError,
            ReservationCoverageError = reservationError,
            Improvements = improvements
        };
    }

    private static IReadOnlyList<MaturityImprovement> BuildImprovements(
        decimal tagScore, bool hasBudget, decimal wastePercent, decimal monthlySavings, decimal reservationScore)
    {
        var improvements = new List<MaturityImprovement>();

        if (tagScore < 80)
        {
            improvements.Add(new MaturityImprovement
            {
                Category = "Tagging",
                Title = "Tag untagged resource groups",
                Description = $"Only {tagScore:F0}% tagged. Tags are essential for cost allocation.",
                Priority = tagScore < 40 ? ImprovementPriority.High : ImprovementPriority.Medium,
                ActionLink = "/tagging"
            });
        }

        if (!hasBudget)
        {
            improvements.Add(new MaturityImprovement
            {
                Category = "Budget",
                Title = "Create a subscription budget",
                Description = "No budget alert configured. Budgets provide cost guardrails.",
                Priority = ImprovementPriority.High,
                ActionLink = "/budgets/subscriptions"
            });
        }

        if (wastePercent > 20)
        {
            improvements.Add(new MaturityImprovement
            {
                Category = "Waste",
                Title = $"Address ${monthlySavings:F0}/mo in identified savings",
                Description = $"Advisor identified {wastePercent:F1}% of monthly spend as potential savings.",
                Priority = wastePercent > 40 ? ImprovementPriority.High : ImprovementPriority.Medium,
                ActionLink = "/advisor"
            });
        }

        if (reservationScore < 20)
        {
            improvements.Add(new MaturityImprovement
            {
                Category = "Reservations",
                Title = "Consider Reserved Instances",
                Description = $"Only {reservationScore:F1}% on reserved pricing. 1–3yr RIs save 30–70%.",
                Priority = ImprovementPriority.Low,
                ActionLink = null
            });
        }

        return improvements;
    }
}
