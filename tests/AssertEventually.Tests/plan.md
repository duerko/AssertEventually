# AssertEventually — Full Library Implementation Plan

## 0. Product definition

Build `AssertEventually`, a .NET testing library for testing **eventual consistency and asynchronous state convergence**.

The core problem:

> A test performs an observation against a system whose state may not be immediately consistent, and the test cares only that the desired assertion becomes true within a defined time window.

The canonical API is:

```csharp
await AssertEventually
    .That(order =>
    {
        order.Should().NotBeNull();
        order!.Status.Should().Be(OrderStatus.Processed);
    })
    .For(() => repository.GetAsync(orderId))
    .Within(TimeSpan.FromSeconds(10));
```

The fundamental semantics are:

* `That` defines the desired state.
* `For` defines how that state is observed.
* `Within` defines the maximum convergence window.
* `For` and `That` are repeatedly evaluated.
* Exceptions and assertion failures during the observation window are **intermediate observations**, not immediate test failures.
* `Within` succeeds as soon as `That` passes.
* If the timeout expires, the test fails with a rich explanation of the observed history.
* `Given` is explicitly **out of scope for the initial release**.

### Why this product exists

Normal assertions answer:

> "Is the system in the expected state right now?"

`AssertEventually` answers:

> "Does the system reach the expected state within the allowed consistency window?"

This is particularly useful for:

* eventually consistent databases
* read replicas
* caches
* search indexes
* message-driven workflows
* event handlers
* background workers
* queues
* projections/read models
* distributed services
* HTTP APIs with asynchronous processing
* cloud services with asynchronous propagation
* integration tests involving multiple processes

The library must optimize for these scenarios rather than becoming a generic retry utility.

---

# 1. Repository architecture

Create a multi-project solution.

```text
AssertEventually/
├── src/
│   ├── AssertEventually/
│   │   ├── AssertEventually.cs
│   │   ├── EventuallyAssertion.cs
│   │   ├── EventuallyExecution.cs
│   │   ├── EventuallyOptions.cs
│   │   ├── EventuallyResult.cs
│   │   ├── EventuallyAttempt.cs
│   │   ├── EventuallyException.cs
│   │   └── ...
│   │
│   ├── AssertEventually.Xunit/
│   ├── AssertEventually.NUnit/
│   ├── AssertEventually.MSTest/
│   └── AssertEventually.TUnit/
│
├── tests/
│   ├── AssertEventually.Tests/
│   ├── AssertEventually.Xunit.Tests/
│   ├── AssertEventually.NUnit.Tests/
│   ├── AssertEventually.MSTest.Tests/
│   └── AssertEventually.TUnit.Tests/
│
├── docs/
├── samples/
├── README.md
├── LICENSE
├── CHANGELOG.md
├── CONTRIBUTING.md
├── Directory.Build.props
├── Directory.Packages.props
└── AssertEventually.sln
```

### Justification

Keep the core completely framework-neutral.

The current .NET ecosystem distinguishes the test platform from the test framework, with MSTest, NUnit, TUnit and xUnit.net all occupying the framework layer. Microsoft.Testing.Platform also provides a common extensibility layer.

A framework-independent core means:

* no xUnit dependency for users of MSTest
* no NUnit dependency in the base package
* smaller dependency graph
* easier versioning
* easier support for future frameworks
* easier unit testing
* cleaner conceptual architecture

---

# 2. Core API — complete

Implement:

```csharp
AssertEventually
    .That<T>(Action<T> assertion)
    .For(Func<Task<T>> observation)
    .Within(TimeSpan timeout);
```

Also support synchronous observations:

```csharp
AssertEventually
    .That(value => value.Should().Be(42))
    .For(() => GetValue())
    .Within(TimeSpan.FromSeconds(5));
```

And asynchronous assertions if useful:

```csharp
AssertEventually
    .That(async value =>
    {
        await AssertSomethingAsync(value);
    })
    .For(...)
    .Within(...);
```

### Justification

Real distributed-system tests commonly involve asynchronous APIs, but some observations are local and synchronous.

Supporting both avoids forcing users to write artificial `Task.FromResult(...)` wrappers.

Do not add dozens of overloads immediately. Prefer a small set of carefully designed overloads with shared internals.

---

# 3. Define the execution semantics precisely

The execution model must be:

```text
start timer
    ↓
observe
    ↓
assert
    ↓
pass?
 ┌──┴──┐
yes    no
 ↓      ↓
done   record attempt
        ↓
     wait/poll
        ↓
      observe
```

For example:

