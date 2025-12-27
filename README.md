# RemoteDrive
A C# example project using the [Windows Projected File System](https://learn.microsoft.com/en-us/windows/win32/projfs/projected-file-system) to provide a virtual file system.

#### [Enabling Windows Projected File System](https://learn.microsoft.com/en-us/windows/win32/projfs/enabling-windows-projected-file-system)
ProjFS enablement is required for this project to work correctly. ProjFS ships as an optional component starting in Windows 10 version 1809.
```
Enable-WindowsOptionalFeature -Online -FeatureName Client-ProjFS -NoRestart
```

### DirectoryProvider
Mirrors one folder to another folder. Similar to a symbolic link but with ProjFS.

### PerforceProvider
Virtual File System for Perforce. Uses the perforce file info to replicate the repository file structure. When a file is read for the first time, it will request the data from the perforce server.

Inspired By: [Virtual Sync: Terabytes on Demand](https://www.gdcvault.com/play/1027699/Virtual-Sync-Terabytes-on)
