namespace AssertEventually;

/// <summary>Controls bounded history retention and polling behavior.</summary>
public sealed class EventuallyOptions
{
    /// <summary>Maximum number of attempts retained in the report.</summary>
    public int MaxRecordedAttempts { get; init; } = 100;

    /// <summary>Delay between attempts when not overridden on the execution.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(100);
}
