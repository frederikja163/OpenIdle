using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Services;

public abstract class ActivityCompletion(Guid profileId) : IEquatable<ActivityCompletion>
{
    public ProfileId ProfileId { get; } = profileId;
    public abstract Task Complete();

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

public sealed class ActivitySchedulerService
{
    private readonly object _lock = new object();
    private readonly Dictionary<ProfileId, ActivityCompletion> _activityMap = new();
    private readonly ManualResetEvent _resetEvent = new(true);
    private readonly PriorityQueue<ProfileId, DateTime> _priorityQueue = new();

    public void StartEvent(ActivityCompletion activityCompletion, DateTime endTime)
    {
        lock (_lock)
        {
            bool isNewEarliest = _priorityQueue.Count == 0;
            if (!isNewEarliest && _priorityQueue.TryPeek(out _, out DateTime currentEndTime))
            {
                isNewEarliest = endTime < currentEndTime;
            }

            _priorityQueue.Enqueue(activityCompletion.ProfileId, endTime);
            _activityMap.Add(activityCompletion.ProfileId, activityCompletion);

            // Wake a background waiter that may be sleeping until a later event: the new event
            // is earlier, so it should re-check now instead of sleeping to the old end time.
            if (isNewEarliest)
            {
                _resetEvent.Reset();
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

    /// <summary>
    /// A single non-blocking poll: completes the earliest event whose end time has passed, if any.
    /// Returns whether an event was processed. The background loop calls this after
    /// <see cref="WaitForNextEvent"/> so it does not busy-spin.
    /// </summary>
    public async Task<bool> NextEvent()
    {
        ActivityCompletion? activityCompletion;
        lock (_lock)
        {
            if (!_priorityQueue.TryPeek(out ProfileId profileId, out DateTime endTime) || DateTime.UtcNow < endTime)
            {
                return false;
            }

            profileId = _priorityQueue.Dequeue();
            if (!_activityMap.Remove(profileId, out activityCompletion))
            {
                return false;
            }
        }

        await activityCompletion.Complete();
        return true;
    }

    /// <summary>
    /// Blocks until the earliest scheduled event is due, or a new earlier event is added via
    /// <see cref="StartEvent"/> (whichever comes first). Returns promptly — at most the idle poll
    /// interval — so the caller can react to shutdown and newly-arriving events.
    /// </summary>
    public void WaitForNextEvent()
    {
        TimeSpan? waitDuration;
        lock (_lock)
        {
            _resetEvent.Reset();
            if (!_priorityQueue.TryPeek(out _, out DateTime endTime))
            {
                waitDuration = TimeSpan.FromSeconds(1);
            }
            else
            {
                TimeSpan remaining = endTime - DateTime.UtcNow;
                waitDuration = remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
        }

        if (waitDuration is null || waitDuration == TimeSpan.Zero)
        {
            return;
        }

        _resetEvent.WaitOne(waitDuration.Value);
    }
}