- Updated to [Dotnet8](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.16-windows-x64-installer)
- Extended Crontab Config
  - Crontabs can now also Execute Windows commands and send Server Console Commands by adding *.csv files to the server config folder (servers\\%ServerID%\\configs\\Crontab) (or click Browse => Server Configs in WindowsGSM while the server is marked, then create the folder Crontab if not existing.)
  - HowTo and Examples: https://github.com/Raziel7893/WindowsGSM/blob/master/Crontab.md
- Extended Backup Config
  - BackupConfig now supports individual/multiple save locations via config
  - File Will be Created After starting a Server the First time with backup enabled
  - It can be found by clicking Browse => Server Configs => BackupConfig.cfg
  - Hint: Do not modify WindowsGSM.cfg manually, everything is changable via the Programm itself, the Syntax of that file is quite easy to destry and your server will disappear from wgsm if you mess it up 
- Send Join Codes via Webhook
  - Will send the joincode text aslong as the autostart or autorestart alert is activated in the webhook settings
  - EMBEDD CONSOLE NEEDS TO BE ENABLED 
- Public IP change Webhook
  - Activate in the Webhook settings
  - Will check your public IP every 2 min via https://ipinfo.io/ip and send out a Webhook if it changed
  - Will also change other Webhooks to include your actual public IP instead of your local one set in WindowsGSM as ServerIP
- DiscordBot SendR
  - Send a Console Command and try to gather the response,  Needs a working Embedded Console
- Customize Discord BOT
  - Add posibility to change Bot name and manually way to change the donation based avatar from wgsm to a custom one.
  - Just put an avatar.png inside configs\discordbot\avatar.png
  - Also added a switch to just stop wgsm to change the profile of the Webhook user

----

## Added by Shenniko

Compared against `Raziel7893/WindowsGSM` `master` on 2026-04-22.
==============================================================================
-To do
  - Minecraft Plugin
    - (check windows env variables for java location, currently points static to 32bit JAVA directory)
  - Firewall.cs
    - Current firewall rule adds the server executable as an allowed app, which allows all TCP/UDP ports for that exe.
    - Replace with explicit inbound port rules where possible.
    - Start with WindowsGSM.cfg values:
      - Server port
      - Query port
      - RCON/admin port
    - Add protocol selection: TCP / UDP / Both.
    - Later, allow plugins/game definitions to declare extra firewall ports such as Steam, beacon, telnet, web admin, voice, or derived offset ports.
    - Keep broad executable allow as a fallback for games/plugins without explicit firewall metadata.
==============================================================================

## Cosmetic change

## Changes
- Updated WindowsGSM from .NET 8 to .NET 10
  - Changed the main WindowsGSM target framework to `net10.0-windows`.
  - Changed the plugin development project to `net10.0-windows`.
  - Kept single-file, self-contained Windows x64 publishing support.
  - Removed unnecessary/obsolete package references that were causing restore/build warnings.
  - Build now completes with 0 warnings.

- Updated AppVeyor CI for .NET 10
  - Added `appveyor.yml` so AppVeyor installs the .NET 10 SDK before building.
  - Build now uses the installed .NET SDK directly instead of AppVeyor's older default MSBuild/.NET SDK.
  - Fixed AppVeyor YAML command quoting for the `dotnet build` step.

- Replaced obsolete HTTP APIs
  - Added `WindowsGSM/Functions/Http.cs` as a shared `HttpClient` helper.
  - Replaced old `WebClient`, `WebRequest`, and `HttpWebRequest` usage across WindowsGSM and plugin development examples.
  - Updated GitHub/config/download helpers, analytics calls, public IP checks, Minecraft downloads, addon downloads, and plugin examples to use the shared HTTP helper.

- Removed LiveCharts dependency
  - Removed `LiveCharts.Wpf`.
  - Replaced the dashboard player chart with native WPF layout controls.
  - Avoids old compatibility warnings while keeping the dashboard player summary visible.

