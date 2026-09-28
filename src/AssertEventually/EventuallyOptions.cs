namespace AssertEventually;

public sealed class EventuallyOptions
{
    public int MaxRecordedAttempts { get; init; } = 100;
}
