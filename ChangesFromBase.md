- Updated to [Dotnet8](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.16-windows-x64-installer)
- Extended Crontab Config
  - Crontabs can now also Execute Windows commands and send Server Console Commands by adding *.csv files to the server config folder (servers\\%ServerID%\\configs\\Crontab) (or click Browse => Server Configs in WindowsGSM while the server is marked, then create the folder Crontab if not existing.)
  - HowTo and Examples: https://github.com/Raziel7893/WindowsGSM/blob/master/Crontab.md
- Extended Backup Config
  - BackupConfig now supports individual/multiple save locations via config
  - File Will be Created After starting a Server the First time with backup enabled
  - It can be found by clicking Browse => Server Configs => BackupConfig.cfg
  - Backup archives are now written directly from the source paths into the zip file instead of copying everything to a temporary folder first
  - Backup path and save path entries can now follow the current WindowsGSM root when the app folder is copied or moved, and corrected paths are written back to BackupConfig.cfg
  - Intentional absolute backup paths outside the WindowsGSM folder are preserved and are not rebased into the app folder.
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
- ToDo / Ideas
  - Minecraft / Java detection
    - Check Windows environment variables and common install locations for Java.
    - Stop relying on a static 32-bit Java path for Minecraft-related flows.
    - Consider validating the detected Java version against plugin or server requirements.
  - Firewall rule improvements
    - Current firewall rule adds the server executable as an allowed app, which allows all TCP/UDP ports for that exe.
    - Replace with explicit inbound port rules where possible.
    - Start with WindowsGSM.cfg values:
      - Server port
      - Query port
      - RCON/admin port
    - Add protocol selection: TCP / UDP / Both.
    - Later, allow plugins/game definitions to declare extra firewall ports such as Steam, beacon, telnet, web admin, voice, or derived offset ports.
    - Keep broad executable allow as a fallback for games/plugins without explicit firewall metadata.
  - Server health checks and recovery
    - Add query/RCON heartbeat checks so a hung server is not treated as healthy just because the process is still running.
    - Allow auto-restart thresholds based on repeated failed checks.
    - Surface the last successful health check and recent failure reason in the UI.
  - Config validation
    - Validate ports, paths, cron syntax, tokens, duplicate server names, and required runtime locations before saving.
    - Warn about conflicting ports across configured servers.
    - Surface invalid plugin custom settings with field-level validation in the UI.
  - In-app crash triage
    - Show a crash summary in the dashboard using the generated crash logs.
    - Include exit code, likely error line, last restart count, and a short recent-log excerpt.
    - Make it easy to jump from a server row to its latest crash log.
  - Plugin capability metadata
    - Let plugins declare supported capabilities such as query method, RCON support, required runtime, firewall ports, backup exclusions, and update channels.
    - Use this metadata to remove hardcoded per-game assumptions from the core app.
  - Large-library management
    - Add search/filter/sort for servers by game, status, tags, favorite, port, or machine role.
    - Consider saved views for admins running many instances.
  - Secret handling
    - Improve storage for webhook URLs, bot tokens, GSLTs, and admin credentials.
    - Prefer protected storage or encryption for sensitive values instead of plain text config where practical.
  - Import / export server profiles
    - Export server definitions and WindowsGSM-managed settings separately from full file backups.
    - Make migration and reproducible server setup easier across machines.
  - Port collision detection
    - Detect overlapping game/query/RCON/admin ports across all configured servers.
    - Warn before save and offer quick navigation to the conflicting server entries.
==============================================================================

## Changes
- Updated WindowsGSM from .NET 8 to .NET 10
  - Changed the main WindowsGSM target framework to `net10.0-windows`.
  - Changed the plugin development project to `net10.0-windows`.
  - Kept single-file, self-contained Windows x64 publishing support.
  - Self-contained published releases bundle the .NET 10 runtime, so end users do not need to install .NET 10 separately.
  - Framework-dependent builds still require the .NET 10 Desktop Runtime to be installed on the target machine.
  - Removed unnecessary/obsolete package references that were causing restore/build warnings.
  - Build now completes with 0 warnings.
  - The main WindowsGSM log now word-wraps instead of showing a horizontal scrollbar.
  - Double-clicking a server row opens `Edit WindowsGSM.cfg`.

- Updated AppVeyor CI for .NET 10
  - Added `appveyor.yml` so AppVeyor installs the .NET 10 SDK before building.
  - Build now uses the installed .NET SDK directly instead of AppVeyor's older default MSBuild/.NET SDK.
  - Fixed AppVeyor YAML command quoting for the `dotnet build` step.
  - Added `skip_branch_with_pr` to reduce duplicate AppVeyor branch builds when a PR build exists.

- Updated GitHub Actions CI
  - Updated `.github/workflows/build.yml` to use `actions/checkout@v4` and `actions/setup-dotnet@v4`.
  - GitHub Actions now builds with the .NET 10 SDK via `dotnet build`.
  - Opted JavaScript actions into Node.js 24 with `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24`.
  - Branch protection should require the `Build` GitHub Actions check and the AppVeyor PR check, not duplicate push/branch contexts.

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
  - `CustomServerSetting` now supports optional allowed values, which render as dropdowns in `Edit WindowsGSM.cfg`.
  - Old plugins remain backward compatible:
    - Plugins without `CustomSettings` keep the original edit config layout.
    - Plugins using simple string `CustomSettings` keep the original custom-setting behavior and built-in fields are still edited through the normal WindowsGSM fields.
  - Plugins using typed `CustomServerSetting` objects can now use the custom settings schema as the full editable config surface.
  - In schema mode, `Server ID` and `Server Game` remain fixed at the top, `Server Start Param` stays as its own field, and other built-in settings can be declared directly by the plugin schema.
  - In schema mode, `Edit WindowsGSM.cfg` opens wider and lays plugin fields out in three wrapping columns.
  - Backup list fields declared as `saveslocation` or `fileslocation` render as taller wrapped text boxes while normal fields keep compact spacing.
  - Custom plugin values are saved into `WindowsGSM.cfg`.
  - New installs seed configured defaults from typed `CustomServerSetting` declarations.
  - `ServerConfig` now preserves unknown/custom config keys and exposes `GetCustomSetting(...)` helpers.
  - Plugin skeleton examples were updated to show the new `CustomSettings` pattern.

