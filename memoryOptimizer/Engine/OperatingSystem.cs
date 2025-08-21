using System;

namespace memoryOptimizer {

  internal static class OperatingSystem {

    public static bool HasCombinedPageList => IsWindows8OrGreater;
    public static bool HasModifiedPageList => IsWindowsVistaOrGreater;
    public static bool HasProcessesWorkingSet => IsWindowsXpOrGreater;
    public static bool HasStandbyList => IsWindowsVistaOrGreater;
    public static bool HasSystemWorkingSet => IsWindowsXpOrGreater;
    public static bool Is64Bit { get; } = Environment.Is64BitOperatingSystem;
    public static bool IsWindows8OrGreater { get; } = Environment.OSVersion.Version.Major >= 6.2;
    public static bool IsWindowsVistaOrGreater { get; } = Environment.OSVersion.Version.Major >= 6;
    public static bool IsWindowsXpOrGreater { get; } = Environment.OSVersion.Version.Major >= 5.1;
  }
}