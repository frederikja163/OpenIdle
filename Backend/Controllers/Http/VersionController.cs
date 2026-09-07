using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Http;

[ApiController]
public sealed class VersionController(VersionService versionService) : ControllerBase
{
    [HttpGet("/version")]
    public IActionResult GetVersion() => Ok(new
    {
        commit = versionService.Commit,
        commitTime = versionService.CommitTimeMs
    });
}