- Added Windrose+ addon installation support
  - Added `Tools > Install Addons > Windrose+` for Windrose Dedicated Server entries.
  - The installer downloads the latest `WindrosePlus.zip`, extracts it safely into the selected server's `serverfiles` folder, and runs `install.ps1`.
  - The installer can detect existing Windrose+ installs and offer reinstall/upgrade.

- Added first-run readiness checks
  - Added `Tools > Readiness Check`.
  - The readiness check opens automatically once on first launch.
  - App-wide checks cover WindowsGSM/logs/backups write access, administrator permissions, SteamCMD presence, Java detection, Windows Firewall API access, disk space, plugin load status, and public IP lookup.
  - Selected-server checks cover server folders, server executable presence, game/query port validity, port collisions, backup location write access, and configured backup source paths.
  - Results are shown as pass/warn/fail/info rows with a short explanation for each check.

- Improved crash and runtime diagnostics
  - Added detailed crash log generation under `logs\servers\<serverId>\crash_*.log`.
  - Crash logs include server ID/name/game, PID, exit code, executable, arguments, working directory, captured console output, and recent `.log`/`.txt` files from `serverfiles`.
  - Crash logs now extract diagnostic lines containing errors/exceptions before the log tail, so important failure reasons are easier to see.
  - Added shared-read log file handling so active server log files can still be included.

- Improved SteamCMD install reliability
  - Installer output now captures stdout/stderr into the install log.
  - First-run SteamCMD `Missing configuration` failures are detected and retried once.
  - Login-token prompts remain visible in the install log.

- Added Steam branch/version selection
  - SteamCMD game installs now show a Steam Branch / Version section without requiring plugin changes.
  - Branches can be refreshed from Steam app info where available, with manual branch entry kept as a fallback for hidden/private branches.
  - Private branch passwords can be entered during install and are passed to SteamCMD as `-betapassword`.
  - `WindowsGSM.cfg` now stores `steambranch`, `steambeta_password`, and `steambranch_lastinstalled`.
  - `Edit WindowsGSM.cfg` shows Steam branch settings for SteamCMD-backed servers and allows changing the target branch later with the same editable dropdown and refresh behavior as install.
  - SteamCMD install/update commands now automatically include configured `-beta` and `-betapassword` arguments.
  - Steam remote build checks now read the configured branch build ID instead of always checking the public branch.
  - Update checks force an update when the configured Steam branch differs from the last installed branch.
  - Install failures now distinguish a SteamCMD process failure from a post-install validation failure, so successful SteamCMD exits with missing expected server files no longer show as `Exit code: 0`.
  - Failed installs now write `InstallFailure.txt` into the server config folder with the selected game, branch, SteamCMD exit code, and validation error.
  - The install progress bar now keeps validation failure text short and sends full details to the install log/failure report so long paths do not get clipped.
  - Update and auto-update logs now include the configured Steam branch and build IDs so branch updates do not look like plain public-version updates.

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

- Improved backup performance and portability
  - Backups now stream files directly into `System.IO.Compression.ZipArchive` instead of copying the configured source paths to a temporary folder before zipping.
  - `BackupConfig.cfg` path entries for `backuplocation`, `saveslocation`, and `fileslocation` can now be rebased to the current `WGSM_PATH` when the WindowsGSM folder is copied or moved.
  - Backup path rebasing now only applies to paths that look like they came from a moved WindowsGSM folder, so deliberate absolute paths such as `D:\Backups\<serverId>` remain unchanged.
  - Corrected rebased backup paths are persisted back into `BackupConfig.cfg`.
  - Added `fileslocation` support so individual files can be included in backups alongside save folders.
  - `saveslocation` and `fileslocation` support multiple entries separated by either `;` or `|`, preserving old semicolon-separated configs while allowing cleaner pipe-separated plugin values.
  - Backup settings can now be overridden from `WindowsGSM.cfg` custom settings using `backuplocation`, `saveslocation`, `fileslocation`, and `maximumbackups`.
  - Backup manifests now distinguish folder and file entries so restores can put individual files back in their original locations.
  - Backup retention via `maximumbackups` is integrated with both `BackupConfig.cfg` and plugin/custom settings.
  - fixed `Backuping`....

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
  - Removed the checked-in legacy NuGet `packages/` folder and now rely on `PackageReference` restore.
  - Removed stale `WindowsGSM.csproj.Backup.tmp` and `WindowsGSM.csproj.user` files from source control.
  - Removed old embedded package DLL/resource entries and the unused `ReferencesEx` assembly resolver path.
  - Added ignore rules for `packages/`, `*.csproj.user`, and `*.Backup.tmp`.
  - Kept required packages for Discord, Roslyn compilation, MahApps, RCON, cron, zip, and management support.
  - Bumped assembly version/file version to `1.26.0`.
