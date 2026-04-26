# CustomServerSetting

`CustomServerSetting` lets a plugin declare editable settings for `Edit WindowsGSM.cfg`.

It is intended to make plugin configuration clearer for users, while keeping older plugins working exactly as they did before.

## Backward Compatibility

Old plugins can continue to use simple string custom settings:

```csharp
public string[] CustomSettings = { "mysetting", "another_setting" };
```

Those plugins keep the original edit-config layout. WindowsGSM still shows the normal built-in fields such as Server Name, IP, Port, Query Port, Max Players, Start Map, GSLT, and Server Start Param.

New plugins can use `CustomServerSetting` objects:

```csharp
using WindowsGSM.Functions;

public CustomServerSetting[] CustomSettings =
{
    new CustomServerSetting("mysetting", "My Setting", "default value")
};
```

When WindowsGSM detects `CustomServerSetting`, it switches to schema mode for that plugin.

## Schema Mode

Schema mode means the plugin controls the editable config surface.

In schema mode:

- `Server ID` remains fixed.
- `Server Game` remains fixed.
- `Server Start Param` remains separate.
- The plugin can declare built-in WindowsGSM settings directly in `CustomSettings`.
- Other normal defaults are ignored unless the plugin declares them.
- The edit window opens wider and lays settings out in multiple columns.

This is useful for games where the plugin should own the whole config experience.

## Basic Example

```csharp
using WindowsGSM.Functions;

public CustomServerSetting[] CustomSettings =
{
    new CustomServerSetting(ServerConfig.SettingName.ServerName, "Server Name", "My Server"),
    new CustomServerSetting(ServerConfig.SettingName.ServerIP, "Server IP Address"),
    new CustomServerSetting(ServerConfig.SettingName.ServerPort, "Server Port", "7777"),
    new CustomServerSetting(ServerConfig.SettingName.ServerQueryPort, "Server Query Port", "7778"),
    new CustomServerSetting(ServerConfig.SettingName.ServerMaxPlayer, "Server Max Players", "8"),
    new CustomServerSetting(ServerConfig.SettingName.ServerMap, "Start Map", "default"),
    new CustomServerSetting("difficulty", "Difficulty", "Normal")
};
```

This will create editable fields for the built-in values and the plugin-specific `difficulty` value.

## Constructor Forms

```csharp
new CustomServerSetting()
```

Creates an empty setting. Usually only useful when setting properties manually.

```csharp
new CustomServerSetting("key")
```

Uses the same text for the config key and label.

```csharp
new CustomServerSetting("key", "Display Label", "default value")
```

Creates a text field with a friendly label and default value.

```csharp
new CustomServerSetting("key", "Display Label", "default value", "Option1", "Option2")
```

Creates a dropdown when options are provided.

## Dropdown Options

Use options when a value has a fixed set of valid choices.

```csharp
new CustomServerSetting("SharedQuests", "Shared Quests", "true", "true", "false"),
new CustomServerSetting("Region", "Server Region", "EU", "SEA", "CIS", "EU"),
new CustomServerSetting("Difficulty", "Difficulty", "Normal", "Easy", "Normal", "Hard")
```

WindowsGSM renders these as dropdowns in `Edit WindowsGSM.cfg`.

## Built-In Setting Keys

Use `ServerConfig.SettingName` when declaring WindowsGSM built-in settings:

```csharp
ServerConfig.SettingName.ServerName
ServerConfig.SettingName.ServerIP
ServerConfig.SettingName.ServerPort
ServerConfig.SettingName.ServerQueryPort
ServerConfig.SettingName.ServerMap
ServerConfig.SettingName.ServerMaxPlayer
ServerConfig.SettingName.ServerGSLT
```

`ServerConfig.SettingName.ServerParam` should not be declared as a custom setting. WindowsGSM keeps Server Start Param as its own field.

## Backup Settings

Plugins can expose backup settings through `CustomServerSetting` too:

```csharp
new CustomServerSetting("saveslocation", "Backup Folders", @"R5\Saved"),
new CustomServerSetting("fileslocation", "Backup Files", @"R5\ServerDescription.json|R5\Saved\SaveProfiles\Default\ServerDescription.json"),
new CustomServerSetting("maximumbackups", "Number of Backups", "3")
```

`saveslocation` and `fileslocation` support multiple entries separated by either:

```text
;
|
```

Relative paths are resolved from the server's `serverfiles` folder.

## Reading Values In A Plugin

Custom values are stored in `WindowsGSM.cfg`.

Read them through the plugin's `ServerConfig` instance:

```csharp
string difficulty = _serverData.GetCustomSetting("difficulty", "Normal");
string region = _serverData.GetCustomSetting("Region", "EU");
```

For numbers and booleans, parse safely and provide fallbacks:

```csharp
private bool GetBool(string key, bool fallback)
{
    string value = _serverData.GetCustomSetting(key, fallback ? "true" : "false");
    return bool.TryParse(value, out bool parsed) ? parsed : fallback;
}

private int GetInt(string key, int fallback)
{
    string value = _serverData.GetCustomSetting(key, fallback.ToString());
    return int.TryParse(value, out int parsed) ? parsed : fallback;
}
```

## Full Example

```csharp
using WindowsGSM.Functions;

public class ExampleServer
{
    private readonly ServerConfig _serverData;

    public ExampleServer(ServerConfig serverData)
    {
        _serverData = serverData;
    }

    public string FullName = "Example Dedicated Server";
    public string StartPath = "ExampleServer.exe";
    public string Port = "7777";
    public string QueryPort = "7778";
    public string Defaultmap = "ExampleMap";
    public string Maxplayers = "8";
    public string Additional = "";

    public CustomServerSetting[] CustomSettings =
    {
        new CustomServerSetting(ServerConfig.SettingName.ServerName, "Server Name", "Example Server"),
        new CustomServerSetting(ServerConfig.SettingName.ServerIP, "Server IP Address"),
        new CustomServerSetting(ServerConfig.SettingName.ServerPort, "Server Port", "7777"),
        new CustomServerSetting(ServerConfig.SettingName.ServerQueryPort, "Server Query Port", "7778"),
        new CustomServerSetting(ServerConfig.SettingName.ServerMaxPlayer, "Server Max Players", "8"),
        new CustomServerSetting(ServerConfig.SettingName.ServerMap, "World Name", "ExampleWorld"),
        new CustomServerSetting("Region", "Region", "EU", "SEA", "CIS", "EU"),
        new CustomServerSetting("AllowPvp", "Allow PvP", "true", "true", "false"),
        new CustomServerSetting("saveslocation", "Backup Folders", @"Saved"),
        new CustomServerSetting("maximumbackups", "Number of Backups", "3")
    };

    public string StartParameters()
    {
        string region = _serverData.GetCustomSetting("Region", "EU");
        bool allowPvp = bool.TryParse(_serverData.GetCustomSetting("AllowPvp", "true"), out bool parsed) && parsed;

        return $"-region {region} -pvp {allowPvp.ToString().ToLowerInvariant()}";
    }
}
```

## Notes For Plugin Authors

- Prefer stable, lowercase config keys for new settings where possible.
- Use friendly labels for user-facing fields.
- Use dropdown options for true/false values and known enum-like values.
- Keep generated/runtime-only values out of editable custom settings unless the user is meant to change them.
- Use built-in setting names through `ServerConfig.SettingName` instead of typing strings manually.
- Keep `Server Start Param` separate; WindowsGSM already provides that field.
- If using schema mode, declare every built-in setting you want users to edit.
