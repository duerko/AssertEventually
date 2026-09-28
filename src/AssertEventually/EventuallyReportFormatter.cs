using System.Text;

namespace AssertEventually;

public static class EventuallyReportFormatter
{
    public static string Format(EventuallyExecutionReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

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
                    attempt.Exception.Message);
            }
            else
            {
                builder.AppendLine(
                    $"         {attempt.FormatObservedValue()}");
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
