using System;
using System.Threading.Tasks;

namespace Backend.Extensions;

internal static class AsyncEventHandlerExtensions
{
    internal static async Task InvokeAsync<TEventArgs>(this AsyncEventHandler<TEventArgs>? handler, object? sender, TEventArgs e)
    {
        if (handler is null)
        {
            return;
        }

        Delegate[] delegates = handler.GetInvocationList();
        Task[] tasks = new Task[delegates.Length];
        for (int i = 0; i < delegates.Length; i++)
        {
            tasks[i] = ((AsyncEventHandler<TEventArgs>)delegates[i])(sender, e);
        }

        await Task.WhenAll(tasks);
    }
}
