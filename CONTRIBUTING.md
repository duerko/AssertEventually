# Contributing

## Development requirements

- .NET 8 SDK or newer
- Git

## Validate changes

Run the core test suite:

```bash
dotnet test tests/AssertEventually.Tests/AssertEventually.Tests.csproj
```

Build the solution and package projects when changing public APIs or
packaging:

```bash
dotnet build AssertEventually.sln
dotnet pack src/AssertEventually/AssertEventually.csproj
```

Keep roadmap work incremental: update `tests/AssertEventually.Tests/plan.md`,
update directly related documentation, and commit each completed roadmap
point separately. CI setup is intentionally deferred until requested.

## Pull requests

Describe the behavior change, include focused tests, and call out any
intentional compatibility or diagnostic-format changes.
