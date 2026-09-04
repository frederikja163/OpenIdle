using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Dtos;
using Backend.Services;

namespace Backend.Controllers;

[SocketController]
public sealed class VersionController(VersionService versionService) : SocketControllerBase
{
    // Reads neither UserId nor ProfileId on purpose: the version footer asks
    // before anyone has signed in.
    [Request]
    public async Task GetVersion(GetVersionRequest request)
    {
        await RespondAsync(new GetVersionResponse
        {
            Commit = versionService.Commit,
            CommitTime = versionService.CommitTimeMs
        });
    }
}
