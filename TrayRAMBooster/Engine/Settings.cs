using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.Win32;
using sergiye.Common;

namespace TrayRAMBooster {

  internal static class Settings {

    static class RegistryKey {
      public const string Settings = @"SOFTWARE\sergiye\TrayRAMBooster";
      public const string ProcessExclusionList = Settings + @"\ProcessExclusionList";
    }

    static class RegistryName {
      public const string AutoOptimizationInterval = "AutoOptimizationInterval";
      public const string AutoOptimizationMemoryUsage = "AutoOptimizationMemoryUsage";
      public const string UpdateIntervalSeconds = "UpdateIntervalSeconds";
      public const string MemoryAreas = "MemoryAreas";
      public const string RunOnPriority = "RunOnPriority";
      public const string ShowOptimizationNotifications = "ShowOptimizationNotifications";
      public const string ShowVirtualMemory = "ShowVirtualMemory";
      public const string TrayIcon = "TrayIcon";
      public const string DoubleClickAction = "DoubleClickAction";
      public const string TrayIconValueColor = "TrayIconValueColor";
      public const string AutoUpdateApp = "AutoUpdateApp";
    }

    private static Color trayIconValueColor;
    private static Enums.TrayIconMode trayIconMode = Enums.TrayIconMode.MemoryAvailable;
    private static Enums.DoubleClickAction doubleClickAction = Enums.DoubleClickAction.Optimize;
    private static bool showVirtualMemory = true;
    private static bool showOptimizationNotifications = true;
    private static Enums.Priority runOnPriority = Enums.Priority.Low;
    private static Enums.MemoryAreas memoryAreas;
    private static int updateIntervalSeconds = 30;
    private static int autoOptimizationMemoryUsage;
    private static int autoOptimizationInterval;
    private static bool autoUpdateApp;

    static Settings() {
      memoryAreas = Enums.MemoryAreas.ModifiedPageList 
                  | Enums.MemoryAreas.ProcessesWorkingSet 
                  | Enums.MemoryAreas.StandbyListLowPriority 
                  | Enums.MemoryAreas.SystemWorkingSet;
      ProcessExclusionList = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
      trayIconValueColor = WinApiHelper.IsTaskbarDark() 
        ? Color.Lime //Cyan / SpringGreen
        : Color.DarkGreen; //Teal / Green

      try {
        using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey.ProcessExclusionList)) {
          if (key != null) {
            foreach (var name in key.GetValueNames())
              ProcessExclusionList.Add(name.RemoveWhitespaces().Replace(".exe", string.Empty).ToLower());
          }
        }

