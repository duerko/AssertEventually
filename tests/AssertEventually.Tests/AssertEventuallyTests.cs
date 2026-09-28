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
            Assert.IsType<global::Xunit.Sdk.EqualException>(
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
            Assert.Equal(2, report.LastObservedValue);
        }

        [Fact]
        public async Task Formats_a_human_readable_report()
        {
            var exception = await Assert.ThrowsAsync<EventuallyTimeoutException>(() =>
                AssertEventually
                    .That<int>(value => Assert.Equal(42, value))
                    .For(() => 1)
                    .Within(
                        TimeSpan.FromMilliseconds(150),
                        new EventuallyOptions { MaxRecordedAttempts = 1 }));

            var text = EventuallyReportFormatter.Format(exception.Report);

            Assert.Contains("AssertEventually failed", text);
            Assert.Contains("Timeout:", text);
            Assert.Contains("Attempts:", text);
            Assert.Contains("AssertionFailure", text);
            Assert.Contains("EqualException", text);
            Assert.Contains("attempt(s) omitted", text);
            Assert.NotEmpty(exception.Report.TimelineSegments);
        }

        [Fact]
        public async Task Includes_explicit_descriptions_in_reports()
        {
            var exception = await Assert.ThrowsAsync<EventuallyTimeoutException>(() =>
                AssertEventually
                    .That<int>(
                        "order eventually becomes processed",
                        value => Assert.Equal(42, value))
                    .For(
                        "read order from replica",
                        () => 1)
                    .Within(TimeSpan.FromMilliseconds(100)));

            Assert.Equal(
                "order eventually becomes processed; read order from replica",
                exception.Report.Description);
            Assert.Contains(
                "Description: order eventually becomes processed; " +
                "read order from replica",
                EventuallyReportFormatter.Format(exception.Report));
            Assert.Contains(
                "\"description\": \"order eventually becomes processed; " +
                "read order from replica\"",
                EventuallyJsonReportFormatter.Format(exception.Report));
            Assert.Contains(
                "order eventually becomes processed; read order from replica",
                EventuallyHtmlReportFormatter.Format(exception.Report));
        }

        [Fact]
        public async Task Formats_a_safe_structured_json_report()
        {
            var exception = await Assert.ThrowsAsync<EventuallyTimeoutException>(() =>
                AssertEventually
                    .That<int>(value => Assert.Equal(42, value))
                    .For(() => 1)
                    .Within(
                        TimeSpan.FromMilliseconds(150),
                        new EventuallyOptions { MaxRecordedAttempts = 1 }));

            var json = EventuallyJsonReportFormatter.Format(exception.Report);

            Assert.Contains("\"successful\": false", json);
            Assert.Contains("\"attemptHistory\"", json);
            Assert.Contains("\"kind\": \"AssertionFailure\"", json);
            Assert.Contains("\"type\": \"Xunit.Sdk.EqualException\"", json);
        }

        [Fact]
        public async Task Formats_a_standalone_html_report()
        {
            var exception = await Assert.ThrowsAsync<EventuallyTimeoutException>(() =>
                AssertEventually
                    .That<string>(value => Assert.Equal("ready", value))
                    .For(() => "<unsafe>")
                    .Within(TimeSpan.FromMilliseconds(150)));

            var html = EventuallyHtmlReportFormatter.Format(exception.Report);

            Assert.Contains("<!doctype html>", html);
            Assert.Contains("AssertEventually", html);
            Assert.Contains("AssertionFailure", html);
            Assert.Contains("&lt;unsafe&gt;", html);
            Assert.DoesNotContain("<unsafe>", html);
        }

        [Fact]
        public async Task Writes_text_json_and_html_report_artifacts()
        {
            var exception = await Assert.ThrowsAsync<EventuallyTimeoutException>(() =>
                AssertEventually
                    .That<int>(value => Assert.Equal(42, value))
                    .For(() => 1)
                    .Within(TimeSpan.FromMilliseconds(150)));
            var directory = Path.Combine(
                Path.GetTempPath(),
                "AssertEventuallyTests",
                Guid.NewGuid().ToString("N"));

            var artifacts = EventuallyReportArtifactWriter.Write(
                exception.Report,
                directory);

            Assert.True(File.Exists(artifacts.TextPath));
            Assert.True(File.Exists(artifacts.JsonPath));
            Assert.True(File.Exists(artifacts.HtmlPath));
        }

        [Fact]
        public async Task Adapter_runner_delegates_timeout_handling_once()
        {
            EventuallyTimeoutException? timeout = null;

            await EventuallyAdapterRunner.RunAsync(
                AssertEventually
                    .That<int>(value => Assert.Equal(42, value))
                    .For(() => 1),
                TimeSpan.FromMilliseconds(150),
                null,
                CancellationToken.None,
                exception =>
                {
                    timeout = exception;
                    return Task.CompletedTask;
                });

            Assert.NotNull(timeout);
            Assert.NotNull(timeout!.Report);
        }

        [Fact]
        public async Task Formats_public_properties_for_default_domain_values()
        {
            var exception = await Assert.ThrowsAsync<EventuallyTimeoutException>(() =>
                AssertEventually
                    .That<DomainValue>(value => Assert.Equal("Processed", value.Status))
                    .For(() => new DomainValue { Id = 123, Status = "Pending" })
                    .Within(TimeSpan.FromMilliseconds(150)));

            var text = EventuallyReportFormatter.Format(exception.Report);

            Assert.Contains("Id = 123", text);
            Assert.Contains("Status = Pending", text);
        }

        [Fact]
        public void Keeps_the_fluent_entry_point_explicitly_available()
        {
            var assertion = global::AssertEventually.AssertEventually
                .That<int>(value => Assert.Equal(1, value));

            Assert.NotNull(assertion);
        }

        private sealed class DomainValue
        {
            public int Id { get; init; }

            public string Status { get; init; } = string.Empty;
        }

        [Fact]
        public async Task Applies_report_value_limits_and_redaction()
        {
            var execution = AssertEventually
                .That<string>(value => Assert.Equal("token=secret", value))
                .For(() => "token=secret");
            await execution.Within(TimeSpan.FromMilliseconds(150));

            var options = new EventuallyReportFormattingOptions
            {
                MaxStringLength = 8,
                Redact = value => value.Replace("secret", "[redacted]")
            };

            var text = EventuallyReportFormatter.Format(execution.Report!, options);

            Assert.Contains("token=[r...", text);
            Assert.DoesNotContain("secret", text);
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
            Assert.NotEmpty(exception.Report.ExceptionGroups);
            Assert.Equal(
                exception.Report.AttemptCount,
                exception.Report.Attempts[^1].Number);
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