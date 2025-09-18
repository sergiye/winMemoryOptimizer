﻿using System;

namespace winMemoryOptimizer {

  internal static class Enums {
    
    [Flags]
    public enum LogLevels {
      Debug = 1,
      Information = 2,
      Warning = 4,
      Error = 8
    }
    
    [Flags]
    public enum MemoryAreas {
      None = 0,
      CombinedPageList = 1,
      ModifiedPageList = 2,
      ProcessesWorkingSet = 4,
      StandbyList = 8,
      StandbyListLowPriority = 16,
      SystemWorkingSet = 32,
      ModifiedFileCache = 64,
    }
    
    public enum MemoryUnit {
      B,
      KB,
      MB,
      GB,
      TB,
      PB,
      EB,
      ZB,
      YB
    }

    public enum TrayIconMode {
      Image,
      MemoryUsageBar,
      MemoryUsagePie,
      MemoryUsagePercent,
      MemoryUsageValue,
      MemoryAvailable,
    }

    public enum DoubleClickAction {
      None,
      Optimize,
      TaskManager,
      ResourceMonitor,
      ShowStatus,
    }

    public enum OptimizationReason {
      Manual,
      Scheduled,
      Usage
    }

    public enum Priority {
      Low,
      Normal,
      High
    }
  }
}