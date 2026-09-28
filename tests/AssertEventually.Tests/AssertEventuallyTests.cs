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
            await Assert.ThrowsAsync<TimeoutException>(() =>
                AssertEventually
                    .That<int>(value => Assert.Equal(42, value))
                    .For(
                        async () =>
                        {
                            await Task.CompletedTask;
                            return 1;
                        })
                    .Within(TimeSpan.FromMilliseconds(100)));
        }
    }
}