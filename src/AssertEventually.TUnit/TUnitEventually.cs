namespace AssertEventually.TUnit;

public static class TUnitEventually
{
    public static async Task AssertAsync<T>(
        EventuallyExecution<T> execution,
        TimeSpan timeout,
        EventuallyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);

        try
        {
            await execution
                .WithCancellation(cancellationToken)
                .Within(timeout, options)
                .ConfigureAwait(false);
        }
        catch (EventuallyTimeoutException exception)
        {
            throw new TUnitEventuallyException(
                EventuallyReportFormatter.Format(exception.Report),
                exception);
        }
    }
}

public sealed class TUnitEventuallyException : Exception
{
    internal TUnitEventuallyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
