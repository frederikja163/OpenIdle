using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Backend.Entities;

namespace Backend.Services;

public sealed class ProfileService
{
    private Dictionary<User, List<Profile>> _profileByUser = [];

    internal List<Profile> GetProfiles(User user)
    {
        if (!_profileByUser.TryGetValue(user, out List<Profile>? profiles))
        {
            profiles = [];
            _profileByUser[user] = profiles;
        }

        return profiles;
    }

    internal Profile GetProfile(User user, Guid guid)
    {
        Profile? profile = GetProfiles(user).FirstOrDefault(p => p.ProfileId == guid);
        if (profile is null)
        {
            throw new Exception("Profile does not belong to user.");
        }

        return profile;
    }

    internal Profile CreateProfile(User user, string name)
    {
        Profile profile = new Profile()
        {
            ProfileId = Guid.NewGuid(),
            Name = name,
        };
        
        GetProfiles(user).Add(profile);
        return profile;
    }

    internal void SelectProfile(Socket socket, User user, Guid profileId)
    {
        socket.Profile = GetProfile(user, profileId);
    }
}