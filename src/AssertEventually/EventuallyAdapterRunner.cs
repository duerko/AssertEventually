namespace AssertEventually;

public static class EventuallyAdapterRunner
{
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
