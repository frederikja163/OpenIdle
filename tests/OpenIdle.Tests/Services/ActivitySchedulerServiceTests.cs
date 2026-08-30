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
        FakeCompletion completion = new(Guid.NewGuid());

        service.StartEvent(completion, DateTime.UtcNow.AddMinutes(5));

        Assert.That(completion.CompleteCalled, Is.False);
    }

    [Test]
    public void StartEvent_MultipleActivities_AllEnqueued()
    {
        ActivitySchedulerService service = new();
        FakeCompletion a = new(Guid.NewGuid());
        FakeCompletion b = new(Guid.NewGuid());

        service.StartEvent(a, DateTime.UtcNow.AddMinutes(5));
        service.StartEvent(b, DateTime.UtcNow.AddMinutes(10));

        Assert.Multiple(() =>
        {
            Assert.That(a.CompleteCalled, Is.False);
            Assert.That(b.CompleteCalled, Is.False);
        });
    }

    [Test]
    public void RemoveEvent_ExistingActivity_RemovesIt()
    {
        ActivitySchedulerService service = new();
        FakeCompletion completion = new(Guid.NewGuid());
        service.StartEvent(completion, DateTime.UtcNow.AddMinutes(5));

        service.RemoveEvent(completion.ProfileId);

        service.NextEvent();
        Assert.That(completion.CompleteCalled, Is.False);
    }

    [Test]
    public void RemoveEvent_NonexistentActivity_DoesNotThrow()
    {
        ActivitySchedulerService service = new();

        Assert.DoesNotThrow(() => service.RemoveEvent(Guid.NewGuid()));
    }

    [Test]
    public void NextEvent_NoEvents_DoesNotThrow()
    {
        ActivitySchedulerService service = new();

        Assert.DoesNotThrow(() => service.NextEvent());
    }

    [Test]
    public void NextEvent_EventInFuture_DoesNotComplete()
    {
        ActivitySchedulerService service = new();
        FakeCompletion completion = new(Guid.NewGuid());
        service.StartEvent(completion, DateTime.UtcNow.AddMinutes(10));

        service.NextEvent();

        Assert.That(completion.CompleteCalled, Is.False);
    }

    [Test]
    public void NextEvent_EventReady_CompletesIt()
    {
        ActivitySchedulerService service = new();
        FakeCompletion completion = new(Guid.NewGuid());
        service.StartEvent(completion, DateTime.UtcNow.AddSeconds(-1));

        service.NextEvent();

        Assert.That(completion.CompleteCalled, Is.True);
    }

    [Test]
    public void NextEvent_MultipleReady_CompletesEarliestFirst()
    {
        ActivitySchedulerService service = new();
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        FakeCompletion first = new(id1);
        FakeCompletion second = new(id2);
        service.StartEvent(first, DateTime.UtcNow.AddSeconds(-2));
        service.StartEvent(second, DateTime.UtcNow.AddSeconds(-1));

        service.NextEvent();

        Assert.Multiple(() =>
        {
            Assert.That(first.CompleteCalled, Is.True);
            Assert.That(second.CompleteCalled, Is.False);
        });
    }

    [Test]
    public void NextEvent_CompleteThenNext_CompletesSecond()
    {
        ActivitySchedulerService service = new();
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        FakeCompletion first = new(id1);
        FakeCompletion second = new(id2);
        service.StartEvent(first, DateTime.UtcNow.AddSeconds(-2));
        service.StartEvent(second, DateTime.UtcNow.AddSeconds(-1));

        service.NextEvent();
        service.NextEvent();

        Assert.Multiple(() =>
        {
            Assert.That(first.CompleteCalled, Is.True);
            Assert.That(second.CompleteCalled, Is.True);
        });
    }

    [Test]
    public void RemoveEvent_BeforeNextEvent_PreventsCompletion()
    {
        ActivitySchedulerService service = new();
        FakeCompletion completion = new(Guid.NewGuid());
        service.StartEvent(completion, DateTime.UtcNow.AddSeconds(-1));

        service.RemoveEvent(completion.ProfileId);
        service.NextEvent();

        Assert.That(completion.CompleteCalled, Is.False);
    }

    [Test]
    public void NextEvent_RemovedEvent_SkipsToNext()
    {
        ActivitySchedulerService service = new();
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        FakeCompletion first = new(id1);
        FakeCompletion second = new(id2);
        service.StartEvent(first, DateTime.UtcNow.AddSeconds(-2));
        service.StartEvent(second, DateTime.UtcNow.AddSeconds(-1));

        service.RemoveEvent(id1);
        service.NextEvent();

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
        FakeCompletion a = new(id);
        FakeCompletion b = new(id);

        Assert.Multiple(() =>
        {
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void ActivityCompletion_DifferentProfileIds_AreNotEqual()
    {
        FakeCompletion a = new(Guid.NewGuid());
        FakeCompletion b = new(Guid.NewGuid());

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void ActivityCompletion_EqualsNull_ReturnsFalse()
    {
        FakeCompletion completion = new(Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(completion.Equals((ActivityCompletion?)null), Is.False);
            Assert.That(completion.Equals((object?)null), Is.False);
        });
    }

    [Test]
    public void ActivityCompletion_EqualsSameReference_ReturnsTrue()
    {
        FakeCompletion completion = new(Guid.NewGuid());

        Assert.That(completion.Equals(completion), Is.True);
    }

    [Test]
    public void ActivityCompletion_EqualsDifferentType_ReturnsFalse()
    {
        FakeCompletion completion = new(Guid.NewGuid());

        Assert.That(completion.Equals("not a completion"), Is.False);
    }

    private sealed class FakeCompletion(Guid profileId) : ActivityCompletion(profileId)
    {
        public bool CompleteCalled { get; private set; }

        public override void Complete()
        {
            CompleteCalled = true;
        }
    }
}
