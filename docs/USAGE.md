# HorizonSync user guide

## Setup

1. On **Time**, choose Windows location, coordinates, or fixed times. Windows location is checked at startup; if unavailable, existing coordinates are kept. Enter fallback coordinates if none are available. Use negative latitude for south and negative longitude for west.
2. Set offsets if desired. A sunset offset of -20 switches to the night wallpaper 20 minutes before sunset. Preview today's schedule.
3. On **Wallpapers**, check the automatic engine status. Green means found and running; amber means waiting or restricted; red means missing or an error. The settings link opens the installation setting directly.
4. Select both wallpapers. Scene wallpapers normally use project.json; video wallpapers use the video file; web wallpapers use index.html. Test buttons change the selected desktop immediately.
5. Save the original Wallpaper Engine playlist and disable its timed switching. Enable HorizonSync switching, then save.

## Language and startup

Open the bottom-left settings icon. The language dropdown offers Windows language, Simplified Chinese and English. A language change is immediate; save settings to remember it. Optional sign-in startup is also configured here.

## Daily operation

The app checks every 15 seconds and usually sends a command within 15 seconds after an event. It uses the current Windows date, time and time zone and never changes the clock. It rechecks after resume, unlock or an engine restart. The computer cannot switch while asleep.

Closing the window hides it to the tray. Use **Quit HorizonSync** to stop. Manual tests are followed by automatic realignment after about 30 seconds if automation is enabled. A successful command return is not visual confirmation; check the first switch yourself.

## Data, recovery and uninstall

Installed mode stores data in `%LOCALAPPDATA%\HorizonSync`; portable mode uses `data` beside the EXE. Keep HorizonSync.exe.config next to the executable.

Settings are stored in settings.xml; .bak is the previous backup. Damaged settings stop automation and are preserved when explicitly repaired. activity.log is bounded and rotated. The app has no telemetry. Windows location services may use the network or cached data.

To restore the original behavior, disable sign-in startup and save, quit from the tray, then load your saved Wallpaper Engine playlist. Uninstall preserves user settings. Removing the data folder removes saved coordinates and paths. After moving a portable folder, configure sign-in startup again.

The current version manages one display index at a time. Wallpaper selection improvements are planned for the next version.
