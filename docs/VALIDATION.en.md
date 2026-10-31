> Historical English details. For current Chinese guidance, see [VALIDATION.md](VALIDATION.md).

# Validation status

Development snapshot: 2026-10-31, version 0.1.0-preview.1.

## Completed

- Release compilation using the Windows .NET Framework compiler.
- Startup discovery found the installed Wallpaper Engine location. The waiting status and the direct link to its settings section were inspected. A later run in the ordinary user environment also detected the running engine and showed the green ready state. The wallpaper page hides the installation path, and the sidebar shows only icons with accessible names and tooltips.
- WPF UI displayed and inspected on the current computer, with per-monitor DPI support. The user confirmed the first DPI correction improved clarity; the interface was then redesigned using consistent navigation, cards and choices. The sidebar was reduced to icons, and button/input padding was tightened without reducing font sizes.
- 43 current core checks pass with `Run-Tests.ps1 -SkipCli`: schedule boundaries, midnight, NZ spring and US autumn DST, invalid/repeated local hours, offsets, polar day/night, system clock/timezone precedence, location precedence/fallback, argument quoting, restricted registry access, missing cached installations, atomic settings backups, corrupt/nil/future configuration recovery, and rapid repair backup uniqueness, English resource loading, localized validation, draft saving before location setup, language persistence and Chinese restoration.
- Before the configuration expansion, 31 checks including three mock process integration checks passed. Those mock checks verified Unicode, spaces and shell metacharacters remain one file argument, exited engine instances are detected, and a stopped engine is not launched.
- On the latest rerun, Windows application control blocked the newly compiled mock executable. This local full-test attempt failed and was followed by a separate explicit partial run; the local partial run is not a full-suite pass. The mock tests remain enabled by default for an unrestricted development/CI environment.
- 60 solar events across eight locations and four seasons compared with Astral 3.2: maximum difference 30.9 seconds, with a 120-second tolerance. Astral is used only for optional independent verification and is not an application dependency.
- Installer compilation using Inno Setup 7.1.1 completed successfully. The build tool was obtained from the official source and its Authenticode signature validated as Pyrsys B.V.; it was extracted in workspace portable mode.

## Not yet validated

- Real Wallpaper Engine switching on actual day/night wallpapers and monitor layouts.
- Actual sleep/resume, unlock and engine-restart behavior end to end.
- Successful Windows system-location acquisition; this environment returned no location. The actual fallback UI was inspected; successful coordinate replacement is covered by core tests.
- Actual per-user installer install, upgrade and uninstall, including startup registration.
- Multi-monitor DPI changes, 100/125/150/200% scaling and keyboard/screen-reader usability on separate machines.

- Publisher signing and a full public release lifecycle. The public repository now exists at https://github.com/fireery/HorizonSync-dev and GPL-3.0-only has been selected.

The application and installer are development previews. Do not label this snapshot production-ready.

## Final preview UI

- English pages were inspected with the approved monochrome icon, compact inputs and bottom-left settings navigation.
- Live English-to-Chinese switching was exercised. One startup hint found during inspection was corrected.
- Language selection now uses the requested dropdown (Windows / Simplified Chinese / English).

## Public CI result

GitHub Actions passed all 46 core and mock CLI checks for application code commit 28b32482530bf2165146dc345de6214044233fbe, including the three process checks blocked locally. The independent 60-event solar comparison also passed with a maximum difference of 30.9 seconds.

Run: https://github.com/fireery/HorizonSync-dev/actions/runs/41112750368

The remaining real-device and installer lifecycle checks above are still open; this release remains a prerelease.
