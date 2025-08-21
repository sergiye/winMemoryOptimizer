using System;
using System.Runtime.InteropServices;

namespace memoryOptimizer {

  internal static class WindowsStructs {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MemoryCombineInformationEx {
      public IntPtr Handle;
      public IntPtr PagesCombined;
      public ulong Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MemoryStatusEx {
      public readonly uint Length; // The size of the structure, in bytes.
      public uint MemoryLoad; // A number between 0 and 100 that specifies the approximate percentage of physical memory that is in use.
      public ulong TotalPhys; // The amount of actual physical memory, in bytes.
      public ulong AvailPhys; // The amount of physical memory currently available, in bytes.
      public ulong TotalPageFile; // The current committed memory limit for the system or the current process, whichever is smaller, in bytes.
      public ulong AvailPageFile; // The maximum amount of memory the current process can commit, in bytes.
      public ulong TotalVirtual; // The size of the user-mode portion of the virtual address space of the calling process, in bytes.
      public ulong AvailVirtual; // The amount of unreserved and uncommitted memory currently in the user-mode portion of the virtual address space of the calling process, in bytes.
      public ulong AvailExtendedVirtual; // Reserved. This value is always 0.

      public MemoryStatusEx() {
        Length = (uint) Marshal.SizeOf(typeof(MemoryStatusEx));
        MemoryLoad = 0;
        TotalPhys = 0;
        AvailPhys = 0;
        TotalPageFile = 0;
        AvailPageFile = 0;
        TotalVirtual = 0;
        AvailVirtual = 0;
        AvailExtendedVirtual = 0;
      }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SystemCacheInformation32 {
      public uint CurrentSize;
      public uint PeakSize;
      public uint PageFaultCount;
      public uint MinimumWorkingSet;
      public uint MaximumWorkingSet;
      public uint Unused1;
      public uint Unused2;
      public uint Unused3;
      public uint Unused4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SystemCacheInformation64 {
      public long CurrentSize;
      public long PeakSize;
      public long PageFaultCount;
      public long MinimumWorkingSet;
      public long MaximumWorkingSet;
      public long Unused1;
      public long Unused2;
      public long Unused3;
      public long Unused4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TokenPrivileges {
      public int Count;
      public long Luid;
      public int Attr;
    }
  }
}
