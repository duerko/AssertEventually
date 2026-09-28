namespace AssertEventually;

public sealed record EventuallyAttempt(
    int Number,
    TimeSpan Elapsed,
    TimeSpan Duration,
    EventuallyAttemptKind Kind,
    object? ObservedValue,
    Exception? Exception)
{
    public bool Succeeded => Kind == EventuallyAttemptKind.Success;

    public string FormatObservedValue(IEventuallyValueFormatter? formatter = null)
    {
        return (formatter ?? new DefaultEventuallyValueFormatter())
            .Format(ObservedValue);
    }
}
