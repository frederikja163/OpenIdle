using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Backend.Entities;

namespace Backend.Services;

public sealed class ProfileService
{
    private readonly ConcurrentDictionary<User, List<Profile>> _profileByUser = new();

    internal Profile[] GetProfiles(User user)
    {
        List<Profile> profiles = _profileByUser.GetOrAdd(user, _ => []);
        lock (profiles)
        {
            return [.. profiles];
        }
    }

    internal Profile GetProfile(User user, Guid profileId)
    {
        List<Profile> profiles = _profileByUser.GetOrAdd(user, _ => []);
        lock (profiles)
        {
            return profiles.FirstOrDefault(p => p.ProfileId == profileId)
                   ?? throw new InvalidOperationException("Profile does not belong to user.");
        }
    }

    internal Profile CreateProfile(User user, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Profile profile = new Profile()
        {
            ProfileId = Guid.NewGuid(),
            Name = name,
        };

        List<Profile> profiles = _profileByUser.GetOrAdd(user, _ => []);
        lock (profiles)
        {
            profiles.Add(profile);
        }

        return profile;
    }

    internal void SelectProfile(Socket socket, User user, Guid profileId)
    {
        socket.Profile = GetProfile(user, profileId);
    }
}