namespace AssertEventually;

public sealed record EventuallyExceptionGroup(
    string Type,
    string Message,
    int Count,
    TimeSpan FirstElapsed,
    TimeSpan LastElapsed);
