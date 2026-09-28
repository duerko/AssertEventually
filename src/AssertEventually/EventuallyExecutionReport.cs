namespace AssertEventually;

public sealed class EventuallyExecutionReport
{
    internal EventuallyExecutionReport(
        TimeSpan timeout,
        TimeSpan duration,
        IReadOnlyList<EventuallyAttempt> attempts,
        int omittedAttemptCount)
    {
        Timeout = timeout;
        Duration = duration;
        Attempts = attempts;
        OmittedAttemptCount = omittedAttemptCount;
    }

    public TimeSpan Timeout { get; }

    public TimeSpan Duration { get; }

    public IReadOnlyList<EventuallyAttempt> Attempts { get; }

    public int OmittedAttemptCount { get; }

    public EventuallyAttempt? LastAttempt => Attempts.Count == 0
        ? null
        : Attempts[^1];
}
