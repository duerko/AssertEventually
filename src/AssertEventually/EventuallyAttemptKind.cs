namespace AssertEventually;

/// <summary>Classifies the result of an execution attempt.</summary>
public enum EventuallyAttemptKind
{
    /// <summary>The observation threw an exception.</summary>
    ObservationException,
    /// <summary>The assertion rejected the observed value.</summary>
    AssertionFailure,
    /// <summary>The assertion succeeded.</summary>
    Success
}
