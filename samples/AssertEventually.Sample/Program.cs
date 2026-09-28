using AssertEventually;

var attempts = 0;

await global::AssertEventually.AssertEventually
    .That<int>(
        "sample value becomes ready",
        value =>
        {
            if (value != 2)
                throw new InvalidOperationException("not ready");
        })
    .For("read sample value", () => ++attempts)
    .Within(TimeSpan.FromSeconds(1));

Console.WriteLine($"Converged after {attempts} attempt(s).");
