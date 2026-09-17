using System;

namespace Backend;

/// <summary>
/// Static tuning for the level-up and activity pacing curves.
///
/// Source of truth: the "Pacing Lab" tool output committed as the final 0-&gt;50 configuration.
/// The tuning/analysis tools themselves live under <c>tools/</c> and stay out of the shipped game;
/// this file is the only thing the opensourced backend depends on.
/// </summary>
public static class LevelCurve
{
    /// <summary>The shape of the XP curve (<c>geom</c> = each level-up costs <see cref="R"/> times the previous one).</summary>
    public const string Model = "geom";

    /// <summary>XP to go from level 0 to level 1. Each later level-up multiplies the last by <see cref="R"/>.</summary>
    public const double Base = 895;

    /// <summary>Per-level growth rate.</summary>
    public const double R = 1.13;

    /// <summary>Highest level this config defines.</summary>
    public const int MaxLevel = 50;

    /// <summary>Base (tier 0) duration of one untooled action, in seconds.</summary>
    public const double BaseUntooledActionSecondsTier0 = 8;

    /// <summary>Extra seconds each tier adds to an untooled action.</summary>
    public const double UntooledGrowsPerTier = 5;

    /// <summary>Tier-0 tool buff, as a speed percentage.</summary>
    public const double ToolBuffPctTier0 = 18;

    /// <summary>Tool buff percentage gained per tier.</summary>
    public const double ToolBuffGrowsPerTier = 12;

    /// <summary>Cap on the accumulated tool buff percentage.</summary>
    public const double ToolBuffMaxPct = 90;

    /// <summary>Fraction (0..1) of a tier elapsed before the tier's tool is unlocked.</summary>
    public const double ToolAfterFractionOfTier = 0.2;

    /// <summary>Levels per tier.</summary>
    public const int LevelsPerTier = 10;

    /// <summary>XP per action in tier 0.</summary>
    public const double XpPerActionTier0 = 200;

    /// <summary>Fractional XP-per-action growth each tier.</summary>
    public const double XpActionGrowthPerTier = 0.17;

    /// <summary>
    /// The per-level XP cost shape is geometric: going from level L to L+1 costs
    /// <c>round(Base * R^L)</c>. The cumulative requirement (level 0 = 0) is the running sum,
    /// computed lazily and cached below.
    /// </summary>
    private static readonly int[] XpForLevelTable = ComputeXpForLevelTable();

    private static int[] ComputeXpForLevelTable()
    {
        int[] table = new int[MaxLevel + 1];
        long cumulative = 0;
        for (int level = 0; level <= MaxLevel; level++)
        {
            table[level] = (int)cumulative;
            cumulative += (long)Math.Round(Base * Math.Pow(R, level));
        }

        return table;
    }

    /// <summary>Returns the cumulative XP needed to <em>be</em> level <paramref name="level"/>.</summary>
    public static int XpForLevel(int level)
    {
        if (level < 0)
        {
            return 0;
        }

        if (level <= MaxLevel)
        {
            return XpForLevelTable[level];
        }

        // Past the configured cap, keep the same geometric per-level cost.
        long xp = XpForLevelTable[^1];
        for (int L = MaxLevel; L < level; L++)
        {
            xp += (long)Math.Round(Base * Math.Pow(R, L));
            if (xp >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)Math.Min(xp, int.MaxValue);
    }

    /// <summary>Returns the level a player with <paramref name="xp"/> earned has reached (0..<see cref="MaxLevel"/>).</summary>
    public static int LevelFromXp(int xp)
    {
        // The XP-for-level table is non-decreasing, so binary search for the highest level
        // whose cumulative requirement the player has met.
        int low = 0;
        int high = MaxLevel;
        while (low < high)
        {
            int mid = low + (high - low + 1) / 2;
            if (xp >= XpForLevelTable[mid])
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }
}
