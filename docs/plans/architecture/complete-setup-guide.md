# Complete Setup Guide: CI/CD, Testing & Staging Environment

This guide covers the complete setup from scratch for CI/CD pipelines, testing infrastructure, and a staging environment.

## Overview

| Component | Status |
|-----------|--------|
| Production Site | https://www.thepredictions.co.uk |
| Staging Site | https://staging.thepredictions.co.uk (to be created) |
| Production Database | Fasthosts SQL Server |
| Development Database | Fasthosts SQL Server (to be created) |
| CI/CD Platform | GitHub Actions (free tier) |

---

## Phase 1: Fasthosts Database Setup

| Step | Who | Task | Details |
|------|-----|------|---------|
| 1.1 | **You** | Log into Fasthosts control panel | https://www.fasthosts.co.uk |
| 1.2 | **You** | Create dev database | Go to SQL Server → Create new database → Name: `PredictionLeague_Dev` |
| 1.3 | **You** | Note the dev connection string | Copy the connection string for the new database |
| 1.4 | **You** | Run schema on dev database | Use SSMS or Azure Data Studio to run your schema creation scripts against the new dev DB |

---

## Phase 2: Fasthosts Staging Subdomain Setup

| Step | Who | Task | Details |
|------|-----|------|---------|
| 2.1 | **You** | Log into Fasthosts control panel | Go to Domains section |
| 2.2 | **You** | Create subdomain | Add subdomain: `staging.thepredictions.co.uk` |
| 2.3 | **You** | Note the FTP path | Usually `/staging/` or `/staging.thepredictions.co.uk/` - check what Fasthosts creates |
| 2.4 | **You** | Note if separate FTP credentials | Some hosts give separate credentials per subdomain |
| 2.5 | **You** | Set up SSL certificate | Request SSL for `staging.thepredictions.co.uk` (Fasthosts may do this automatically or via Let's Encrypt) |

---

## Phase 3: Staging App Configuration

| Step | Who | Task | Details |
|------|-----|------|---------|
| 3.1 | Claude | Create `appsettings.Staging.json` | Configuration pointing to dev database |
| 3.2 | Claude | Update deploy workflow | Add staging deployment option |

---

## Phase 4: GitHub Secrets Setup

| Step | Who | Task | Details |
|------|-----|------|---------|
| 4.1 | **You** | Go to GitHub repository | https://github.com/AntonyW94/PredictionLeague |
| 4.2 | **You** | Navigate to secrets | Settings → Secrets and variables → Actions |
| 4.3 | **You** | Add `FTP_SERVER` | Your Fasthosts FTP server (e.g., `ftp.fasthosts.co.uk`) |
| 4.4 | **You** | Add `FTP_USERNAME` | Your Fasthosts FTP username (production) |
| 4.5 | **You** | Add `FTP_PASSWORD` | Your Fasthosts FTP password (production) |
| 4.6 | **You** | Add `FTP_USERNAME_STAGING` | Staging FTP username (may be same as prod) |
| 4.7 | **You** | Add `FTP_PASSWORD_STAGING` | Staging FTP password (may be same as prod) |
| 4.8 | **You** | Add `FTP_PATH_STAGING` | Staging FTP path (e.g., `/staging/` or `/staging.thepredictions.co.uk/`) |
| 4.9 | **You** | Add `PROD_CONNECTION_STRING` | Your production SQL connection string |
| 4.10 | **You** | Add `DEV_CONNECTION_STRING` | Your new dev SQL connection string (from step 1.3) |
| 4.11 | **You** | Add `TEST_ACCOUNT_PASSWORD` | Choose a password for test accounts (e.g., `TestPassword123!`) |

---

## Phase 5: GitHub Actions Workflows

| Step | Who | Task | Details |
|------|-----|------|---------|
| 5.1 | Claude | Create `.github/workflows/ci.yml` | Build and test on every push/PR |
| 5.2 | Claude | Create `.github/workflows/deploy-prod.yml` | Manual deploy to production |
| 5.3 | Claude | Create `.github/workflows/deploy-staging.yml` | Manual deploy to staging |
| 5.4 | Claude | Create `.github/workflows/refresh-dev-db.yml` | Weekly database refresh |
| 5.5 | Claude | Create `.github/workflows/e2e.yml` | Playwright tests against staging |

---

## Phase 6: Tools for Database Management

| Step | Who | Task | Details |
|------|-----|------|---------|
| 6.1 | Claude | Create `tools/PredictionLeague.DevDbRefresh/` | Tool to copy prod → dev with anonymisation |
| 6.2 | Claude | Create `tools/PredictionLeague.TestDbSeeder/` | Tool to seed SQLite for E2E tests |

---

## Phase 7: Initial Database Refresh

| Step | Who | Task | Details |
|------|-----|------|---------|
| 7.1 | **You** | Verify secrets are set | Double-check all secrets in GitHub |
| 7.2 | **You** | Merge PR with tools | Merge the code Claude creates |
| 7.3 | **You** | Manually trigger DB refresh | Actions → Refresh Dev Database → Run workflow |
| 7.4 | **You** | Verify refresh succeeded | Check workflow logs |
| 7.5 | **You** | Verify test accounts exist | Connect to dev DB and check `testplayer@dev.local` and `testadmin@dev.local` exist |

---

## Phase 8: Test Infrastructure Setup

| Step | Who | Task | Details |
|------|-----|------|---------|
| 8.1 | Claude | Create `tests/PredictionLeague.Tests.Shared/` | Shared fixtures, builders, helpers |
| 8.2 | Claude | Create `tests/PredictionLeague.Domain.Tests/` | Domain unit tests |
| 8.3 | Claude | Create `tests/PredictionLeague.Validators.Tests/` | Validator unit tests |
| 8.4 | Claude | Create `tests/PredictionLeague.Application.Tests/` | Handler unit tests |
| 8.5 | Claude | Create `tests/PredictionLeague.Infrastructure.Tests/` | Integration tests (SQLite) |
| 8.6 | Claude | Create `tests/PredictionLeague.API.Tests/` | API integration tests |
| 8.7 | Claude | Create `tests/PredictionLeague.E2E.Tests/` | Playwright E2E tests |
| 8.8 | Claude | Add all test projects to solution | Update `PredictionLeague.sln` |

---

## Phase 9: Write Tests

| Step | Who | Task | Details |
|------|-----|------|---------|
| 9.1 | Claude | Write Domain tests | League, Round, UserPrediction, BoostEligibilityEvaluator |
| 9.2 | Claude | Write Validator tests | All 28 validators |
| 9.3 | Claude | Write Application tests | Command handlers |
| 9.4 | Claude | Write Infrastructure tests | Query handlers, repositories |
| 9.5 | Claude | Write API tests | Controller integration tests |
| 9.6 | Claude | Write E2E tests | Login, predictions, leaderboards |
| 9.7 | **You** | Review tests | Check for accuracy, edge cases |
| 9.8 | **You** | Run tests locally | Verify they pass in Visual Studio |

---

## Phase 10: First Staging Deployment

| Step | Who | Task | Details |
|------|-----|------|---------|
| 10.1 | **You** | Merge test PR | Merge the code with all tests |
| 10.2 | **You** | Verify CI passes | Check Actions tab for green build |
| 10.3 | **You** | Deploy to staging | Actions → Deploy to Staging → Run workflow |
| 10.4 | **You** | Test staging site | Visit https://staging.thepredictions.co.uk |
| 10.5 | **You** | Login with test account | Use `testplayer@dev.local` / your TEST_ACCOUNT_PASSWORD |

---

## Phase 11: E2E Tests Against Staging

| Step | Who | Task | Details |
|------|-----|------|---------|
| 11.1 | **You** | Trigger E2E tests | Actions → E2E Tests → Run workflow |
| 11.2 | **You** | Review results | Check for failures, view screenshots |
| 11.3 | Claude | Fix any failing tests | Adjust selectors or test logic |

---

## Phase 12: Production Deployment (When Ready)

| Step | Who | Task | Details |
|------|-----|------|---------|
| 12.1 | **You** | Review all tests pass | CI green, E2E green |
| 12.2 | **You** | Deploy to production | Actions → Deploy to Production → Type "deploy" |
| 12.3 | **You** | Verify production | Visit https://www.thepredictions.co.uk |

---

## Summary: Who Does What

### You (Manual Tasks)

| Phase | Tasks |
|-------|-------|
| **Fasthosts** | Create dev database, create staging subdomain, SSL setup |
| **GitHub** | Add all secrets (11 total) |
| **Verification** | Merge PRs, trigger workflows, test sites, review tests |

### Claude (Code Tasks)

| Phase | Tasks |
|-------|-------|
| **Workflows** | Create all 5 GitHub Actions workflow files |
| **Tools** | Create DB refresh tool, test seeder tool |
| **Config** | Create staging appsettings |
| **Tests** | Create all 7 test projects, write all tests |

---

## Quick Reference: All GitHub Secrets Needed

| Secret | Example Value | Description |
|--------|---------------|-------------|
| `FTP_SERVER` | `ftp.fasthosts.co.uk` | FTP server hostname |
| `FTP_USERNAME` | `your-ftp-username` | Production FTP username |
| `FTP_PASSWORD` | `your-ftp-password` | Production FTP password |
| `FTP_USERNAME_STAGING` | `your-ftp-username` | Staging FTP username (may be same as prod) |
| `FTP_PASSWORD_STAGING` | `your-ftp-password` | Staging FTP password (may be same as prod) |
| `FTP_PATH_STAGING` | `/staging.thepredictions.co.uk/` | FTP path to staging site |
| `PROD_CONNECTION_STRING` | `Server=sql...;Database=PredictionLeague;...` | Production database connection |
| `DEV_CONNECTION_STRING` | `Server=sql...;Database=PredictionLeague_Dev;...` | Dev database connection |
| `TEST_ACCOUNT_PASSWORD` | `TestPassword123!` | Password for test accounts |

---

## Test Accounts (Created by DB Refresh)

| Account | Email | Role | Notes |
|---------|-------|------|-------|
| Test Player | `testplayer@dev.local` | User | Regular user for testing |
| Test Admin | `testadmin@dev.local` | Admin | Admin user for testing |

Both accounts use the password stored in `TEST_ACCOUNT_PASSWORD` secret.

---

## Related Documentation

- [Test Suite Plan](./test-suite-plan.md) - Comprehensive testing strategy
- [GitHub Actions CI/CD Plan](./github-actions-cicd-plan.md) - Detailed workflow documentation

---

## Starting a New Session

When starting a new Claude session to continue this work:

1. Reference this document: `docs/plans/architecture/complete-setup-guide.md`
2. Tell Claude which phase you're on
3. Let Claude know which steps you've completed
4. Claude can then pick up where you left off

**Example prompt for new session:**
> "I'm working on setting up CI/CD and testing for PredictionLeague. I've completed Phases 1-4 (created dev database, staging subdomain, and added all GitHub secrets). Please continue from Phase 5 and create the GitHub Actions workflow files."
