using FinOps.Components;
using FinOps.Data;
using FinOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<IPrivateEndpointService, PrivateEndpointService>();
builder.Services.AddScoped<IRightsizingService, RightsizingService>();
builder.Services.AddScoped<IMaturityService, MaturityService>();
builder.Services.AddScoped<ICarbonService, CarbonService>();
builder.Services.AddScoped<IUpsellService, UpsellService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
builder.Services.AddSingleton<IThemeService, ThemeService>();
builder.Services.AddSingleton<ICopilotService, CopilotService>();

// Identity + EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddRazorPages();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

// Ensure database and Identity tables exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapRazorPages();

app.Run();
