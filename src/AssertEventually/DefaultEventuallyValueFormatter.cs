namespace AssertEventually;

internal sealed class DefaultEventuallyValueFormatter : IEventuallyValueFormatter
{
    private readonly EventuallyReportFormattingOptions _options;

    public DefaultEventuallyValueFormatter(
        EventuallyReportFormattingOptions? options = null)
    {
        _options = options ?? new EventuallyReportFormattingOptions();
    }

    public string Format(object? value)
    {
        return FormatValue(value, 0, new HashSet<object>());
    }

    private string FormatValue(
        object? value,
        int depth,
        HashSet<object> visited)
    {
        if (value is null)
            return "null";

        if (value is string or char or bool
            || value.GetType().IsPrimitive
            || value is decimal or DateTime or DateTimeOffset or TimeSpan or Guid)
        {
            return _options.Format(value.ToString() ?? "null");
        }

        if (depth >= _options.MaxObjectDepth)
            return _options.Format(value.ToString() ?? value.GetType().Name);

        if (!value.GetType().IsValueType && !visited.Add(value))
            return "[cycle]";

        if (value is System.Collections.IEnumerable sequence)
        {
            var items = sequence
                .Cast<object?>()
                .Take(_options.MaxCollectionElements)
                .Select(item => FormatValue(item, depth + 1, visited));
            return _options.Format(
                $"[{string.Join(", ", items)}]");
        }

        var type = value.GetType();
        var toString = value.ToString() ?? string.Empty;
        if (type.GetMethod(nameof(ToString), Type.EmptyTypes)?.DeclaringType != typeof(object))
            return _options.Format(toString);

        var properties = type
            .GetProperties(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public)
            .Where(property => property.GetMethod is not null)
            .Take(_options.MaxCollectionElements)
            .Select(property =>
                $"{property.Name} = {FormatValue(property.GetValue(value), depth + 1, visited)}");

        return _options.Format($"{type.Name} {{ {string.Join(", ", properties)} }}");
    }
}
