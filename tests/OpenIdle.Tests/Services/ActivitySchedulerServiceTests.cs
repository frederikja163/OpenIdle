using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Services;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class ActivitySchedulerServiceTests
{
    [Test]
    public void StartEvent_EnqueuesActivity()
    {
        ActivitySchedulerService service = new();
        FakeCompletion completion = new(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5));

        service.StartEvent(completion);

        Assert.That(completion.CompleteCalled, Is.False);
    }

    [Test]
    public void StartEvent_MultipleActivities_AllEnqueued()
    {
        ActivitySchedulerService service = new();
        FakeCompletion a = new(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5));
        FakeCompletion b = new(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(10));

        service.StartEvent(a);
        service.StartEvent(b);

        Assert.Multiple(() =>
        {
            Assert.That(a.CompleteCalled, Is.False);
            Assert.That(b.CompleteCalled, Is.False);
        });
    }

    [Test]
    public async Task RemoveEvent_ExistingActivity_RemovesIt()
    {
        ActivitySchedulerService service = new();
        FakeCompletion completion = new(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5));
        service.StartEvent(completion);

        service.RemoveEvent(completion.ProfileId);

        await service.NextEvent();
        Assert.That(completion.CompleteCalled, Is.False);
    }

    [Test]
    public void RemoveEvent_NonexistentActivity_DoesNotThrow()
    {
        ActivitySchedulerService service = new();

        Assert.DoesNotThrow(() => service.RemoveEvent(Guid.NewGuid()));
    }

    [Test]
    public async Task NextEvent_NoEvents_DoesNotThrow()
    {
        ActivitySchedulerService service = new();

        await service.NextEvent();
    }

    [Test]
    public async Task NextEvent_EventInFuture_DoesNotComplete()
    {
        ActivitySchedulerService service = new();
        FakeCompletion completion = new(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(10));
        service.StartEvent(completion);

        await service.NextEvent();

        Assert.That(completion.CompleteCalled, Is.False);
    }

    [Test]
    public async Task NextEvent_EventReady_CompletesIt()
    {
        ActivitySchedulerService service = new();
        FakeCompletion completion = new(Guid.NewGuid(), DateTime.UtcNow.AddSeconds(-1));
        service.StartEvent(completion);

        await service.NextEvent();

        Assert.That(completion.CompleteCalled, Is.True);
    }

    [Test]
    public async Task NextEvent_MultipleReady_CompletesEarliestFirst()
    {
        ActivitySchedulerService service = new();
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        FakeCompletion first = new(id1, DateTime.UtcNow.AddSeconds(-2));
        FakeCompletion second = new(id2, DateTime.UtcNow.AddSeconds(-1));
        service.StartEvent(first);
        service.StartEvent(second);

        await service.NextEvent();

        Assert.Multiple(() =>
        {
            Assert.That(first.CompleteCalled, Is.True);
            Assert.That(second.CompleteCalled, Is.False);
        });
    }

    [Test]
    public async Task NextEvent_CompleteThenNext_CompletesSecond()
    {
        ActivitySchedulerService service = new();
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        FakeCompletion first = new(id1, DateTime.UtcNow.AddSeconds(-2));
        FakeCompletion second = new(id2, DateTime.UtcNow.AddSeconds(-1));
        service.StartEvent(first);
        service.StartEvent(second);

        await service.NextEvent();
        await service.NextEvent();

        Assert.Multiple(() =>
        {
            Assert.That(first.CompleteCalled, Is.True);
            Assert.That(second.CompleteCalled, Is.True);
        });
    }

    [Test]
    public async Task RemoveEvent_BeforeNextEvent_PreventsCompletion()
    {
        ActivitySchedulerService service = new();
        FakeCompletion completion = new(Guid.NewGuid(), DateTime.UtcNow.AddSeconds(-1));
        service.StartEvent(completion);

        service.RemoveEvent(completion.ProfileId);
        await service.NextEvent();

        Assert.That(completion.CompleteCalled, Is.False);
    }

    [Test]
    public async Task NextEvent_RemovedEvent_SkipsToNext()
    {
        ActivitySchedulerService service = new();
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        FakeCompletion first = new(id1, DateTime.UtcNow.AddSeconds(-2));
        FakeCompletion second = new(id2, DateTime.UtcNow.AddSeconds(-1));
        service.StartEvent(first);
        service.StartEvent(second);

        service.RemoveEvent(id1);
        await service.NextEvent();

        Assert.Multiple(() =>
        {
            Assert.That(first.CompleteCalled, Is.False);
            Assert.That(second.CompleteCalled, Is.True);
        });
    }

    [Test]
    public void ActivityCompletion_EqualProfileIds_AreEqual()
    {
        Guid id = Guid.NewGuid();
        DateTime endTime = DateTime.UtcNow;
        FakeCompletion a = new(id, endTime);
        FakeCompletion b = new(id, endTime);

        Assert.Multiple(() =>
        {
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void ActivityCompletion_DifferentProfileIds_AreNotEqual()
    {
        DateTime endTime = DateTime.UtcNow;
        FakeCompletion a = new(Guid.NewGuid(), endTime);
        FakeCompletion b = new(Guid.NewGuid(), endTime);

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void ActivityCompletion_EqualsNull_ReturnsFalse()
    {
        FakeCompletion completion = new(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(completion.Equals((ActivityCompletion?)null), Is.False);
            Assert.That(completion.Equals((object?)null), Is.False);
        });
    }

    [Test]
    public void ActivityCompletion_EqualsSameReference_ReturnsTrue()
    {
        FakeCompletion completion = new(Guid.NewGuid(), DateTime.UtcNow);

        Assert.That(completion.Equals(completion), Is.True);
    }

    [Test]
    public void ActivityCompletion_EqualsDifferentType_ReturnsFalse()
    {
        FakeCompletion completion = new(Guid.NewGuid(), DateTime.UtcNow);

        Assert.That(completion.Equals("not a completion"), Is.False);
    }

    private sealed class FakeCompletion(Guid profileId, DateTime endTime) : ActivityCompletion(profileId, endTime)
    {
        public bool CompleteCalled { get; private set; }

        public override Task Complete()
        {
            CompleteCalled = true;
            return Task.CompletedTask;
        }
    }
}
