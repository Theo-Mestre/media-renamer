using System.Text.Json;
using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed class SavedSessionState
{
    public string? LastRootDirectory { get; set; }
    public string? LastThemeName { get; set; }
    public RenamePipelineConfig? LastPipelineConfig { get; set; }
}

public static class SessionStorage
{
    private static readonly string SessionPath = Path.Combine(AppContext.BaseDirectory, "session.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SavedSessionState Load()
    {
        try
        {
            if (File.Exists(SessionPath))
            {
                return JsonSerializer.Deserialize<SavedSessionState>(File.ReadAllText(SessionPath), JsonOptions)
                       ?? new SavedSessionState();
            }
        }
        catch
        {
            // Fall back cleanly if session.json is corrupted
        }
        return new SavedSessionState();
    }

    public static void Save(string rootDirectory, string themeName, RenamePipelineConfig config)
    {
        try
        {
            var state = new SavedSessionState
            {
                LastRootDirectory = rootDirectory,
                LastThemeName = themeName,
                LastPipelineConfig = config
            };
            File.WriteAllText(SessionPath, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // Ignore write errors on read-only media
        }
    }
}