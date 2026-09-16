using Backend;
using NUnit.Framework;

namespace OpenIdle.Tests;

[TestFixture]
public sealed class LevelCurveTests
{
    [TestCase(0, 0)]
    [TestCase(1, 895)]
    [TestCase(14, 31219)]
    [TestCase(24, 122465)]
    [TestCase(29, 231433)]
    [TestCase(49, 2739261)]
    [TestCase(50, 3096260)]
    public void XpForLevel_ReturnsCurveRequirement(int level, int expectedXp)
    {
        Assert.That(LevelCurve.XpForLevel(level), Is.EqualTo(expectedXp));
    }

    [Test]
    public void XpForLevel_WhenRequirementExceedsIntMax_ReturnsIntMax()
    {
        Assert.That(LevelCurve.XpForLevel(int.MaxValue), Is.EqualTo(int.MaxValue));
    }

    [TestCase(0, 0)]
    [TestCase(894, 0)]
    [TestCase(895, 1)]
    [TestCase(2739260, 48)]
    [TestCase(2739261, 49)]
    [TestCase(int.MaxValue, 50)]
    public void LevelFromXp_MapsXpToLevel(int xp, int expectedLevel)
    {
        Assert.That(LevelCurve.LevelFromXp(xp), Is.EqualTo(expectedLevel));
    }
}
