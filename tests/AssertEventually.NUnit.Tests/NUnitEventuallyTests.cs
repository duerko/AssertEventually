using AssertEventually.NUnit;
using NUnit.Framework;

namespace AssertEventually.NUnit.Tests;

public class NUnitEventuallyTests
{
    [Test]
    public async Task Runs_successful_execution()
    {
        await NUnitEventually.AssertAsync(
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
    [Explicit("Run this case separately to verify native NUnit failure output.")]
    public async Task Converts_timeout_to_nunit_failure()
    {
        Exception? exception = null;
        try
        {
            await NUnitEventually.AssertAsync(
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

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("AssertEventually failed"));
    }
}
