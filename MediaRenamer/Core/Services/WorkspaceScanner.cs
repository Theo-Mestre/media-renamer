using MediaRenamer.Core.Abstractions;
using MediaRenamer.Core.Infrastructure;
using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed class WorkspaceScanner : IWorkspaceScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tiff",
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".mp3", ".wav", ".flac"
    };

    private readonly IFileSystem _fileSystem;

    public WorkspaceScanner(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
    }

    public List<MediaFileItem> ScanDirectory(string rootDirectory)
    {
        if (!_fileSystem.DirectoryExists(rootDirectory))
            return [];

        return _fileSystem.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                string fileName = Path.GetFileName(path);
                return !fileName.StartsWith(".tmp_rename_", StringComparison.OrdinalIgnoreCase) &&
                       SupportedExtensions.Contains(Path.GetExtension(path));
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new MediaFileItem(path, rootDirectory))
            .ToList();
    }
}