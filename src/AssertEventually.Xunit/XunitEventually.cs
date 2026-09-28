using Xunit.Sdk;

namespace AssertEventually.Xunit;

public static class XunitEventually
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
            throw new XunitException(
                EventuallyReportFormatter.Format(exception.Report),
                exception);
        }
    }
}
