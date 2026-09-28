namespace AssertEventually;

/// <summary>Immutable diagnostics produced by an eventual execution.</summary>
public sealed class EventuallyExecutionReport
{
    internal EventuallyExecutionReport(
        TimeSpan timeout,
        TimeSpan duration,
        IReadOnlyList<EventuallyAttempt> attempts,
        int omittedAttemptCount,
        string? description)
    {
        Timeout = timeout;
        Duration = duration;
        Attempts = attempts;
        OmittedAttemptCount = omittedAttemptCount;
        Description = description;
    }

    /// <summary>Configured execution timeout.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Elapsed monotonic execution duration.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Optional assertion and observation description.</summary>
    public string? Description { get; }

    /// <summary>Bounded attempt history.</summary>
    public IReadOnlyList<EventuallyAttempt> Attempts { get; }

    /// <summary>Number of attempts omitted from the bounded history.</summary>
    public int OmittedAttemptCount { get; }

    /// <summary>Total number of attempts executed.</summary>
    public int AttemptCount => Attempts.Count + OmittedAttemptCount;

    /// <summary>Whether the final retained attempt succeeded.</summary>
    public bool Succeeded => LastAttempt?.Succeeded == true;

    /// <summary>The final retained attempt, if any.</summary>
    public EventuallyAttempt? LastAttempt => Attempts.Count == 0
        ? null
        : Attempts[^1];

    /// <summary>The value observed by the final retained attempt.</summary>
    public object? LastObservedValue => LastAttempt?.ObservedValue;

    /// <summary>The exception from the final retained attempt.</summary>
    public Exception? LastException => LastAttempt?.Exception;

    /// <summary>Groups repeated exceptions by type and message.</summary>
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

    /// <summary>Summarizes contiguous timeline behavior.</summary>
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
