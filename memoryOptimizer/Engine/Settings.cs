using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace memoryOptimizer {

  internal static class Settings {
  
    static Settings() {
      AutoOptimizationInterval = 0;
      AutoOptimizationMemoryUsage = 0;
      MemoryAreas = Enums.Memory.Areas.CombinedPageList | Enums.Memory.Areas.ModifiedPageList |
                    Enums.Memory.Areas.ProcessesWorkingSet | Enums.Memory.Areas.StandbyList |
                    Enums.Memory.Areas.SystemWorkingSet;
      ProcessExclusionList = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
      RunOnPriority = Enums.Priority.Low;
      ShowOptimizationNotifications = true;
      ShowVirtualMemory = true;
      TrayIconЬщвуIcon = Enums.TrayIconMode.Image;

      try {
        using (var key = Registry.CurrentUser.OpenSubKey(Constants.App.Registry.Key.ProcessExclusionList)) {
          if (key != null) {
            foreach (var name in key.GetValueNames())
              ProcessExclusionList.Add(name.RemoveWhitespaces().Replace(".exe", string.Empty).ToLower());
          }
        }

        using (var key = Registry.CurrentUser.OpenSubKey(Constants.App.Registry.Key.Settings)) {
          if (key == null) return;
          AutoOptimizationInterval = Convert.ToInt32(key.GetValue(Constants.App.Registry.Name.AutoOptimizationInterval,
            AutoOptimizationInterval));
          AutoOptimizationMemoryUsage = Convert.ToInt32(key.GetValue(Constants.App.Registry.Name.AutoOptimizationMemoryUsage,
            AutoOptimizationMemoryUsage));

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.App.Registry.Name.MemoryAreas, MemoryAreas)),
                out Enums.Memory.Areas memoryAreas) && memoryAreas.IsValid()) {
            if ((memoryAreas & Enums.Memory.Areas.StandbyList) != 0 &&
                (memoryAreas & Enums.Memory.Areas.StandbyListLowPriority) != 0)
              memoryAreas &= ~Enums.Memory.Areas.StandbyListLowPriority;

            MemoryAreas = memoryAreas;
          }

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.App.Registry.Name.RunOnPriority, RunOnPriority)),
                out Enums.Priority runOnPriority) && runOnPriority.IsValid())
            RunOnPriority = runOnPriority;

          ShowOptimizationNotifications = Convert.ToBoolean(key.GetValue(Constants.App.Registry.Name.ShowOptimizationNotifications, ShowOptimizationNotifications));
          ShowVirtualMemory = Convert.ToBoolean(key.GetValue(Constants.App.Registry.Name.ShowVirtualMemory, ShowVirtualMemory));

          if (Enum.TryParse(Convert.ToString(key.GetValue(Constants.App.Registry.Name.TrayIcon, TrayIconЬщвуIcon)),
                out Enums.TrayIconMode trayIcon) && trayIcon.IsValid())
            TrayIconЬщвуIcon = trayIcon;
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
    public static Enums.Memory.Areas MemoryAreas { get; set; }
    public static SortedSet<string> ProcessExclusionList { get; }
    public static Enums.Priority RunOnPriority { get; set; }
    public static bool ShowOptimizationNotifications { get; set; }
    public static bool ShowVirtualMemory { get; set; }
    public static Enums.TrayIconMode TrayIconЬщвуIcon { get; set; }

    public static void Save() {
      try {
        Registry.CurrentUser.DeleteSubKey(Constants.App.Registry.Key.ProcessExclusionList, false);

        if (ProcessExclusionList.Any()) {
          using (var key = Registry.CurrentUser.CreateSubKey(Constants.App.Registry.Key.ProcessExclusionList)) {
            if (key != null) {
              foreach (var process in ProcessExclusionList)
                key.SetValue(process.RemoveWhitespaces().Replace(".exe", string.Empty).ToLower(), string.Empty,
                  RegistryValueKind.String);
            }
          }
        }

        using (var key = Registry.CurrentUser.CreateSubKey(Constants.App.Registry.Key.Settings)) {
          if (key == null) return;
          key.SetValue(Constants.App.Registry.Name.AutoOptimizationInterval, AutoOptimizationInterval);
          key.SetValue(Constants.App.Registry.Name.AutoOptimizationMemoryUsage, AutoOptimizationMemoryUsage);
          key.SetValue(Constants.App.Registry.Name.MemoryAreas, (int) MemoryAreas);
          key.SetValue(Constants.App.Registry.Name.RunOnPriority, (int) RunOnPriority);
          key.SetValue(Constants.App.Registry.Name.ShowOptimizationNotifications, ShowOptimizationNotifications ? 1 : 0);
          key.SetValue(Constants.App.Registry.Name.ShowVirtualMemory, ShowVirtualMemory ? 1 : 0);
          key.SetValue(Constants.App.Registry.Name.TrayIcon, (int) TrayIconЬщвуIcon);
        }
      }
      catch (Exception e) {
        Logger.Error(e);
      }
    }
  }
}