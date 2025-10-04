# Release Instructions for v2.0.0

This document contains instructions for publishing version 2.0.0 of Space.Modules.InMemoryCache.

## Changes Made

The following changes have been completed in this PR:

1. **Upgraded Space.Abstraction** from `1.3.4-preview` to `2.0.0`
2. **Upgraded Space.DependencyInjection** from `1.3.4-preview` to `2.0.0`
3. **Created CHANGELOG.md** to document version history
4. **Created git tag v2.0.0** (local only)

## Verification

All tests pass successfully:
- ✅ 29 tests passed
- ✅ No breaking changes detected
- ✅ Build succeeds with no errors

## Publishing Steps

To publish this version to NuGet, follow these steps:

### 1. Push the Git Tag

After merging this PR to the main branch, push the tag:

```bash
git checkout master  # or main
git pull origin master
git tag -a v2.0.0 -m "Release v2.0.0 - Upgrade to Space.Abstraction 2.0.0"
git push origin v2.0.0
```

### 2. Create a GitHub Release

1. Go to https://github.com/salihcantekin/Space.Modules.InMemoryCache/releases/new
2. Choose tag: `v2.0.0`
3. Release title: `v2.0.0`
4. Description: Use content from CHANGELOG.md or similar:
   ```
   ## What's Changed
   - Upgraded Space.Abstraction from v1.3.4-preview to v2.0.0
   - Upgraded Space.DependencyInjection from v1.3.4-preview to v2.0.0
   
   All tests pass. No breaking changes detected.
   ```
5. Click "Publish release"

### 3. Automated Publishing

Once the GitHub release is published, the CI/CD pipeline (`.github/workflows/prod-ci.yml`) will automatically:
- Build the project with version 2.0.0
- Create the NuGet package
- Publish to NuGet.org

### 4. Verify Publication

After the workflow completes, verify the package is available:
- https://www.nuget.org/packages/Space.Modules.InMemoryCache/2.0.0

## Notes

- The release pipeline requires the `NUGET_API_KEY` secret to be configured in the repository
- For a preview release, mark the GitHub release as "pre-release" to create version `2.0.0-preview`
- The version number is extracted from the git tag (must be in format `vX.Y.Z`)
