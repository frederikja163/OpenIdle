using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Backend.Services;

public sealed class VersionService(string? commit, long? commitTimeMs)
{
    internal const string CommitKey = "GitCommit";
    internal const string CommitTimeKey = "GitCommitTime";

    public string? Commit => commit;

    public long? CommitTimeMs => commitTimeMs;

    public static VersionService FromAssembly()
    {
        Dictionary<string, string?> metadata = typeof(VersionService).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        return FromMetadata(metadata);
    }

    internal static VersionService FromMetadata(IReadOnlyDictionary<string, string?> metadata)
    {
        string? commit = metadata.GetValueOrDefault(CommitKey);
        commit = string.IsNullOrWhiteSpace(commit) ? null : commit.Trim();

        long? commitTimeMs = null;
        if (long.TryParse(metadata.GetValueOrDefault(CommitTimeKey), NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
        {
            commitTimeMs = seconds * 1000;
        }

        return new VersionService(commit, commitTimeMs);
    }
}
