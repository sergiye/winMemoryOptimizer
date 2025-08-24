using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.Win32;

namespace TrayRAMBooster {

  internal static class Settings {
    private static Color trayIconValueColor;
    private static Enums.TrayIconMode trayIconMode;
    private static Enums.DoubleClickAction doubleClickAction;
    private static bool showVirtualMemory;
    private static bool showOptimizationNotifications;
    private static Enums.Priority runOnPriority;
    private static Enums.MemoryAreas memoryAreas;
    private static int updateIntervalSeconds;
    private static int autoOptimizationMemoryUsage;
    private static int autoOptimizationInterval;

    static Settings() {
      autoOptimizationInterval = 0;
      autoOptimizationMemoryUsage = 0;
      updateIntervalSeconds = 30;
      memoryAreas = Enums.MemoryAreas.ModifiedPageList 
                  | Enums.MemoryAreas.ProcessesWorkingSet 
                  | Enums.MemoryAreas.StandbyListLowPriority 
                  | Enums.MemoryAreas.SystemWorkingSet;
      ProcessExclusionList = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
      runOnPriority = Enums.Priority.Low;
      showOptimizationNotifications = true;
      showVirtualMemory = true;
      trayIconMode = Enums.TrayIconMode.MemoryAvailable;
      doubleClickAction = Enums.DoubleClickAction.Optimize;
      var taskbarColor = NativeMethods.GetTaskbarColor(); 
      trayIconValueColor = taskbarColor.IsDark() 
        ? Color.Lime //Cyan / SpringGreen
        : Color.DarkGreen; //Teal / Green

      try {
        using (var key = Registry.CurrentUser.OpenSubKey(Constants.RegistryKey.ProcessExclusionList)) {
          if (key != null) {
            foreach (var name in key.GetValueNames())
              ProcessExclusionList.Add(name.RemoveWhitespaces().Replace(".exe", string.Empty).ToLower());
          }
        }

        using (var key = Registry.CurrentUser.OpenSubKey(Constants.RegistryKey.Settings)) {
          if (key == null) return;
          autoOptimizationInterval = Convert.ToInt32(key.GetValue(Constants.RegistryName.AutoOptimizationInterval, autoOptimizationInterval));
          autoOptimizationMemoryUsage = Convert.ToInt32(key.GetValue(Constants.RegistryName.AutoOptimizationMemoryUsage, autoOptimizationMemoryUsage));
          updateIntervalSeconds = Convert.ToInt32(key.GetValue(Constants.RegistryName.UpdateIntervalSeconds, updateIntervalSeconds));

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.RegistryName.MemoryAreas, memoryAreas)),
                out Enums.MemoryAreas memoryAreasValue) && memoryAreasValue.IsValid()) {
            if ((memoryAreasValue & Enums.MemoryAreas.StandbyList) != 0 &&
                (memoryAreasValue & Enums.MemoryAreas.StandbyListLowPriority) != 0)
              memoryAreasValue &= ~Enums.MemoryAreas.StandbyListLowPriority;

            memoryAreas = memoryAreasValue;
          }

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.RegistryName.RunOnPriority, runOnPriority)),
                out Enums.Priority runOnPriorityValue) && runOnPriorityValue.IsValid())
            runOnPriority = runOnPriorityValue;

          showOptimizationNotifications = Convert.ToBoolean(key.GetValue(Constants.RegistryName.ShowOptimizationNotifications, showOptimizationNotifications));
          showVirtualMemory = Convert.ToBoolean(key.GetValue(Constants.RegistryName.ShowVirtualMemory, showVirtualMemory));

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.RegistryName.TrayIcon, trayIconMode)),
                out Enums.TrayIconMode trayIcon) && trayIcon.IsValid())
            trayIconMode = trayIcon;

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.RegistryName.DoubleClickAction, doubleClickAction)),
                out Enums.DoubleClickAction clickAction) && doubleClickAction.IsValid())
            doubleClickAction = clickAction;

          trayIconValueColor = Color.FromArgb(Convert.ToInt32(key.GetValue(Constants.RegistryName.TrayIconValueColor, trayIconValueColor)));
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
        Registry.CurrentUser.DeleteSubKey(Constants.RegistryKey.ProcessExclusionList, false);

        if (ProcessExclusionList.Any()) {
          using (var key = Registry.CurrentUser.CreateSubKey(Constants.RegistryKey.ProcessExclusionList)) {
            if (key != null) {
              foreach (var process in ProcessExclusionList)
                key.SetValue(process.RemoveWhitespaces().Replace(".exe", string.Empty).ToLower(), string.Empty,
                  RegistryValueKind.String);
            }
          }
        }

        using (var key = Registry.CurrentUser.CreateSubKey(Constants.RegistryKey.Settings)) {
          if (key == null) return;
          key.SetValue(Constants.RegistryName.AutoOptimizationInterval, AutoOptimizationInterval);
          key.SetValue(Constants.RegistryName.AutoOptimizationMemoryUsage, AutoOptimizationMemoryUsage);
          key.SetValue(Constants.RegistryName.UpdateIntervalSeconds, UpdateIntervalSeconds);
          key.SetValue(Constants.RegistryName.MemoryAreas, (int) MemoryAreas);
          key.SetValue(Constants.RegistryName.RunOnPriority, (int) RunOnPriority);
          key.SetValue(Constants.RegistryName.ShowOptimizationNotifications, ShowOptimizationNotifications ? 1 : 0);
          key.SetValue(Constants.RegistryName.ShowVirtualMemory, ShowVirtualMemory ? 1 : 0);
          key.SetValue(Constants.RegistryName.TrayIcon, (int) TrayIconMode);
          key.SetValue(Constants.RegistryName.DoubleClickAction, (int) DoubleClickAction);
          key.SetValue(Constants.RegistryName.TrayIconValueColor, TrayIconValueColor.ToArgb());
        }
      }
      catch (Exception e) {
        Logger.Error(e);
      }
    }
  }
}