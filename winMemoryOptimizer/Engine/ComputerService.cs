using sergiye.Common;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace winMemoryOptimizer {
  
  internal class ComputerService {

    public static bool HasCombinedPageList => OperatingSystemHelper.IsWindows8OrGreater;
    public static bool HasModifiedFileCache => OperatingSystemHelper.IsWindowsXpOrGreater;
    public static bool HasModifiedPageList => OperatingSystemHelper.IsWindowsVistaOrGreater;
    public static bool HasProcessesWorkingSet => OperatingSystemHelper.IsWindowsXpOrGreater;
    public static bool HasStandbyList => OperatingSystemHelper.IsWindowsVistaOrGreater;
    public static bool HasSystemWorkingSet => OperatingSystemHelper.IsWindowsXpOrGreater;

    private WindowsStructs.MemoryStatusEx memoryStatusEx;

    public ComputerService() {
      memoryStatusEx = new WindowsStructs.MemoryStatusEx();
      UpdateMemoryState();
    }

    public Memory Memory { get; } = new Memory();

    public bool UpdateMemoryState() {
      try {
        if (NativeMethods.GlobalMemoryStatusEx(ref memoryStatusEx)) {
          Memory.Update(memoryStatusEx);
          return true;
        }
        else
          Logger.Error(new Win32Exception(Marshal.GetLastWin32Error()));
      }
      catch (Exception e) {
        Logger.Error(e.Message);
      }
      return false;
    }
    
    public event Action<byte, string> OnOptimizeProgressUpdate;

    private static bool SetIncreasePrivilege(string privilegeName) {
      var result = false;

      using (var current = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges)) {
        WindowsStructs.TokenPrivileges newState;
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

    public void Optimize(Enums.MemoryAreas areas, Enums.OptimizationReason reason) {
      if (areas == Enums.MemoryAreas.None)
        return;

      var errorLog = new StringBuilder();
      const string errorLogFormat = "{0} ({1}: {2})";
      var infoLog = new StringBuilder();
      infoLog.AppendLine($"Optimization start reason: {reason}");
      const string infoLogFormat = "{0} ({1}) ({2:0.0} {3})";
      var runtime = TimeSpan.Zero;
      var stopwatch = new Stopwatch();
      var value = (byte) 0;

      // Optimize Processes Working Set
      if ((areas & Enums.MemoryAreas.ProcessesWorkingSet) != 0) {
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

      if ((areas & Enums.MemoryAreas.SystemWorkingSet) != 0) {
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

      if ((areas & Enums.MemoryAreas.ModifiedPageList) != 0) {
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

      if ((areas & (Enums.MemoryAreas.StandbyList | Enums.MemoryAreas.StandbyListLowPriority)) != 0) {
        var lowPriority = (areas & Enums.MemoryAreas.StandbyListLowPriority) != 0;

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

      if ((areas & Enums.MemoryAreas.CombinedPageList) != 0) {
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

      // Optimize Modified File Cache
      if ((areas & Enums.MemoryAreas.ModifiedFileCache) != 0) {
        try {
          if (OnOptimizeProgressUpdate != null) {
            value++;
            OnOptimizeProgressUpdate(value, "Modified file cache");
          }

          stopwatch.Restart();

          OptimizeModifiedFileCache();
          
          infoLog.AppendLine(string.Format(infoLogFormat, "Modified file cache", "Optimized",
            stopwatch.Elapsed.TotalSeconds, "seconds"));
          runtime = runtime.Add(stopwatch.Elapsed);
        }
        catch (Exception e) {
          errorLog.AppendLine(string.Format(errorLogFormat, "Modified file cache", "Error", e.GetMessage()));
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

    private static void OptimizeCombinedPageList() {
      if (!HasCombinedPageList)
        throw new Exception("The Combined Page List optimization is not supported on this operating system version");

      if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
        throw new Exception(string.Format("This operation requires administrator privileges ({0})",
          Constants.Windows.Privilege.SeProfSingleProcessName));

      var handle = GCHandle.Alloc(0);
      try {
        var memoryCombineInformationEx = new WindowsStructs.MemoryCombineInformationEx();
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

    private void OptimizeModifiedFileCache() {

      if (!HasModifiedFileCache)
        throw new Exception("The Modified File Cache optimization is not supported on this version of the operating system");

      foreach (var drive in DriveInfo.GetDrives()) {
        if (drive == null || drive.DriveType != DriveType.Fixed || string.IsNullOrWhiteSpace(drive.Name))
          continue;

        using (var handle = OpenVolumeHandle(drive.Name)) {
          if (handle == null || handle.IsInvalid)
            continue;

          if (OperatingSystemHelper.IsWindows7OrGreater) {
            try {
              var buffer = Marshal.AllocHGlobal(1);
              try {
                if (!NativeMethods.DeviceIoControl(handle, Constants.Windows.Drive.IoControlResetWriteOrder,
                      buffer, 1, IntPtr.Zero, 0, out _, IntPtr.Zero))
                  throw new Win32Exception(Marshal.GetLastWin32Error());
              }
              finally {
                Marshal.FreeHGlobal(buffer);
              }
            }
            catch {
              // ignored
            }

            if (OperatingSystemHelper.IsWindows8OrGreater) {
              try {
                if (!NativeMethods.DeviceIoControl(handle, Constants.Windows.Drive.FsctlDiscardVolumeCache, 
                      IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                  throw new Win32Exception(Marshal.GetLastWin32Error());
              }
              catch {
                // ignored
              }
            }
          }

          if (!NativeMethods.FlushFileBuffers(handle))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
      }
    }

    private static void OptimizeModifiedPageList() {
      if (!HasModifiedPageList)
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

    private static void OptimizeProcessesWorkingSet() {
      if (!HasProcessesWorkingSet)
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

    private static void OptimizeStandbyList(bool lowPriority = false) {
      if (!HasStandbyList)
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

    private static void OptimizeSystemWorkingSet() {
      if (!HasSystemWorkingSet)
        throw new Exception("The System Working Set optimization is not supported on this operating system version");

      if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeIncreaseQuotaName))
        throw new Exception($"This operation requires administrator privileges ({Constants.Windows.Privilege.SeIncreaseQuotaName})");

      var handle = GCHandle.Alloc(0);
      try {
        object systemCacheInformation;
        if (OperatingSystemHelper.Is64Bit)
          systemCacheInformation = new WindowsStructs.SystemCacheInformation64
            {MinimumWorkingSet = -1L, MaximumWorkingSet = -1L};
        else
          systemCacheInformation = new WindowsStructs.SystemCacheInformation32
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

    private static SafeFileHandle OpenVolumeHandle(string driveLetter) {
      if (string.IsNullOrWhiteSpace(driveLetter))
        return null;
      return NativeMethods.CreateFile(
        @"\\.\" + driveLetter.TrimEnd(':', '\\') + ":",
        FileAccess.ReadWrite,
        FileShare.Read | FileShare.Write,
        IntPtr.Zero,
        FileMode.Open,
        (int) FileAttributes.Normal | Constants.Windows.File.FlagsNoBuffering,
        IntPtr.Zero
      );
    }
  }
}