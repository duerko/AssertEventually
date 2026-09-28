namespace AssertEventually;

/// <summary>Aggregates repeated exceptions in an execution report.</summary>
public sealed record EventuallyExceptionGroup(
    string Type,
    string Message,
    int Count,
    TimeSpan FirstElapsed,
    TimeSpan LastElapsed);
