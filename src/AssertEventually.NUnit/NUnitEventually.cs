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
        await EventuallyAdapterRunner.RunAsync(
            execution,
            timeout,
            options,
            cancellationToken,
            exception =>
            {
                AttachArtifacts(exception.Report);
                Assert.Fail(EventuallyReportFormatter.Format(exception.Report));
                return Task.CompletedTask;
            });
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
