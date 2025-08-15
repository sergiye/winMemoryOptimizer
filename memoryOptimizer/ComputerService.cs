using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using memoryOptimizer.Model.Memory;
using OperatingSystem = memoryOptimizer.Model.OperatingSystem;

namespace memoryOptimizer.Service {
  
  public class ComputerService {
    
    private Memory memory = new Memory(new Structs.Windows.MemoryStatusEx());
    private OperatingSystem operatingSystem;

    public Memory Memory {
      get {
        try {
          var memoryStatusEx = new Structs.Windows.MemoryStatusEx();

          if (!NativeMethods.GlobalMemoryStatusEx(memoryStatusEx))
            Logger.Error(new Win32Exception(Marshal.GetLastWin32Error()));

          memory = new Memory(memoryStatusEx);
        }
        catch (Exception e) {
          Logger.Error(e.Message);
        }

        return memory;
      }
    }

    public OperatingSystem OperatingSystem =>
      operatingSystem ??= new OperatingSystem {
        Is64Bit = Environment.Is64BitOperatingSystem,
        IsWindows8OrGreater = Environment.OSVersion.Version.Major >= 6.2,
        IsWindowsVistaOrGreater = Environment.OSVersion.Version.Major >= 6,
        IsWindowsXpOrGreater = Environment.OSVersion.Version.Major >= 5.1
      };

    public event Action<byte, string> OnOptimizeProgressUpdate;

    private static bool SetIncreasePrivilege(string privilegeName) {
      var result = false;

      using (var current = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges)) {
        Structs.Windows.TokenPrivileges newState;
        newState.Count = 1;
        newState.Luid = 0L;
        newState.Attr = Constants.Windows.PrivilegeAttribute.Enabled;

        if (NativeMethods.LookupPrivilegeValue(null, privilegeName, ref newState.Luid)) {
          result = NativeMethods.AdjustTokenPrivileges(current.Token, false, ref newState, 0, IntPtr.Zero,
            IntPtr.Zero);
        }

        if (!result)
          Logger.Error(new Win32Exception(Marshal.GetLastWin32Error()));
      }

      return result;
    }

