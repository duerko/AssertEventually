using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssertEventually.MSTest;

public static class MSTestEventually
{
    public static async Task AssertAsync<T>(
        EventuallyExecution<T> execution,
        TimeSpan timeout,
        EventuallyOptions? options = null,
        CancellationToken cancellationToken = default,
        TestContext? testContext = null)
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
            AttachArtifacts(exception.Report, testContext);
            Assert.Fail(EventuallyReportFormatter.Format(exception.Report));
        }
    }

    private static void AttachArtifacts(
        EventuallyExecutionReport report,
        TestContext? testContext)
    {
        if (testContext is null)
            return;

        var artifacts = EventuallyReportArtifactWriter.Write(
            report,
            Path.Combine(
                testContext.ResultsDirectory,
                "assert-eventually"));
        testContext.AddResultFile(artifacts.TextPath);
        testContext.AddResultFile(artifacts.JsonPath);
        testContext.AddResultFile(artifacts.HtmlPath);
    }
}