        using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey.Settings)) {
          if (key == null) return;
          autoOptimizationInterval = key.Get(RegistryName.AutoOptimizationInterval, autoOptimizationInterval);
          autoOptimizationMemoryUsage = key.Get(RegistryName.AutoOptimizationMemoryUsage, autoOptimizationMemoryUsage);
          updateIntervalSeconds = key.Get(RegistryName.UpdateIntervalSeconds, updateIntervalSeconds);
          showOptimizationNotifications = key.Get(RegistryName.ShowOptimizationNotifications, showOptimizationNotifications);
          showVirtualMemory = key.Get(RegistryName.ShowVirtualMemory, showVirtualMemory);
          autoUpdateApp = key.Get(RegistryName.AutoUpdateApp, autoUpdateApp);
          trayIconValueColor = key.Get(RegistryName.TrayIconValueColor, trayIconValueColor);
          memoryAreas = key.GetEnum(RegistryName.MemoryAreas, memoryAreas);
          runOnPriority = key.GetEnum(RegistryName.RunOnPriority, runOnPriority);
          trayIconMode = key.GetEnum(RegistryName.TrayIcon, trayIconMode);
          doubleClickAction = key.GetEnum(RegistryName.DoubleClickAction, doubleClickAction);
        }
      }
      catch (Exception e) {
        Logger.Error(e);
      }
      finally {
        Save();
      }
    }

    public static int AutoOptimizationInterval {
      get => autoOptimizationInterval;
      set {
        if (autoOptimizationInterval == value) return;
        autoOptimizationInterval = value;
        Save();
      }
    }

    public static int AutoOptimizationMemoryUsage {
      get => autoOptimizationMemoryUsage;
      set {
        if (autoOptimizationMemoryUsage == value) return;
        autoOptimizationMemoryUsage = value;
        Save();
      }
    }

    public static int UpdateIntervalSeconds {
      get => updateIntervalSeconds;
      set {
        if (updateIntervalSeconds == value) return;
        updateIntervalSeconds = value;
        Save();
      }
    }

    public static Enums.MemoryAreas MemoryAreas {
      get => memoryAreas;
      set {
        if (memoryAreas == value) return;
        memoryAreas = value;
        Save();
      }
    }

    public static SortedSet<string> ProcessExclusionList { get; }

    public static Enums.Priority RunOnPriority {
      get => runOnPriority;
      set {
        if (RunOnPriority == value) return;
        runOnPriority = value;
        Save();
      }
    }

    public static bool ShowOptimizationNotifications {
      get => showOptimizationNotifications;
      set {
        if (showOptimizationNotifications == value) return;
        showOptimizationNotifications = value;
        Save();
      }
    }

    public static bool ShowVirtualMemory {
      get => showVirtualMemory;
      set {
        if (showVirtualMemory == value) return;
        showVirtualMemory = value;
        Save();
      }
    }

    public static bool AutoUpdateApp {
      get => autoUpdateApp;
      set {
        if (autoUpdateApp == value) return;
        autoUpdateApp = value;
        Save();
      }
    }

    public static Enums.TrayIconMode TrayIconMode {
      get => trayIconMode;
      set {
        if (trayIconMode == value) return;
        trayIconMode = value;
        Save();
      }
    }

    public static Enums.DoubleClickAction DoubleClickAction {
      get => doubleClickAction;
      set {
        if (doubleClickAction == value) return;
        doubleClickAction = value;
        Save();
      }
    }

    public static Color TrayIconValueColor {
      get => trayIconValueColor;
      set {
        if (trayIconValueColor == value) return;
        trayIconValueColor = value;
        Save();
      }
    }

    public static void Save() {
      try {
        Registry.CurrentUser.DeleteSubKey(RegistryKey.ProcessExclusionList, false);

        if (ProcessExclusionList.Any()) {
          using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey.ProcessExclusionList)) {
            if (key != null) {
              foreach (var process in ProcessExclusionList)
                key.SetValue(process.RemoveWhitespaces().Replace(".exe", string.Empty).ToLower(), string.Empty,
                  RegistryValueKind.String);
            }
          }
        }

        using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey.Settings)) {
          if (key == null) return;
          key.SetValue(RegistryName.AutoOptimizationInterval, autoOptimizationInterval);
          key.SetValue(RegistryName.AutoOptimizationMemoryUsage, autoOptimizationMemoryUsage);
          key.SetValue(RegistryName.UpdateIntervalSeconds, updateIntervalSeconds);
          key.SetValue(RegistryName.MemoryAreas, (int) memoryAreas);
          key.SetValue(RegistryName.RunOnPriority, (int) runOnPriority);
          key.SetValue(RegistryName.ShowOptimizationNotifications, showOptimizationNotifications ? 1 : 0);
          key.SetValue(RegistryName.ShowVirtualMemory, showVirtualMemory ? 1 : 0);
          key.SetValue(RegistryName.AutoUpdateApp, autoUpdateApp ? 1 : 0);
          key.SetValue(RegistryName.TrayIcon, (int) trayIconMode);
          key.SetValue(RegistryName.DoubleClickAction, (int) doubleClickAction);
          key.SetValue(RegistryName.TrayIconValueColor, trayIconValueColor.ToArgb());
        }
      }
      catch (Exception e) {
        Logger.Error(e);
      }
    }
  }
}