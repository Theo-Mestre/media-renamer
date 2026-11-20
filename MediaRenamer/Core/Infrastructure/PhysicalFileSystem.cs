using MediaRenamer.Core.Abstractions;

namespace MediaRenamer.Core.Infrastructure;

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        => Directory.EnumerateFiles(path, searchPattern, searchOption);

    public string ReadAllText(string path) => File.ReadAllText(path);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public void MoveFile(string sourceFileName, string destFileName) => File.Move(sourceFileName, destFileName);
    public void DeleteFile(string path) => File.Delete(path);
}