    public void Optimize(Enums.Memory.Areas areas) {
      if (areas == Enums.Memory.Areas.None)
        return;

      var errorLog = new StringBuilder();
      const string errorLogFormat = "{0} ({1}: {2})";
      var infoLog = new StringBuilder();
      const string infoLogFormat = "{0} ({1}) ({2:0.0} {3})";
      var runtime = TimeSpan.Zero;
      var stopwatch = new Stopwatch();
      var value = (byte) 0;

      // Optimize Processes Working Set
      if ((areas & Enums.Memory.Areas.ProcessesWorkingSet) != 0) {
        try {
          if (OnOptimizeProgressUpdate != null) {
            value++;
            OnOptimizeProgressUpdate(value, "Processes Working Set");
          }

          stopwatch.Restart();

          OptimizeProcessesWorkingSet();

          runtime = runtime.Add(stopwatch.Elapsed);

          infoLog.AppendLine(string.Format(infoLogFormat, "Processes Working Set", "Optimized",
            stopwatch.Elapsed.TotalSeconds, "seconds"));
        }
        catch (Exception e) {
          errorLog.AppendLine(string.Format(errorLogFormat, "Processes Working Set", "Error",
            e.GetMessage()));
        }
      }

      if ((areas & Enums.Memory.Areas.SystemWorkingSet) != 0) {
        try {
          if (OnOptimizeProgressUpdate != null) {
            value++;
            OnOptimizeProgressUpdate(value, "System Working Set");
          }

          stopwatch.Restart();

          OptimizeSystemWorkingSet();

          runtime = runtime.Add(stopwatch.Elapsed);

          infoLog.AppendLine(string.Format(infoLogFormat, "System Working Set", "Optimized",
            stopwatch.Elapsed.TotalSeconds, "seconds"));
        }
        catch (Exception e) {
          errorLog.AppendLine(string.Format(errorLogFormat, "System Working Set", "Error", e.GetMessage()));
        }
      }

      if ((areas & Enums.Memory.Areas.ModifiedPageList) != 0) {
        try {
          if (OnOptimizeProgressUpdate != null) {
            value++;
            OnOptimizeProgressUpdate(value, "Modified Page List");
          }

          stopwatch.Restart();

          OptimizeModifiedPageList();

          runtime = runtime.Add(stopwatch.Elapsed);

          infoLog.AppendLine(string.Format(infoLogFormat, "Modified Page List", "Optimized",
            stopwatch.Elapsed.TotalSeconds, "seconds"));
        }
        catch (Exception e) {
          errorLog.AppendLine(string.Format(errorLogFormat, "Modified Page List", "Error", e.GetMessage()));
        }
      }

      if ((areas & (Enums.Memory.Areas.StandbyList | Enums.Memory.Areas.StandbyListLowPriority)) != 0) {
        var lowPriority = (areas & Enums.Memory.Areas.StandbyListLowPriority) != 0;

        try {
          if (OnOptimizeProgressUpdate != null) {
            value++;
            OnOptimizeProgressUpdate(value, lowPriority ? "Standby List (Low Priority)" : "Standby List");
          }

          stopwatch.Restart();

          OptimizeStandbyList(lowPriority);

          runtime = runtime.Add(stopwatch.Elapsed);

          infoLog.AppendLine(string.Format(infoLogFormat,
            lowPriority ? "Standby List (Low Priority)" : "Standby List", "Optimized",
            stopwatch.Elapsed.TotalSeconds, "seconds"));
        }
        catch (Exception e) {
          errorLog.AppendLine(string.Format(errorLogFormat,
            lowPriority ? "Standby List (Low Priority)" : "Standby List", "Error", e.GetMessage()));
        }
      }

      if ((areas & Enums.Memory.Areas.CombinedPageList) != 0) {
        try {
          if (OnOptimizeProgressUpdate != null) {
            value++;
            OnOptimizeProgressUpdate(value, "Combined Page List");
          }

          stopwatch.Restart();

          OptimizeCombinedPageList();

          runtime = runtime.Add(stopwatch.Elapsed);

          infoLog.AppendLine(string.Format(infoLogFormat, "Combined Page List", "Optimized",
            stopwatch.Elapsed.TotalSeconds, "seconds"));
        }
        catch (Exception e) {
          errorLog.AppendLine(string.Format(errorLogFormat, "Combined Page List", "Error", e.GetMessage()));
        }
      }

      if (infoLog.Length > 0) {
        infoLog.Insert(0,
          $"{"Memory areas".ToUpper()} ({runtime.TotalSeconds:0.0} seconds){Environment.NewLine}{Environment.NewLine}");

        Logger.Information(infoLog.ToString());

        infoLog.Clear();
      }

      if (errorLog.Length > 0) {
        errorLog.Insert(0, $"{"Memory areas".ToUpper()}{Environment.NewLine}{Environment.NewLine}");
        Logger.Error(errorLog.ToString());
        errorLog.Clear();
      }

      try {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
      }
      catch {
        // ignored
      }
      finally {
        if (OnOptimizeProgressUpdate != null) {
          value++;
          OnOptimizeProgressUpdate(value, "Optimized");
        }
      }
    }

    private void OptimizeCombinedPageList() {
      if (!OperatingSystem.HasCombinedPageList)
        throw new Exception("The Combined Page List optimization is not supported on this operating system version");

      if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
        throw new Exception(string.Format("This operation requires administrator privileges ({0})",
          Constants.Windows.Privilege.SeProfSingleProcessName));

      var handle = GCHandle.Alloc(0);
      try {
        var memoryCombineInformationEx = new Structs.Windows.MemoryCombineInformationEx();
        handle = GCHandle.Alloc(memoryCombineInformationEx, GCHandleType.Pinned);
        var length = Marshal.SizeOf(memoryCombineInformationEx);
        if (NativeMethods.NtSetSystemInformation(
              Constants.Windows.SystemInformationClass.SystemCombinePhysicalMemoryInformation,
              handle.AddrOfPinnedObject(), length) != Constants.Windows.SystemErrorCode.ErrorSuccess)
          throw new Win32Exception(Marshal.GetLastWin32Error());
      }
      finally {
        try {
          if (handle.IsAllocated)
            handle.Free();
        }
        catch (InvalidOperationException) {
          // ignored
        }
      }
    }

    private void OptimizeModifiedPageList() {
      if (!OperatingSystem.HasModifiedPageList)
        throw new Exception("The Modified Page List optimization is not supported on this operating system version");

      if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
        throw new Exception($"This operation requires administrator privileges ({Constants.Windows.Privilege.SeProfSingleProcessName})");

      var handle = GCHandle.Alloc(Constants.Windows.SystemMemoryListCommand.MemoryFlushModifiedList,
        GCHandleType.Pinned);
      try {
        if (NativeMethods.NtSetSystemInformation(
              Constants.Windows.SystemInformationClass.SystemMemoryListInformation,
              handle.AddrOfPinnedObject(),
              Marshal.SizeOf(Constants.Windows.SystemMemoryListCommand.MemoryFlushModifiedList)) !=
            Constants.Windows.SystemErrorCode.ErrorSuccess)
          throw new Win32Exception(Marshal.GetLastWin32Error());
      }
      finally {
        try {
          if (handle.IsAllocated)
            handle.Free();
        }
        catch (InvalidOperationException) {
          // ignored
        }
      }
    }

