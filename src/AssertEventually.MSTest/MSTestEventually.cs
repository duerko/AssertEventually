using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssertEventually.MSTest;

public static class MSTestEventually
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
            Assert.Fail(EventuallyReportFormatter.Format(exception.Report));
        }
    }
}