```text
0.00s  GET /orders/123 → 404
0.25s  GET /orders/123 → 404
0.50s  GET /orders/123 → 200 Pending
0.75s  GET /orders/123 → 200 Processing
1.00s  GET /orders/123 → 200 Processed ✓
```

The test passes.

### Justification

This is the defining behavior of the product.

The intermediate failures are not necessarily system failures. They may be exactly what eventual consistency means.

Do not use a conventional test retry abstraction as the underlying semantic model. A retry generally treats the test itself as failed and reruns it; `AssertEventually` instead treats each observation as a sample of a changing system.

---

# 4. Exception semantics

During `For`/`That` execution:

* observation exceptions are captured
* assertion exceptions are captured
* execution continues until success or timeout
* the latest failure is retained
* the full attempt history is available for diagnostics

Example:

```text
0.0s  HttpRequestException: connection refused
0.3s  404 Not Found
0.6s  404 Not Found
0.9s  Assertion failed: expected Processed, got Pending
1.2s  Assertion failed: expected Processed, got Processing
1.5s  SUCCESS
```

### Justification

This is the central differentiator from ordinary assertions.

A missing record, transient connection failure, stale replica, or intermediate assertion failure may all be expected during convergence.

However, failures during `Given`—if `Given` is added later—must be treated differently because setup establishes the scenario rather than observing convergence.

---

# 5. Timeout behavior

When the timeout expires:

* throw a dedicated `EventuallyAssertionException`
* preserve the final underlying exception
* preserve all relevant attempt information
* report elapsed time
* report number of attempts
* report the last observation
* report the last assertion failure
* report the polling intervals
* distinguish observation failures from assertion failures

Example:

```text
AssertEventually failed after 10.02s.

Assertion:
    order.Status.Should().Be(OrderStatus.Processed)

Observation:
    repository.GetAsync(orderId)

Attempts:
    38

Last observation:
    Order { Id = 123, Status = Processing }

Last assertion:
    Expected Status to be Processed
    but found Processing.

Timeline:
    0.00s  NotFoundException
    0.25s  NotFoundException
    0.50s  Status = Pending
    0.75s  Status = Pending
    ...
    9.75s  Status = Processing
    10.02s Status = Processing
```

### Justification

A 10-second timeout is almost useless diagnostically if all the developer receives is:

```text
Expected Processed but found Processing.
```

The entire purpose of this library is dealing with temporal behavior. The failure report therefore needs to explain the temporal behavior.

---

# 6. Attempt model — complete

Create an internal/publicly useful model representing an attempt.

For example:

```csharp
public sealed record EventuallyAttempt(
    TimeSpan Elapsed,
    TimeSpan Duration,
    EventuallyAttemptKind Kind,
    object? ObservedValue,
    Exception? Exception);
```

Where:

```csharp
public enum EventuallyAttemptKind
{
    ObservationException,
    AssertionFailure,
    Success
}
```

Do not blindly serialize arbitrary observed objects.

Create a safe formatter abstraction:

```csharp
public interface IEventuallyValueFormatter
{
    string Format(object? value);
}
```

Provide a default formatter.

### Justification

Diagnostics need structured data rather than strings constructed throughout the execution loop.

This also gives future integrations the ability to render the same information differently:

* console
* IDE output
* HTML
* JSON
* TRX attachment
* GitHub Actions summary

---

# 7. Do not retain unbounded history — complete

Record attempt history, but provide a configurable limit.

For example:

```csharp
MaxRecordedAttempts = 100;
```

When exceeded:

* retain first attempt
* retain representative/milestone attempts
* retain recent attempts
* report omitted attempt count

### Justification

A fast polling interval over a long timeout could generate thousands of attempts.

A diagnostic system that consumes unbounded memory during a test failure is unacceptable.

---

# 8. Polling strategy — complete

Implement a default polling interval.

Initially:

```csharp
100ms
```

Then expose:

```csharp
.PollEvery(TimeSpan.FromMilliseconds(250))
```

Later support:

```csharp
.PollWith(...)
```

or an abstraction such as:

```csharp
public interface IPollingStrategy
{
    TimeSpan GetDelay(int attempt, TimeSpan elapsed);
}
```

Provide:

* fixed interval
* exponential backoff
* exponential backoff with maximum
* possibly jitter

### Justification

Different systems have different consistency characteristics.

A local in-memory projection might converge in milliseconds; a cloud queue workflow may take seconds.

However, polling configuration should remain separate from the core assertion semantics.

---

# 9. Timeout must use a monotonic clock

Do not calculate elapsed time exclusively using:

```csharp
DateTime.UtcNow
```

Use `Stopwatch`/monotonic timing for timeout measurement.

