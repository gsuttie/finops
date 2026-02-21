using MudBlazor;

namespace FinOps.Services;

public enum AppTheme
{
    AzureBlue,
    MissionControl,
    DarkFinance
}

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    bool DefaultIsDarkMode { get; }
    MudTheme GetMudTheme();
    event Action? OnThemeChanged;
    Task SetThemeAsync(AppTheme theme);
}
