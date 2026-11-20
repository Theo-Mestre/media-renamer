using MediaRenamer.Core.Models;
using MediaRenamer.Theme;

namespace MediaRenamer.Core.Abstractions;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    void MoveFile(string sourceFileName, string destFileName);
    void DeleteFile(string path);
}

public interface IWorkspaceScanner
{
    List<MediaFileItem> ScanDirectory(string rootDirectory);
}

public sealed record PipelinePreset(string Name, string FilePath, RenamePipelineConfig Config);

public interface IPresetRepository
{
    void EnsureDefaults();
    List<PipelinePreset> LoadAll();
    void Save(string name, RenamePipelineConfig config);
    void Delete(PipelinePreset preset);
}

public sealed class SavedSessionState
{
    public string? LastRootDirectory { get; set; }
    public string? LastThemeName { get; set; }
    public RenamePipelineConfig? LastPipelineConfig { get; set; }
}

public interface ISessionStore
{
    SavedSessionState Load();
    void Save(string rootDirectory, string themeName, RenamePipelineConfig config);
}

public interface IThemeRepository
{
    void EnsureDefaults();
    List<AppTheme> LoadAll();
}