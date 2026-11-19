using System.Text.Json;
using Spectre.Console;

namespace MediaRenamer.Theme;

public sealed class AppTheme
{
    private readonly Dictionary<string, Color> _colorCache = new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; set; } = "Monokai Slate";

    // Window & Terminal Chrome
    public string BackgroundHex { get; set; } = "#272822";
    public string TitleBarHex { get; set; } = "#272822";
    public string TitleBarTextHex { get; set; } = "#75715E";
    public string BorderHex { get; set; } = "#49483E";

    // Reference Palette
    public string HeaderAccentHex { get; set; } = "#E6DB74";
    public string CategoryLabelHex { get; set; } = "#F92672";
    public string TextHex { get; set; } = "#F8F8F2";
    public string MutedHex { get; set; } = "#75715E";
    public string PrimaryHex { get; set; } = "#66D9EF";
    public string SecondaryHex { get; set; } = "#AE81FF";
    public string SuccessHex { get; set; } = "#A6E22E";
    public string WarningHex { get; set; } = "#FD971F";
    public string ErrorHex { get; set; } = "#F92672";
    public string FolderBadgeHex { get; set; } = "#66D9EF";

    public string BorderStyle { get; set; } = "Minimal";

    public Color ToColor(string hex)
    {
        if (_colorCache.TryGetValue(hex, out var cached))
            return cached;

        string clean = hex.TrimStart('#');
        Color parsed = clean.Length == 6
            ? new Color(
                Convert.ToByte(clean[0..2], 16),
                Convert.ToByte(clean[2..4], 16),
                Convert.ToByte(clean[4..6], 16))
            : Color.White;

        _colorCache[hex] = parsed;
        return parsed;
    }

    public TableBorder GetTableBorder() => BorderStyle.ToLowerInvariant() switch
    {
        "square" => TableBorder.Square,
        "double" => TableBorder.Double,
        "heavy" => TableBorder.Heavy,
        "rounded" => TableBorder.Rounded,
        _ => TableBorder.Simple
    };
}

public static class ThemeManager
{
    private static readonly string ThemesDir = Path.Combine(AppContext.BaseDirectory, "themes");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void EnsureDefaultThemes()
    {
        Directory.CreateDirectory(ThemesDir);

        string defaultPath = Path.Combine(ThemesDir, "monokai-slate.json");
        if (!File.Exists(defaultPath))
        {
            File.WriteAllText(defaultPath, JsonSerializer.Serialize(new AppTheme(), JsonOptions));
        }

        string nordPath = Path.Combine(ThemesDir, "nord-dark.json");
        if (!File.Exists(nordPath))
        {
            var nord = new AppTheme
            {
                Name = "Nord Dark",
                BackgroundHex = "#2E3440",
                TitleBarHex = "#2E3440",
                TitleBarTextHex = "#4C566A",
                BorderHex = "#3B4252",
                HeaderAccentHex = "#EBCB8B",
                CategoryLabelHex = "#BF616A",
                TextHex = "#ECEFF4",
                MutedHex = "#616E88",
                PrimaryHex = "#88C0D0",
                SecondaryHex = "#B48EAD",
                SuccessHex = "#A3BE8C",
                WarningHex = "#D08770",
                ErrorHex = "#BF616A",
                FolderBadgeHex = "#81A1C1",
                BorderStyle = "Minimal"
            };
            File.WriteAllText(nordPath, JsonSerializer.Serialize(nord, JsonOptions));
        }
    }

    public static List<AppTheme> LoadAllThemes()
    {
        EnsureDefaultThemes();
        var themes = new List<AppTheme>();

        foreach (string file in Directory.EnumerateFiles(ThemesDir, "*.json"))
        {
            try
            {
                var theme = JsonSerializer.Deserialize<AppTheme>(File.ReadAllText(file), JsonOptions);
                if (theme != null)
                {
                    if (string.IsNullOrWhiteSpace(theme.Name))
                        theme.Name = Path.GetFileNameWithoutExtension(file);
                    themes.Add(theme);
                }
            }
            catch
            {
                // Skip malformed theme JSON files gracefully
            }
        }

        return themes.Count > 0 ? themes : [new AppTheme()];
    }
}