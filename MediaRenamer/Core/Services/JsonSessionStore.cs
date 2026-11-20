using System.Text.Json;
using MediaRenamer.Core.Abstractions;
using MediaRenamer.Core.Infrastructure;
using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed class JsonSessionStore : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IFileSystem _fileSystem;
    private readonly string _sessionPath;

    public JsonSessionStore(IFileSystem? fileSystem = null, string? sessionPath = null)
    {
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
        _sessionPath = sessionPath ?? Path.Combine(AppContext.BaseDirectory, "session.json");
    }

    public SavedSessionState Load()
    {
        try
        {
            if (_fileSystem.FileExists(_sessionPath))
            {
                return JsonSerializer.Deserialize<SavedSessionState>(_fileSystem.ReadAllText(_sessionPath), JsonOptions)
                       ?? new SavedSessionState();
            }
        }
        catch
        {
            // Fall back cleanly
        }
        return new SavedSessionState();
    }

    public void Save(string rootDirectory, string themeName, RenamePipelineConfig config)
    {
        try
        {
            var state = new SavedSessionState
            {
                LastRootDirectory = rootDirectory,
                LastThemeName = themeName,
                LastPipelineConfig = config
            };
            _fileSystem.WriteAllText(_sessionPath, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // Ignore write errors
        }
    }
}