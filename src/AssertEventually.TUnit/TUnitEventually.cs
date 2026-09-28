namespace AssertEventually.TUnit;

public static class TUnitEventually
{
    public static async Task AssertAsync<T>(
        EventuallyExecution<T> execution,
        TimeSpan timeout,
        EventuallyOptions? options = null,
        CancellationToken cancellationToken = default,
        global::TUnit.Core.TestContext? testContext = null)
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
            var context = testContext ?? global::TUnit.Core.TestContext.Current;
            if (context is not null)
            {
                var artifacts = EventuallyReportArtifactWriter.Write(
                    exception.Report,
                    Path.Combine(
                        Path.GetTempPath(),
                        "AssertEventually",
                        Guid.NewGuid().ToString("N")));
                context.Output.AttachArtifact(
                    artifacts.TextPath,
                    "AssertEventually text report",
                    "Convergence timeline");
                context.Output.AttachArtifact(
                    artifacts.JsonPath,
                    "AssertEventually JSON report",
                    "Machine-readable convergence timeline");
                context.Output.AttachArtifact(
                    artifacts.HtmlPath,
                    "AssertEventually HTML report",
                    "Visual convergence timeline");
            }

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
