# Publishing WindowsGSM

This guide covers building a release copy of WindowsGSM for Windows users.

## Recommended Release Publish

Use a self-contained Windows x64 publish for public releases:

```powershell
dotnet publish .\WindowsGSM\WindowsGSM.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o D:\WindowsGSM
```

This creates a release build in:

```text
D:\WindowsGSM
```

Run the published executable from that output folder:

```text
D:\WindowsGSM\WindowsGSM.exe
```

## .NET Runtime Requirements

Self-contained releases bundle the .NET runtime.

That means users do not need to install .NET 10 separately when using the recommended publish command.

Framework-dependent builds are different. If you publish without `--self-contained true`, users must install the matching .NET Desktop Runtime before WindowsGSM can start.

## Common Publish Mistake

Do not double-click the small project/apphost executable from the source or intermediate build folders when testing a single-file release.

Use the executable from the publish output folder.

For example:

```text
D:\WindowsGSM\WindowsGSM.exe
```

not an older executable from:

```text
WindowsGSM\bin
WindowsGSM\obj
```

## Build Verification

Before publishing, run:

```powershell
dotnet build .\WindowsGSM\WindowsGSM.csproj -v:m
```

Expected result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

## Portable Folder Notes

WindowsGSM is designed to keep managed data inside the WindowsGSM folder:

```text
Backups\
bin\
logs\
plugins\
servers\
```

The app resolves its root path from the running executable. This helps copied or moved published folders keep working.

Some config paths may be rebased when they clearly point to an older WindowsGSM root. Intentional absolute paths outside the WindowsGSM folder, such as `D:\Backups\1`, should be preserved.

## Files To Exclude From Source Control

Do not commit generated local runtime/build folders such as:

```text
.dotnet_home\
bin\
obj\
release\
```

The `.dotnet_home` folder can appear when building in restricted environments. It is local tooling/cache data and should not be committed.
