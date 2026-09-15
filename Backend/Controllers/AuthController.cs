using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Database.Entities;
using Backend.Services;
using Backend.Dtos;
using Microsoft.Extensions.Options;

namespace Backend.Controllers;

[SocketController]
public sealed class AuthController(
    IOptions<AuthOptions> authOptions,
    UserService userService,
    ProfileService profileService,
    TokenValidationService tokenValidationService) : SocketControllerBase
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
    public async Task Login(LoginRequest request)
    {
        if (Socket.UserId is not null)
        {
            throw new BackendException("Already logged in.");
        }

        string subject = await tokenValidationService.ValidateAsync(request.AccessToken, CancellationToken.None);
        User user = await userService.GetOrCreateBySubjectAsync(subject);
        userService.SignIn(Socket, user.UserId);
        await RespondAsync(new LoginResponse() { User = user.ToDto() });
    }

    [Request]
    public async Task LoginAsTestUser(LoginAsTestUserRequest request)
    {
        if (!authOptions.Value.AllowTestLogin)
        {
            throw new BackendException("Test user login is not allowed on this server");
        }
        if (Socket.UserId is not null)
        {
            throw new BackendException("Already logged in.");
        }

        User testUser = await userService.GetTestUserAsync();
        userService.SignIn(Socket, testUser.UserId);
        await RespondAsync(new LoginAsTestUserResponse());
    }

    [Request]
    public async Task SelectProfile(SelectProfileRequest request)
    {
        await profileService.SelectProfileAsync(Socket, UserId, request.ProfileId);
        await RespondAsync(new SelectProfileResponse());
    }
}