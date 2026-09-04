using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Backend.Services;

/// <summary>
/// The build this process was compiled from, for the version footer in the
/// frontend. The values are assembly metadata that Backend.csproj stamps from
/// the GitCommit / GitCommitTime MSBuild properties, which only the image build
/// passes (see Backend/Dockerfile). Every other build carries none and is
/// reported as a local build through nulls.
/// </summary>
public sealed class VersionService(string? commit, long? commitTimeMs)
{
    internal const string CommitKey = "GitCommit";
    internal const string CommitTimeKey = "GitCommitTime";

    /// <summary>Full SHA of the commit the assembly was built from, or null.</summary>
    public string? Commit => commit;

    /// <summary>Committer date of that commit as epoch milliseconds, or null.</summary>
    public long? CommitTimeMs => commitTimeMs;

    public static VersionService FromAssembly()
    {
        // This assembly rather than the entry assembly, which under the test
        // host is the runner's.
        Dictionary<string, string?> metadata = typeof(VersionService).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        return FromMetadata(metadata);
    }

    internal static VersionService FromMetadata(IReadOnlyDictionary<string, string?> metadata)
    {
        string? commit = metadata.GetValueOrDefault(CommitKey);
        commit = string.IsNullOrWhiteSpace(commit) ? null : commit.Trim();

        // CI passes the committer date as unix seconds (git log --format=%ct);
        // timestamps travel as milliseconds.
        long? commitTimeMs = null;
        if (long.TryParse(metadata.GetValueOrDefault(CommitTimeKey), NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
        {
            commitTimeMs = seconds * 1000;
        }

        return new VersionService(commit, commitTimeMs);
    }
}
