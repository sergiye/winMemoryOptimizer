using System;

namespace memoryOptimizer {

  internal static class Enums {
    
    [Flags]
    public enum LogLevels {
      Debug = 1,
      Information = 2,
      Warning = 4,
      Error = 8
    }

    public static class Memory {
      [Flags]
      public enum Areas {
        None = 0,
        CombinedPageList = 1,
        ModifiedPageList = 2,
        ProcessesWorkingSet = 4,
        StandbyList = 8,
        StandbyListLowPriority = 16,
        SystemWorkingSet = 32
      }

      public enum Unit {
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
    }

    public enum TrayIconMode {
      Image,
      MemoryUsage,
      MemoryAvailable,
      MemoryUsed,
    }

    public enum Priority {
      Low,
      Normal,
      High
    }
  }
}