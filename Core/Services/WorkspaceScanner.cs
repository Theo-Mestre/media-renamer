using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed class WorkspaceScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tiff",
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".mp3", ".wav", ".flac"
    };

    public List<MediaFileItem> ScanDirectory(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return [];

        return Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new MediaFileItem(f, rootPath))
            .ToList();
    }
}