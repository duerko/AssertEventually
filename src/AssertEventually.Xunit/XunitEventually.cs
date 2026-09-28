using global::Xunit.Sdk;

namespace AssertEventually.Xunit;

public static class XunitEventually
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
                throw new XunitException(
                    EventuallyReportFormatter.Format(exception.Report),
                    exception);
            });
    }

    private static void AttachArtifacts(EventuallyExecutionReport report)
    {
        var context = global::Xunit.v3.TestContextAccessor.Instance.Current;
        if (context is null)
            return;

        var artifacts = EventuallyReportArtifactWriter.Write(
            report,
            Path.Combine(
                Path.GetTempPath(),
                "AssertEventually",
                Guid.NewGuid().ToString("N")));
        context.AddAttachment(
            "assert-eventually.txt",
            File.ReadAllText(artifacts.TextPath));
        context.AddAttachment(
            "assert-eventually.json",
            File.ReadAllText(artifacts.JsonPath));
        context.AddAttachment(
            "assert-eventually.html",
            File.ReadAllText(artifacts.HtmlPath));
    }
}
