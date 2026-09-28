using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssertEventually.MSTest;

/// <summary>Provides MSTest integration for AssertEventually.</summary>
public static class MSTestEventually
{
    /// <summary>Runs an eventual assertion through MSTest.</summary>
    public static async Task AssertAsync<T>(
        EventuallyExecution<T> execution,
        TimeSpan timeout,
        EventuallyOptions? options = null,
        CancellationToken cancellationToken = default,
        TestContext? testContext = null)
    {
        await EventuallyAdapterRunner.RunAsync(
            execution,
            timeout,
            options,
            cancellationToken,
            exception =>
            {
                AttachArtifacts(exception.Report, testContext);
                Assert.Fail(EventuallyReportFormatter.Format(exception.Report));
                return Task.CompletedTask;
            });
    }

    private static void AttachArtifacts(
        EventuallyExecutionReport report,
        TestContext? testContext)
    {
        if (testContext?.ResultsDirectory is not { } resultsDirectory)
            return;

        var artifacts = EventuallyReportArtifactWriter.Write(
            report,
            Path.Combine(
                resultsDirectory,
                "assert-eventually"));
        testContext.AddResultFile(artifacts.TextPath);
        testContext.AddResultFile(artifacts.JsonPath);
        testContext.AddResultFile(artifacts.HtmlPath);
    }
}
