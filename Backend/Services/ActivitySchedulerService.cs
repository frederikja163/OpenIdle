using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Services;

public abstract class ActivityCompletion(Guid profileId, TimeSpan duration) : IEquatable<ActivityCompletion>
{
    public ProfileId ProfileId { get; } = profileId;
    public TimeSpan Duration { get; } = duration;
    public abstract Task Complete(DateTime endTime);

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
    private TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PriorityQueue<ProfileId, DateTime> _priorityQueue = new();

    public void StartEvent(ActivityCompletion activityCompletion, DateTime startTime)
    {
        lock (_lock)
        {
            DateTime endTime = startTime + activityCompletion.Duration;
            bool isNewEarliest = _priorityQueue.Count == 0;
            if (!isNewEarliest && _priorityQueue.TryPeek(out _, out DateTime currentEndTime))
            {
                isNewEarliest = endTime < currentEndTime;
            }

            _priorityQueue.Enqueue(activityCompletion.ProfileId, endTime);
            _activityMap[activityCompletion.ProfileId] = activityCompletion;

            if (isNewEarliest)
            {
                _signal.TrySetResult();
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

    public async Task NextEvent()
    {
        ActivityCompletion? activityCompletion;
        DateTime endTime;
        lock (_lock)
        {
            if (!_priorityQueue.TryPeek(out ProfileId profileId, out endTime) || DateTime.UtcNow < endTime)
            {
                return;
            }

            profileId = _priorityQueue.Dequeue();
            
            if (!_activityMap.TryGetValue(profileId, out activityCompletion))
            {
                return;
            }
            StartEvent(activityCompletion, endTime);
        }

        await activityCompletion.Complete(endTime);
    }

    public async Task WaitForNextEvent(CancellationToken cancellationToken = default)
    {
        TimeSpan? waitDuration;
        lock (_lock)
        {
            _signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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

        using CancellationTokenSource timeout = new(waitDuration.Value);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        await _signal.Task.WaitAsync(linked.Token);
    }
}
