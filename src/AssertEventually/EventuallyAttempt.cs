namespace AssertEventually;

/// <summary>Diagnostics for one observation and assertion attempt.</summary>
public sealed record EventuallyAttempt(
    int Number,
    TimeSpan Elapsed,
    TimeSpan Duration,
    TimeSpan ObservationDuration,
    TimeSpan AssertionDuration,
    EventuallyAttemptKind Kind,
    object? ObservedValue,
    Exception? Exception)
{
    /// <summary>Whether this attempt passed the assertion.</summary>
    public bool Succeeded => Kind == EventuallyAttemptKind.Success;

    /// <summary>Formats the observed value with a bounded formatter.</summary>
    public string FormatObservedValue(IEventuallyValueFormatter? formatter = null)
    {
        return (formatter ?? new DefaultEventuallyValueFormatter())
            .Format(ObservedValue);
    }
}
