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
}