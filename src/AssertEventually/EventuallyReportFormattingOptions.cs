namespace AssertEventually;

public sealed class EventuallyReportFormattingOptions
{
    public int MaxStringLength { get; init; } = 1_000;

    public int MaxCollectionElements { get; init; } = 10;

    public int MaxObjectDepth { get; init; } = 2;

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
