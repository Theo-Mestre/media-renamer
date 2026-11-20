using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed class CrossFolderCounter
{
    private readonly Dictionary<string, int> _highestByScope = new(StringComparer.OrdinalIgnoreCase);

    public CrossFolderCounter(
        IReadOnlyList<MediaFileItem> allFiles,
        IReadOnlySet<string> excludedSelectedPaths,
        CountScope scope)
    {
        foreach (var file in allFiles)
        {
            if (excludedSelectedPaths.Contains(file.FullPath))
                continue;

            if (!file.Parsed.IndexNumber.HasValue)
                continue;

            string key = file.Parsed.GetScopeKey(scope);
            int num = file.Parsed.IndexNumber.Value;

            if (!_highestByScope.TryGetValue(key, out int currentMax) || num > currentMax)
            {
                _highestByScope[key] = num;
            }
        }
    }

    public int NextIndexFor(string scopeKey)
    {
        int next = _highestByScope.TryGetValue(scopeKey, out int current) ? current + 1 : 1;
        _highestByScope[scopeKey] = next;
        return next;
    }
}