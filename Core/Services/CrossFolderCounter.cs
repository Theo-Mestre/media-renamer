using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed class CrossFolderCounter
{
    private readonly Dictionary<string, int> _counters = new(StringComparer.OrdinalIgnoreCase);

    public CrossFolderCounter(
        IEnumerable<MediaFileItem> allWorkspaceFiles,
        IReadOnlySet<string> excludedPaths,
        CountScope scope)
    {
        foreach (var file in allWorkspaceFiles)
        {
            if (excludedPaths.Contains(file.FullPath) || !file.Parsed.IndexNumber.HasValue)
                continue;

            string key = file.Parsed.GetScopeKey(scope);
            int val = file.Parsed.IndexNumber.Value;

            if (!_counters.TryGetValue(key, out int currentMax) || val > currentMax)
            {
                _counters[key] = val;
            }
        }
    }

    public int NextIndexFor(string scopeKey)
    {
        int next = _counters.TryGetValue(scopeKey, out int current) ? current + 1 : 1;
        _counters[scopeKey] = next;
        return next;
    }
}