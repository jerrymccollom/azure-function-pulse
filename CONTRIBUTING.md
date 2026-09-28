# Contributing to Azure Function Pulse

Thank you for contributing to Azure Function Pulse! Please follow these guidelines to ensure a smooth collaboration process.

## Branch Management

### Always Start from Latest Main

Before starting any new work:

1. **Fetch the latest changes:**
   ```bash
   git fetch origin main
   ```

2. **Ensure your local main is up to date:**
   ```bash
   git checkout main
   git pull origin main
   ```

3. **Create your feature branch from main:**
   ```bash
   git checkout -b your-branch-name
   ```

### Keeping Your Branch Up to Date

Before submitting a pull request, ensure your branch is up to date with main:

```bash
git fetch origin main
git rebase origin/main
# OR if you prefer merging:
git merge origin/main
```

**Important:** PRs must be up to date with main before they can be merged. The `up-to-date-with-main` status check will verify this requirement.

## Pull Request Process

1. Ensure your branch is up to date with main (see above)
2. Push your changes to GitHub
3. Open a pull request against the `main` branch
4. Complete the PR template checklist
5. Wait for CI checks to pass
6. Request review from maintainers

## Branch Protection

This repository enforces branch protection rules on `main`:

- **Required status checks:** All CI checks must pass, including the `up-to-date-with-main` check
- **Up-to-date requirement:** Your PR branch must be up to date with main before merging
- **Pull request reviews:** May be required by repository settings

If the `up-to-date-with-main` check fails, rebase or merge main into your branch and push the update.
