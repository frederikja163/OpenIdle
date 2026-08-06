using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Database.Entities;
using Backend.Services;
using Backend.Dtos;

namespace Backend.Controllers;

[SocketController]
public sealed class UserController(UserService userService, ProfileService profileService) : SocketControllerBase
{
    [Request]
    public async Task CreateProfile(CreateProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(User);
        await profileService.CreateProfileAsync(User, request.Name);
        await RespondAsync(new CreateProfileResponse());
    }
    
    [Request]
    public async Task ListProfiles(ListProfilesRequest request)
    {
        ArgumentNullException.ThrowIfNull(User);
        Profile[] profiles = await profileService.GetProfilesAsync(User);
        await RespondAsync(new ListProfilesResponse() { Profiles = profiles.Select(p => p.ToDto()).ToArray() });
    }

    [Request]
    public async Task LoginAsTestUser(LoginAsTestUserRequest request)
    {
        if (User is not null)
        {
            throw new Exception("Already logged in.");
        }

        User testUser = await userService.GetTestUserAsync();
        userService.SignIn(Socket, testUser);
        await RespondAsync(new LoginAsTestUserResponse());
    }

    [Request]
    public async Task SelectProfile(SelectProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(User);
        await profileService.SelectProfileAsync(Socket, User, request.ProfileId);
        await RespondAsync(new SelectProfileResponse());
    }
}