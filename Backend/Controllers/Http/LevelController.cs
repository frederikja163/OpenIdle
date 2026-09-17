using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Http;

[ApiController]
public sealed class LevelController : ControllerBase
{
    /// <summary>
    /// The cumulative XP needed for each level, indexed by level. Static tuning
    /// rather than game state, so it belongs on the plumbing side; the client
    /// fetches it once at startup instead of re-deriving the curve itself.
    /// One entry past <see cref="LevelCurve.MaxLevel"/> is included so the top
    /// level's XP bar can still be sized.
    /// </summary>
    [HttpGet("/levels")]
    public IActionResult GetLevels()
    {
        int[] xpForLevel = new int[LevelCurve.MaxLevel + 2];
        for (int level = 0; level < xpForLevel.Length; level++)
        {
            xpForLevel[level] = LevelCurve.XpForLevel(level);
        }

        return Ok(xpForLevel);
    }
}
