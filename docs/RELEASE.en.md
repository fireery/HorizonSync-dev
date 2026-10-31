> Historical English details. For current Chinese guidance, see [RELEASE.md](RELEASE.md).

# Release checklist

Repository: https://github.com/fireery/HorizonSync-dev. The owner authorized source upload and downloadable preview packages. License: GPL-3.0-only. The initial release is a prerelease; unverified hardware/installer scenarios remain documented.

Before a public preview:

- Confirm GPL-3.0-only is included in repository, application information, installer, portable package and corresponding source.
- Verify the existing fireery/HorizonSync-dev repository and its support/release URLs.
- Run `build.ps1` and the complete `tests/Run-Tests.ps1` on a normal Windows machine or CI runner.
- Check each choice, input, browse dialog, wallpaper-library selector, tray action and disabled/failed state.
- Use installed Wallpaper Engine to validate day/night commands, the selected monitor, custom presets and concurrent playlist behavior. Record supported behavior rather than assume it.
- Validate system-location success/failure, manual fallback, changed timezone, midnight, manual clock changes and DST.
- Test cold startup with Wallpaper Engine delayed, engine restart, sleep/resume, lock/unlock and complete exit.
- Test clean per-user installation, same-version reinstall, upgrade and uninstall. Verify user configuration survives, and startup registration is removed only when it points to the uninstalled copy.
- Check per-monitor DPI and supported Windows versions, plus keyboard focus and accessibility names.
- Build clean installer and portable artifacts. Include only app files, runtime configuration and relevant user documentation; exclude user settings, paths, logs, toolchains and test profiles.
- Generate SHA-256 checksums. Decide on publisher signing; do not imply an unsigned build is signed. No automatic updater is included in this preview.
- Write release notes with known limitations and label the release as a prerelease until the required validation is complete.

Before a stable release, close the documented validation gaps, review error reporting and configuration migration, and repeat install/upgrade/uninstall tests with the final signed build if signing is adopted.

## Build packages

Run package.ps1 to build a portable ZIP and source archive. Pass -IsccPath with the Inno Setup 7 compiler path to also produce the installer. Files and SHA256SUMS.txt are written to dist. CI builds and tests Windows source and a portable preview; it does not publish releases automatically.
