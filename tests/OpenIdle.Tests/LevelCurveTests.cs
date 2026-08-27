using Backend;
using NUnit.Framework;

namespace OpenIdle.Tests;

[TestFixture]
public sealed class LevelCurveTests
{
    [TestCase(1, 0)]
    [TestCase(2, 895)]
    [TestCase(15, 31219)]
    [TestCase(25, 122465)]
    [TestCase(30, 231433)]
    [TestCase(50, 2739261)]
    public void XpForLevel_ReturnsCurveRequirement(int level, int expectedXp)
    {
        Assert.That(LevelCurve.XpForLevel(level), Is.EqualTo(expectedXp));
    }

    [TestCase(0, 1)]
    [TestCase(894, 1)]
    [TestCase(895, 2)]
    [TestCase(2739260, 49)]
    [TestCase(2739261, 50)]
    [TestCase(int.MaxValue, 50)]
    public void LevelFromXp_MapsXpToLevel(int xp, int expectedLevel)
    {
        Assert.That(LevelCurve.LevelFromXp(xp), Is.EqualTo(expectedLevel));
    }
}
