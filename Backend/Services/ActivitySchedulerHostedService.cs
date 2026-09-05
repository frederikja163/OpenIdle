using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Backend.Services;

internal sealed class ActivitySchedulerHostedService(ActivitySchedulerService scheduler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await scheduler.WaitForNextEvent(stoppingToken);
                await scheduler.NextEvent();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }
    }
}
