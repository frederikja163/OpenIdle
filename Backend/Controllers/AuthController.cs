using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Database.Entities;
using Backend.Services;
using Backend.Dtos;

namespace Backend.Controllers;

[SocketController]
public sealed class AuthController(UserService userService, ProfileService profileService, SkillService skillService) : SocketControllerBase
{
    private async Task<ProfileDto[]> GetProfiles(UserId userId)
    {
        return await Task.WhenAll((await profileService.GetProfilesAsync(userId)).Select(GetDto));

        async Task<ProfileDto> GetDto(Profile profile)
        {
            Skill[] skills = await skillService.GetSkillsAsync(profile.ProfileId);
            return profile.ToDto(skills.Sum(s => s.Level), SocketRegistry.IsProfileOnline(profile.ProfileId));
        }
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