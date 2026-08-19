using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Services;

internal abstract class ActivityCompletion(Guid profileId) : IEquatable<ActivityCompletion>
{
    public ProfileId ProfileId { get; } = profileId;
    public abstract void Complete();

    public bool Equals(ActivityCompletion? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ProfileId.Equals(other.ProfileId);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ActivityCompletion)obj);
    }

    public override int GetHashCode()
    {
        return ProfileId.GetHashCode();
    }
}

internal sealed class ActivityStartService
{
    private readonly object _lock = new object();
    private readonly Dictionary<ProfileId, ActivityCompletion> _activityMap = new();
    private readonly ManualResetEvent _resetEvent = new(true);
    private readonly PriorityQueue<ProfileId, DateTime> _priorityQueue = new();

    public void StartEvent(ActivityCompletion activityCompletion, DateTime endTime)
    {
        lock (_lock)
        {
            _priorityQueue.Enqueue(activityCompletion.ProfileId, endTime);
            _activityMap.Add(activityCompletion.ProfileId, activityCompletion);
            if (!_priorityQueue.TryPeek(out ProfileId _, out endTime))
            {
                _resetEvent.Set();
            }
        }
    }

    public void RemoveEvent(ProfileId profileId)
    {
        lock (_lock)
        {
            if (!_activityMap.Remove(profileId, out ActivityCompletion? _))
                return;
            _priorityQueue.Remove(profileId, out _, out _);
        }
    }

    public void NextEvent()
    {
        WaitNext();

        ActivityCompletion? activityCompletion;
        lock (_lock)
        {
            if (!_priorityQueue.TryPeek(out ProfileId profileId, out DateTime endTime) || DateTime.Now < endTime)
            {
                return;
            }

            profileId = _priorityQueue.Dequeue();
            if (!_activityMap.Remove(profileId, out activityCompletion))
            {
                return;
            }
        }
        activityCompletion.Complete();
    }

    private void WaitNext()
    {
        DateTime endTime;
        lock (_lock)
        {
            if (!_priorityQueue.TryPeek(out ProfileId _, out endTime))
            {
                _resetEvent.WaitOne(TimeSpan.FromSeconds(1));
                return;
            }
        }

        TimeSpan remaining = endTime - DateTime.Now;
        if (remaining <= TimeSpan.Zero || !_resetEvent.WaitOne(remaining))
        {
            return;
        }
    }
}