# Backups

WindowsGSM can back up server folders and individual files.

Backup behavior can be configured through `BackupConfig.cfg` and, for plugins that expose custom settings, through `WindowsGSM.cfg`.

## BackupConfig.cfg

Each server can have:

```text
servers\<serverId>\configs\BackupConfig.cfg
```

This file is created after backup support is used for a server.

Important settings:

```ini
backuplocation=""
saveslocation=""
fileslocation=""
maximumbackups="3"
```

## Backup Location

`backuplocation` controls where backup archives are written.

Example:

```ini
backuplocation="D:\ServerBackups\1"
```

Intentional absolute paths outside the WindowsGSM folder are preserved.

Paths that clearly came from a moved WindowsGSM folder can be rebased to the current WindowsGSM root.

## Folder Backups

`saveslocation` lists folders to include in backups.

Examples:

```ini
saveslocation="Saves"
saveslocation="R5\Saved"
saveslocation="Saves|Config|Profiles"
```

Multiple entries can be separated by:

```text
;
|
```

Relative paths are resolved from:

```text
servers\<serverId>\serverfiles
```

## File Backups

`fileslocation` lists individual files to include in backups.

Example:

```ini
fileslocation="ServerDescription.json|Config\ServerSettings.json"
```

Like folders, multiple files can be separated by `;` or `|`.

## Retention

`maximumbackups` controls how many backup archives WindowsGSM keeps.

Example:

```ini
maximumbackups="5"
```

Values below `1` are treated as `1`.

## Plugin Custom Settings

Plugins can expose backup settings directly in `Edit WindowsGSM.cfg` by using `CustomServerSetting`.

Example:

```csharp
new CustomServerSetting("saveslocation", "Backup Folders", @"R5\Saved"),
new CustomServerSetting("fileslocation", "Backup Files", @"R5\ServerDescription.json|R5\Saved\SaveProfiles\Default\ServerDescription.json"),
new CustomServerSetting("maximumbackups", "Number of Backups", "3")
```

When present in `WindowsGSM.cfg`, these values can override `BackupConfig.cfg`.

## Restore Notes

Backup manifests distinguish folder entries from file entries.

This allows restores to put individual file backups back into the correct location instead of treating every backed-up item as a folder.

## Practical Tips

- Use relative paths when the files live inside `serverfiles`.
- Use absolute paths only when the target is intentionally outside WindowsGSM.
- Use `|` as a readable separator when exposing lists through plugin custom settings.
- Keep backup lists focused on save/config data rather than the entire game install where possible.
