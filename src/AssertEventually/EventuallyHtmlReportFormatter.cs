using System.Net;
using System.Text;

namespace AssertEventually;

/// <summary>Formats execution reports as standalone HTML.</summary>
public static class EventuallyHtmlReportFormatter
{
    /// <summary>Formats a report.</summary>
    public static string Format(
        EventuallyExecutionReport report,
        EventuallyReportFormattingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var status = report.Succeeded ? "PASSED" : "FAILED";
        var formatter = new DefaultEventuallyValueFormatter(options);
        var reportOptions = options ?? new EventuallyReportFormattingOptions();
        var builder = new StringBuilder("""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>AssertEventually report</title>
              <style>
                body { font-family: sans-serif; margin: 2rem; }
                .passed { color: #176b2c; }
                .failed { color: #a11; }
                table { border-collapse: collapse; width: 100%; }
                th, td { border: 1px solid #ccc; padding: .5rem; text-align: left; }
              </style>
            </head>
            <body>
            """);

        builder.Append("<h1>AssertEventually</h1>");
        builder.Append(
            $"<h2 class=\"{status.ToLowerInvariant()}\">{status}</h2>");
        builder.Append("<dl>");
        AppendDefinition(builder, "Timeout", report.Timeout.ToString());
        AppendDefinition(builder, "Elapsed", report.Duration.ToString());
        AppendDefinition(builder, "Attempts", report.AttemptCount.ToString());
        if (report.Description is not null)
            AppendDefinition(builder, "Description", report.Description);
        builder.Append("</dl>");
        builder.Append("<h2>Timeline</h2><table><thead><tr>");
        builder.Append("<th>#</th><th>Elapsed</th><th>Kind</th><th>Details</th>");
        builder.Append("</tr></thead><tbody>");

        foreach (var attempt in report.Attempts)
        {
            var details = attempt.Exception is null
                ? formatter.Format(attempt.ObservedValue)
                : $"{attempt.Exception.GetType().Name}: " +
                  reportOptions.Format(attempt.Exception.Message);
            builder.Append("<tr><td>")
                .Append(attempt.Number)
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(attempt.Elapsed.ToString()))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(attempt.Kind.ToString()))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(details))
                .Append("</td></tr>");
        }

        builder.Append("</tbody></table>");
        if (report.OmittedAttemptCount > 0)
        {
            builder.Append("<p>")
                .Append(report.OmittedAttemptCount)
                .Append(" attempt(s) omitted.</p>");
        }

        builder.Append("</body></html>");
        return builder.ToString();
    }

    private static void AppendDefinition(
        StringBuilder builder,
        string name,
        string value)
    {
        builder.Append("<dt>")
            .Append(WebUtility.HtmlEncode(name))
            .Append("</dt><dd>")
            .Append(WebUtility.HtmlEncode(value))
            .Append("</dd>");
    }
}
