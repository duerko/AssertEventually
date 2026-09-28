namespace AssertEventually;

public sealed class EventuallyAssertion<T>
{
    private readonly Func<T, Task> _assertion;

    internal EventuallyAssertion(Action<T> assertion)
        : this(value =>
        {
            assertion(value);
            return Task.CompletedTask;
        })
    {
    }

    internal EventuallyAssertion(Func<T, Task> assertion)
    {
        _assertion = assertion;
    }

    public EventuallyExecution<T> For(Func<T> observation)
    {
        return For(() => Task.FromResult(observation()));
    }

    public EventuallyExecution<T> For(
        Func<Task<T>> observation
    )
    {

        return new EventuallyExecution<T>(_assertion, observation);
    }
}