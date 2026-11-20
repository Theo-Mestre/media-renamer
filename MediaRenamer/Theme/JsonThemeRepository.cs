using MediaRenamer.Core.Abstractions;
using MediaRenamer.Core.Infrastructure;
using Spectre.Console;
using System.Text.Json;

namespace MediaRenamer.Theme;

public sealed class AppTheme
{
    public string Name { get; set; } = "Monokai Slate";
    public string BackgroundHex { get; set; } = "#1E1F1C";
    public string BorderHex { get; set; } = "#49483E";
    public string PrimaryHex { get; set; } = "#66D9EF";
    public string HeaderAccentHex { get; set; } = "#FD971F";
    public string CategoryLabelHex { get; set; } = "#E6DB74";
    public string TextHex { get; set; } = "#F8F8F2";
    public string MutedHex { get; set; } = "#75715E";
    public string SuccessHex { get; set; } = "#A6E22E";
    public string WarningHex { get; set; } = "#E6DB74";
    public string ErrorHex { get; set; } = "#F92672";
    public string FolderBadgeHex { get; set; } = "#AE81FF";
    public string BorderStyle { get; set; } = "Rounded";

    public Color ToColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6 &&
            byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
            byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
            byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return new Color(r, g, b);
        }
        return Color.White;
    }

    public TableBorder GetTableBorder() => BorderStyle.ToLowerInvariant() switch
    {
        "heavy" => TableBorder.Heavy,
        "double" => TableBorder.Double,
        "minimal" => TableBorder.Minimal,
        "simple" => TableBorder.Simple,
        "none" => TableBorder.None,
        _ => TableBorder.Rounded
    };

    public static AppTheme MonokaiSlate => new()
    {
        Name = "Monokai Slate",
        BackgroundHex = "#1E1F1C",
        BorderHex = "#49483E",
        PrimaryHex = "#66D9EF",
        HeaderAccentHex = "#FD971F",
        CategoryLabelHex = "#E6DB74",
        TextHex = "#F8F8F2",
        MutedHex = "#75715E",
        SuccessHex = "#A6E22E",
        WarningHex = "#E6DB74",
        ErrorHex = "#F92672",
        FolderBadgeHex = "#AE81FF",
        BorderStyle = "Rounded"
    };

    public static AppTheme NordDark => new()
    {
        Name = "Nord Dark",
        BackgroundHex = "#2E3440",
        BorderHex = "#4C566A",
        PrimaryHex = "#88C0D0",
        HeaderAccentHex = "#81A1C1",
        CategoryLabelHex = "#EBCB8B",
        TextHex = "#ECEFF4",
        MutedHex = "#616E88",
        SuccessHex = "#A3BE8C",
        WarningHex = "#EBCB8B",
        ErrorHex = "#BF616A",
        FolderBadgeHex = "#B48EAD",
        BorderStyle = "Rounded"
    };
}

public sealed class JsonThemeRepository : IThemeRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IFileSystem _fileSystem;
    private readonly string _themesDir;

    public JsonThemeRepository(IFileSystem? fileSystem = null, string? themesDir = null)
    {
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
        _themesDir = themesDir ?? Path.Combine(AppContext.BaseDirectory, "themes");
    }

    public void EnsureDefaults()
    {
        _fileSystem.CreateDirectory(_themesDir);

        string monokaiPath = Path.Combine(_themesDir, "monokai-slate.json");
        if (!_fileSystem.FileExists(monokaiPath))
            _fileSystem.WriteAllText(monokaiPath, JsonSerializer.Serialize(AppTheme.MonokaiSlate, JsonOptions));

        string nordPath = Path.Combine(_themesDir, "nord-dark.json");
        if (!_fileSystem.FileExists(nordPath))
            _fileSystem.WriteAllText(nordPath, JsonSerializer.Serialize(AppTheme.NordDark, JsonOptions));
    }

    public List<AppTheme> LoadAll()
    {
        EnsureDefaults();
        var loaded = new List<AppTheme>();

        foreach (string file in _fileSystem.EnumerateFiles(_themesDir, "*.json", SearchOption.TopDirectoryOnly).OrderBy(f => f))
        {
            try
            {
                var theme = JsonSerializer.Deserialize<AppTheme>(_fileSystem.ReadAllText(file), JsonOptions);
                if (theme != null && !string.IsNullOrWhiteSpace(theme.Name))
                    loaded.Add(theme);
            }
            catch
            {
                // Skip invalid theme JSON files
            }
        }

        if (loaded.Count == 0)
            loaded.Add(AppTheme.MonokaiSlate);

        return loaded;
    }
}