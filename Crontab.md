# Improved Crontab Managing.
Crontabs can now also Execute Windows commands and send Server Console Commands 
They can now be configured by adding *.csv files to the server config folder (servers\\%ServerID%\\configs\\Crontab) (or click Browse => Server Configs in WindowsGSM while the server is marked, then create the folder Crontab if not existing.)

You can Add multiple lines to that csv file, and also add multiple files. WGSM will try to read all *.csv files in that folder.
Comments can be added by 2 leading slashes "//" as first characters in that line

## File Structure
> CrontabExpression;Type;Command;Arguments

1. Example Contents for Execute:
> 5 1 * * *;exec;cmd.exe;/C "C:\Full\Path\To\test.bat"
> 
> 6 1 * * *;exec;ping.exe;127.0.0.1 /n 8

2. Example for sending Commands:
> 5 * * * *;ServerConsoleCommand;cheat serverchat this message will occure every hour

3. Example for sending Commands:
> 5 * * * *;RconCommand;say this message will occure every hour

4. Example for restart with message Commands in ARK:
> 5 6 * * *;ServerConsoleCommand;cheat serverchat server will restart in 5 mins
> 
> 9 6 * * *;ServerConsoleCommand;cheat saveworld
> 
> 10 6 * * *;restart

5. Example for additional Restarts besides the Gui defined one:
> * 2 * * *;restart

## RCON Support
Rcon needs to be configured beforehand, and windowsgsm needs to be closed at that point.
* Find the file servers\\%ServerID%\\configs\\WindowsGSM.cfg
* Find the RCON entries at the end (you need to have the new wgsm version started once for it to have been created)
* Set the Port, IP and Password according to your server. 
  * It can be that 127.0.0.1 does not work, at least for Minecraft it only listens for the actual local IP wgsm set as ServerIP (look at the beginning of the file to find it) 
  * For Plugins using the random Password function wgsm will preset that itself
> 5 6 * * *;RconCommand;say server will restart in 5 mins

### Known issues

Some Games do not have an exact implementation of the Source RCON protocoll and will not work with that(HumanityZ for example). 
You will get a "Timeout while waiting for response" or similar in the right log window.
If you have a working RCON client, ( https://github.com/Tiiffi/mcrcon for example) you can actually use that with my crontab. The line would be something like that then:
> 1 1 * * *;exec;C:/WindowsGSM/mcrcon.exe; -H 127.0.0.1 -P port -p password "admin The server will restart in 5 min"

If the config does not appear on an existing server, paste the following to the end of your WindowsGSM.cfg of that particular server while Windowsgsm is shut down
```ini
rconip="192.168.201.22"
rconport="25575"
rconpassword="dtS9bi9SvHN0"
```

### Notes 
Restart WGSM after creating or changing the file or restart the gameserver, it should reload it aswell

Make sure none of the crontabs overlapp too much. Exec programms will only be stopped on the Restart of that server, so make sure the programms do not run continously.

The config Folder is Admin only Protected, as this would allow an easy rights escalation

### Crontab Syntax 
https://crontab.guru /
