namespace AssertEventually;

public sealed class EventuallyExecution<T>
{
    private readonly Func<T, Task> _assertion;
    private readonly Func<Task<T>> _observation;
    private readonly string? _description;
    private CancellationToken _cancellationToken;

    public EventuallyExecutionReport? Report { get; private set; }

    public EventuallyExecution<T> PollEvery(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Polling interval must be positive.");

        _pollInterval = interval;
        return this;
    }

    public EventuallyExecution<T> WithCancellation(
        CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return this;
    }

    internal EventuallyExecution(
        Func<T, Task> assertion,
        Func<Task<T>> observation,
        string? description)
    {
        _assertion = assertion;
        _observation = observation;
        _description = description;
    }

    private TimeSpan? _pollInterval;

    public async Task Within(
        TimeSpan timeout,
        EventuallyOptions? options = null)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        options ??= new EventuallyOptions();
        if (options.MaxRecordedAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxRecordedAttempts must be at least 1.");
        }

        var pollInterval = _pollInterval ?? options.PollInterval;
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "PollInterval must be positive.");
        }

        _cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var attempts = new List<EventuallyAttempt>();
        var omittedAttemptCount = 0;
        Exception? lastException = null;
        var attemptNumber = 0;

        while (stopwatch.Elapsed < timeout)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            attemptNumber++;
            var attemptStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var observationStopwatch = System.Diagnostics.Stopwatch.StartNew();
            T? value = default;

            try
            {
                value = await _observation().ConfigureAwait(false);
                _cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                RecordAttempt(
                    attempts,
                    options.MaxRecordedAttempts,
                    ref omittedAttemptCount,
                    new EventuallyAttempt(
                    attemptNumber,
                    stopwatch.Elapsed,
                    attemptStopwatch.Elapsed,
                    observationStopwatch.Elapsed,
                    TimeSpan.Zero,
                    EventuallyAttemptKind.ObservationException,
                    null,
                    ex));
                goto WaitForNextAttempt;
            }

            var assertionStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _assertion(value).ConfigureAwait(false);
                _cancellationToken.ThrowIfCancellationRequested();

                RecordAttempt(
                    attempts,
                    options.MaxRecordedAttempts,
                    ref omittedAttemptCount,
                    new EventuallyAttempt(
                    attemptNumber,
                    stopwatch.Elapsed,
                    attemptStopwatch.Elapsed,
                    observationStopwatch.Elapsed,
                    assertionStopwatch.Elapsed,
                    EventuallyAttemptKind.Success,
                    value,
                    null));
                Report = CreateReport(
                    timeout,
                    stopwatch.Elapsed,
                    attempts,
                    omittedAttemptCount);
                return;
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var assertionDuration = assertionStopwatch.Elapsed;
                lastException = ex;
                RecordAttempt(
                    attempts,
                    options.MaxRecordedAttempts,
                    ref omittedAttemptCount,
                    new EventuallyAttempt(
                    attemptNumber,
                    stopwatch.Elapsed,
                    attemptStopwatch.Elapsed,
                    observationStopwatch.Elapsed,
                    assertionDuration,
                    EventuallyAttemptKind.AssertionFailure,
                    value,
                    ex));
            }

        WaitForNextAttempt:
            var remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(
                remaining < pollInterval ? remaining : pollInterval,
                _cancellationToken).ConfigureAwait(false);
        }

        var report = CreateReport(
            timeout,
            stopwatch.Elapsed,
            attempts,
            omittedAttemptCount);
        Report = report;

        throw new EventuallyTimeoutException(
            $"The assertion did not pass within {timeout}.",
            lastException,
            report);
    }

    private EventuallyExecutionReport CreateReport(
        TimeSpan timeout,
        TimeSpan duration,
        List<EventuallyAttempt> attempts,
        int omittedAttemptCount)
    {
        return new EventuallyExecutionReport(
            timeout,
            duration,
            attempts.AsReadOnly(),
            omittedAttemptCount,
            _description);
    }

    private static void RecordAttempt(
        List<EventuallyAttempt> attempts,
        int maximum,
        ref int omittedAttemptCount,
        EventuallyAttempt attempt)
    {
        if (attempts.Count < maximum)
        {
            attempts.Add(attempt);
            return;
        }

        if (maximum > 1)
        {
            attempts.RemoveAt(1);
            attempts.Add(attempt);
        }
        else
        {
            attempts[^1] = attempt;
        }

        omittedAttemptCount++;
    }
}