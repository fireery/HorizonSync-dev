# HorizonSync development handoff

## Agreed product direction

- Independent Windows companion for Wallpaper Engine, using local sunrise/sunset calculations. No Auto Dark Mode dependency.
- Name: HorizonSync. Repository: https://github.com/fireery/HorizonSync-dev (public, created under owner authorization).
- Intended distribution: public source plus downloadable installer. License selected by the owner: GPL-3.0-only. The owner explicitly requested completing this version and uploading it to GitHub with preview packages.
- Windows date, time and time zone take precedence. In system-location mode, available Windows coordinates replace stored coordinates at startup; unavailable location preserves the fallback and explains the problem.
- UI uses one consistent WPF design inspired by the general layout of Auto Dark Mode, with original code and no copied assets.
- Sidebar contains only icons, with tooltips and accessible names. Buttons and inputs are compact without reducing font size.
- Wallpaper page shows installation status, not the engine path: green ready, amber waiting, red missing/error. An actionable link opens the exact settings section. Engine paths and manual discovery belong in settings. Discovery is automatic and reuses the cached location first.

## Implemented

- Core solar/scheduling logic, offsets, fixed-time fallback, DST, polar handling and argument quoting.
- WPF pages, matching wallpaper-library dialog, tray, startup option and discovery/cache/status.
- LocalAppData profile by default, portable mode and explicit data directory for testing.
- Atomic settings backups, schema validation, preserved corrupt files and bounded logs.
- Compiler build script, Visual Studio project, tests, optional independent Astral comparison, portable packaging, Inno Setup installer and a CI workflow that does not publish releases.

## Validation

See docs/VALIDATION.md for exact evidence and limits. Latest partial core run: 43 passed with -SkipCli. Three mock process checks remain enabled by default, but the latest full local run was blocked by Windows application control; never describe the partial run as a full-suite pass. Earlier mock integration checks passed. Sixty solar events match Astral within 30.9 seconds.

The installed engine was found; waiting status and settings navigation were inspected. Ordinary-user execution also showed the real green ready state. Actual wallpaper switching, successful system-location acquisition, installer lifecycle and a full CI run remain unverified. The CI workflow is prepared and will run on source upload.

## First operation on resume

Read this file, inspect current files/build outputs and running preview processes, and check the owner’s latest UI changes. Then run build.ps1 and the appropriate test suite. Do not overwrite an in-use executable or discard unsaved choices in a preview window.

## Next work

Finish real Wallpaper Engine, resume/unlock, multi-monitor/DPI and installer install/upgrade/uninstall validation. Review the final UI with the owner, run CI and verify the GitHub source and preview release. Publisher signing is not configured.

The owner explicitly deferred wallpaper-selection experience improvements to the next version. Track that work in docs/ROADMAP.md; do not keep expanding the selection UI in this iteration. No specific redesign or acceptance criteria have been approved yet.

## Do not repeat

- Do not reconsider the chosen name or ask whether to depend on Auto Dark Mode.
- Do not restore the old blurry WinForms window or mix old selection controls into the WPF pages.
- Do not show the engine installation path on the wallpaper page or reintroduce sidebar labels.
- Do not bypass application-control restrictions to make tests pass.
- Do not include profiles, coordinates, local paths, logs or build tools in release/source archives.
- Local design comparison sheets and the draft raster concept are also excluded from public upload; publish the approved final vector/application exports only.

## Language and README decisions

English and Simplified Chinese are implemented. The owner requested a dropdown selector (Windows language / 简体中文 / English) with instant changes and saved preference. The README follows HMCL''s centered header, badges and section structure, with original HorizonSync assets/content and English/Chinese versions. User data and toolchains must not be uploaded.
