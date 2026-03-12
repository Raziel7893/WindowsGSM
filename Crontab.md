## Improved Crontab Managing.
Crontabs can now also Execute Windows commands and send Server Console Commands 
They can now be configured by adding *.csv files to the server config folder (servers\\%ServerID%\\configs\\Crontab) (or click Browse => Server Configs in WindowsGSM while the server is marked, then create the folder Crontab if not existing.)

You can Add multiple lines to that csv file, and also add multiple files. WGSM will try to read all *.csv files in that folder.
Comments can be added by 2 leading slashes "//" as first characters in that line
### File Structure
> CrontabExpression;Type;Command;Arguments

1. Example Contents for Execute:
> * 6 * * *;exec;cmd.exe;/C "C:\Full\Path\To\test.bat"
> * 7 * * *;exec;ping.exe;127.0.0.1 /n 8

2. Example for sending Commands:
> 5 * * * *;ServerConsoleCommand;cheat serverchat this message will occure every hour

3. Example for restart with message Commands:
> 5 6 * * *;ServerConsoleCommand;cheat serverchat server will restart in 5 mins
> 9 6 * * *;ServerConsoleCommand;cheat saveworld
> 10 6 * * *;restart

4. Example for additional Restarts besides the Gui defined one:
> * 2 * * *;restart

### Notes 
Restart WGSM after creating or changing the file or restart the gameserver, it should reload it aswell

Make sure none of the crontabs overlapp too much. Exec programms will only be stopped on the Restart of that server, so make sure the programms do not run continously.

The config Folder is Admin only Protected, as this would allow an easy rights escalation

### Crontab Syntax 
https://crontab.guru /
