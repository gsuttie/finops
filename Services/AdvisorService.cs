using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Advisor;
using Azure.ResourceManager.Resources;
using FinOps.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinOps.Services;

public class AdvisorService(
    TenantClientManager tenantClientManager,
    IHttpClientFactory httpClientFactory,
    ILogger<AdvisorService> logger) : IAdvisorService
{
    public async Task<IReadOnlyList<AdvisorRecommendation>> GetRecommendationsAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return [];

        var tenantGroups = subList.GroupBy(s => s.TenantId);

        var tenantTasks = tenantGroups.Select(group =>
            ProcessTenantAsync(group.Key, group.ToList()));
        var tenantResults = await Task.WhenAll(tenantTasks);

        return tenantResults.SelectMany(r => r).ToList();
    }

    public async Task<Dictionary<string, double>> GetAdvisorScoresAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return new Dictionary<string, double>();

        logger.LogInformation("Fetching Advisor Scores for {Count} subscriptions", subList.Count);

        var allScores = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        var tenantGroups = subList.GroupBy(s => s.TenantId);

        foreach (var group in tenantGroups)
        {
            var client = tenantClientManager.GetClientForTenant(group.Key);

            foreach (var sub in group)
            {
                try
                {
                    logger.LogInformation("Fetching scores for subscription {SubName} ({SubId})", sub.DisplayName, sub.SubscriptionId);
                    var scores = await GetAdvisorScoresViaRestApiAsync(client, sub.SubscriptionId);

                    logger.LogInformation("Retrieved {Count} category scores for {SubName}", scores.Count, sub.DisplayName);

                    foreach (var score in scores)
                    {
                        logger.LogInformation("  {Category}: {Score}%", score.Key, score.Value);

                        if (!allScores.ContainsKey(score.Key))
                        {
                            allScores[score.Key] = new List<double>();
                        }
                        allScores[score.Key].Add(score.Value);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to get scores for subscription {SubId}", sub.SubscriptionId);
                    continue;
                }
            }
        }

        // Average scores across subscriptions
        var result = new Dictionary<string, double>();
        foreach (var kvp in allScores)
        {
            result[kvp.Key] = Math.Round(kvp.Value.Average(), 0);
            logger.LogInformation("Final averaged score - {Category}: {Score}%", kvp.Key, result[kvp.Key]);
        }

        return result;
    }

    private async Task<Dictionary<string, double>> GetAdvisorScoresViaRestApiAsync(
        Azure.ResourceManager.ArmClient client, string subscriptionId)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        try
        {
            logger.LogInformation("Getting access token for subscription {SubId}", subscriptionId);

            // Get access token from the credential
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var credential = new AzureCliCredential();
            var token = await credential.GetTokenAsync(tokenRequestContext, default);

            logger.LogInformation("Token obtained, making REST API call");

            // Make REST API call to get Advisor Scores
            var httpClient = httpClientFactory.CreateClient();
            var requestUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Advisor/advisorScore?api-version=2023-01-01";

            logger.LogInformation("Request URL: {Url}", requestUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            var response = await httpClient.SendAsync(request);

            logger.LogInformation("Response status: {StatusCode}", response.StatusCode);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            logger.LogInformation("Response content length: {Length}", content.Length);
            logger.LogDebug("Response content: {Content}", content);

            var jsonDoc = JsonDocument.Parse(content);

            // Parse the response to extract scores
            if (jsonDoc.RootElement.TryGetProperty("value", out var valueArray))
            {
                logger.LogInformation("Found value array with {Count} items", valueArray.GetArrayLength());

                foreach (var item in valueArray.EnumerateArray())
                {
                    if (item.TryGetProperty("properties", out var properties))
                    {
                        string? categoryName = null;
                        double? scoreValue = null;

                        // Get category name
                        if (properties.TryGetProperty("category", out var category))
                        {
                            categoryName = category.GetString();
                            logger.LogInformation("Processing category: {Category}", categoryName);
                        }

                        // Get the score from timeSeries
                        if (properties.TryGetProperty("timeSeries", out var timeSeries) && timeSeries.ValueKind == JsonValueKind.Array)
                        {
                            var latestEntry = timeSeries.EnumerateArray().LastOrDefault();
                            // Check if latestEntry is valid (not default JsonElement)
                            if (latestEntry.ValueKind != JsonValueKind.Undefined &&
                                latestEntry.TryGetProperty("aggregatedColumns", out var columns) &&
                                columns.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var column in columns.EnumerateArray())
                                {
                                    if (column.TryGetProperty("name", out var colName) &&
                                        colName.GetString() == "Score" &&
                                        column.TryGetProperty("value", out var value))
                                    {
                                        scoreValue = value.GetDouble();
                                        logger.LogInformation("Found score value: {Score}", scoreValue);
                                        break;
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(categoryName) && scoreValue.HasValue)
                        {
                            scores[categoryName] = scoreValue.Value;
                            logger.LogInformation("Added score: {Category} = {Score}%", categoryName, scoreValue.Value);
                        }
                    }
                }
            }
            else
            {
                logger.LogWarning("No 'value' property found in response");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching Advisor Scores via REST API for subscription {SubId}", subscriptionId);
        }

        return scores;
    }

    private async Task<List<AdvisorRecommendation>> ProcessTenantAsync(
        string tenantId,
        List<TenantSubscription> subscriptions)
    {
        var client = tenantClientManager.GetClientForTenant(tenantId);

        var subTasks = subscriptions.Select(sub =>
            ProcessSubscriptionAsync(client, sub));
        var subResults = await Task.WhenAll(subTasks);

        return subResults.SelectMany(r => r).ToList();
    }

    private static async Task<List<AdvisorRecommendation>> ProcessSubscriptionAsync(
        Azure.ResourceManager.ArmClient client,
        TenantSubscription sub)
    {
        var results = new List<AdvisorRecommendation>();

        var scope = new ResourceIdentifier($"/subscriptions/{sub.SubscriptionId}");
        var collection = client.GetResourceRecommendationBases(scope);

        await foreach (var rec in collection.GetAllAsync())
        {
            var data = rec.Data;

            // Build detailed solution text from available fields
            var solutionParts = new List<string>();

            // Start with the solution from short description
            if (!string.IsNullOrEmpty(data.ShortDescription?.Solution))
            {
                solutionParts.Add(data.ShortDescription.Solution);
            }

            // Add the full description which contains detailed remediation steps
            if (!string.IsNullOrEmpty(data.Description) && data.Description != data.ShortDescription?.Problem)
            {
                solutionParts.Add(data.Description);
            }

            // Add label if it provides additional context
            if (!string.IsNullOrEmpty(data.Label) &&
                data.Label != data.ShortDescription?.Solution &&
                data.Label != data.ShortDescription?.Problem)
            {
                solutionParts.Add(data.Label);
            }

            // Check for potential benefits
            if (!string.IsNullOrEmpty(data.PotentialBenefits))
            {
                solutionParts.Add($"Benefits: {data.PotentialBenefits}");
            }

            // Check ExtendedProperties for additional remediation details
            if (data.ExtendedProperties != null && data.ExtendedProperties.Count > 0)
            {
                foreach (var prop in data.ExtendedProperties)
                {
                    var key = prop.Key;
                    var value = prop.Value?.ToString();

                    if (!string.IsNullOrEmpty(value) &&
                        (key.Contains("action", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("remediation", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("recommendation", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("step", StringComparison.OrdinalIgnoreCase)))
                    {
                        solutionParts.Add(value);
                    }
                }
            }

            // Combine all parts into a comprehensive solution
            var solution = solutionParts.Count > 0
                ? string.Join(" ", solutionParts.Distinct())
                : data.ShortDescription?.Solution ?? "No detailed solution available.";

            results.Add(new AdvisorRecommendation
            {
                RecommendationId = rec.Id.ToString(),
                Name = data.Name ?? "",
                Category = data.Category?.ToString() ?? "Unknown",
                Impact = data.Impact?.ToString() ?? "Unknown",
                Problem = data.ShortDescription?.Problem ?? "",
                Solution = solution,
                ImpactedField = data.ImpactedField ?? "",
                ImpactedValue = data.ImpactedValue ?? "",
                ResourceId = rec.Id.ToString(),
                LastUpdated = data.LastUpdated,
                SubscriptionName = sub.DisplayName,
                SubscriptionId = sub.SubscriptionId,
                TenantId = sub.TenantId
            });
        }

        return results;
    }

    public async Task<decimal> GetPotentialCostSavingsAsync(
        IEnumerable<TenantSubscription> subscriptions)
    {
        var subList = subscriptions.ToList();
        if (subList.Count == 0)
            return 0m;

        logger.LogInformation("Calculating potential cost savings for {Count} subscriptions", subList.Count);

        var tenantGroups = subList.GroupBy(s => s.TenantId);
        decimal totalSavings = 0m;

        foreach (var group in tenantGroups)
        {
            var client = tenantClientManager.GetClientForTenant(group.Key);

            foreach (var sub in group)
            {
                try
                {
                    var scope = new ResourceIdentifier($"/subscriptions/{sub.SubscriptionId}");
                    var collection = client.GetResourceRecommendationBases(scope);

                    await foreach (var rec in collection.GetAllAsync())
                    {
                        var data = rec.Data;

                        // Only process Cost category recommendations
                        if (data.Category?.ToString().Equals("Cost", StringComparison.OrdinalIgnoreCase) != true)
                            continue;

                        // Try to extract savings from ExtendedProperties
                        if (data.ExtendedProperties != null)
                        {
                            decimal savings = 0m;

                            // Try common property names for cost savings
                            var savingsKeys = new[]
                            {
                                "annualSavingsAmount",
                                "savingsAmount",
                                "monthlySavings",
                                "estimatedSavings",
                                "savings",
                                "annualSavings"
                            };

                            foreach (var key in savingsKeys)
                            {
                                if (data.ExtendedProperties.TryGetValue(key, out var value))
                                {
                                    var valueStr = value?.ToString();
                                    if (!string.IsNullOrEmpty(valueStr))
                                    {
                                        // Try to parse as decimal
                                        if (decimal.TryParse(valueStr, out var parsedValue))
                                        {
                                            savings = parsedValue;
                                            logger.LogInformation(
                                                "Found savings of {Savings:C2} for recommendation {Name} (key: {Key})",
                                                savings, data.Name, key);
                                            break;
                                        }
                                    }
                                }
                            }

                            // If this is an annual savings, convert to monthly
                            if (savings > 0 && data.ExtendedProperties.ContainsKey("annualSavingsAmount"))
                            {
                                savings = savings / 12m;
                                logger.LogInformation("Converted annual savings to monthly: {MonthlySavings:C2}", savings);
                            }

                            totalSavings += savings;
                        }
                    }

                    logger.LogInformation(
                        "Subscription {SubName} potential savings: {Savings:C2}",
                        sub.DisplayName, totalSavings);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to get cost savings for subscription {SubId}", sub.SubscriptionId);
                }
            }
        }

        logger.LogInformation("Total potential cost savings across all subscriptions: {TotalSavings:C2}", totalSavings);
        return totalSavings;
    }
}
