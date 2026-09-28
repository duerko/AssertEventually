namespace AssertEventually;

/// <summary>Indicates that an eventual assertion did not succeed in time.</summary>
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

    /// <summary>Diagnostic report for the timed-out execution.</summary>
    public EventuallyExecutionReport Report { get; }
}
