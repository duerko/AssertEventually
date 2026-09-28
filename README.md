# AssertEventually

AssertEventually is a .NET testing library for eventual consistency.

It is designed for tests where the intermediate state of a system
does not matter — only that the system eventually reaches the
expected state within a given amount of time.

Failures and exceptions during the observation period are treated
as intermediate observations. The test only fails if the expected
condition is not eventually met.

The public entry point intentionally uses the fluent
`AssertEventually.That(...)` form. When an explicit type reference is
needed, use `global::AssertEventually.AssertEventually`; retaining this
name avoids breaking the established API.

The core execution model records an ordered convergence timeline. Each
attempt includes its elapsed time and any observation or assertion
exception. Successful executions expose this through
`EventuallyExecution.Report`; timed-out executions throw
`EventuallyTimeoutException`, which includes the same report while
remaining compatible with `TimeoutException` catches.

Each attempt is classified as an observation exception, assertion
failure, or success, and includes the observed value when one was
available. Reports can format observed values through the
`IEventuallyValueFormatter` abstraction.

Recorded history is bounded by default. Pass
`EventuallyOptions.MaxRecordedAttempts` to control the limit; reports
retain the first and most recent attempts and expose the number of
omitted attempts.

Polling defaults to 100 ms and can be configured per execution:

```csharp
await AssertEventually
    .That<int>(value => Assert.Equal(42, value))
    .For(() => GetValue())
    .PollEvery(TimeSpan.FromMilliseconds(250))
    .Within(TimeSpan.FromSeconds(5));
```

Timeouts are measured with a monotonic stopwatch, so system clock
adjustments cannot extend or prematurely end a convergence window.

Polling can be canceled independently of the timeout:

```csharp
await AssertEventually
    .That<int>(value => Assert.Equal(42, value))
    .For(() => GetValue())
    .WithCancellation(cancellationToken)
    .Within(TimeSpan.FromSeconds(5));
```

Cancellation stops polling and propagates `OperationCanceledException`.

The core loop uses asynchronous observation, assertion, and delay
operations without blocking waits or sleeps.

Each attempt also records how long its observation took, separately
from the total attempt duration.

Assertion duration is recorded separately as well, making it possible
to identify whether convergence is slow because of the system under
observation or the assertion itself.

Successful executions retain their report for optional diagnostics;
`EventuallyExecution.Report.Succeeded` identifies successful
convergence without producing output automatically.

Reports also expose total attempt count, the last observed value, and
the last exception for integrations that do not need to render every
timeline entry.

Use `EventuallyReportFormatter.Format(report)` to render a
framework-neutral human-readable timeline for logs, CI output, or
custom test integrations.

For machine-readable diagnostics, use
`EventuallyJsonReportFormatter.Format(report)`. It emits explicit
metadata and formatted values rather than attempting to serialize
arbitrary observed objects or exception graphs.

Framework integrations are separate packages:
`AssertEventually.Xunit`, `AssertEventually.NUnit`,
`AssertEventually.MSTest`, and `AssertEventually.TUnit`. Each adapter
depends on the core package, while the core remains test-framework
neutral.

The xUnit adapter provides `XunitEventually.AssertAsync(...)`, which
accepts the execution timeout, surfaces the core timeline as native
xUnit failure output, and retains the original timeout exception as
the inner exception.

The NUnit adapter provides the equivalent
`NUnitEventually.AssertAsync(...)` wrapper and sends the formatted
timeline through NUnit's assertion output.

The MSTest adapter provides `MSTestEventually.AssertAsync(...)` and
routes timeout diagnostics through MSTest's native assertion output.

The TUnit adapter provides `TUnitEventually.AssertAsync(...)` and
surfaces timeout diagnostics as a TUnit test failure while retaining
the original timeout exception as the inner exception.

Microsoft.Testing.Platform can consume the human-readable or JSON
report through a future runner integration, but it is intentionally
not a dependency of the core library or its framework adapters.

The convergence timeline is the library's unique artifact. Integrations
may request text, JSON, or HTML files under an
`assert-eventually/` directory; generic runner formats remain the
responsibility of the runner and its existing extensions.

`EventuallyHtmlReportFormatter.Format(report)` produces a standalone
HTML timeline with escaped diagnostic values, timing metadata, status,
and omitted-attempt information.

On timeout, the framework adapters write text, JSON, and HTML reports
and attach them through the framework-native result surface when a
runner context is available: xUnit v3 `TestContext`, NUnit
`TestContext`, MSTest `TestContext`, or TUnit `TestContext`.

All adapters share the core `EventuallyAdapterRunner`, so timeout and
cancellation semantics stay consistent across frameworks.

Built-in formatters accept `EventuallyReportFormattingOptions` to
limit diagnostic string length and redact sensitive content before it
is rendered.

The default value formatter expands bounded public object properties
and collection elements, while limiting depth and preserving custom
`IEventuallyValueFormatter` implementations.

Reports group repeated exception type/message pairs and expose their
counts and first/last occurrence times through `ExceptionGroups`.

Human-readable reports summarize equivalent timeline entries into
segments; the complete retained attempt list remains available through
the structured report.

Both synchronous observations and asynchronous assertions are supported:

```csharp
await AssertEventually
    .That<int>(async value =>
    {
        await AssertSomethingAsync(value);
    })
    .For(() => GetValue())
    .Within(TimeSpan.FromSeconds(5));
```