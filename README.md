# Memory Optimizer<

This free RAM optimizer uses native Windows features to clear and optimize memory areas. Sometimes, programs do not release the allocated memory, making the computer slow. That is when you need to optimize the memory so you can keep working without wasting time restarting your system. 

The app has no UI, just notification icon. It's portable, and you do not need to install it, but it requires administrator privileges to run. Click on the download button below and run the executable to get started.

## 🖥️ Computer requirements

- Microsoft .NET Framework 4
- Windows `XP` `Vista` `7` `8` `10` `11`
- Windows Server `2003` `2008` `2012` `2016` `2019` `2022`

## 🚀 Features

### Auto optimization

- `Every X hours` The optimization will run by period
- `When free memory is below X percent` The optimization will run if free memory is below the specified percentage

### Memory Areas

- `Combined Page List` Flushes the blocks from the combined page list effectively only when page combining is enabled
- `Modified Page List` Flushes memory from the modified page list, writing unsaved data to disk and moving the pages to the standby list
- `Processes Working Set` Removes memory from all user-mode and system working sets and moves it to the standby or modified page lists
- `Standby List` Flushes pages from all standby lists to the free list
- `Standby List (Low Priority)` Flushes pages from the lowest-priority standby list to the free list
- `System Working Set` Removes memory from the system cache working set

### Processes excluded from optimization

- You can build a list of processes to ignore when memory is optimized

### Settings

- `Auto update` Keeps the app up to date. It checks for updates every 24 hours
- `Close after optimization` Closes the app after optimization
- `Run on low priority` It limits the app resource usage by reducing the process priority and ensuring it runs efficiently. It might increase the optimization time, but it helps if your Windows freezes during it
- `Run on startup` Runs the app after the system boots up. It creates an entry on Windows **Task Scheduler** and Windows Registry path **SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run**
- `Show optimization notifications` Sends a message to the notification area after optimization. It includes the approximate memory released
- `Show virtual memory` It also monitors the virtual memory usage

### System tray (Notification area)

- Menu

<picture>
  <img src="/.github/images/system-tray.png">
</picture>

- Notification

<picture>
  <img src="/.github/images/notification.png">
</picture>

### Tray icon

- `Image` Show app icon
- `Memory usage` Show physical memory usage with a background color based on the value
  - `(0% - 79%)` <picture><img src="/.github/images/memory-usage.png"></picture>
  - `(80% - 89%)` <picture><img src="/.github/images/memory-usage-warning.png"></picture>
  - `(90% - 100%)` <picture><img src="/.github/images/memory-usage-danger.png"></picture>

## 🔳 Command arguments (NO GUI)

You can use the following arguments to run the app silently.

- `/CombinedPageList`
- `/ModifiedPageList`
- `/ProcessesWorkingSet`
- `/StandbyList` OR `/StandbyListLowPriority`
- `/SystemWorkingSet`

Shortcut target example

`C:\memoryOptimizer.exe /ModifiedPageList /ProcessesWorkingSet /StandbyList /SystemWorkingSet`

## 📖 Logs

The app generates logs in the Windows event

1. Press **Win + R** to open the Run command dialog box
2. Type **eventvwr** and press **Enter** to open the Event Viewer

<picture>
  <img src="/.github/images/windows-event-log.png">
</picture>

## ❓ Frequently Asked Questions (FAQ)

### What are the project requirements?

- Portable (Single .exe file)
- Use of Windows native methods for memory management
- Windows Registry to save user config
- Windows Event to save logs

### Where does the app save the user configuration?

Each user setting is saved in the Windows registry path `Computer\HKEY_CURRENT_USER\Software\sergiye\memoryOptimizer`

### Why has the app been flagged as Malware/Virus and blocked by Windows Defender, SmartScreen, or Antivirus?

One of the reasons for this **false alarm** is that the application adds entries to the registry and task scheduler to run the application at startup. Windows doesn't “like” applications with administrator privileges running at startup. I understand that, but this is the way to do it. I apologize, but the application cannot deep clean memory without administrator privileges.

That's a common issue that persists every time a new app version is released. 

I constantly submit the executable to Microsoft. Usually, it takes up to 72 hours for Microsoft to remove the detection.
It helps if more users [submit the app for malware analysis](https://www.microsoft.com/en-us/wdsi/filesubmission)

Meanwhile, as a workaround, you can [add an exclusion to Windows Security](https://support.microsoft.com/en-us/windows/add-an-exclusion-to-windows-security-811816c0-4dfd-af4a-47e4-c301afe13b26)

## 🌐 Bugs & 

💡 If you are a .NET developer

