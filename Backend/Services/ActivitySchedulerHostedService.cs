using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Backend.Services;

internal sealed class ActivitySchedulerHostedService(ActivitySchedulerService scheduler) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await scheduler.NextEvent();
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                }
            }
        }, CancellationToken.None);
    }
}
