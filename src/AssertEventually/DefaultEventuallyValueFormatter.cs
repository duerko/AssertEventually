namespace AssertEventually;

internal sealed class DefaultEventuallyValueFormatter : IEventuallyValueFormatter
{
    private readonly EventuallyReportFormattingOptions _options;

    public DefaultEventuallyValueFormatter(
        EventuallyReportFormattingOptions? options = null)
    {
        _options = options ?? new EventuallyReportFormattingOptions();
    }

    public string Format(object? value)
    {
        return _options.Format(value?.ToString() ?? "null");
    }
}
