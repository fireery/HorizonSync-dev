# HorizonSync user guide

## Setup

1. On **Time**, choose Windows location, coordinates, or fixed times. Windows location is checked at startup; if unavailable, existing coordinates are kept. Enter fallback coordinates if none are available. Use negative latitude for south and negative longitude for west.
2. Set offsets if desired. A sunset offset of -20 switches to the night wallpaper 20 minutes before sunset. Preview today's schedule.
3. On **Wallpapers**, check the automatic engine status. Green means found and running; amber means waiting or restricted; red means missing or an error. The settings link opens the installation setting directly.
4. Select both wallpapers. Scene wallpapers normally use project.json; video wallpapers use the video file; web wallpapers use index.html. Test buttons change the selected desktop immediately.
5. Save the original Wallpaper Engine playlist and disable its timed switching. Enable HorizonSync switching, then save.

## Browse and preview wallpapers

The installed library opens as a searchable thumbnail grid. Click a card to open a centered, enlarged project preview. GIFs loop automatically; other images stay static. **Pause preview** releases the animation decoder and shows the first frame; **Play preview** resumes it. The overlay shows the title, author and project ID. **Use this wallpaper** updates the relevant day/night path; save settings in the main window to persist it.

Use **Back to library**, click the dimmed backdrop, or press Esc to return. Another Esc closes the picker. Arrow keys move card focus; Enter opens the preview. Enter in search does not submit a selection. Rescan to refresh the local library, or return to the main window to browse for a file.

Only the current large preview animates. Returning, minimizing or closing stops and releases it; restoring a minimized window reloads it. GIFs above 16 MiB, or with a conservative width × height × 4 × frame-count estimate above 64 MiB, fall back to their first frame. Missing or unsupported previews do not block selection. Video playback and actual scene/web rendering are outside this preview feature.

Offscreen cards are recycled. The static thumbnail LRU is bounded by both 128 images and 24 MiB and is cleared on close; animated frames never enter it. Releasing resources does not imply an immediate drop in the process memory reported by Windows.

Local author information takes precedence. For the opened preview only, missing authors are looked up using Steam Workshop IDs and public profile names, with a 10-second total deadline and no API key. No wallpaper files, settings or coordinates are uploaded. Successes and failures are cached for this app session; **Retry author lookup** retries a failure. Offline or unavailable results show a known Steam ID or “Unknown author.” Name lookup depends on Steam's deprecated community XML endpoint and may become unavailable. Workshop and profile links open in a browser only when clicked.

## Language and startup

Open the bottom-left settings icon. The language dropdown offers Windows language, Simplified Chinese and English. A language change is immediate; save settings to remember it. Optional sign-in startup is also configured here.

## Daily operation

The app checks every 15 seconds and usually sends a command within 15 seconds after an event. It uses the current Windows date, time and time zone and never changes the clock. It rechecks after resume, unlock or an engine restart. The computer cannot switch while asleep.

Closing the window hides it to the tray. Use **Quit HorizonSync** to stop. Manual tests are followed by automatic realignment after about 30 seconds if automation is enabled. A successful command return is not visual confirmation; check the first switch yourself.

## Data, recovery and uninstall

Installed mode stores data in `%LOCALAPPDATA%\HorizonSync`; portable mode uses `data` beside the EXE. Keep HorizonSync.exe.config next to the executable.

Settings are stored in settings.xml; .bak is the previous backup. Damaged settings stop automation and are preserved when explicitly repaired. activity.log is bounded and rotated. The app has no telemetry. Windows location services may use the network or cached data.

To restore the original behavior, disable sign-in startup and save, quit from the tray, then load your saved Wallpaper Engine playlist. Uninstall preserves user settings. Removing the data folder removes saved coordinates and paths. After moving a portable folder, configure sign-in startup again.

The current version manages one display index at a time.
