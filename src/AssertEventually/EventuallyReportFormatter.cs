using System.Text;

namespace AssertEventually;

public static class EventuallyReportFormatter
{
    public static string Format(
        EventuallyExecutionReport report,
        EventuallyReportFormattingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var formatter = new DefaultEventuallyValueFormatter(options);
        var builder = new StringBuilder();
        builder.AppendLine(
            report.Succeeded
                ? "AssertEventually passed"
                : "AssertEventually failed");
        builder.AppendLine();
        builder.AppendLine($"Timeout:  {report.Timeout}");
        builder.AppendLine($"Elapsed:  {report.Duration}");
        builder.AppendLine($"Attempts: {report.AttemptCount}");
        builder.AppendLine();
        builder.AppendLine("Timeline:");

        foreach (var attempt in report.Attempts)
        {
            builder.AppendLine(
                $"  {attempt.Elapsed}  {attempt.Kind}");

            if (attempt.Exception is not null)
            {
                builder.AppendLine(
                    $"         {attempt.Exception.GetType().Name}: " +
                    (options ?? new EventuallyReportFormattingOptions())
                        .Format(attempt.Exception.Message));
                builder.AppendLine(
                    $"         Observed: {formatter.Format(attempt.ObservedValue)}");
            }
            else
            {
                builder.AppendLine(
                    $"         {formatter.Format(attempt.ObservedValue)}");
            }
        }

        if (report.OmittedAttemptCount > 0)
        {
            builder.AppendLine(
                $"  ... {report.OmittedAttemptCount} attempt(s) omitted");
        }

        return builder.ToString().TrimEnd();
    }
}
