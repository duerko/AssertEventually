# AssertEventually

AssertEventually is a .NET testing library for eventual consistency.

It is designed for tests where the intermediate state of a system
does not matter — only that the system eventually reaches the
expected state within a given amount of time.

Failures and exceptions during the observation period are treated
as intermediate observations. The test only fails if the expected
condition is not eventually met.