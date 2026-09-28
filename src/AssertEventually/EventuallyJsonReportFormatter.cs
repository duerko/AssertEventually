using System.Text.Json;

namespace AssertEventually;

public static class EventuallyJsonReportFormatter
{
    public static string Format(
        EventuallyExecutionReport report,
        IEventuallyValueFormatter? valueFormatter = null,
        EventuallyReportFormattingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var formatter = valueFormatter
            ?? new DefaultEventuallyValueFormatter(options);
        var reportOptions = options ?? new EventuallyReportFormattingOptions();
        var payload = new
        {
            successful = report.Succeeded,
            timeout = report.Timeout.ToString(),
            elapsed = report.Duration.ToString(),
            attempts = report.AttemptCount,
            omittedAttempts = report.OmittedAttemptCount,
            attemptHistory = report.Attempts.Select(attempt => new
            {
                number = attempt.Number,
                elapsed = attempt.Elapsed.ToString(),
                duration = attempt.Duration.ToString(),
                observationDuration = attempt.ObservationDuration.ToString(),
                assertionDuration = attempt.AssertionDuration.ToString(),
                kind = attempt.Kind.ToString(),
                observedValue = formatter.Format(attempt.ObservedValue),
                exception = attempt.Exception is null
                    ? null
                    : new
                    {
                        type = attempt.Exception.GetType().FullName,
                        message = reportOptions.Format(attempt.Exception.Message)
                    }
            })
        };

        return JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions { WriteIndented = true });
    }
}
