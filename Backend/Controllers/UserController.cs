using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Dtos.Auth;
using Backend.Entities;
using Backend.Services;

namespace Backend.Controllers;

[SocketController]
public sealed class UserController(UserService userService, ProfileService profileService) : SocketControllerBase
{
    [Request]
    public async Task CreateProfile(CreateProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(User);
        profileService.CreateProfile(User, request.Name);
        await RespondAsync(new CreateProfileResponse());
    }
    
    [Request]
    public async Task ListProfiles(ListProfilesRequest request)
    {
        ArgumentNullException.ThrowIfNull(User);
        Profile[] profiles = profileService.GetProfiles(User);
        await RespondAsync(new ListProfilesResponse() { Profiles = profiles.Select(p => p.ToDto()).ToArray() });
    }

    [Request]
    public async Task LoginAsTestUser(LoginAsTestUserRequest request)
    {
        if (User is not null)
        {
            throw new Exception("Already logged in.");
        }

        User testUser = userService.GetTestUser();
        userService.SignIn(Socket, testUser);
        await RespondAsync(new LoginAsTestUserResponse());
    }

    [Request]
    public async Task SelectProfile(SelectProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(User);
        profileService.SelectProfile(Socket, User, request.ProfileId);
        await RespondAsync(new SelectProfileResponse());
    }
}