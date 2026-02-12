# Staging Environment Setup

## Status

**Not Started** | In Progress | Complete

## Overview

This document covers the setup of a staging environment for PredictionLeague, including subdomain configuration, SSL, and deployment configuration.

| Component | Value |
|-----------|-------|
| Staging Site | https://staging.thepredictions.co.uk (to be created) |
| Production Site | https://www.thepredictions.co.uk |
| Hosting Provider | Fasthosts |

---

## Phase 1: Fasthosts Staging Subdomain Setup

| Step | Who | Task | Details |
|------|-----|------|---------|
| 1.1 | **You** | Log into Fasthosts control panel | Go to Domains section |
| 1.2 | **You** | Create subdomain | Add subdomain: `staging.thepredictions.co.uk` |
| 1.3 | **You** | Note the FTP path | Usually `/staging/` or `/staging.thepredictions.co.uk/` - check what Fasthosts creates |
| 1.4 | **You** | Note if separate FTP credentials | Some hosts give separate credentials per subdomain |
| 1.5 | **You** | Set up SSL certificate | Request SSL for `staging.thepredictions.co.uk` (Fasthosts may do this automatically or via Let's Encrypt) |

---

## Phase 2: Staging App Configuration

| Step | Who | Task | Details |
|------|-----|------|---------|
| 2.1 | Claude | Create `appsettings.Staging.json` | Configuration pointing to dev database |
| 2.2 | Claude | Update deploy workflow | Add staging deployment option |

### appsettings.Staging.json

Create this file in the API project:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sql.fasthosts.co.uk;Database=PredictionLeague_Dev;User Id=xxx;Password=xxx;TrustServerCertificate=True"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Note:** The actual connection string should come from GitHub Secrets, not be committed to the repository.

---

## Phase 3: GitHub Secrets for Staging

These secrets need to be added to the GitHub repository:

| Secret | Description | Example |
|--------|-------------|---------|
| `FTP_USERNAME_STAGING` | Staging FTP username (may be same as prod) | `your-ftp-username` |
| `FTP_PASSWORD_STAGING` | Staging FTP password (may be same as prod) | `your-ftp-password` |
| `FTP_PATH_STAGING` | Staging FTP path | `/staging.thepredictions.co.uk/` |

---

## Phase 4: First Staging Deployment

| Step | Who | Task | Details |
|------|-----|------|---------|
| 4.1 | **You** | Verify secrets are set | Double-check all staging secrets in GitHub |
| 4.2 | **You** | Merge PR with staging config | Merge the code with staging configuration |
| 4.3 | **You** | Deploy to staging | Actions → Deploy to Staging → Run workflow |
| 4.4 | **You** | Test staging site | Visit https://staging.thepredictions.co.uk |
| 4.5 | **You** | Login with test account | Use `testplayer@dev.local` / your TEST_ACCOUNT_PASSWORD |

---

## Verification Checklist

- [ ] Subdomain created in Fasthosts
- [ ] SSL certificate active for staging subdomain
- [ ] FTP path noted and configured in GitHub Secrets
- [ ] `appsettings.Staging.json` created (without secrets)
- [ ] GitHub workflow updated for staging deployment
- [ ] First deployment successful
- [ ] Site accessible at https://staging.thepredictions.co.uk
- [ ] Test account login working

---

## Related Documentation

- [CI/CD Plan](../ci-cd/README.md) - GitHub Actions workflow details
- [Test Suite Plan](../test-suite/README.md) - E2E tests against staging
