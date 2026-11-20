using System.Text.Json;
using MediaRenamer.Core.Abstractions;
using MediaRenamer.Core.Infrastructure;
using MediaRenamer.Core.Models;
using MediaRenamer.Core.Pipeline;

namespace MediaRenamer.Core.Services;

public sealed class JsonPresetRepository(IFileSystem? fileSystem = null, string? presetsDir = null) : IPresetRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IFileSystem _fileSystem = fileSystem ?? new PhysicalFileSystem();
    private readonly string _presetsDir = presetsDir ?? Path.Combine(AppContext.BaseDirectory, "presets");

    public void EnsureDefaults()
    {
        _fileSystem.CreateDirectory(_presetsDir);

        string cleanupPath = Path.Combine(_presetsDir, "new-download-cleanup.json");
        if (!_fileSystem.FileExists(cleanupPath))
        {
            var cleanup = new RenamePipelineConfig
            {
                Operations =
                [
                    new StripLegacyNumberOperation(),
                    new ReplaceStringOperation { SearchFor = "_", ReplaceWith = " " },
                    new SplitNameArtistOperation { SplitAfterWords = 1 },
                    new CaseTransformOperation { CaseMode = CaseTransform.TitleCase }
                ],
                AutoCount = true,
                CompactGaps = false,
                Scope = CountScope.NameAndArtist,
                PaddingDigits = 3
            };
            _fileSystem.WriteAllText(cleanupPath, JsonSerializer.Serialize(cleanup, JsonOptions));
        }

        string favPath = Path.Combine(_presetsDir, "mark-favorite-prefix.json");
        if (!_fileSystem.FileExists(favPath))
        {
            var fav = new RenamePipelineConfig
            {
                Operations =
                [
                    new StripLegacyNumberOperation(),
                    new AddAffixOperation { Prefix = "! " }
                ],
                AutoCount = false
            };
            _fileSystem.WriteAllText(favPath, JsonSerializer.Serialize(fav, JsonOptions));
        }
    }

    public List<PipelinePreset> LoadAll()
    {
        EnsureDefaults();
        var presets = new List<PipelinePreset>();

        foreach (string file in _fileSystem.EnumerateFiles(_presetsDir, "*.json", SearchOption.TopDirectoryOnly).OrderBy(f => f))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<RenamePipelineConfig>(_fileSystem.ReadAllText(file), JsonOptions);
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
                // Skip malformed preset files
            }
        }

        return presets;
    }

    public void Save(string name, RenamePipelineConfig config)
    {
        _fileSystem.CreateDirectory(_presetsDir);
        string safeFileName = string.Concat(name.Trim().Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "custom-preset";

        string fullPath = Path.Combine(_presetsDir, $"{safeFileName}.json");
        _fileSystem.WriteAllText(fullPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    public void Delete(PipelinePreset preset)
    {
        if (_fileSystem.FileExists(preset.FilePath))
        {
            _fileSystem.DeleteFile(preset.FilePath);
        }
    }
}