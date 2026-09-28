namespace AssertEventually;

public sealed class EventuallyTimeoutException : TimeoutException
{
    internal EventuallyTimeoutException(
        string message,
        Exception? innerException,
        EventuallyExecutionReport report)
        : base(message, innerException)
    {
        Report = report;
    }

    public EventuallyExecutionReport Report { get; }
}
