using System.Text.Json;
using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed record PipelinePreset(string Name, string FilePath, RenamePipelineConfig Config);

public static class PresetManager
{
    private static readonly string PresetsDir = Path.Combine(AppContext.BaseDirectory, "presets");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void EnsureDefaultPresets()
    {
        Directory.CreateDirectory(PresetsDir);

        string cleanupPath = Path.Combine(PresetsDir, "new-download-cleanup.json");
        if (!File.Exists(cleanupPath))
        {
            var cleanup = new RenamePipelineConfig
            {
                StripLegacyNumbers = true,
                SplitNameAfterWords = 1,
                CaseMode = CaseTransform.TitleCase,
                AutoCount = true,
                CompactGaps = false,
                Scope = CountScope.NameAndArtist,
                PaddingDigits = 3
            };
            File.WriteAllText(cleanupPath, JsonSerializer.Serialize(cleanup, JsonOptions));
        }

        string favPath = Path.Combine(PresetsDir, "mark-favorite-prefix.json");
        if (!File.Exists(favPath))
        {
            var fav = new RenamePipelineConfig
            {
                Prefix = "! ",
                StripLegacyNumbers = true,
                AutoCount = false
            };
            File.WriteAllText(favPath, JsonSerializer.Serialize(fav, JsonOptions));
        }
    }

    public static List<PipelinePreset> LoadAllPresets()
    {
        EnsureDefaultPresets();
        var presets = new List<PipelinePreset>();

        foreach (string file in Directory.EnumerateFiles(PresetsDir, "*.json").OrderBy(f => f))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<RenamePipelineConfig>(File.ReadAllText(file), JsonOptions);
                if (cfg != null)
                {
                    string displayName = Path.GetFileNameWithoutExtension(file)
                        .Replace('-', ' ')
                        .Replace('_', ' ');
                    presets.Add(new PipelinePreset(displayName, file, cfg));
                }
            }
            catch
            {
                // Skip malformed preset files gracefully
            }
        }

        return presets;
    }

    public static void SavePreset(string name, RenamePipelineConfig config)
    {
        Directory.CreateDirectory(PresetsDir);
        string safeFileName = string.Concat(name.Trim().Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "custom-preset";

        string fullPath = Path.Combine(PresetsDir, $"{safeFileName}.json");
        File.WriteAllText(fullPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    public static void DeletePreset(PipelinePreset preset)
    {
        if (File.Exists(preset.FilePath))
        {
            File.Delete(preset.FilePath);
        }
    }
}