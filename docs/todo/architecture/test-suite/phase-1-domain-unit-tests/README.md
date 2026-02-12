# Phase 1: Domain Unit Tests

## Status

**Not Started** | In Progress | Complete

## Summary

Create comprehensive unit tests for all domain entities and services. This phase includes introducing `IDateTimeProvider` to make the domain layer testable, then writing pure unit tests with a simple hand-rolled fake — no mocking framework or database required.

## Acceptance Criteria

- [ ] Test project builds and all tests pass
- [ ] `IDateTimeProvider` introduced and all `DateTime.UtcNow` calls replaced in the domain layer
- [ ] Every entity factory method has validation tests
- [ ] Every business logic method has happy-path and edge-case tests
- [ ] BoostEligibilityEvaluator has full branch coverage
- [ ] UserPrediction.SetOutcome covers all scoring scenarios
- [ ] League winner/ranking methods handle ties and empty collections
- [ ] Test naming follows `{Method}_Should{Behaviour}_When{Conditions}` convention

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 0 | [Project setup](./00-project-setup.md) | Create test project, packages, solution references | Not Started |
| 0a | [Introduce IDateTimeProvider](./00a-datetime-provider.md) | Replace `DateTime.UtcNow` with injectable provider, create `FakeDateTimeProvider` | Not Started |
| 1 | [UserPrediction tests](./01-user-prediction-tests.md) | Core scoring algorithm (SetOutcome) and factory | Not Started |
| 2 | [BoostEligibilityEvaluator tests](./02-boost-eligibility-evaluator-tests.md) | Most complex domain logic with 12+ conditional paths | Not Started |
| 3 | [League winners and rankings tests](./03-league-winners-rankings-tests.md) | GetRoundWinners, GetPeriodWinners, GetOverallRankings, GetMostExactScoresWinners | Not Started |
| 4 | [League management tests](./04-league-management-tests.md) | Create, UpdateDetails, AddMember, RemoveMember, prizes, entry codes | Not Started |
| 5 | [Round tests](./05-round-tests.md) | Create, status transitions, match management | Not Started |
| 6 | [Match and Team tests](./06-match-team-tests.md) | Match and Team factory methods and business logic | Not Started |
| 7 | [Season tests](./07-season-tests.md) | Season factory, validation, duration constraints | Not Started |
| 8 | [LeagueMember tests](./08-league-member-tests.md) | Status transitions (Approve, Reject, DismissAlert) | Not Started |
| 9 | [Supporting entity tests](./09-supporting-entity-tests.md) | Winning, LeaguePrizeSetting, ApplicationUser, PasswordResetToken, RefreshToken | Not Started |
| 10 | [Domain service and utility tests](./10-domain-service-utility-tests.md) | PredictionDomainService, NameValidator | Not Started |

## Dependencies

- Task 0a (`IDateTimeProvider`) must be completed before writing tests — factory methods and business logic methods require the provider as a parameter

## Technical Notes

- All tests use xUnit.v3 and FluentAssertions
- A hand-rolled `FakeDateTimeProvider` is used to control time in tests — no mocking framework needed for domain unit tests
- `FakeDateTimeProvider` has a settable `UtcNow` property, allowing tests to advance time mid-test (useful for token expiry, deadline checks)
- Follow test naming convention: `{Method}_Should{Behaviour}_When{Conditions}`
- One test class per entity/service, mirroring the source folder structure
