# Steam Branches And Versions

WindowsGSM can install and update SteamCMD servers from a selected Steam branch.

This is useful for games that publish beta, experimental, previous-version, or private/password branches.

## What Is A Steam Branch?

SteamCMD usually installs the default public branch:

```text
app_update <appid> validate
```

When a branch is selected, WindowsGSM runs SteamCMD with:

```text
app_update <appid> -beta <branch> validate
```

For private/password branches:

```text
app_update <appid> -beta <branch> -betapassword <password> validate
```

## Install Flow

When installing a SteamCMD-backed game server, WindowsGSM shows:

- Steam Branch / Version
- Refresh
- Private Branch Password

The branch dropdown is editable.

If the branch is visible from Steam app info, `Refresh` can populate it. If Steam does not expose the branch, enter it manually.

Leaving the branch blank, or selecting `public`, installs the default public branch.

## Edit WindowsGSM.cfg

For SteamCMD-backed servers, `Edit WindowsGSM.cfg` shows:

- Steam Branch / Version
- Steam Branch Password
- Last Installed Steam Branch

Changing the target branch there affects the next update.

## Config Values

Steam branch settings are stored in `WindowsGSM.cfg`:

```ini
steambranch=""
steambeta_password=""
steambranch_lastinstalled=""
```

`steambranch` is the desired branch.

`steambeta_password` is passed to SteamCMD as `-betapassword` when set.

`steambranch_lastinstalled` records the branch that last updated successfully.

## Update Behavior

WindowsGSM checks the configured branch build ID instead of always checking the public build ID.

If `steambranch` differs from `steambranch_lastinstalled`, WindowsGSM forces an update.

`steambranch_lastinstalled` is only updated after SteamCMD exits successfully. If SteamCMD fails, the branch change stays pending and the next normal update trigger can retry it.

Normal update triggers include:

- user clicks Update
- Update on Start
- Auto Update interval

## Private Branches

Some branches require a password. Enter the branch name and password.

If refresh does not show a private branch, that does not always mean the branch is invalid. Steam may hide it from anonymous/app-info output. Manual entry is still supported.

If the game requires a Steam login, use Set Account before refreshing or installing.

## Old Branch Caveats

Old Steam branches may not contain the same dedicated server executable or folder layout as the current public branch.

For example, a game branch might install successfully through SteamCMD but fail WindowsGSM validation because the plugin expects:

```text
7DaysToDieServer.exe
```

while the selected branch only contains:

```text
7DaysToDie.exe
```

In that case SteamCMD succeeded, but the selected branch is not compatible with the current plugin's expected server files.

WindowsGSM writes `InstallFailure.txt` into the server config folder when this happens.

## Logs

Steam branch-aware updates are logged like:

```text
Action: Update | Steam branch v2.4
Checking: Steam branch v2.4 build (20371871) => (22422094)
Server: Updated Steam branch v2.4 to build 22422094
```

If the branch changed:

```text
Checking: Steam branch change required (public) => (v2.4)
```