- Added per-server dashboard resource history
  - Dashboard now samples CPU and memory usage for each running server process.
  - Keeps the last hour of samples in memory.
  - Added a native WPF line chart with CPU/Memory toggle buttons and a color-coded server legend.

- Added plugin custom server settings support
  - Added `CustomServerSetting` so plugins can declare editable per-plugin settings.
  - Plugins can expose `CustomSettings` as either simple strings or `CustomServerSetting` objects.
  - `Edit WindowsGSM.cfg` now shows a scrollable `Plugin Settings` section between `Server GSLT` and `Server Start Param` when a plugin declares custom fields.
  - Custom plugin values are saved into `WindowsGSM.cfg`.
  - `ServerConfig` now preserves unknown/custom config keys and exposes `GetCustomSetting(...)` helpers.
  - Plugin skeleton examples were updated to show the new `CustomSettings` pattern.

- Improved crash and runtime diagnostics
  - Added detailed crash log generation under `logs\servers\<serverId>\crash_*.log`.
  - Crash logs include server ID/name/game, PID, exit code, executable, arguments, working directory, captured console output, and recent `.log`/`.txt` files from `serverfiles`.
  - Crash logs now extract diagnostic lines containing errors/exceptions before the log tail, so important failure reasons are easier to see.
  - Added shared-read log file handling so active server log files can still be included.

- Improved SteamCMD install reliability
  - Installer output now captures stdout/stderr into the install log.
  - First-run SteamCMD `Missing configuration` failures are detected and retried once.
  - Login-token prompts remain visible in the install log.

- Fixed 7 Days to Die startup/config issues
  - Removed the `-quit` launch argument from the 7 Days to Die server start command.
  - Sanitized generated 7 Days to Die `GameName` values so invalid characters such as `#` do not cause the server to exit.
  - Keeps `UserDataFolder` inside the WindowsGSM server folder.

- Updated Minecraft Java handling
  - Minecraft Java install and update paths now request Java 25 by default.
  - Minecraft download/update paths now use the shared HTTP helper.
  - (todo - check windows env variables for java location, currently points static to 32bit JAVA directory)

- Improved published single-file behavior
  - `WGSM_PATH` now resolves from the actual process executable path with a fallback to `AppContext.BaseDirectory`.
  - Helps the published EXE behave correctly when copied to a separate folder such as `D:\WindowsGSM`.

- Fixed custom window behavior regressions
  - Added native title-bar hit testing and drag handling so the custom MahApps title bar can move the window.
  - Restoring from the tray now shows, normalizes, activates, and focuses the window.
  - Minimize-to-tray handling was kept working with the native window message hook.

- Updated updater/release behavior
  - Removed the old automatic `WindowsGSM-Updater.exe` flow.
  - Latest release checks now point at `Raziel7893/WindowsGSM`.
  - Update prompt now tells users to manually replace the EXE from the Raziel7893 releases page.

- Cleaned warning-prone code
  - Converted optional `Notice` fields from unassigned fields to properties across game server classes.
  - Removed unused Discord dashboard fields.
  - Adjusted fire-and-forget async calls to avoid compiler warnings.
  - Removed unused exception variable names where the exception was intentionally ignored.

- Updated firewall implementation
  - Removed the old COM reference.
  - Reworked firewall rule handling to avoid the embedded COM interop package/reference.

- Updated plugin development project
  - Removed unnecessary `Microsoft.CSharp` package reference.
  - Updated plugin examples to use `HttpClient` through the shared helper.
  - Added the new custom setting declaration example to both skeleton plugin templates.

- Package/project cleanup
  - Removed old or duplicate package references such as old `Discord.Net.WebSocket`, `LiveCharts.Wpf`, `Microsoft.CSharp`, and other unused framework compatibility packages.
  - Kept required packages for Discord, Roslyn compilation, MahApps, RCON, cron, zip, and management support.
  - Bumped assembly version/file version to `1.25.1.19`.
