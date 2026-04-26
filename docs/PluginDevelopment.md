# Plugin Development

WindowsGSM plugins allow additional game servers to be added without modifying the core application.

This document is a starting point for plugin authors.

## Plugin Folder Layout

Installed plugins live in:

```text
plugins\
```

WindowsGSM scans folders ending in `.cs`.

Expected layout:

```text
plugins\
  MyGame.cs\
    MyGame.cs
    MyGame.png
    author.png
```

The folder name and main `.cs` file name should match.

Example:

```text
plugins\Windrose.cs\Windrose.cs
```

## Loading

WindowsGSM compiles plugins at runtime with Roslyn.

Only loaded plugins appear in:

- Install GameServer
- Import GameServer
- View Plugins

If a plugin fails to load, WindowsGSM writes plugin logs under:

```text
logs\plugins
```

## Naming

Plugins are shown with the plugin filename appended:

```text
Windrose Dedicated Server [Windrose.cs]
```

This helps distinguish built-in game servers from plugin-backed servers.

## Basic Plugin Members

Most plugins expose values similar to built-in game server classes:

```csharp
public string FullName = "Example Dedicated Server";
public string StartPath = "ExampleServer.exe";
public string Port = "7777";
public string QueryPort = "7778";
public string Defaultmap = "ExampleMap";
public string Maxplayers = "8";
public string Additional = "";
```

SteamCMD plugins normally expose:

```csharp
public string AppId = "123456";
public bool loginAnonymous = true;
```

## Custom Settings

Use `CustomServerSetting` for user-editable plugin settings.

See:

[CustomServerSetting](CustomServerSetting.md)

## Steam Branch Support

Steam branch/version selection is handled by WindowsGSM core for SteamCMD-backed servers.

Plugins do not need to be rewritten just to support branch selection.

If the plugin exposes a Steam `AppId`, WindowsGSM can show branch controls during install and edit config.

See:

[Steam Branches And Versions](SteamBranches.md)

## Backups

Plugins can expose backup fields using custom settings:

```csharp
new CustomServerSetting("saveslocation", "Backup Folders", @"Saved"),
new CustomServerSetting("fileslocation", "Backup Files", @"Config\Server.json"),
new CustomServerSetting("maximumbackups", "Number of Backups", "3")
```

See:

[Backups](Backups.md)

## Tips

- Keep plugin defaults conservative.
- Use `ServerConfig.SettingName` for built-in WindowsGSM config keys.
- Validate paths and generated config files before starting the server.
- Avoid hardcoding user-specific absolute paths.
- Keep generated/runtime values out of editable fields unless users should change them.
- Log clear errors when install/start validation fails.
