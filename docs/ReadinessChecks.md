# Readiness Checks

WindowsGSM includes a readiness check tool to help users find common setup problems before a server fails to start.

## Where To Find It

Open:

```text
Tools > Readiness Check
```

The readiness check also opens automatically once on first launch.

## App-Wide Checks

WindowsGSM checks:

- WindowsGSM folder write access
- logs folder write access
- backups folder write access
- administrator status
- SteamCMD presence
- Java detection
- Windows Firewall API access
- disk space
- plugin load status
- public IP lookup

## Selected Server Checks

When a server is selected, WindowsGSM also checks:

- server folder exists
- serverfiles folder exists
- config folder exists
- expected server executable exists
- game/query port validity
- port collisions with other configured servers
- backup location write access
- configured backup source paths

## Result Types

Readiness results are shown as:

```text
Pass
Warning
Fail
Info
```

Warnings do not always block operation, but they point to issues likely to confuse users later.

Failures usually mean WindowsGSM cannot perform a required action.

## Notes

Some checks are best-effort.

For example, public IP lookup depends on network access, and firewall checks depend on Windows permissions/API availability.

If a check fails, read the message in the readiness flyout and fix the underlying path, permission, runtime, or config issue.
