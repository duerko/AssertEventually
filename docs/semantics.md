# AssertEventually semantics

This document is the normative description of one eventual execution.

## What repeats

`AssertEventually` invokes the observation delegate once per attempt and
then invokes the assertion delegate for the returned value. The
state-changing operation that makes the system converge belongs outside
the execution and should be performed once by the test.

Observation and assertion delegates may be synchronous or asynchronous.
An attempt never overlaps the next attempt: polling waits until the
current observation and assertion have finished.

## Exceptions

An exception from observation is recorded as an observation exception
and the execution continues polling. An exception from assertion is
recorded as an assertion failure and the execution continues polling.
The most recent exception is preserved as the inner exception of a
timeout.

`OperationCanceledException` caused by the configured cancellation
token is not treated as an intermediate failure. Cancellation is
propagated immediately, including when it occurs during observation,
assertion, or the polling delay.

## Timeout and polling

`Within(timeout)` requires a positive timeout. The timeout window uses a
monotonic `Stopwatch`, not wall-clock time. The execution checks the
remaining window before each attempt and before each delay. A delay is
bounded by both the configured polling interval and the remaining
timeout.

Polling defaults to 100 milliseconds. `PollEvery(...)` overrides the
interval for one execution, and `EventuallyOptions.PollInterval` sets
the call-level default.

## Diagnostics

Every attempt records its number, elapsed time, total duration,
observation duration, assertion duration, result kind, observed value,
and exception. Reports retain a bounded history by default: the first
attempt and the most recent attempts are kept, and omitted entries are
counted explicitly.

Successful executions retain their report. Timed-out executions throw
`EventuallyTimeoutException`, which also exposes the report and remains
compatible with `TimeoutException` catches.
