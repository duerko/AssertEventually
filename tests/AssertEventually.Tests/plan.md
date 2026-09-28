# AssertEventually — Full Library Implementation Plan

## 0. Product definition — complete

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

# 1. Repository architecture — complete

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

# 3. Define the execution semantics precisely — complete

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

# 4. Exception semantics — complete

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

# 5. Timeout behavior — complete

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

# 9. Timeout must use a monotonic clock — complete

Do not calculate elapsed time exclusively using:

```csharp
DateTime.UtcNow
```

Use `Stopwatch`/monotonic timing for timeout measurement.

### Justification

Wall-clock time can move forwards or backwards due to system clock adjustments.

A timeout is a duration, so elapsed-duration measurement should use a monotonic clock.

---

# 10. Cancellation — complete

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

# 11. Async correctness — complete

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

# 12. Observation duration — complete

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

# 13. Assertion duration — complete

Also measure assertion duration.

### Justification

Assertions can themselves be expensive, particularly when validating large object graphs or collections.

Separating observation time from assertion time gives useful performance diagnostics.

---

# 14. Success diagnostics — complete

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

# 15. Diagnostic report model — complete

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

# 16. Human-readable report — complete

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

# 17. Structured JSON report — complete

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

# 18. Framework integration architecture — complete

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

# 19. xUnit integration — complete

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

# 20. NUnit integration — complete

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

# 21. MSTest integration — complete

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

# 22. TUnit integration — complete

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

# 23. Microsoft.Testing.Platform consideration — complete

Investigate whether a dedicated MTP integration package is useful in addition to framework-specific packages.

Do not duplicate functionality that MTP already provides.

MTP already supports extensions for reports including TRX, HTML, JUnit, CTRF, Azure DevOps and GitHub Actions. It also has artifact/test-node mechanisms for associating files with individual tests.

### Justification

The library should complement the platform rather than compete with it.

The AssertEventually-specific value is the **convergence history**, not generic test reporting.

Where MTP can carry an artifact, AssertEventually should produce the artifact and let the platform carry it.

---

# 24. Report artifact strategy — complete

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

# 25. HTML report — complete

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

# 26. Report safety — complete

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

# 27. Exception grouping — complete

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

