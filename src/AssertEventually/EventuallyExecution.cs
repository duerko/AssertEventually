namespace AssertEventually;

public sealed class EventuallyExecution<T>
{
    private readonly Action<T> _assertion;
    private readonly Func<Task<T>> _observation;

    internal EventuallyExecution(
        Action<T> assertion,
        Func<Task<T>> observation)
    {
        _assertion = assertion;
        _observation = observation;
    }

    public async Task Within(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var value = await _observation();

                _assertion(value);

                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"The assertion did not pass within {timeout}.",
            lastException);
    }
}