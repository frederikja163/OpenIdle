using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Services;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class ActivitySchedulerServiceTests
{
    [Test]
    public async Task WaitForNextEvent_InternalTimeout_ReturnsWithoutThrowing()
    {
        ActivitySchedulerService scheduler = new();
        TestCompletion completion = new();
        scheduler.StartEvent(completion, DateTime.UtcNow);

        await scheduler.WaitForNextEvent(CancellationToken.None);

        Assert.That(completion.Completed, Is.EqualTo(0));
    }

    [Test]
    public async Task NextEvent_AfterInternalTimeout_CompletesDueEvent()
    {
        ActivitySchedulerService scheduler = new();
        TestCompletion completion = new();
        scheduler.StartEvent(completion, DateTime.UtcNow);

        await scheduler.WaitForNextEvent(CancellationToken.None);
        await scheduler.NextEvent();

        Assert.That(completion.Completed, Is.EqualTo(1));
    }

    [Test]
    public void WaitForNextEvent_CallerCancellation_ThrowsOperationCanceledException()
    {
        ActivitySchedulerService scheduler = new();
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(50));

        Assert.That(
            async () => await scheduler.WaitForNextEvent(cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    private sealed class TestCompletion : ActivityCompletion
    {
        public TestCompletion() : base(Guid.NewGuid(), TimeSpan.FromMilliseconds(50)) { }

        public int Completed { get; private set; }

        public override Task Complete(DateTime endTime)
        {
            Completed++;
            return Task.CompletedTask;
        }
    }
}