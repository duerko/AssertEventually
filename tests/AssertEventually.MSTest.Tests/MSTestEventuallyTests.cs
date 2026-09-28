using AssertEventually.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssertEventually.MSTest.Tests;

[TestClass]
public class MSTestEventuallyTests
{
    [TestMethod]
    public async Task Runs_successful_execution()
    {
        await MSTestEventually.AssertAsync(
            global::AssertEventually.AssertEventually
                .That<int>(value => Assert.AreEqual(2, value))
                .For(() => 2),
            TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task Converts_timeout_to_mstest_failure()
    {
        var exception = await Assert.ThrowsExceptionAsync<AssertFailedException>(() =>
            MSTestEventually.AssertAsync(
                global::AssertEventually.AssertEventually
                    .That<int>(value => Assert.AreEqual(2, value))
                    .For(() => 1),
                TimeSpan.FromMilliseconds(20)));

        StringAssert.Contains(exception.Message, "AssertEventually failed");
    }
}
