namespace AssertEventually;

/// <summary>Limits and redaction controls for report formatting.</summary>
public sealed class EventuallyReportFormattingOptions
{
    /// <summary>Maximum rendered string length.</summary>
    public int MaxStringLength { get; init; } = 1_000;

    /// <summary>Maximum rendered collection elements.</summary>
    public int MaxCollectionElements { get; init; } = 10;

    /// <summary>Maximum reflected object depth.</summary>
    public int MaxObjectDepth { get; init; } = 2;

    /// <summary>Optional transformation applied to rendered exception text.</summary>
    public Func<string, string>? Redact { get; init; }

    internal string Format(string value)
    {
        if (MaxStringLength < 1)
            throw new ArgumentOutOfRangeException(
                nameof(MaxStringLength),
                "MaxStringLength must be positive.");

        var redacted = Redact?.Invoke(value) ?? value;
        return redacted.Length <= MaxStringLength
            ? redacted
            : redacted[..MaxStringLength] + "...";
    }
}
