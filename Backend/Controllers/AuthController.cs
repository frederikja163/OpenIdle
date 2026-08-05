using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Dtos.Auth;
using Backend.Entities;
using Backend.Services;

namespace Backend.Controllers;

[SocketController]
public sealed class AuthController(UserService userService, ProfileService profileService) : SocketControllerBase
{
    private async Task<ProfileDto[]> GetProfiles(User user)
    {
        return (await profileService.GetProfilesAsync(user)).Select(p => p.ToDto()).ToArray();
    }
    
    [Request]
    public async Task CreateProfile(CreateProfileRequest request)
    {
        await profileService.CreateProfileAsync(UserOrThrow, request.Name);
        await SendUserEventAsync(new ProfilesChangedEvent() { Profiles = await GetProfiles(UserOrThrow) });
        await RespondAsync(new CreateProfileResponse());
    }
    
    [Request]
    public async Task ListProfiles(ListProfilesRequest request)
    {
        await RespondAsync(new ListProfilesResponse() { Profiles = await GetProfiles(UserOrThrow) });
    }

    [Request]
    public async Task LoginAsTestUser(LoginAsTestUserRequest request)
    {
        if (Socket.User is not null)
        {
            throw new InvalidOperationException("Already logged in.");
        }

        User testUser = await userService.GetTestUserAsync();
        userService.SignIn(Socket, testUser);
        await RespondAsync(new LoginAsTestUserResponse());
    }

    [Request]
    public async Task SelectProfile(SelectProfileRequest request)
    {
        await profileService.SelectProfileAsync(Socket, UserOrThrow, request.ProfileId);
        await RespondAsync(new SelectProfileResponse());
    }
}