using System;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Dtos;
using Backend.Services;

namespace Backend.Controllers;

[SocketController]
public sealed class ActivityController(ActivityService activityService) : SocketControllerBase
{
    [Request]
    public async Task StartActivity(StartActivityRequest request)
    {
        await activityService.StartActivityAsync(ProfileId, request.ActivityId, DateTime.UtcNow);
        await RespondAsync(new StartActivityResponse());
    }

    [Request]
    public async Task StopActivity(StopActivityRequest request)
    {
        await activityService.StopActivityAsync(ProfileId);
        await RespondAsync(new StopActivityResponse());
    }
}
