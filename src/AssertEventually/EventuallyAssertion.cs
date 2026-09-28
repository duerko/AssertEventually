namespace AssertEventually;

/// <summary>Configures the assertion and observation used for polling.</summary>
public sealed class EventuallyAssertion<T>
{
    private readonly Func<T, Task> _assertion;
    private readonly string? _description;

    internal EventuallyAssertion(Action<T> assertion)
        : this(value =>
        {
            assertion(value);
            return Task.CompletedTask;
        })
    {
    }

    internal EventuallyAssertion(string description, Action<T> assertion)
        : this(
            description,
            value =>
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

    internal EventuallyAssertion(
        string description,
        Func<T, Task> assertion)
        : this(assertion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        _description = description;
    }

    /// <summary>Uses a synchronous observation.</summary>
    public EventuallyExecution<T> For(Func<T> observation)
    {
        return For(() => Task.FromResult(observation()));
    }

    /// <summary>Uses an asynchronous observation.</summary>
    public EventuallyExecution<T> For(
        Func<Task<T>> observation
    )
    {
        return new EventuallyExecution<T>(_assertion, observation, _description);
    }

    /// <summary>Uses a described synchronous observation.</summary>
    public EventuallyExecution<T> For(
        string description,
        Func<T> observation)
    {
        return For(description, () => Task.FromResult(observation()));
    }

    /// <summary>Uses a described asynchronous observation.</summary>
    public EventuallyExecution<T> For(
        string description,
        Func<Task<T>> observation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new EventuallyExecution<T>(
            _assertion,
            observation,
            _description is null
                ? description
                : $"{_description}; {description}");
    }
}