    private void OptimizeProcessesWorkingSet() {
      if (!OperatingSystem.HasProcessesWorkingSet)
        throw new Exception("The Processes Working Set optimization is not supported on this operating system version");

      if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeDebugName))
        throw new Exception($"This operation requires administrator privileges ({Constants.Windows.Privilege.SeDebugName})");

      var errors = new StringBuilder();
      var processes = Process.GetProcesses().Where(process => process != null && !Settings.ProcessExclusionList.Contains(process.ProcessName));
      foreach (var process in processes) {
        using (process) {
          try {
            if (!NativeMethods.EmptyWorkingSet(process.Handle))
              throw new Win32Exception(Marshal.GetLastWin32Error());
          }
          catch (InvalidOperationException) {
            // ignored
          }
          catch (Win32Exception e) {
            if (e.NativeErrorCode != Constants.Windows.SystemErrorCode.ErrorAccessDenied)
              errors.Append($"{process.ProcessName}: {e.GetMessage()} | ");
          }
        }
      }

      if (errors.Length > 3) {
        errors.Remove(errors.Length - 3, 3);
        throw new Exception(errors.ToString());
      }
    }

    private void OptimizeStandbyList(bool lowPriority = false) {
      if (!OperatingSystem.HasStandbyList)
        throw new Exception("The Standby List optimization is not supported on this operating system version");

      if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
        throw new Exception($"This operation requires administrator privileges ({Constants.Windows.Privilege.SeProfSingleProcessName})");

      object memoryPurgeStandbyList = lowPriority
        ? Constants.Windows.SystemMemoryListCommand.MemoryPurgeLowPriorityStandbyList
        : Constants.Windows.SystemMemoryListCommand.MemoryPurgeStandbyList;
      var handle = GCHandle.Alloc(memoryPurgeStandbyList, GCHandleType.Pinned);

      try {
        if (NativeMethods.NtSetSystemInformation(
              Constants.Windows.SystemInformationClass.SystemMemoryListInformation,
              handle.AddrOfPinnedObject(), Marshal.SizeOf(memoryPurgeStandbyList)) !=
            Constants.Windows.SystemErrorCode.ErrorSuccess)
          throw new Win32Exception(Marshal.GetLastWin32Error());
      }
      finally {
        try {
          if (handle.IsAllocated)
            handle.Free();
        }
        catch (InvalidOperationException) {
          // ignored
        }
      }
    }

    private void OptimizeSystemWorkingSet() {
      if (!OperatingSystem.HasSystemWorkingSet)
        throw new Exception("The System Working Set optimization is not supported on this operating system version");

      if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeIncreaseQuotaName))
        throw new Exception($"This operation requires administrator privileges ({Constants.Windows.Privilege.SeIncreaseQuotaName})");

      var handle = GCHandle.Alloc(0);
      try {
        object systemCacheInformation;

        if (OperatingSystem.Is64Bit)
          systemCacheInformation = new Structs.Windows.SystemCacheInformation64
            {MinimumWorkingSet = -1L, MaximumWorkingSet = -1L};
        else
          systemCacheInformation = new Structs.Windows.SystemCacheInformation32
            {MinimumWorkingSet = uint.MaxValue, MaximumWorkingSet = uint.MaxValue};

        handle = GCHandle.Alloc(systemCacheInformation, GCHandleType.Pinned);

        var length = Marshal.SizeOf(systemCacheInformation);

        if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemFileCacheInformation,
              handle.AddrOfPinnedObject(), length) != Constants.Windows.SystemErrorCode.ErrorSuccess)
          throw new Win32Exception(Marshal.GetLastWin32Error());
      }
      finally {
        try {
          if (handle.IsAllocated)
            handle.Free();
        }
        catch (InvalidOperationException) {
          // ignored
        }
      }

      var fileCacheSize = IntPtr.Subtract(IntPtr.Zero, 1); // Flush
      if (!NativeMethods.SetSystemFileCacheSize(fileCacheSize, fileCacheSize, 0))
        throw new Win32Exception(Marshal.GetLastWin32Error());
    }
  }
}