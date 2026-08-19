using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Services;
using Backend.Dtos;

namespace Backend.Controllers;

[SocketController]
public sealed class AuthController(UserService userService, ProfileService profileService) : SocketControllerBase
{
    private async Task<ProfileDto[]> GetProfiles(UserId userId)
    {
        return (await profileService.GetProfilesAsync(userId)).Select(p => p.ToDto()).ToArray();
    }
    
    [Request]
    public async Task CreateProfile(CreateProfileRequest request)
    {
        await profileService.CreateProfileAsync(UserId, request.Name);
        await SendUserEventAsync(new ProfilesChangedEvent() { Profiles = await GetProfiles(UserId) });
        await RespondAsync(new CreateProfileResponse());
    }
    
    [Request]
    public async Task ListProfiles(ListProfilesRequest request)
    {
        await RespondAsync(new ListProfilesResponse() { Profiles = await GetProfiles(UserId) });
    }

    [Request]
    public async Task LoginAsTestUser(LoginAsTestUserRequest request)
    {
        if (Socket.UserId is not null)
        {
            throw new BackendException("Already logged in.");
        }

        UserId testUserId = await userService.GetTestUserAsync();
        userService.SignIn(Socket, testUserId);
        await RespondAsync(new LoginAsTestUserResponse());
    }

    [Request]
    public async Task SelectProfile(SelectProfileRequest request)
    {
        await profileService.SelectProfileAsync(Socket, UserId, request.ProfileId);
        await RespondAsync(new SelectProfileResponse());
    }
}