namespace AssertEventually;

/// <summary>Formats observed values for diagnostics.</summary>
public interface IEventuallyValueFormatter
{
    /// <summary>Formats an observed value.</summary>
    string Format(object? value);
}
