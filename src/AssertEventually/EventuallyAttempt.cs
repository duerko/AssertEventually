namespace AssertEventually;

public sealed record EventuallyAttempt(
    int Number,
    TimeSpan Elapsed,
    Exception? Exception)
{
    public bool Succeeded => Exception is null;
}
