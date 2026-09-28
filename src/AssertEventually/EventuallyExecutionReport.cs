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

    public int AttemptCount => Attempts.Count + OmittedAttemptCount;

    public bool Succeeded => LastAttempt?.Succeeded == true;

    public EventuallyAttempt? LastAttempt => Attempts.Count == 0
        ? null
        : Attempts[^1];

    public object? LastObservedValue => LastAttempt?.ObservedValue;

    public Exception? LastException => LastAttempt?.Exception;

    public IReadOnlyList<EventuallyExceptionGroup> ExceptionGroups =>
        Attempts
            .Where(attempt => attempt.Exception is not null)
            .GroupBy(attempt => new
            {
                Type = attempt.Exception!.GetType().FullName
                    ?? attempt.Exception.GetType().Name,
                attempt.Exception.Message
            })
            .Select(group => new EventuallyExceptionGroup(
                group.Key.Type,
                group.Key.Message,
                group.Count(),
                group.First().Elapsed,
                group.Last().Elapsed))
            .ToArray();

    public IReadOnlyList<EventuallyTimelineSegment> TimelineSegments =>
        CreateTimelineSegments(Attempts);

    private static IReadOnlyList<EventuallyTimelineSegment> CreateTimelineSegments(
        IReadOnlyList<EventuallyAttempt> attempts)
    {
        var segments = new List<EventuallyTimelineSegment>();
        EventuallyAttempt? first = null;
        var count = 0;
        var label = string.Empty;
        var lastElapsed = TimeSpan.Zero;

        foreach (var attempt in attempts)
        {
            var attemptLabel = attempt.Exception is null
                ? attempt.FormatObservedValue()
                : $"{attempt.Exception.GetType().FullName}: " +
                  attempt.Exception.Message +
                  $" | Observed: {attempt.FormatObservedValue()}";

            if (first is not null
                && (first.Kind != attempt.Kind || label != attemptLabel))
            {
                segments.Add(new EventuallyTimelineSegment(
                    first.Kind,
                    label,
                    count,
                    first.Elapsed,
                    lastElapsed));
                first = null;
            }

            if (first is null)
            {
                first = attempt;
                label = attemptLabel;
                count = 0;
            }

            count++;
            lastElapsed = attempt.Elapsed;
        }

        if (first is not null)
        {
            segments.Add(new EventuallyTimelineSegment(
                first.Kind,
                label,
                count,
                first.Elapsed,
                lastElapsed));
        }

        return segments;
    }
}
