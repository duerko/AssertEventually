using AssertEventually.TUnit;
using TUnit.Core;

namespace AssertEventually.TUnit.Tests;

public class TUnitEventuallyTests
{
    [Test]
    public async Task Runs_successful_execution()
    {
        await TUnitEventually.AssertAsync(
            global::AssertEventually.AssertEventually
                .That<int>(value =>
                {
                    if (value != 2)
                        throw new InvalidOperationException("not ready");
                })
                .For(() => 2),
            TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Converts_timeout_to_tunit_failure()
    {
        Exception? exception = null;
        try
        {
            await TUnitEventually.AssertAsync(
                global::AssertEventually.AssertEventually
                    .That<int>(value =>
                    {
                        if (value != 2)
                            throw new InvalidOperationException("not ready");
                    })
                    .For(() => 1),
                TimeSpan.FromMilliseconds(20));
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("AssertEventually failed");
    }
}
