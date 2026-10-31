# Development workflow / 开发协作

## Branches

| Branch | Purpose | Merge destination |
| --- | --- | --- |
| `main` | Published code; remains the GitHub default branch | A reviewed release from `develop` |
| `develop` | Integration branch for the next version; default local working branch | `main` when ready for release |
| `feature/<short-name>` | One agreed feature, using lowercase English words separated by hyphens | `develop` |

Start feature branches from an up-to-date `develop`. Open feature pull requests against `develop`, not `main`. Create branches when work starts; do not create empty placeholder branches.

```powershell
git switch develop
git pull --ff-only origin develop
git switch -c feature/your-feature
# Implement and validate the agreed change.
git push -u origin feature/your-feature
```

`main` 用于发布，`develop` 汇总下一版本开发，具体功能从 `develop` 建立 `feature/*` 分支并合回 `develop`。本地日常工作停留在 `develop` 或当前功能分支。

## Validation and releases

Run `./build.ps1` and `./tests/Run-Tests.ps1` for relevant changes. GitHub Actions runs the full core/mock CLI suite and the independent solar comparison. If local application control blocks a mock executable, record that limitation and use the explicit `-SkipCli` option for a separately reported partial run; never bypass the control or call the partial run a full pass.

Before a release, review a pull request from `develop` to `main`, require the relevant checks to pass, and record remaining real-device validation limits. Create the version tag and GitHub Release from the resulting `main` commit. Keep previews marked as prereleases. Never move an existing release tag or silently replace its attachments.

This document describes the workflow; it does not enable repository protection rules or change account permissions.

## Local files and handoff

Build outputs belong in the ignored `build/` and `dist/` directories. Keep personal profiles, logs, toolchains and design drafts outside the repository. Historical development materials are preserved in the sibling `HorizonSync-local/` folder; they are not an alternative source checkout.

Read `CODEX_HANDOFF.md` and `git status` when resuming work. Update the handoff after a major stage and before long work, including completed work, remaining tasks, build/test evidence, the next operation and actions that must not be repeated. Keep private machine details in the local auxiliary folder.

Picker changes also require `./tests/Run-PickerTests.ps1`. Use `./tests/Run-PickerTests.ps1 -Ui -Fixture` for an isolated 500-item UI library, or omit `-Fixture` to inspect installed wallpapers without invoking the desktop-switching scheduler. Add `-English` for English. Test data stays in ignored `build/tests/`.
