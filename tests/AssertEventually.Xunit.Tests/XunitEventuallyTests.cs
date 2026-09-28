using AssertEventually.Xunit;
using Xunit;

namespace AssertEventually.Xunit.Tests;

public class XunitEventuallyTests
{
    [Fact]
    public async Task Runs_successful_execution()
    {
        await XunitEventually.AssertAsync(
            global::AssertEventually.AssertEventually
                .That<int>(value => Assert.Equal(2, value))
                .For(() => 2),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Converts_timeout_to_xunit_failure()
    {
        var exception = await Assert.ThrowsAsync<global::Xunit.Sdk.XunitException>(() =>
            XunitEventually.AssertAsync(
                global::AssertEventually.AssertEventually
                    .That<int>(value => Assert.Equal(2, value))
                    .For(() => 1),
                TimeSpan.FromMilliseconds(20)));

        Assert.Contains("AssertEventually failed", exception.Message);
    }
}
