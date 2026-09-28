namespace AssertEventually;

internal sealed class DefaultEventuallyValueFormatter : IEventuallyValueFormatter
{
    public string Format(object? value)
    {
        return value?.ToString() ?? "null";
    }
}
