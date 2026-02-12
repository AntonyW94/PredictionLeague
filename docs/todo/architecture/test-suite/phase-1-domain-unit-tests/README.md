# Phase 1: Domain Unit Tests

## Status

**Not Started** | In Progress | Complete

## Summary

Create comprehensive unit tests for all domain entities and services. These are pure unit tests with no mocks or database required, offering the highest ROI for the lowest effort.

## Acceptance Criteria

- [ ] Test project builds and all tests pass
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

- None — these are pure unit tests with no infrastructure dependencies

## Technical Notes

- All tests use xUnit.v3 and FluentAssertions
- No mocking required — all domain entity methods are pure (entry code uniqueness checking was moved to the command handler)
- Follow test naming convention: `{Method}_Should{Behaviour}_When{Conditions}`
- One test class per entity/service, mirroring the source folder structure
