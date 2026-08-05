using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend.Extensions;

public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);

public static class AsyncEventHandlerExtensions
{
    extension<T>(AsyncEventHandler<T> extensions)
    {
        internal async Task InvokeAsync(object? sender, T e)
        {
            await Task.WhenAll(extensions.GetInvocationList().OfType<AsyncEventHandler<T>>().Select(d => d.Invoke(sender, e)));
        }
    }
}