# Validation status

Published preview baseline: 2026-10-31, version 0.1.0-preview.1. Current development version: 0.2.0-preview.1; see Issue #1 validation below.

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


## 0.2.0-preview.1 / Issue #1 development validation — 2026-11-01

- Application script build passed. All 46 core/mock CLI checks and 36 deterministic picker checks passed locally.
- Picker checks cover 500-item discovery/deduplication, scene/video/web paths, corrupt metadata, missing/broken/transparent images, file-handle release, independent LRU count/byte limits, real GIF frame progression/looping, reused pixel buffers, pause-to-static, 200 actual animation sessions, rapid replacement, cancellation and both GIF budgets.
- After warm-up, the final three measured batches of 50 GIF sessions retained approximately 1.7 MiB managed memory; private bytes were approximately 36.7 MiB and handle counts stayed at 393–395. Garbage collection was requested only in the measurement harness, never by application code. These are small deterministic fixture measurements, not a whole-app memory cap.
- All 56 locally installed wallpapers had decodable static previews, with no metadata errors. Real Chinese and English thumbnail grids, enlarged GIFs, search, live author lookup, pause, Escape returning focus to the card and Enter confirmation were inspected. The UI harness uses the application's .NET Framework 4.8 target/configuration; this is necessary to reproduce production TLS and WPF compatibility behavior.
- The centered GIF preview reuses one worker pixel buffer, one coalesced UI staging buffer and one WriteableBitmap; it does not enqueue a dispatcher operation or allocate an immutable full-size image for each frame. Leaving a preview clears frame references and cancels/releases the decoder before the next session starts. Pause provides a static first frame.
- Synthetic stress assets are separate from the installed library. Their animation uses near-identical muted colors, and the synthetic UI is explicitly labeled. No private wallpaper images, library names, author IDs, local paths or logs are included in the public repository.
- GitHub round trip: a fresh clone of `9e076d83da174f16b9715c3d74d54e5591d03acc` matched the uploaded commit and built. Its complete core run failed because Windows application control blocked the spawned mock engine. A separately labeled `-SkipCli` run passed 43 checks; all 36 picker checks passed. This partial local result is not a full-suite pass.
- Full GitHub CI passed for that commit, including all core/mock CLI and picker checks, the independent solar comparison and portable packaging: https://github.com/fireery/HorizonSync-dev/actions/runs/41237486881.
- An additional project-build check found two existing project-file omissions: `System.Xml` was not explicitly referenced, and MSBuild generated a duplicate of the manually supplied TargetFramework attribute. Both are corrected. Local legacy MSBuild now builds, but reports missing .NET 4.8 targeting-pack/GAC fallback warnings; CI now also builds the project using Visual Studio MSBuild. Visual Studio project compilation and the complete CI workflow passed for follow-up commit `3949128a18b1c7542e82b2fba154c3b6a402a873`: https://github.com/fireery/HorizonSync-dev/actions/runs/41237644206.

Remaining feature validation: native 150%/200% DPI and monitor transitions, extended screen-reader use, and larger real-world GIF memory/CPU profiling. The tests above do not claim those checks. Previewing or selecting a wallpaper does not invoke the desktop-switching command; the original real-device/installer validation gaps remain open.
