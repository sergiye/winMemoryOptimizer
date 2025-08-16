namespace memoryOptimizer {

  internal class OperatingSystem {

    public bool HasCombinedPageList => IsWindows8OrGreater;
    public bool HasModifiedPageList => IsWindowsVistaOrGreater;
    public bool HasProcessesWorkingSet => IsWindowsXpOrGreater;
    public bool HasStandbyList => IsWindowsVistaOrGreater;
    public bool HasSystemWorkingSet => IsWindowsXpOrGreater;
    public bool Is64Bit { get; set; }
    public bool IsWindows8OrGreater { get; set; }
    public bool IsWindowsVistaOrGreater { get; set; }
    public bool IsWindowsXpOrGreater { get; set; }
  }
}