# 28. Timeline summarization — complete

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
Status = Processed ✓
```

Provide the full history in JSON/expandable HTML.

Justification

This produces a much more useful failure experience while preserving complete information for debugging.

29. Description capture

Allow optional explicit descriptions:

.That(
    "order eventually becomes processed",
    order => order.Status.Should().Be(Processed))

and:

.For(
    "read order from replica",
    () => replica.GetAsync(orderId))

But don't require descriptions.

Justification

Lambda expressions cannot always be represented meaningfully at runtime.

Explicit descriptions make reports dramatically better for CI failures.

30. Do not attempt source-code extraction initially

Do not build a Roslyn-based system to recover the source expression.

Justification

This adds significant complexity and is not required for the core product.

FluentAssertions already demonstrates how valuable subject identification can be, but AssertEventually's job is temporal orchestration, not expression analysis.

Add this only if real-world usage demonstrates that descriptions are insufficient.

31. FluentAssertions compatibility

Do not create a FluentAssertions integration package initially.

Simply ensure:

.That(order =>
{
    order.Status.Should().Be(OrderStatus.Processed);
})

works naturally.

Justification

FluentAssertions already provides rich assertion exceptions and supports multiple .NET test frameworks.

AssertEventually should wrap existing assertion ecosystems rather than compete with them.

32. Test the semantics heavily

Build deterministic tests using fake clocks and fake observations where possible.

Test:

immediate success
success on second attempt
success on final possible attempt
timeout
observation exception
assertion exception
alternating exceptions/assertion failures
synchronous observation
asynchronous observation
asynchronous assertion
cancellation
zero timeout
negative timeout
extremely short timeout
observation slower than polling interval
assertion slower than polling interval
cancellation during observation
cancellation during delay
maximum history
report formatting
exception preservation
Justification

The retry loop itself is simple.

The difficult bugs are temporal edge cases.

A fake clock and deterministic scheduler prevent tests from becoming flaky themselves.

33. Integration-test the framework integrations

For every supported framework, create actual test projects that run under the real runner.

Verify:

successful AssertEventually tests appear as passed
timeout tests appear as failed
failure messages are useful
reports are attached
cancellation works
artifacts appear in CI-compatible result directories
Justification

A framework adapter that compiles but doesn't behave correctly inside the real runner is not a useful integration.

MTP/VSTest behavior differs across frameworks, so real runner tests are necessary.

34. Package targeting

Target a modern baseline initially, preferably:

net8.0

Consider netstandard2.0 only if compatibility requirements justify it.

Justification

Modern .NET test platforms are moving toward Microsoft.Testing.Platform and current .NET versions, while xUnit's current v3 extensibility supports netstandard2.0 and current runtimes.

Do not sacrifice API quality and maintainability merely to support every historical .NET runtime.

35. NuGet package design

Publish:

AssertEventually
AssertEventually.Xunit
AssertEventually.NUnit
AssertEventually.MSTest
AssertEventually.TUnit

Potentially later:

AssertEventually.Reporting

if reporting becomes sufficiently large.

Justification

Users should install only the framework integration they need.

A base package should remain lightweight.

36. Package metadata

Configure:

package description
repository URL
project URL
license
README
icon
tags
symbols/source link
generated XML docs
release notes
package validation
Justification

For an open-source library, NuGet is part of the product surface. Good metadata improves discoverability and makes the package trustworthy.

37. Analyzer/source documentation

Document all public APIs with XML documentation.

Warnings should explain semantic behavior, particularly:

Exceptions thrown by the observation are treated as intermediate failures until the timeout expires.

Justification

This behavior is unusual enough that users must not have to infer it from implementation details.

38. README design

Make the README visually polished.

Start with:

# AssertEventually

[logo/icon]

> Test that your distributed system eventually reaches the state you expect.

[NuGet] [Build] [License] [.NET] [Tests]

Then immediately show:

await AssertEventually
    .That(order =>
        order.Status.Should().Be(OrderStatus.Processed))
    .For(() => repository.GetAsync(orderId))
    .Within(TimeSpan.FromSeconds(10));

Follow with:

The read model doesn't need to be consistent immediately.

AssertEventually waits for it to become consistent.
Justification

GitHub visitors need to understand the project in seconds.

The API itself is the strongest marketing material.

39. README sections

Include:

Hero/value proposition
10-second example
Why AssertEventually exists
How it works
Real-world examples
Failure output
Framework support
Configuration
Reporting
Installation
API reference
When NOT to use it
Contributing
License
Justification

The README should answer:

What is it?
Why do I need it?
How does it differ from retries?
How do I use it?
What happens when it fails?
Does it work with my framework?
40. Real-world README examples

Include examples for:

Event-driven processing
await publisher.PublishAsync(new OrderCreated(orderId));

await AssertEventually
    .That(() => projection.Get(orderId),
        order => order.Status.Should().Be(Processed))
    .For(...)
    .Within(10.Seconds());
Read replica
await primary.SaveAsync(entity);

await AssertEventually
    .That(entity => entity.Should().NotBeNull())
    .For(() => replica.GetAsync(entity.Id))
    .Within(5.Seconds());
Search index
await search.IndexAsync(document);

await AssertEventually
    .That(results => results.Should().Contain(x => x.Id == document.Id))
    .For(() => search.QueryAsync(document.Id))
    .Within(15.Seconds());
Background worker
await queue.EnqueueAsync(message);

await AssertEventually
    .That(result => result.Status.Should().Be(Completed))
    .For(() => jobs.GetAsync(jobId))
    .Within(30.Seconds());
Justification

These examples demonstrate that the library isn't merely a retry helper. It is designed around real distributed-system consistency boundaries.

41. Explicitly explain the difference from retry libraries

README should say:

AssertEventually is not a generic retry library.

A retry says:

    "Run this operation again if it fails."

AssertEventually says:

    "Observe this system repeatedly until it reaches this state."
Justification

This is one of the most important positioning decisions.

The library should not be confused with Polly or test-runner retry functionality.

42. Explain the difference from test retries

Include:

Test retry:

    test fails
       ↓
    rerun the test

AssertEventually:

    one test
       ↓
    observe changing state
       ↓
    assert repeatedly
       ↓
    pass when state converges
Justification

Test retries can repeat setup and side effects, potentially creating duplicate messages/orders/resources.

AssertEventually keeps the scenario intact and repeatedly observes the same system state.

43. Add a "When not to use AssertEventually" section

Examples:

deterministic unit tests
tests where an intermediate failure indicates a real bug
operations that are unsafe to repeat
tests where waiting would hide a deterministic failure
cases where test-level retries are genuinely desired
Justification

A credible open-source project should clearly communicate its boundaries.

This also discourages users from using eventual consistency as a blanket solution for flaky tests.

44. CI pipeline

Set up GitHub Actions for:

restore
build
unit tests
integration tests
package validation
README/package validation
formatting
analyzers
multi-framework test matrix
Justification

The library's core value proposition is reliability. Its own CI must demonstrate that reliability.

45. Multi-framework CI matrix

Run integration tests against:

xUnit
NUnit
MSTest
TUnit

and relevant modern .NET SDKs.

Justification

Framework integrations are independently version-sensitive.

A matrix catches breakage that a single test project cannot.

46. Mutation/property testing consideration

After the initial implementation is stable, consider mutation testing for the execution engine.

Mutations should be caught for:

timeout handling
success detection
exception handling
retry behavior
cancellation
Justification

A retry/convergence engine can appear well tested while having subtle semantic gaps.

Mutation testing verifies that the tests actually protect the algorithm.

47. Performance tests

Add benchmarks for:

immediate success
many failed attempts
exception-heavy attempts
large diagnostic histories
report generation
Justification

Most successful eventual assertions should finish quickly.

The library must have negligible overhead compared with the system being observed.

48. Thread-safety and parallel tests

Ensure independent executions do not share mutable state.

Do not use static mutable collections for reports or configuration.

Justification

Test runners execute tests in parallel.

A global mutable attempt history would produce corrupted diagnostics and race conditions.

49. Configuration hierarchy

Eventually support:

library defaults
    ↓
execution configuration
    ↓
per-test configuration

Avoid global mutable configuration.

Justification

Global configuration is dangerous in parallel test execution and makes tests order-dependent.

Prefer immutable options.

50. API evolution

Do not add Given until real usage demonstrates its value.

Potential future API:

await AssertEventually
    .Given(() => CreateOrder())
    .That(...)
    .For(...)
    .Within(...);

But keep it out of the initial implementation.

Justification

Given introduces lifecycle semantics and setup failure semantics that are not required to solve the core problem.

The initial product should prove:

That → For → Within

before expanding the language.

51. Future features — explicitly defer

Do NOT implement initially:

database-specific integrations
Kafka-specific integrations
RabbitMQ-specific integrations
Elasticsearch-specific integrations
Azure-specific integrations
AWS-specific integrations
automatic screenshot capture
distributed tracing
OpenTelemetry
custom BDD syntax
test-run retries
automatic flaky-test detection
AI-generated diagnostics
Justification

These are all potentially useful, but they turn a focused library into a platform before the core abstraction has been validated.

Build the generic primitive first.

52. Documentation for failure semantics

Create a dedicated document:

docs/semantics.md

Document precisely:

what gets retried
what doesn't
exception handling
timeout behavior
cancellation
observation timing
assertion timing
polling behavior
diagnostic retention
Justification

The most important feature of AssertEventually is not its syntax. It is its semantics.

Users need a canonical specification to know exactly what the library guarantees.

53. README visual polish

Use:

badges
clean typography
short code examples
horizontal sections
diagrams using Mermaid where useful
a small architecture diagram
a failure-report screenshot or HTML example
no giant wall of text

Suggested hero:

# AssertEventually

### Test distributed systems by asserting where they end up,
### not where they happen to be right now.

await AssertEventually
    .That(...)
    .For(...)
    .Within(...);
Justification

The GitHub repository is the project's storefront.

A developer should understand the problem, API, and value proposition without opening another document.

54. README architecture diagram

Use something like:

             AssertEventually
                    │
          ┌─────────┴─────────┐
          │                   │
       That(...)           For(...)
          │                   │
     desired state        observation
          │                   │
          └─────────┬─────────┘
                    │
                Within(...)
                    │
                    ▼
              polling engine
                    │
             ┌──────┴──────┐
             │             │
          success        timeout
             │             │
             ▼             ▼
           PASS          report
Justification

The architecture is simple enough to explain visually, and the diagram reinforces the project's core mental model.

55. README failure example

Include a realistic failure:

✗ AssertEventually failed after 10.03s

Assertion
  order.Status.Should().Be(Processed)

Observation
  repository.GetAsync(orderId)

38 attempts

Timeline
  0.01s  NotFoundException
  0.28s  NotFoundException
  0.54s  Pending
  1.02s  Pending
  2.41s  Processing
  ...
  9.82s  Processing
  10.03s Processing

Expected:
  Processed

Last observed:
  Processing
Justification

This is the feature that will make someone choose this library rather than writing their own loop.

56. Package quality

Before the first public release:

XML documentation enabled
nullable enabled
analyzers enabled
warnings treated appropriately
SourceLink
deterministic builds
package validation
symbol packages
license
README embedded in NuGet
semantic versioning
changelog
Justification

A library intended for broad open-source adoption needs production-grade packaging from the beginning.

57. Version 0.1 acceptance criteria

The implementation is ready for 0.1 only when all of these work:

await AssertEventually
    .That(x => x.Should().Be(expected))
    .For(() => GetValueAsync())
    .Within(TimeSpan.FromSeconds(5));

It must:

retry observation failures
retry assertion failures
pass immediately when the assertion succeeds
stop immediately on success
timeout deterministically
support cancellation
preserve useful exception information
generate a rich failure report
work with xUnit
work with NUnit
work with MSTest
work with TUnit
produce framework-native diagnostics/artifacts where possible
have a polished README
have real integration tests for every framework
have CI covering the supported matrix
58. Most important implementation principle

Do not optimize for feature count.

Optimize for this experience:

await AssertEventually
    .That(order => order.Status.Should().Be(Processed))
    .For(() => repository.GetAsync(orderId))
    .Within(TimeSpan.FromSeconds(10));

When that test fails, the developer should immediately understand:

what was expected
what was observed
how long we waited
how many times we checked
what happened along the way
why the system never converged

That is the product.

Everything else is supporting infrastructure.


---

# 59. Reopen sections 19–22: framework adapters don't implement the `TestContext` artifact attachment they specify — complete

Sections 19–22 are marked "— complete" and each explicitly requires
using the framework's own `TestContext`/attachment mechanism (xUnit v3
`TestContext`, NUnit `TestContext.AddTestAttachment`, MSTest
`TestContext.AddResultFile`/`ResultsDirectory`, TUnit
`TestContext.Current`/`Output.AttachArtifact`) to attach the full
report as a runner-native artifact.

None of the four adapters (`src/AssertEventually.Xunit/XunitEventually.cs`,
`AssertEventually.NUnit/NUnitEventually.cs`,
`AssertEventually.MSTest/MSTestEventually.cs`,
`AssertEventually.TUnit/TUnitEventually.cs`) reference `TestContext`,
`AddTestAttachment`, `AddResultFile`, or `AttachArtifact` at all — each
one only catches `EventuallyTimeoutException` and rethrows or fails
with a formatted string message. The "attach the full report on
failure" requirement in sections 19–22 is unimplemented in all four
packages.

### Justification

Marking these sections complete when a concrete, explicitly-specified
requirement is entirely absent from the code makes the plan's status
markers unreliable. Either implement the artifact attachment described
in sections 19–22, or edit those sections to accurately describe what
was actually built (message-only diagnostics, no runner attachments)
and re-open them as the remaining work.

---

# 60. Deduplicate the framework adapter implementations — complete

`XunitEventually.AssertAsync`, `NUnitEventually.AssertAsync`,
`MSTestEventually.AssertAsync`, and `TUnitEventually.AssertAsync` are
near-identical copy-pasted implementations that differ only in how
they report a timeout (throw `XunitException`, call `Assert.Fail`,
call `Assert.Fail`, throw `TUnitEventuallyException`).

Extract the shared `WithCancellation(...).Within(timeout, options)`
call and `EventuallyTimeoutException` catch into a single internal
helper (e.g. in the core package, or a small shared internal package),
and have each adapter supply only its framework-specific failure
reporting.

### Justification

Four copies of the same logic quadruple the maintenance and
regression surface for a single behavior. Any core-side bug fix or
behavior change today must be manually replicated across four
packages, which will not scale as more frameworks are added.

---

# 61. Improve default formatting of complex observed values — complete

`DefaultEventuallyValueFormatter` falls back to `value.ToString()`.
For a typical domain object without a `ToString()` override (the
common case — e.g. an `Order` entity), every report line renders as
just the type name (e.g. `MyApp.Order`), which is not useful
diagnostic output.

Consider a default formatter that reflects over public properties (or
serializes with `System.Text.Json`) when no custom `ToString()` is
present, while still allowing `IEventuallyValueFormatter` to override
this per call.

### Justification

The convergence timeline is this library's unique, differentiating
artifact. If the default experience produces uninformative output for
the most common case — asserting on a domain object — the core value
proposition is undermined for most real-world usage.

---

# 62. Resolve the `AssertEventually` type/namespace collision — pending

The entry-point class `AssertEventually` lives inside the
`AssertEventually` namespace, so the type name is identical to its
containing namespace (and the package name). This currently compiles
and works for normal fluent usage, but it is a known anti-pattern that
risks confusing errors as the API surface grows (aliasing, reflection,
documentation generation, IDE tooling).

Evaluate renaming the entry-point type (e.g. `Eventually`) while
keeping `AssertEventually` as the namespace/package name, or
explicitly confirm and document why the collision is intentional and
safe.

### Justification

Avoiding a type name identical to its own namespace is a widely
followed .NET naming convention precisely because it can surface as
confusing compiler behavior later. It costs little to avoid now,
before the API is public and the type name is locked in by adoption.

---

# 63. Reconcile "— complete" status markers with actual repository state — pending

Sections 1–28 above use a "— complete" suffix; sections 29–58 (this
plan's original, restored middle section) intentionally carry no
status suffix yet. Before continuing to add new sections, audit the
existing ones against what is actually in the repository. Confirmed
gaps as of this audit:

* **Section 1** (repository architecture, marked complete): `docs/`,
  `samples/`, `CHANGELOG.md`, `CONTRIBUTING.md`,
  `Directory.Build.props`, and per-adapter test projects
  (`AssertEventually.Xunit.Tests`, `AssertEventually.NUnit.Tests`,
  `AssertEventually.MSTest.Tests`, `AssertEventually.TUnit.Tests`)
  don't exist.
* **Sections 19–22** (framework integrations, marked complete): none
  of the four adapters use `TestContext`/artifact attachment as
  specified — see section 59.
* **Section 33** (integration-test the framework integrations): no
  test project runs any adapter under NUnit's, MSTest's, or TUnit's
  real runner at all (only xUnit has a test project, and it only
  covers the timeout path).
* **Section 34** (package targeting, recommends `net8.0` baseline):
  every `.csproj` still targets `net10.0` only.
* **Sections 35/36/56** (NuGet package design/metadata/quality): no
  `.csproj` sets `PackageId`, `Version`, `Description`, license,
  tags, README embedding, or SourceLink; no XML documentation exists
  anywhere in `src/` (section 37 is also untouched).
* **Sections 38–43, 53–55** (README design and positioning):
  `README.md` is still a flat feature log with no hero section, no
  "why it's not a retry library" framing, no "when not to use it"
  section, and no badges/diagram, despite `copilot-instructions.md`
  already containing the right positioning line to build from.
* **Section 44/45** (CI pipeline): intentionally deferred for now, per
  explicit direction — do not start this until asked.

### Justification

A plan is only useful as a tracking tool if its status markers are
trustworthy. Do not add a "— complete" suffix to any section (existing
or new) until its literal, stated deliverables exist in the repository
and can be pointed to by file path. Where a section's scope was
reduced or deferred on purpose, say so explicitly in the section
rather than marking it complete.
