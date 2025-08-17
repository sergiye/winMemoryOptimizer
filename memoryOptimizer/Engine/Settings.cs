using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.Win32;

namespace memoryOptimizer {

  internal static class Settings {
  
    static Settings() {
      AutoOptimizationInterval = 0;
      AutoOptimizationMemoryUsage = 0;
      UpdateIntervalSeconds = 30;
      MemoryAreas = Enums.MemoryAreas.CombinedPageList | Enums.MemoryAreas.ModifiedPageList |
                    Enums.MemoryAreas.ProcessesWorkingSet | Enums.MemoryAreas.StandbyList |
                    Enums.MemoryAreas.SystemWorkingSet;
      ProcessExclusionList = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
      RunOnPriority = Enums.Priority.Low;
      ShowOptimizationNotifications = true;
      ShowVirtualMemory = true;
      TrayIconMode = Enums.TrayIconMode.Image;
      var taskbarColor = NativeMethods.GetTaskbarColor(); 
      TrayIconValueColor = taskbarColor.IsDark() ? Color.White : Color.Black;

      try {
        using (var key = Registry.CurrentUser.OpenSubKey(Constants.RegistryKey.ProcessExclusionList)) {
          if (key != null) {
            foreach (var name in key.GetValueNames())
              ProcessExclusionList.Add(name.RemoveWhitespaces().Replace(".exe", string.Empty).ToLower());
          }
        }

        using (var key = Registry.CurrentUser.OpenSubKey(Constants.RegistryKey.Settings)) {
          if (key == null) return;
          AutoOptimizationInterval = Convert.ToInt32(key.GetValue(Constants.RegistryName.AutoOptimizationInterval, AutoOptimizationInterval));
          AutoOptimizationMemoryUsage = Convert.ToInt32(key.GetValue(Constants.RegistryName.AutoOptimizationMemoryUsage, AutoOptimizationMemoryUsage));
          UpdateIntervalSeconds = Convert.ToInt32(key.GetValue(Constants.RegistryName.UpdateIntervalSeconds, UpdateIntervalSeconds));

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.RegistryName.MemoryAreas, MemoryAreas)),
                out Enums.MemoryAreas memoryAreas) && memoryAreas.IsValid()) {
            if ((memoryAreas & Enums.MemoryAreas.StandbyList) != 0 &&
                (memoryAreas & Enums.MemoryAreas.StandbyListLowPriority) != 0)
              memoryAreas &= ~Enums.MemoryAreas.StandbyListLowPriority;

            MemoryAreas = memoryAreas;
          }

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.RegistryName.RunOnPriority, RunOnPriority)),
                out Enums.Priority runOnPriority) && runOnPriority.IsValid())
            RunOnPriority = runOnPriority;

          ShowOptimizationNotifications = Convert.ToBoolean(key.GetValue(Constants.RegistryName.ShowOptimizationNotifications, ShowOptimizationNotifications));
          ShowVirtualMemory = Convert.ToBoolean(key.GetValue(Constants.RegistryName.ShowVirtualMemory, ShowVirtualMemory));

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.RegistryName.TrayIcon, TrayIconMode)),
                out Enums.TrayIconMode trayIcon) && trayIcon.IsValid())
            TrayIconMode = trayIcon;

          TrayIconValueColor = Color.FromArgb(Convert.ToInt32(key.GetValue(Constants.RegistryName.TrayIconValueColor, 0)));
        }
      }
      catch (Exception e) {
        Logger.Error(e);
      }
      finally {
        Save();
      }
    }

    public static int AutoOptimizationInterval { get; set; }
    public static int AutoOptimizationMemoryUsage { get; set; }
    public static int UpdateIntervalSeconds { get; set; }
    public static Enums.MemoryAreas MemoryAreas { get; set; }
    public static SortedSet<string> ProcessExclusionList { get; }
    public static Enums.Priority RunOnPriority { get; set; }
    public static bool ShowOptimizationNotifications { get; set; }
    public static bool ShowVirtualMemory { get; set; }
    public static Enums.TrayIconMode TrayIconMode { get; set; }
    public static Color TrayIconValueColor { get; set; }

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
          key.SetValue(Constants.RegistryName.TrayIconValueColor, TrayIconValueColor.ToArgb());
        }
      }
      catch (Exception e) {
        Logger.Error(e);
      }
    }
  }
}