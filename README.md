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