namespace AssertEventually;

public sealed class EventuallyExecutionReport
{
    internal EventuallyExecutionReport(
        TimeSpan timeout,
        TimeSpan duration,
        IReadOnlyList<EventuallyAttempt> attempts)
    {
        Timeout = timeout;
        Duration = duration;
        Attempts = attempts;
    }

    public TimeSpan Timeout { get; }

    public TimeSpan Duration { get; }

    public IReadOnlyList<EventuallyAttempt> Attempts { get; }

    public EventuallyAttempt? LastAttempt => Attempts.Count == 0
        ? null
        : Attempts[^1];
}
