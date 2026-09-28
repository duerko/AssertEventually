using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<EventuallyBenchmarks>();

public class EventuallyBenchmarks
{
    [Benchmark]
    public async Task ImmediateSuccess()
    {
        await AssertEventually.AssertEventually
            .That<int>(value => _ = value)
            .For(() => 1)
            .Within(TimeSpan.FromSeconds(1));
    }

    [Benchmark]
    public async Task DiagnosticHistory()
    {
        var attempt = 0;
        await AssertEventually.AssertEventually
            .That<int>(value => _ = value)
            .For(() => ++attempt)
            .Within(
                TimeSpan.FromSeconds(1),
                new AssertEventually.EventuallyOptions
                {
                    MaxRecordedAttempts = 10
                });
    }
}
