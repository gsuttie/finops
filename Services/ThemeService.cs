using System.Text.Json;
using MudBlazor;

namespace FinOps.Services;

public class ThemeService : IThemeService
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "theme.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppTheme CurrentTheme { get; private set; }
    public event Action? OnThemeChanged;

    public bool DefaultIsDarkMode => CurrentTheme is AppTheme.MissionControl or AppTheme.DarkFinance;

    public ThemeService()
    {
        CurrentTheme = Load();
    }

    public async Task SetThemeAsync(AppTheme theme)
    {
        CurrentTheme = theme;
        var json = JsonSerializer.Serialize(theme.ToString(), JsonOptions);
        await File.WriteAllTextAsync(FilePath, json);
        OnThemeChanged?.Invoke();
    }

    public MudTheme GetMudTheme() => CurrentTheme switch
    {
        AppTheme.MissionControl => MissionControlTheme,
        AppTheme.DarkFinance => DarkFinanceTheme,
        _ => AzureBlueTheme
    };

    private static AppTheme Load()
    {
        if (!File.Exists(FilePath))
            return AppTheme.AzureBlue;

        try
        {
            var json = File.ReadAllText(FilePath);
            var name = JsonSerializer.Deserialize<string>(json);
            return Enum.TryParse<AppTheme>(name, out var theme) ? theme : AppTheme.AzureBlue;
        }
        catch
        {
            return AppTheme.AzureBlue;
        }
    }

    private static readonly MudTheme AzureBlueTheme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0078D4",
            PrimaryDarken = "#005A9E",
            PrimaryLighten = "#50A0FF",
            Secondary = "#00B4FF",
            AppbarBackground = "#0078D4",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#0078D4",
            PrimaryDarken = "#005A9E",
            PrimaryLighten = "#50A0FF",
            Secondary = "#00B4FF",
            AppbarBackground = "#0078D4",
        }
    };

    private static readonly MudTheme MissionControlTheme = new()
    {
        PaletteDark = new PaletteDark
        {
            Black = "#080D08",
            Background = "#080D08",
            BackgroundGray = "#0C170C",
            Surface = "#0C170C",
            DrawerBackground = "#0C170C",
            DrawerText = "#A0F0A8",
            DrawerIcon = "#00C940",
            AppbarBackground = "#040A04",
            AppbarText = "#A0F0A8",
            Primary = "#00C940",
            PrimaryContrastText = "#080D08",
            Secondary = "#FF8C00",
            SecondaryContrastText = "#080D08",
            TextPrimary = "#A0F0A8",
            TextSecondary = "#6EC874",
            TextDisabled = "#2C5A2C",
            ActionDefault = "#A0F0A8",
            ActionDisabled = "#2C5A2C",
            ActionDisabledBackground = "#163016",
            Divider = "#163016",
            DividerLight = "#163016",
            TableLines = "#163016",
            LinesDefault = "#163016",
            LinesInputs = "#1E4A1E",
            TableStriped = "#0E1D0E",
            TableHover = "#163016",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#00C940",
            Secondary = "#FF8C00",
            AppbarBackground = "#040A04",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Share Tech Mono", "monospace"] },
            H1 = new H1Typography { FontFamily = ["Share Tech Mono", "monospace"] },
            H2 = new H2Typography { FontFamily = ["Share Tech Mono", "monospace"] },
            H3 = new H3Typography { FontFamily = ["Share Tech Mono", "monospace"] },
            H4 = new H4Typography { FontFamily = ["Share Tech Mono", "monospace"] },
            H5 = new H5Typography { FontFamily = ["Share Tech Mono", "monospace"] },
            H6 = new H6Typography { FontFamily = ["Share Tech Mono", "monospace"] },
            Body1 = new Body1Typography { FontFamily = ["Share Tech Mono", "monospace"] },
            Body2 = new Body2Typography { FontFamily = ["Share Tech Mono", "monospace"] },
            Button = new ButtonTypography { FontFamily = ["Share Tech Mono", "monospace"] },
            Caption = new CaptionTypography { FontFamily = ["Share Tech Mono", "monospace"] },
            Subtitle1 = new Subtitle1Typography { FontFamily = ["Share Tech Mono", "monospace"] },
            Subtitle2 = new Subtitle2Typography { FontFamily = ["Share Tech Mono", "monospace"] },
        }
    };

    private static readonly MudTheme DarkFinanceTheme = new()
    {
        PaletteDark = new PaletteDark
        {
            Black = "#070C18",
            Background = "#070C18",
            BackgroundGray = "#0D1526",
            Surface = "#0D1526",
            DrawerBackground = "#0D1526",
            DrawerText = "#E2E8F0",
            DrawerIcon = "#F59E0B",
            AppbarBackground = "#050A14",
            AppbarText = "#E2E8F0",
            Primary = "#F59E0B",
            PrimaryContrastText = "#070C18",
            Secondary = "#22D3EE",
            SecondaryContrastText = "#070C18",
            TextPrimary = "#E2E8F0",
            TextSecondary = "#94A3B8",
            TextDisabled = "#334155",
            ActionDefault = "#E2E8F0",
            ActionDisabled = "#334155",
            ActionDisabledBackground = "#0D1526",
            Divider = "#1E293B",
            DividerLight = "#1E293B",
            TableLines = "#1E293B",
            LinesDefault = "#1E293B",
            LinesInputs = "#2D3D5A",
            TableStriped = "#0D1A2E",
            TableHover = "#1E293B",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#F59E0B",
            Secondary = "#22D3EE",
            AppbarBackground = "#050A14",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Barlow", "sans-serif"] },
            H1 = new H1Typography { FontFamily = ["Barlow Condensed", "sans-serif"] },
            H2 = new H2Typography { FontFamily = ["Barlow Condensed", "sans-serif"] },
            H3 = new H3Typography { FontFamily = ["Barlow Condensed", "sans-serif"] },
            H4 = new H4Typography { FontFamily = ["Barlow Condensed", "sans-serif"] },
            H5 = new H5Typography { FontFamily = ["Barlow Condensed", "sans-serif"] },
            H6 = new H6Typography { FontFamily = ["Barlow Condensed", "sans-serif"] },
            Body1 = new Body1Typography { FontFamily = ["Barlow", "sans-serif"] },
            Body2 = new Body2Typography { FontFamily = ["Barlow", "sans-serif"] },
            Button = new ButtonTypography { FontFamily = ["Barlow Condensed", "sans-serif"] },
            Caption = new CaptionTypography { FontFamily = ["IBM Plex Mono", "monospace"] },
            Subtitle1 = new Subtitle1Typography { FontFamily = ["Barlow", "sans-serif"] },
            Subtitle2 = new Subtitle2Typography { FontFamily = ["IBM Plex Mono", "monospace"] },
        }
    };
}
