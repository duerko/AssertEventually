namespace AssertEventually.Tests
{
    public class AssertEventuallyTests
    {
        [Fact]
        public async Task Succeeds_when_assertion_eventually_passes()
        {
            var attempts = 0;

            await AssertEventually
                .That<int>(value => Assert.Equal(3, value))
                .For(
                    async () =>
                    {
                        attempts++;
                        await Task.CompletedTask;

                        return attempts;
                    })
                .Within(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Retries_when_observation_throws()
        {
            var attempts = 0;

            await AssertEventually
                .That<int>(value => Assert.Equal(3, value))
                .For(
                    async () =>
                    {
                        attempts++;

                        if (attempts < 3)
                            throw new InvalidOperationException();

                        return attempts;
                    })
                .Within(TimeSpan.FromSeconds(1));
        }
        [Fact]
        public async Task Retries_when_assertion_fails()
        {
            var attempts = 0;

            await AssertEventually
                .That<int>(value => Assert.Equal(3, value))
                .For(
                    async () =>
                    {
                        attempts++;
                        return attempts;
                    })
                .Within(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Supports_synchronous_observations()
        {
            var attempts = 0;

            await AssertEventually
                .That<int>(value => Assert.Equal(3, value))
                .For(() => ++attempts)
                .Within(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Supports_asynchronous_assertions()
        {
            var attempts = 0;

            await AssertEventually
                .That<int>(async value =>
                {
                    await Task.Yield();
                    Assert.Equal(2, value);
                })
                .For(async () =>
                {
                    await Task.CompletedTask;
                    return ++attempts;
                })
                .Within(TimeSpan.FromSeconds(1));
        }
        [Fact]
        public async Task Throws_when_timeout_is_reached()
        {
            var exception = await Assert.ThrowsAsync<EventuallyTimeoutException>(() =>
                AssertEventually
                    .That<int>(value => Assert.Equal(42, value))
                    .For(
                        async () =>
                        {
                            await Task.CompletedTask;
                            return 1;
                        })
                    .Within(TimeSpan.FromMilliseconds(100)));

            Assert.NotEmpty(exception.Report.Attempts);
            Assert.All(exception.Report.Attempts, attempt =>
                Assert.False(attempt.Succeeded));
            Assert.All(
                exception.Report.Attempts,
                attempt => Assert.Equal(
                    EventuallyAttemptKind.AssertionFailure,
                    attempt.Kind));
            Assert.Equal(
                exception.Report.Attempts[^1].Exception,
                exception.InnerException);
        }

        [Fact]
        public async Task Reports_observation_and_assertion_failures_in_order()
        {
            var attempts = 0;

            var exception = await Assert.ThrowsAsync<EventuallyTimeoutException>(() =>
                AssertEventually
                    .That<int>(value => Assert.Equal(3, value))
                    .For(
                        async () =>
                        {
                            attempts++;

                            if (attempts == 1)
                                throw new InvalidOperationException("not ready");

                            return 2;
                        })
                    .Within(TimeSpan.FromMilliseconds(150)));

            Assert.True(exception.Report.Attempts.Count >= 2);
            Assert.IsType<InvalidOperationException>(
                exception.Report.Attempts[0].Exception);
            Assert.Equal(
                EventuallyAttemptKind.ObservationException,
                exception.Report.Attempts[0].Kind);
            Assert.IsType<Xunit.Sdk.EqualException>(
                exception.Report.Attempts[1].Exception);
            Assert.Equal(
                EventuallyAttemptKind.AssertionFailure,
                exception.Report.Attempts[1].Kind);
            Assert.True(
                exception.Report.Attempts[0].Elapsed
                <= exception.Report.Attempts[1].Elapsed);
        }

        [Fact]
        public async Task Reports_the_successful_attempt_and_observed_value()
        {
            EventuallyExecutionReport? report = null;

            var execution = AssertEventually
                .That<int>(value => Assert.Equal(2, value))
                .For(() => 2);

            await execution.Within(TimeSpan.FromSeconds(1));
            report = execution.Report;

            var attempt = Assert.Single(report!.Attempts);
            Assert.True(report.Succeeded);
            Assert.Equal(EventuallyAttemptKind.Success, attempt.Kind);
            Assert.Equal(2, attempt.ObservedValue);
            Assert.True(attempt.Duration >= TimeSpan.Zero);
            Assert.True(attempt.ObservationDuration >= TimeSpan.Zero);
            Assert.True(attempt.AssertionDuration >= TimeSpan.Zero);
            Assert.Equal("2", attempt.FormatObservedValue());
        }

        [Fact]
        public async Task Limits_recorded_attempt_history()
        {
            var exception = await Assert.ThrowsAsync<EventuallyTimeoutException>(() =>
                AssertEventually
                    .That<int>(value => Assert.Equal(42, value))
                    .For(() => 1)
                    .Within(
                        TimeSpan.FromSeconds(2),
                        new EventuallyOptions { MaxRecordedAttempts = 2 }));

            Assert.Equal(2, exception.Report.Attempts.Count);
            Assert.True(exception.Report.OmittedAttemptCount > 0);
            Assert.Equal(1, exception.Report.Attempts[0].Number);
            Assert.Equal(
                exception.Report.Attempts[^1].Number,
                exception.Report.OmittedAttemptCount + 2);
        }

        [Fact]
        public async Task Supports_configuring_the_polling_interval()
        {
            var attempts = 0;

            await AssertEventually
                .That<int>(value => Assert.Equal(2, value))
                .For(() => ++attempts)
                .PollEvery(TimeSpan.FromMilliseconds(10))
                .Within(TimeSpan.FromSeconds(1));

            Assert.Equal(2, attempts);
        }

        [Fact]
        public async Task Cancellation_stops_polling_without_becoming_a_failure()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(25));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                AssertEventually
                    .That<int>(value => Assert.Equal(42, value))
                    .For(() => 1)
                    .PollEvery(TimeSpan.FromSeconds(1))
                    .WithCancellation(cancellation.Token)
                    .Within(TimeSpan.FromSeconds(5)));
        }
    }
}