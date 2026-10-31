# HorizonSync development handoff

## Current state — 2026-11-01

- Public repository: https://github.com/fireery/HorizonSync-dev.
- Published prerelease: `v0.1.0-preview.1`, commit `458d0484398eb21773444b166aaa55235e4434cb`. All four release attachments were verified against their original SHA256 values. Do not rebuild or replace those attachments.
- The full repository history has been cloned into the owner's new programming directory. The previous workspace is preserved in the sibling `HorizonSync-local/workspace-archive/`; released artifacts also have a dedicated copy in `HorizonSync-local/releases/v0.1.0-preview.1/`.
- Migration verified 1,177 files, 91 directories and 89,693,909 bytes by relative path, size and SHA256. All 41 public source files match the release's Git objects. The local auxiliary folder contains the complete historical handoff and migration reports; do not upload it.
- `main` remains the default GitHub branch and the published commit. Local development uses `develop`, tracking `origin/develop`. Create `feature/<short-name>` branches only when a feature starts. See `CONTRIBUTING.md`.

## Product decisions

- Independent Windows companion for Wallpaper Engine, with offline sunrise/sunset calculations, fixed-time fallback and separate offsets. No Auto Dark Mode dependency.
- Windows time and time zone take precedence. In system-location mode, available Windows coordinates replace stored coordinates; failures preserve the fallback and explain the issue.
- WPF interface with unified compact controls, icon-only sidebar and bottom-left settings without a divider. Wallpaper Engine paths belong in settings; the wallpaper page shows readiness and an actionable settings link.
- Approved artistic monochrome brand assets, English/Simplified Chinese language dropdown and GPL-3.0-only licensing. Do not restore rejected colored icon concepts or the old WinForms interface.
- Issue #1 implementation approved: virtualized thumbnail grid, centered large GIF preview, one disposable preview session, bounded thumbnail cache and optional author enrichment. Work on `feature/wallpaper-thumbnail-picker`.

## Build and validation

- The published application passed all 46 core/mock CLI checks and the independent comparison of 60 solar events, with maximum difference 30.9 seconds. See `docs/VALIDATION.md`.
- The installed engine was detected and the actual green readiness state inspected. Real wallpaper switching, successful Windows location acquisition, sleep/resume/unlock, installer install/upgrade/uninstall and multi-monitor DPI remain open real-device checks. The release is still a preview.
- New-path validation passed: application build, all 46 core/mock CLI tests and 60 solar-event comparisons (maximum difference 30.9 seconds). Full local process checks now passed without changing application-control settings. A second complete manifest verification passed before removing the old workspace copies. Cleanup is complete; only a migration pointer remains at the old location.

## First operation on resume

Read this file and `git status --short --branch`. Issue #1 is implemented and uploaded as draft PR #2 targeting develop. Commit 9e076d83da174f16b9715c3d74d54e5591d03acc passed GitHub push CI (run 41237486881) and matched a fresh GitHub clone. That clone built and passed 43 explicitly partial core checks plus 36 picker checks; the complete mock run was blocked by Windows application control. Original workspace complete 46 + 36 tests passed. Real Chinese/English browsing, playback/pause, author lookup, search, Escape return-to-card and keyboard confirmation inspected. Additional local MSBuild checks found and fixed a missing System.Xml reference and duplicate TargetFramework attribute; added a VS MSBuild CI step and ignored obj/. Next commit/push those follow-ups, pull them into the clean GitHub validation clone, rebuild/retest and verify new CI before recording final evidence. Do not change application-control policy, repeat migration, modify desktop wallpapers, publish attachments or merge PR #2. Native 150%/200% DPI/monitor transitions remain open.

## Do not repeat

- Do not recreate the repository, rewrite `main`, move the published tag or republish the preview.
- Do not upload local archives, profiles, coordinates, logs, toolchains or rejected design drafts.
- Do not bypass Windows application-control restrictions or change global Git identity/configuration.
- Do not overwrite a running executable, discard unsaved selections or alter Wallpaper Engine's playlist as part of migration.

## Task recovery rule

Maintain this root `CODEX_HANDOFF.md` after each major stage and before long work. Record completed and remaining work, build/test evidence, the first operation after recovery and actions not to repeat. If usage limits are near, avoid starting large new work and leave the repository consistent and recoverable.
