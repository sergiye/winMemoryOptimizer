# Tray RAM Booster

[![Release (latest)](https://img.shields.io/github/v/release/sergiye/TrayRAMBooster)](https://github.com/sergiye/TrayRAMBooster/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/sergiye/TrayRAMBooster/total?color=ff4f42)](https://github.com/sergiye/TrayRAMBooster/releases)
[![GitHub last commit](https://img.shields.io/github/last-commit/sergiye/TrayRAMBooster?color=00AD00)](https://github.com/sergiye/trayRAMbooster/commits/master)
[![](https://img.shields.io/badge/WINDOWS-7%20%E2%80%93%2011-blue)](https://endoflife.date/windows) 
[![](https://img.shields.io/badge/SERVER-2012%20%E2%80%93%202025-blue)](https://endoflife.date/windows-server) 

TrayRAMBooster uses native Windows features to clear and optimize memory areas. Sometimes, programs do not release the allocated memory, making the computer slow. That is when you need to optimize the memory so you can keep working without wasting time restarting your system. 

This tool is inspired by the [Igor Mundstein's WinMemoryCleaner project](https://github.com/IgorMundstein/WinMemoryCleaner).
The main idea was to create super-minimalistic and portable application with all functionality.

The app has no UI, just notification icon. 
It's portable, and you do not need to install it, but it requires administrator privileges to run. Click on the download button below and run the executable to get started.


## What does it look like?

Here is an example of the application running on Windows 10 (light/dark theme):

[<img src="https://github.com/sergiye/TrayRAMBooster/raw/master/preview.png" alt="preview"/>](https://github.com/sergiye/TrayRAMBooster/raw/master/preview.png)

and a preview of the app notification after optimization completed:

[<img src="https://github.com/sergiye/TrayRAMBooster/raw/master/preview_notification.png" alt="preview_notification"/>](https://github.com/sergiye/TrayRAMBooster/raw/master/preview_notification.png)

## Download Latest Version

The published version can be obtained from [releases](https://github.com/sergiye/TrayRAMBooster/releases) page, or get the newer one directly from:
[Latest Version](https://github.com/sergiye/TrayRAMBooster/releases/latest)

## Features

### Auto optimization

- `Every X hours` The optimization will run by period
- `When free memory is below X percent` The optimization will run if free memory is below the specified percentage

### Settings

- `Run on startup` Runs the app after the system boots up. It creates an entry on Windows **Task Scheduler**
- `Show optimization notifications` Sends a message to the notification area after optimization. It includes the approximate memory released
- `Show virtual memory` It also monitors the virtual memory usage
- `Run on low priority` It limits the app resource usage by reducing the process priority and ensuring it runs efficiently. It might increase the optimization time, but it helps if your Windows freezes during it
- `Auto update app` Keeps the app up to date. 

### Memory Areas

- `Combined Page List` Flushes the blocks from the combined page list effectively only when page combining is enabled
- `Modified Page List` Flushes memory from the modified page list, writing unsaved data to disk and moving the pages to the standby list
- `Processes Working Set` Removes memory from all user-mode and system working sets and moves it to the standby or modified page lists
- `Standby List` Flushes pages from all standby lists to the free list
- `Standby List (Low Priority)` Flushes pages from the lowest-priority standby list to the free list
- `System Working Set` Removes memory from the system cache working set

<!-- ### Processes excluded from optimization
- You can build a list of processes to ignore when memory is optimized -->

### Tray icon type

- `Image` Show app icon
- `Memory usage` Show physical memory usage (in %)
- `Memory available` Show physical memory available (in Gb)
- `Memory used` Show physical memory used (in Gb)

The tray icon tooltip also depends on the selected `Icon type`, but can contain virtual memory value if `Show virtual memory` option checked.

## Logs

The app generates logs in the Windows event

1. Press **Win + R** to open the Run command dialog box
2. Type **eventvwr** and press **Enter** to open the Event Viewer
3. Open `Windows Logs` -> `Applications`


## Frequently Asked Questions (FAQ)

### Why has the app been flagged as Malware/Virus and blocked by Windows Defender, SmartScreen, or Antivirus?

One of the reasons for this **false alarm** is that the application adds entries to the registry and task scheduler to run the application at startup. Windows doesn't “like” applications with administrator privileges running at startup. Sorry, but the application cannot deep clean memory without administrator privileges.

That's a common issue that persists every time a new app version is released. 
Everyone can submit the executable to Microsoft, usually, it takes up to 72 hours for Microsoft to remove the detection.
It helps if more users [submit the app for malware analysis](https://www.microsoft.com/en-us/wdsi/filesubmission)

Meanwhile, as a workaround, you can [add an exclusion to Windows Security](https://support.microsoft.com/en-us/windows/add-an-exclusion-to-windows-security-811816c0-4dfd-af4a-47e4-c301afe13b26)

## How can I help improve it?
The TrayRAMBooster team welcomes feedback and contributions!<br/>
You can check if it works properly on your PC. If you notice any inaccuracies, please send us a pull request. If you have any suggestions or improvements, don't hesitate to create an issue.

Also, don't forget to star the repository to help other people find it.

[![Star History Chart](https://api.star-history.com/svg?repos=sergiye/TrayRAMBooster&type=Date)](https://star-history.com/#sergiye/TrayRAMBooster&Date)

<!-- [//]: # ([![Stargazers over time]&#40;https://starchart.cc/sergiye/TrayRAMBooster.svg?variant=adaptive&#41;]&#40;https://starchart.cc/sergiye/TrayRAMBooster&#41;) -->

[![Stargazers for @sergiye/TrayRAMBooster](https://reporoster.com/stars/sergiye/TrayRAMBooster)](https://github.com/sergiye/TrayRAMBooster/stargazers)

## Donate!
Every [cup of coffee](https://patreon.com/SergiyE) you donate will help this app become better and let me know that this project is in demand.

## License
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
