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
            Assert.IsType<Xunit.Sdk.EqualException>(
                exception.Report.Attempts[1].Exception);
            Assert.True(
                exception.Report.Attempts[0].Elapsed
                <= exception.Report.Attempts[1].Elapsed);
        }
    }
}