using System.Reflection;
using Backend.Services;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class VersionServiceTests
{
    private const string Commit = "1e1c256a0b1c2d3e4f5061728394a5b6c7d8e9f0";

    [Test]
    public void FromMetadata_WithoutBuildInfo_ReportsLocalBuild()
    {
        VersionService service = VersionService.FromMetadata(new Dictionary<string, string?>());

        Assert.Multiple(() =>
        {
            Assert.That(service.Commit, Is.Null);
            Assert.That(service.CommitTimeMs, Is.Null);
        });
    }

    [Test]
    public void FromMetadata_WithEmptyValues_ReportsLocalBuild()
    {
        // What every build outside the image stamps: the csproj items exist, the
        // properties behind them do not.
        VersionService service = VersionService.FromMetadata(new Dictionary<string, string?>
        {
            [VersionService.CommitKey] = "",
            [VersionService.CommitTimeKey] = ""
        });

        Assert.Multiple(() =>
        {
            Assert.That(service.Commit, Is.Null);
            Assert.That(service.CommitTimeMs, Is.Null);
        });
    }

    [Test]
    public void FromMetadata_WithBuildInfo_ConvertsSecondsToMilliseconds()
    {
        VersionService service = VersionService.FromMetadata(new Dictionary<string, string?>
        {
            [VersionService.CommitKey] = Commit,
            [VersionService.CommitTimeKey] = "1788564400"
        });

        Assert.Multiple(() =>
        {
            Assert.That(service.Commit, Is.EqualTo(Commit));
            Assert.That(service.CommitTimeMs, Is.EqualTo(1_788_564_400_000L));
        });
    }

    [Test]
    public void FromMetadata_WithUnparsableTime_KeepsCommitAndDropsTime()
    {
        VersionService service = VersionService.FromMetadata(new Dictionary<string, string?>
        {
            [VersionService.CommitKey] = $" {Commit} ",
            [VersionService.CommitTimeKey] = "2026-09-04T23:26:40Z"
        });

        Assert.Multiple(() =>
        {
            Assert.That(service.Commit, Is.EqualTo(Commit));
            Assert.That(service.CommitTimeMs, Is.Null);
        });
    }

    [Test]
    public void FromAssembly_ReadsTheStampedMetadata()
    {
        // The test build passes no properties, so the stamped values are empty.
        // What this pins is that Backend.csproj stamps both keys at all: without
        // the AssemblyMetadata items a CI image would silently report `local`.
        string[] stampedKeys = typeof(VersionService).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Select(attribute => attribute.Key)
            .ToArray();
        VersionService service = VersionService.FromAssembly();

        Assert.Multiple(() =>
        {
            Assert.That(stampedKeys, Does.Contain(VersionService.CommitKey));
            Assert.That(stampedKeys, Does.Contain(VersionService.CommitTimeKey));
            Assert.That(service.Commit, Is.Null);
            Assert.That(service.CommitTimeMs, Is.Null);
        });
    }
}
