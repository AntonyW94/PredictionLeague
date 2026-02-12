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
| 1 | [Project setup](./01-project-setup.md) | Create test project, packages, solution references | Not Started |
| 2 | [Introduce IDateTimeProvider](./02-datetime-provider.md) | Replace `DateTime.UtcNow` with injectable provider, create `FakeDateTimeProvider` | Complete |
| 3 | [UserPrediction tests](./03-user-prediction-tests.md) | Core scoring algorithm (SetOutcome) and factory | Not Started |
| 4 | [BoostEligibilityEvaluator tests](./04-boost-eligibility-evaluator-tests.md) | Most complex domain logic with 12+ conditional paths | Not Started |
| 5 | [League winners and rankings tests](./05-league-winners-rankings-tests.md) | GetRoundWinners, GetPeriodWinners, GetOverallRankings, GetMostExactScoresWinners | Not Started |
| 6 | [League management tests](./06-league-management-tests.md) | Create, UpdateDetails, AddMember, RemoveMember, prizes, entry codes | Not Started |
| 7 | [Round tests](./07-round-tests.md) | Create, status transitions, match management | Not Started |
| 8 | [Match and Team tests](./08-match-team-tests.md) | Match and Team factory methods and business logic | Not Started |
| 9 | [Season tests](./09-season-tests.md) | Season factory, validation, duration constraints | Not Started |
| 10 | [LeagueMember tests](./10-league-member-tests.md) | Status transitions (Approve, Reject, DismissAlert) | Not Started |
| 11 | [Supporting entity tests](./11-supporting-entity-tests.md) | Winning, LeaguePrizeSetting, ApplicationUser, PasswordResetToken, RefreshToken, LeagueRoundResult | Not Started |
| 12 | [Domain service and utility tests](./12-domain-service-utility-tests.md) | PredictionDomainService, NameValidator | Not Started |

## Dependencies

- Task 2 (`IDateTimeProvider`) must be completed before writing tests — factory methods and business logic methods require the provider as a parameter

## Technical Notes

- All tests use xUnit.v3 and FluentAssertions
- A hand-rolled `FakeDateTimeProvider` is used to control time in tests — no mocking framework needed for domain unit tests
- `FakeDateTimeProvider` has a settable `UtcNow` property, allowing tests to advance time mid-test (useful for token expiry, deadline checks)
- Follow test naming convention: `{Method}_Should{Behaviour}_When{Conditions}`
- One test class per entity/service, mirroring the source folder structure
