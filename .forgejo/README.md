# Forgejo Actions CI/CD Setup

This directory contains the CI/CD workflows for the AspireDeezNuts project using Forgejo Actions.

## Workflows Overview

### 1. CI Pipeline (`ci.yml`)
**Triggers**: Push/PR to `main` and `release/*` branches

**Jobs**:
- **Test**: Runs all unit tests using `make test`
- **Build Containers**: Builds and pushes Docker images to Forgejo registry (only on main/release branches)
- **Quality Gate**: Ensures tests pass and builds succeed before allowing merges

### 2. GitFlow Release Management (`gitflow-release.yml`)
**Triggers**: Push to `release/*` branches or manual dispatch

**Jobs**:
- **Create Release PR**: Automatically creates PR from release branch to main
- Adds release labels and enables auto-merge
- Prevents direct pushes to main, enforcing GitFlow via PRs

### 3. Release Publishing (`release-publish.yml`)
**Triggers**: Git tags (`v*`), GitHub releases, or manual dispatch

**Jobs**:
- **Build and Publish**: Creates versioned Docker images with multiple tags
- **Create Deployment Manifests**: Updates Kubernetes manifests with new image versions
- Publishes to Forgejo container registry with semantic versioning

## Required Secrets

Configure these in your Forgejo repository settings:

```bash
# Repository Settings > Actions > Secrets
RUNNER_TOKEN          # Personal access token with repository and package permissions
```

### Creating the RUNNER_TOKEN:
1. Go to Forgejo User Settings > Applications > Access Tokens
2. Create new token with scopes:
   - `write:repository` (for repository access)
   - `write:package` (for container registry)
   - `read:user` (for user info)
3. Copy the token and add it as `RUNNER_TOKEN` secret

## Container Registry

Images are published to your Forgejo instance's container registry:
```
<your-forgejo-url>/<username>/<repo>/aspire-deez-nuts-api:<version>
<your-forgejo-url>/<username>/<repo>/aspire-deez-nuts-web:<version>
```

### Supported Tags:
- `latest` (latest release)
- `v1.2.3` (exact version)
- `1.2.3` (clean version without 'v')
- `1.2` (major.minor)
- `main-<sha>` (development builds)
- `main-YYYYMMDD-HHmmss` (timestamped builds)

## GitFlow Integration

### Standard Flow:
1. **Feature Development**: Work on feature branches
2. **Release Preparation**: Create `release/vX.Y.Z` branch
3. **Automatic PR Creation**: Push to release branch triggers PR to main
4. **Testing**: All tests must pass for PR to be mergeable
5. **Release**: Merge PR or create Git tag triggers image publishing

### Branch Protection:
Configure branch protection rules in Forgejo:
- **main**: Require PR reviews, require status checks to pass
- **release/***: Require status checks to pass

## Runner Configuration

Your Forgejo runner should have:
- Docker daemon running
- Access to pull `mcr.microsoft.com/dotnet/sdk:9.0`
- Network access to your Forgejo instance
- Sufficient disk space for builds and image layers

## Deployment Process

### Automated (CI/CD):
1. Push to `release/v1.2.3` → Tests run, PR created
2. Merge PR to main → Images built and pushed
3. Tag `v1.2.3` → Release images published with manifests

### Manual:
```bash
# Trigger manual release build
# Go to Actions > Release Publishing > Run workflow
# Enter version tag (e.g., v1.2.3)
```

### Kubernetes Deployment:
```bash
# Download deployment artifacts from Actions
# Extract and apply
kubectl apply -f deployment-v1.2.3/
```

## Testing Locally

Test workflows locally using [act](https://github.com/nektos/act):

```bash
# Install act
# Test CI pipeline
act -W .forgejo/workflows/ci.yml

# Test with specific event
act push -W .forgejo/workflows/ci.yml
```

## Monitoring

Monitor your workflows:
1. **Forgejo**: Repository > Actions tab
2. **Container Registry**: Repository > Packages tab
3. **Deployments**: Check Kubernetes cluster status

## Troubleshooting

### Common Issues:

**Authentication Errors**:
- Verify `RUNNER_TOKEN` secret is set correctly
- Ensure token has `write:package` scope

**Docker Build Failures**:
- Check Dockerfile syntax
- Verify base images are accessible
- Monitor runner disk space

**Test Failures**:
- Review test output in Actions logs
- Check if tests pass locally with `make test`
- Verify test dependencies are available

**GitFlow Issues**:
- Ensure branch naming follows `release/vX.Y.Z` pattern
- Check that main branch protection is configured
- Verify PR creation permissions

### Debugging Steps:
1. Check Actions logs for detailed error messages
2. Verify runner connectivity and Docker daemon
3. Test Docker builds locally
4. Validate Kubernetes manifests with `kubectl apply --dry-run`
5. Check Forgejo registry permissions and storage

## Customization

### Modify Triggers:
Edit the `on:` sections in workflow files to change when jobs run.

### Add Environments:
Add staging/production environment gates in `release-publish.yml`.

### Custom Tests:
Modify the test job in `ci.yml` to add additional quality gates:
- Code coverage
- Security scanning  
- Performance tests

### Notification Integration:
Add notification steps to workflows for Slack, Discord, or email alerts.