### Justification

Wall-clock time can move forwards or backwards due to system clock adjustments.

A timeout is a duration, so elapsed-duration measurement should use a monotonic clock.

---

# 10. Cancellation

Support:

```csharp
.WithCancellation(cancellationToken)
```

Cancellation must:

* stop further observations
* stop waiting
* dispose/clean up correctly
* throw `OperationCanceledException`
* not be converted into a normal assertion failure

### Justification

Integration tests frequently run under CI cancellation, test-runner cancellation, or application shutdown.

Cancellation means "stop this work", not "the eventual condition failed."

---

# 11. Async correctness

Ensure:

* no `.Result`
* no `.Wait()`
* no blocking sleeps
* `Task.Delay(..., cancellationToken)`
* `ConfigureAwait(false)` where appropriate for library code
* exceptions are captured without losing stack/inner exception information

### Justification

This library will be heavily used in asynchronous integration tests. Blocking the test thread undermines scalability and can cause deadlocks.

---

# 12. Observation duration

Measure how long each observation takes.

Example:

```text
Attempt 17
Elapsed: 4.25s
Observation duration: 812ms
Result: assertion failed
```

### Justification

A test may appear to have a 10-second timeout but actually spend most of that time waiting on slow observations.

This distinction is crucial when diagnosing slow integration tests.

---

# 13. Assertion duration

Also measure assertion duration.

### Justification

Assertions can themselves be expensive, particularly when validating large object graphs or collections.

Separating observation time from assertion time gives useful performance diagnostics.

---

# 14. Success diagnostics

Do not only produce diagnostics on failure.

Make optional success information available:

```text
AssertEventually passed after 1.42s.

Attempts: 6
Final observation: Processed
```

Do not necessarily print this by default.

### Justification

Successful convergence timing can be useful when diagnosing flaky integration tests or determining appropriate consistency windows.

But noisy test output is undesirable, so successful diagnostics should be opt-in or integration-controlled.

---

# 15. Diagnostic report model

Create a framework-independent:

```csharp
EventuallyExecutionReport
```

containing:

```text
Assertion description
Observation description
StartedAt
Elapsed
Timeout
AttemptCount
Successful
Attempts[]
LastObservation
LastException
Polling information
```

### Justification

The core should produce **data**, not test-framework-specific output.

This report becomes the single source of truth for every integration.

---

# 16. Human-readable report

Build a formatter:

```csharp
EventuallyReportFormatter
```

that produces text like:

```text
AssertEventually failed
────────────────────────────────────────

Timeout:     10.00s
Elapsed:     10.03s
Attempts:    38

Assertion:
    order.Status.Should().Be(OrderStatus.Processed)

Observation:
    repository.GetAsync(orderId)

Timeline:

  0.00s  ObservationException
         OrderNotFoundException

  0.27s  ObservationException
         OrderNotFoundException

  0.53s  AssertionFailure
         Expected Status to be Processed
         but found Pending

  ...

  9.81s  AssertionFailure
         Expected Status to be Processed
         but found Processing

Last observed value:
    Order
    {
        Id = 123,
        Status = Processing
    }
```

### Justification

The failure message should be useful directly in a terminal, IDE, CI log, or GitHub Actions output.

---

# 17. Structured JSON report

Add optional JSON serialization:

```text
assert-eventually-report.json
```

Example structure:

```json
{
  "successful": false,
  "timeout": "00:00:10",
  "elapsed": "00:00:10.03",
  "attempts": 38,
  "assertion": "...",
  "observation": "...",
  "attemptHistory": []
}
```

### Justification

Structured output enables:

* CI tooling
* custom dashboards
* post-processing
* future IDE extensions
* machine-readable diagnostics

Do not make JSON the primary user experience; it is a machine-readable companion to the human report.

---

# 18. Framework integration architecture

Create separate packages:

```text
AssertEventually.Xunit
AssertEventually.NUnit
AssertEventually.MSTest
AssertEventually.TUnit
```

Each integration package should depend on:

```text
AssertEventually
+
specific test framework
```

The base package must not depend on any of them.

### Justification

.NET's current testing ecosystem includes MSTest, NUnit, TUnit and xUnit.net, while Microsoft.Testing.Platform provides a common modern platform for several of them.

Framework-specific packages allow the same core report to appear naturally in each runner.

---

# 19. xUnit integration

Support xUnit.net v3 first.

Use the xUnit v3 test context/output/attachment mechanisms where appropriate.

xUnit v3 provides `TestContext`, including cancellation, diagnostic messages, key/value storage and test-result attachments.

Integration should:

