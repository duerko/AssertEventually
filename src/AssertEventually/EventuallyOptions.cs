namespace AssertEventually;

public sealed class EventuallyOptions
{
    public int MaxRecordedAttempts { get; init; } = 100;

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(100);
}
