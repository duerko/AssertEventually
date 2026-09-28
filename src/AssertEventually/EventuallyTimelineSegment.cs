namespace AssertEventually;

public sealed record EventuallyTimelineSegment(
    EventuallyAttemptKind Kind,
    string Label,
    int Count,
    TimeSpan FirstElapsed,
    TimeSpan LastElapsed);
