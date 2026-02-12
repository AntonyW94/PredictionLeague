# Task: Configure GitHub Secrets

**Parent Feature:** [Development Database Refresh](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add the required connection strings and test account password as GitHub repository secrets so the refresh workflow can access both databases securely.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| N/A (GitHub UI task) | N/A | Secrets configured via GitHub repository settings |

## Implementation Steps

### Step 1: Navigate to GitHub Secrets

1. Go to the repository on GitHub: `https://github.com/AntonyW94/PredictionLeague`
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret** for each secret below

### Step 2: Add Production Connection String

| Field | Value |
|-------|-------|
| Name | `PROD_CONNECTION_STRING` |
| Value | `Server=mssql04.mssql.prositehosting.net;Database=ThePredictions;User ID=Refresh;Password=YSBe3ABkptBx2t;TrustServerCertificate=True;` |

### Step 3: Add Development Connection String

| Field | Value |
|-------|-------|
| Name | `DEV_CONNECTION_STRING` |
| Value | `Server=mssql04.mssql.prositehosting.net;Database=ThePredictionsDev;User ID=RefreshDev;Password=THev_05c4r;TrustServerCertificate=True;` |

### Step 4: Add Test Account Password

| Field | Value |
|-------|-------|
| Name | `TEST_ACCOUNT_PASSWORD` |
| Value | `THns_05c4r` |

## Verification

- [ ] `PROD_CONNECTION_STRING` secret exists in GitHub repository settings
- [ ] `DEV_CONNECTION_STRING` secret exists in GitHub repository settings
- [ ] `TEST_ACCOUNT_PASSWORD` secret exists in GitHub repository settings
- [ ] All three secrets show as "Updated" with the correct date

## Edge Cases to Consider

- If the repository is a fork, secrets may need to be set on the fork specifically
- Secrets are not available in pull requests from forks (only relevant if using fork-based workflow)

## Notes

These secrets are consumed by the GitHub Actions workflow (Task 4) and passed as environment variables to the refresh tool. They must never be committed to source control.
