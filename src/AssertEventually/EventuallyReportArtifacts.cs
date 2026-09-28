namespace AssertEventually;

/// <summary>Paths to the generated execution report artifacts.</summary>
public sealed record EventuallyReportArtifacts(
    string TextPath,
    string JsonPath,
    string HtmlPath);

/// <summary>Writes text, JSON, and HTML execution report artifacts.</summary>
public static class EventuallyReportArtifactWriter
{
    /// <summary>Writes report artifacts to a directory.</summary>
    public static EventuallyReportArtifacts Write(
        EventuallyExecutionReport report,
        string directory,
        EventuallyReportFormattingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);

        var artifacts = new EventuallyReportArtifacts(
            Path.Combine(directory, "report.txt"),
            Path.Combine(directory, "report.json"),
            Path.Combine(directory, "report.html"));

        File.WriteAllText(
            artifacts.TextPath,
            EventuallyReportFormatter.Format(report, options));
        File.WriteAllText(
            artifacts.JsonPath,
            EventuallyJsonReportFormatter.Format(report, options: options));
        File.WriteAllText(
            artifacts.HtmlPath,
            EventuallyHtmlReportFormatter.Format(report, options));

        return artifacts;
    }
}
