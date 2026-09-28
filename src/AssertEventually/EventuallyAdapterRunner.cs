namespace AssertEventually;

/// <summary>Runs adapter executions with shared timeout behavior.</summary>
public static class EventuallyAdapterRunner
{
    /// <summary>Runs an execution and invokes the timeout handler if needed.</summary>
    public static async Task RunAsync<T>(
        EventuallyExecution<T> execution,
        TimeSpan timeout,
        EventuallyOptions? options,
        CancellationToken cancellationToken,
        Func<EventuallyTimeoutException, Task> onTimeout)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(onTimeout);

        try
        {
            await execution
                .WithCancellation(cancellationToken)
                .Within(timeout, options)
                .ConfigureAwait(false);
        }
        catch (EventuallyTimeoutException exception)
        {
            await onTimeout(exception).ConfigureAwait(false);
        }
    }
}
