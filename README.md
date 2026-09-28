# AssertEventually

AssertEventually is a .NET testing library for eventual consistency.

It is designed for tests where the intermediate state of a system
does not matter — only that the system eventually reaches the
expected state within a given amount of time.

Failures and exceptions during the observation period are treated
as intermediate observations. The test only fails if the expected
condition is not eventually met.

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