* capture the AssertEventually report
* add concise failure output
* attach the full report when appropriate
* respect test cancellation
* avoid requiring users to manually pass an output helper into AssertEventually

### Justification

xUnit v3 has modern extensibility and native test attachments, making it possible to integrate diagnostics without polluting the core API.

Do not initially attempt to build a custom xUnit test runner.

---

# 20. NUnit integration

Integrate with NUnit's `TestContext`.

NUnit exposes test output through `TestContext.Out` and supports test attachments through `TestContext.AddTestAttachment`.

Integration should:

* write concise diagnostics to NUnit test output
* attach full HTML/JSON/text report on failure
* preserve the original AssertEventually exception semantics
* use NUnit's test context only from the integration package

### Justification

NUnit already has explicit concepts for output and test attachments, so AssertEventually should use those rather than inventing another reporting mechanism.

---

# 21. MSTest integration

Integrate with MSTest `TestContext`.

MSTest exposes result directories and `AddResultFile`, allowing files to be associated with test results.

Integration should:

* emit concise failure diagnostics
* attach the full JSON/text/HTML report
* use `TestContext.ResultsDirectory`
* use `AddResultFile` where available
* respect MSTest cancellation/execution semantics

### Justification

MSTest's TestContext is explicitly designed to provide test-run information and result files, so it provides the correct integration point.

---

# 22. TUnit integration

Support TUnit as a first-class framework.

Use TUnit's `TestContext` and artifact APIs.

TUnit exposes test output and artifact attachment through `TestContext.Current`, including `Output.AttachArtifact`.

Integration should:

* write concise report output
* attach full diagnostic artifacts
* use the current test context
* avoid any dependency from the core library on TUnit

### Justification

TUnit is now one of the .NET frameworks explicitly listed by Microsoft, and it is built on Microsoft.Testing.Platform.

---

# 23. Microsoft.Testing.Platform consideration

Investigate whether a dedicated MTP integration package is useful in addition to framework-specific packages.

Do not duplicate functionality that MTP already provides.

MTP already supports extensions for reports including TRX, HTML, JUnit, CTRF, Azure DevOps and GitHub Actions. It also has artifact/test-node mechanisms for associating files with individual tests.

### Justification

The library should complement the platform rather than compete with it.

The AssertEventually-specific value is the **convergence history**, not generic test reporting.

Where MTP can carry an artifact, AssertEventually should produce the artifact and let the platform carry it.

---

# 24. Report artifact strategy

On timeout/failure, generate:

```text
assert-eventually/
    report.txt
    report.json
    report.html
```

Potentially only generate HTML/JSON when integration requests artifacts.

### Justification

A text report is useful in logs.

JSON is useful for machines.

HTML is useful for humans investigating a complex distributed-system failure.

Do not force all three formats into every test run because this creates unnecessary I/O.

---

# 25. HTML report

Build a polished standalone HTML report.

Include:

* pass/fail status
* timeout
* elapsed time
* attempt count
* assertion
* observation
* timeline
* exception details
* final observed value
* expandable individual attempts
* duration chart
* exception type grouping

Example visual:

```text
AssertEventually
──────────────────────────────────────

FAILED

10.03s / 10.00s
38 attempts

Pending ──────── Processing ──── Processing
   │                 │                │
   └─────────────────┴────────────────┘
                         timeout
```

### Justification

Temporal failures are inherently easier to understand visually than as a wall of stack traces.

The HTML report should become a signature feature of the project.

---

# 26. Report safety

Do not automatically dump arbitrary object graphs into reports without limits.

Implement:

* maximum string length
* maximum collection elements
* maximum exception depth
* maximum report size
* sensitive-value redaction hook

### Justification

Distributed-system objects frequently contain:

* access tokens
* connection strings
* customer data
* PII
* large payloads

A debugging library must not accidentally create a security/data-leak mechanism.

---

# 27. Exception grouping

Group repeated identical exception types/messages.

Instead of:

```text
Exception
Exception
Exception
Exception
Exception
```

show:

```text
OrderNotFoundException
    occurred 14 times
    first: 0.01s
    last: 2.81s
```

Allow expansion to see individual occurrences.

### Justification

Repeated transient failures are common in eventual-consistency scenarios. Grouping makes the actual convergence pattern visible without producing enormous reports.

---

# 28. Timeline summarization

Do not blindly display every polling attempt in the human report.

Summarize repetitive sequences:

```text
0.0s - 2.4s
OrderNotFoundException × 12

2.5s - 4.8s
Status = Pending × 10

4.9s - 7.2s
Status = Processing × 10

7.3s
Statu
```
