using NUnit.Framework;

namespace AssertEventually.NUnit;

public static class NUnitEventually
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
            AttachArtifacts(exception.Report);
            Assert.Fail(EventuallyReportFormatter.Format(exception.Report));
        }
    }

    private static void AttachArtifacts(EventuallyExecutionReport report)
    {
        var artifacts = EventuallyReportArtifactWriter.Write(
            report,
            Path.Combine(
                Path.GetTempPath(),
                "AssertEventually",
                Guid.NewGuid().ToString("N")));
        TestContext.AddTestAttachment(artifacts.TextPath, "AssertEventually text report");
        TestContext.AddTestAttachment(artifacts.JsonPath, "AssertEventually JSON report");
        TestContext.AddTestAttachment(artifacts.HtmlPath, "AssertEventually HTML report");
    }
}
