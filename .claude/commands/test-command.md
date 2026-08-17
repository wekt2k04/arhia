Invoke the qa-executioner agent to mandate and write tests for the following code. The agent must: identify all missing test scenarios (happy path, edge cases, IDOR, fail-closed, business invariants — see docs/LOGIQUE_METIER.md for the current V8 business rules), write the complete xUnit/Moq/FluentAssertions test code, and run `dotnet test -c Release` to verify the full suite is green (N/N). Do not declare coverage without running the suite.

$ARGUMENTS
