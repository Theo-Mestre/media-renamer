using MediaRenamer.Core.Models;
using MediaRenamer.Core.Services;
using MediaRenamer.Theme;

namespace MediaRenamer.UI;

public sealed class AppSession
{
    public string RootDirectory { get; set; } = Directory.GetCurrentDirectory();
    public List<MediaFileItem> AllFiles { get; set; } = [];
    public List<MediaFileItem> SelectedFiles { get; set; } = [];
    public RenamePipelineConfig PipelineConfig { get; } = new();
    public AppTheme CurrentTheme { get; set; }

    public WorkspaceScanner Scanner { get; } = new();
    public RenameEngine Engine { get; } = new();

    public AppSession(AppTheme initialTheme)
    {
        CurrentTheme = initialTheme;
    }

    public void RescanWorkspace()
    {
        AllFiles = Scanner.ScanDirectory(RootDirectory);
        SelectedFiles.Clear();
    }

    public void RescanAndRetainSelection(IReadOnlyList<RenameRecord> appliedRecords)
    {
        var remappedPaths = appliedRecords
            .Select(r => r.NewFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var untouchedSelected = SelectedFiles
            .Where(f => !appliedRecords.Any(r => string.Equals(r.OriginalFullPath, f.FullPath, StringComparison.OrdinalIgnoreCase)))
            .Select(f => f.FullPath);

        foreach (string path in untouchedSelected)
            remappedPaths.Add(path);

        AllFiles = Scanner.ScanDirectory(RootDirectory);
        SelectedFiles = AllFiles
            .Where(f => remappedPaths.Contains(f.FullPath))
            .ToList();
    }

    public void ClearSelection() => SelectedFiles.Clear();

    public void SaveState() => SessionStorage.Save(RootDirectory, CurrentTheme.Name, PipelineConfig);
}