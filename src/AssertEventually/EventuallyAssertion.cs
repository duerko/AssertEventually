namespace AssertEventually;

public sealed class EventuallyAssertion<T>
{
    private readonly Action<T> _assertion;

    internal EventuallyAssertion(Action<T> assertion)
    {
        _assertion = assertion;
    }

    public EventuallyExecution<T> For(
        Func<Task<T>> observation
    )
    {

        return new EventuallyExecution<T>(_assertion, observation);
    }
}