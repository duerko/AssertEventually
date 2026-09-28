namespace AssertEventually;

public sealed class EventuallyExecution<T>
{
    private readonly Func<T, Task> _assertion;
    private readonly Func<Task<T>> _observation;

    public EventuallyExecutionReport? Report { get; private set; }

    internal EventuallyExecution(
        Func<T, Task> assertion,
        Func<Task<T>> observation)
    {
        _assertion = assertion;
        _observation = observation;
    }

    public async Task Within(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var attempts = new List<EventuallyAttempt>();
        Exception? lastException = null;

        while (stopwatch.Elapsed < timeout)
        {
            var attemptNumber = attempts.Count + 1;

            try
            {
                var value = await _observation();

                await _assertion(value);

                attempts.Add(new EventuallyAttempt(
                    attemptNumber,
                    stopwatch.Elapsed,
                    null));
                Report = CreateReport(timeout, stopwatch.Elapsed, attempts);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempts.Add(new EventuallyAttempt(
                    attemptNumber,
                    stopwatch.Elapsed,
                    ex));
            }

            var remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(
                TimeSpan.FromMilliseconds(Math.Min(100, remaining.TotalMilliseconds)));
        }

        var report = CreateReport(timeout, stopwatch.Elapsed, attempts);
        Report = report;

        throw new EventuallyTimeoutException(
            $"The assertion did not pass within {timeout}.",
            lastException,
            report);
    }

    private static EventuallyExecutionReport CreateReport(
        TimeSpan timeout,
        TimeSpan duration,
        List<EventuallyAttempt> attempts)
    {
        return new EventuallyExecutionReport(
            timeout,
            duration,
            attempts.AsReadOnly());
    }
}