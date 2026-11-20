using MediaRenamer.Core.Abstractions;

namespace MediaRenamer.Tests;

public sealed class InMemoryFileSystem : IFileSystem
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryFileSystem(params string[] initialFilePaths)
    {
        foreach (string path in initialFilePaths)
        {
            Files[path] = string.Empty;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directories.Add(dir);
            }
        }
    }

    public bool FileExists(string path) => Files.ContainsKey(path);
    public bool DirectoryExists(string path) => Directories.Contains(path);
    public void CreateDirectory(string path) => Directories.Add(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        => Files.Keys.Where(k => k.StartsWith(path, StringComparison.OrdinalIgnoreCase));

    public string ReadAllText(string path) => Files[path];
    public void WriteAllText(string path, string contents) => Files[path] = contents;

    public void MoveFile(string sourceFileName, string destFileName)
    {
        string content = Files[sourceFileName];
        Files.Remove(sourceFileName);
        Files[destFileName] = content;
    }

    public void DeleteFile(string path) => Files.Remove(path);
}