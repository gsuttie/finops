using FinOps.Components;
using FinOps.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddHttpClient();

builder.Services.AddScoped<TenantClientManager>();
builder.Services.AddScoped<IAzureSubscriptionService, AzureSubscriptionService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<ICostAnalysisService, CostAnalysisService>();
builder.Services.AddScoped<IResourceTaggingService, ResourceTaggingService>();
builder.Services.AddScoped<IPolicyService, PolicyService>();
builder.Services.AddScoped<ITenantConnectionService, TenantConnectionService>();
builder.Services.AddScoped<IOrphanedResourceService, OrphanedResourceService>();
builder.Services.AddScoped<IAdvisorService, AdvisorService>();
builder.Services.AddScoped<ILogAnalyticsService, LogAnalyticsService>();
builder.Services.AddScoped<ISecurityRecommendationService, SecurityRecommendationService>();
builder.Services.AddScoped<IServiceRetirementService, ServiceRetirementService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
