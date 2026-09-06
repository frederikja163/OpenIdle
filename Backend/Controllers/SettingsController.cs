using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Dtos;
using Backend.Services;

namespace Backend.Controllers;

[SocketController]
public sealed class SettingsController(SettingsService service) : SocketControllerBase
{
    [Request]
    public async Task SetUserSettings(SetUserSettingsRequest request)
    {
        await service.SetUserSettings(UserId, request.Settings);
        await RespondAsync(new SetUserSettingsResponse());
    }

    [Request]
    public async Task GetUserSettings(GetUserSettingsRequest request)
    {
        await RespondAsync(new GetUserSettingsResponse()
        {
            Settings = request.Settings is null
                ? await service.GetUserSettings(UserId)
                : await service.GetUserSettings(UserId, request.Settings),
        });
    }

    [Request]
    public async Task SetProfileSettings(SetProfileSettingsRequest request)
    {
        await service.SetProfileSettings(ProfileId, request.Settings);
        await RespondAsync(new SetProfileSettingsResponse());
    }

    [Request]
    public async Task GetProfileSettings(GetProfileSettingsRequest request)
    {
        await RespondAsync(new GetProfileSettingsResponse()
        {
            Settings = request.Settings is null
                ? await service.GetProfileSettings(ProfileId)
                : await service.GetProfileSettings(ProfileId, request.Settings),
        });
    }
}
