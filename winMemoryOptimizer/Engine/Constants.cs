namespace winMemoryOptimizer {
  
  internal static class Constants {
    
    public static class Windows {
      public static class Privilege {
        public const string SeDebugName = "SeDebugPrivilege"; // Required to debug and adjust the memory of a process owned by another account. User Right: Debug programs.

        public const string SeIncreaseQuotaName = "SeIncreaseQuotaPrivilege"; // Required to increase the quota assigned to a process. User Right: Adjust memory quotas for a process.

        public const string SeProfSingleProcessName = "SeProfileSingleProcessPrivilege"; // Required to gather profiling information for a single process. User Right: Profile single process.
      }

      public static class PrivilegeAttribute {
        public const int Enabled = 2;
      }

      public static class SystemErrorCode {
        public const int ErrorAccessDenied = 5; // (ERROR_ACCESS_DENIED) Access is denied
        public const int ErrorSuccess = 0; // (ERROR_SUCCESS) The operation completed successfully
      }

      public static class SystemInformationClass {
        public const int SystemCombinePhysicalMemoryInformation = 130; // 0x82
        public const int SystemFileCacheInformation = 21; // 0x15
        public const int SystemMemoryListInformation = 80; // 0x50
      }

      public static class SystemMemoryListCommand {
        public const int MemoryFlushModifiedList = 3;
        public const int MemoryPurgeLowPriorityStandbyList = 5;
        public const int MemoryPurgeStandbyList = 4;
      }

      public static class Drive {
        public const int FsctlDiscardVolumeCache = 589828; // 0x00090054 - FSCTL_DISCARD_VOLUME_CACHE
        public const int IoControlResetWriteOrder = 589832; // 0x000900F8 - FSCTL_RESET_WRITE_ORDER
      }

      public static class File {
        public const int FlagsNoBuffering = 536870912; // 0x20000000 - FILE_FLAG_NO_BUFFERING
      }
    }
  }
}