namespace AssertEventually;

/// <summary>Summarizes a contiguous run of equivalent attempts.</summary>
public sealed record EventuallyTimelineSegment(
    EventuallyAttemptKind Kind,
    string Label,
    int Count,
    TimeSpan FirstElapsed,
    TimeSpan LastElapsed);
