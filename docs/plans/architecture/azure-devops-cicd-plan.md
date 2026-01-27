# Azure DevOps CI/CD Pipeline Plan for PredictionLeague

## Overview

Set up a single Azure DevOps pipeline with two stages:
1. **Build Stage** - Runs automatically on push/PR (CI)
2. **Deploy Stage** - Manual trigger with approval gate (CD)

## Configuration Summary

| Setting | Value |
|---------|-------|
| Key Vault (Prod) | `https://the-predictions-prod.vault.azure.net` |
| Key Vault (Dev) | `https://the-predictions-dev.vault.azure.net` |
| FTP Host | `ftp.fasthosts.co.uk` |
| FTP Path | `/htdocs` |
| Build Strictness | Fail on warnings |
| Pipeline Type | Single pipeline (Build + Deploy stages) |

## Secrets Required in Key Vault

Add these secrets to your Azure Key Vault:

| Secret Name | Description |
|-------------|-------------|
| `Ftp-Host` | FTP server hostname (`ftp.fasthosts.co.uk`) |
| `Ftp-Username` | FTP username |
| `Ftp-Password` | FTP password |
| `Azure-TenantId` | Azure AD tenant ID for Key Vault auth |
| `Azure-ClientId` | Azure AD app registration client ID |
| `Azure-ClientSecret` | Azure AD app registration client secret |

---

## Phase 1: Azure Portal Setup (User Actions)

### 1.1 Create Azure AD App Registration (for Key Vault access)

If you don't already have one:

1. Go to **Azure Portal** > **Azure Active Directory** > **App registrations**
2. Click **New registration**
3. Name: `PredictionLeague-Pipeline`
4. Click **Register**
5. Note the **Application (client) ID** and **Directory (tenant) ID**
6. Go to **Certificates & secrets** > **New client secret**
7. Create a secret and **copy the value immediately** (you won't see it again)

### 1.2 Grant Key Vault Access to App Registration

1. Go to **Key Vault** (`the-predictions-prod`)
2. **Access policies** > **Add Access Policy**
3. Secret permissions: **Get**, **List**
4. Select principal: `PredictionLeague-Pipeline` (the app you created)
5. Click **Add**, then **Save**

### 1.3 Add Secrets to Key Vault

Add these 6 secrets to Key Vault:
- `Ftp-Host` = `ftp.fasthosts.co.uk`
- `Ftp-Username` = (your FTP username)
- `Ftp-Password` = (your FTP password)
- `Azure-TenantId` = (from step 1.1)
- `Azure-ClientId` = (from step 1.1)
- `Azure-ClientSecret` = (from step 1.1)

---

## Phase 2: Azure DevOps Setup (User Actions)

### 2.1 Create Azure DevOps Project

1. Go to https://dev.azure.com
2. Create new project: `PredictionLeague`
3. Visibility: Private

### 2.2 Connect GitHub Repository

1. **Project Settings** > **Service connections** > **New service connection**
2. Select **GitHub**
3. Choose **OAuth** or **Personal Access Token**
4. Authorize and select your `AntonyW94/PredictionLeague` repository

### 2.3 Create Azure Resource Manager Service Connection

1. **Project Settings** > **Service connections** > **New service connection**
2. Select **Azure Resource Manager**
3. Choose **Service principal (automatic)** or **Service principal (manual)**
4. Select subscription containing your Key Vault
5. Name: `Azure-KeyVault-Connection`
6. Grant access to all pipelines

### 2.4 Create Variable Group Linked to Key Vault

1. **Pipelines** > **Library** > **+ Variable group**
2. Name: `PredictionLeague-Secrets`
3. Toggle **Link secrets from an Azure key vault as variables**
4. Select Azure subscription: `Azure-KeyVault-Connection`
5. Select Key Vault: `the-predictions-prod`
6. Click **+ Add** and select all 6 secrets:
   - `Ftp-Host`
   - `Ftp-Username`
   - `Ftp-Password`
   - `Azure-TenantId`
   - `Azure-ClientId`
   - `Azure-ClientSecret`
7. Click **Save**

### 2.5 Create Deployment Environment with Approval

1. **Pipelines** > **Environments** > **New environment**
2. Name: `Production`
3. Resource: **None**
4. Click **Create**
5. Click on the environment > **...** (three dots) > **Approvals and checks**
6. **+ Add check** > **Approvals**
7. Add yourself as approver
8. Click **Create**

---

## Phase 3: Pipeline File

### File: `/azure-pipelines.yml`

```yaml
# Azure DevOps CI/CD Pipeline for PredictionLeague
# Triggers on push to master branch and pull requests

trigger:
  branches:
    include:
      - master

pr:
  branches:
    include:
      - master

variables:
  - group: PredictionLeague-Secrets
  - name: buildConfiguration
    value: 'Release'
  - name: dotnetVersion
    value: '8.x'
  - name: projectPath
    value: 'PredictionLeague.Web/PredictionLeague.Web/PredictionLeague.Web.csproj'

stages:
  # ============================================
  # BUILD STAGE - Runs on every push/PR
  # ============================================
  - stage: Build
    displayName: 'Build & Test'
    jobs:
      - job: BuildJob
        displayName: 'Build Application'
        pool:
          vmImage: 'windows-latest'

        steps:
          # Checkout source code
          - checkout: self
            fetchDepth: 1

          # Install .NET SDK
          - task: UseDotNet@2
            displayName: 'Install .NET $(dotnetVersion)'
            inputs:
              packageType: 'sdk'
              version: '$(dotnetVersion)'

          # Restore NuGet packages
          - task: DotNetCoreCLI@2
            displayName: 'Restore packages'
            inputs:
              command: 'restore'
              projects: 'PredictionLeague.sln'

          # Build solution (fail on warnings)
          - task: DotNetCoreCLI@2
            displayName: 'Build solution'
            inputs:
              command: 'build'
              projects: 'PredictionLeague.sln'
              arguments: '--configuration $(buildConfiguration) --no-restore /p:TreatWarningsAsErrors=true'

          # Create appsettings.Production.Secrets.json for Key Vault auth
          # Structure must match: Configuration["AzureCredentials:TenantId"] etc.
          - task: PowerShell@2
            displayName: 'Create secrets config file'
            inputs:
              targetType: 'inline'
              script: |
                $secretsPath = "$(Build.SourcesDirectory)/PredictionLeague.Web/PredictionLeague.Web/appsettings.Production.Secrets.json"
                $secrets = @{
                  AzureCredentials = @{
                    TenantId = "$(Azure-TenantId)"
                    ClientId = "$(Azure-ClientId)"
                    ClientSecret = "$(Azure-ClientSecret)"
                  }
                }
                $secrets | ConvertTo-Json -Depth 3 | Set-Content $secretsPath
                Write-Host "Created secrets config at: $secretsPath"

          # Publish application
          - task: DotNetCoreCLI@2
            displayName: 'Publish application'
            inputs:
              command: 'publish'
              projects: '$(projectPath)'
              arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)/publish --no-build'
              publishWebProjects: false

          # Publish build artifacts
          - task: PublishBuildArtifacts@1
            displayName: 'Publish artifacts'
            inputs:
              PathtoPublish: '$(Build.ArtifactStagingDirectory)/publish'
              ArtifactName: 'webapp'
              publishLocation: 'Container'

  # ============================================
  # DEPLOY STAGE - Manual trigger with approval
  # ============================================
  - stage: Deploy
    displayName: 'Deploy to Production'
    dependsOn: Build
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/master'))
    jobs:
      - deployment: DeployJob
        displayName: 'Deploy via FTP'
        pool:
          vmImage: 'windows-latest'
        environment: 'Production'
        strategy:
          runOnce:
            deploy:
              steps:
                # Download build artifacts
                - download: current
                  artifact: webapp
                  displayName: 'Download artifacts'

                # Deploy to Fasthosts via FTP
                - task: FtpUpload@2
                  displayName: 'FTP Upload to Fasthosts'
                  inputs:
                    credentialsOption: 'inputs'
                    serverUrl: 'ftp://$(Ftp-Host)'
                    username: '$(Ftp-Username)'
                    password: '$(Ftp-Password)'
                    rootDirectory: '$(Pipeline.Workspace)/webapp'
                    remoteDirectory: '/htdocs'
                    clean: false
                    cleanContents: false
                    preservePaths: true
                    trustSSL: true
```

---

## Phase 4: Create Pipeline in Azure DevOps (User Actions)

### 4.1 Commit Pipeline File

After Claude creates `azure-pipelines.yml`:

1. Review the file in the pull request
2. Merge to master branch

### 4.2 Create Pipeline from YAML

1. **Pipelines** > **New pipeline**
2. **Where is your code?** > **GitHub**
3. Select repository: `AntonyW94/PredictionLeague`
4. **Configure your pipeline** > **Existing Azure Pipelines YAML file**
5. Branch: `master`
6. Path: `/azure-pipelines.yml`
7. Click **Continue**, then **Run**

---

## Pipeline Behaviour Summary

| Trigger | Build Stage | Deploy Stage |
|---------|-------------|--------------|
| Push to `master` | Runs automatically | Waits for approval |
| Pull request to `master` | Runs automatically | Skipped (PR builds don't deploy) |
| Manual run | Runs | Waits for approval |

### Deployment Flow

1. Code pushed to `master`
2. Build stage runs automatically
3. If build succeeds, deploy stage is queued
4. You receive approval notification
5. You approve (or reject) the deployment
6. If approved, FTP upload begins
7. Site is live after upload completes

---

## Files to Create

| File | Description |
|------|-------------|
| `/azure-pipelines.yml` | Main pipeline configuration |

---

## Verification Steps

After setup is complete:

1. **Test CI**: Create a PR with a small change - verify build runs
2. **Test Build Failure**: Introduce a warning - verify build fails
3. **Test CD**: Merge PR to master - verify approval request appears
4. **Test Deployment**: Approve deployment - verify FTP upload succeeds
5. **Test Site**: Visit https://www.thepredictions.co.uk - verify site works

---

## Future Enhancements (Out of Scope)

- Add unit tests to build stage
- Add staging environment
- Add Slack/Teams notifications
- Add branch policies requiring successful builds
