using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TrayRAMBooster {
  
  internal static class NativeMethods {
    
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AdjustTokenPrivileges(IntPtr tokenHandle,
      [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges, ref WindowsStructs.TokenPrivileges newState,
      int bufferLength, IntPtr previousState, IntPtr returnLength);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("psapi.dll", SetLastError = true)]
    internal static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref WindowsStructs.MemoryStatusEx lpBuffer);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref long lpLuid);

    [DllImport("ntdll.dll", SetLastError = true)]
    internal static extern uint NtSetSystemInformation(int infoClass, IntPtr info, int length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetSystemFileCacheSize(IntPtr minimumFileCacheSize, IntPtr maximumFileCacheSize,
      int flags);
    
    private struct APPBARDATA {
      public int cbSize;
      public IntPtr hWnd;
      public int uCallbackMessage;
      public int uEdge;
      public RECT rc;
      public IntPtr lParam;
    }

    private struct RECT {
      public int left, top, right, bottom;
    }

    private const int ABM_GETTASKBARPOS = 5;

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(int msg, ref APPBARDATA data);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
    private static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc,
      int ySrc, int dwRop);

    private static Rectangle GetTaskbarPosition() {
      var data = new APPBARDATA();
      data.cbSize = Marshal.SizeOf(data);

      var retval = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
      if (retval == IntPtr.Zero) {
        throw new Win32Exception("Please re-install Windows");
      }

      return new Rectangle(data.rc.left, data.rc.top, data.rc.right - data.rc.left, data.rc.bottom - data.rc.top);
    }

    private static Color GetColorAt(Point location) {
      using (var screenPixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
      using (var gDst = Graphics.FromImage(screenPixel)) {
        using (var gSrc = Graphics.FromHwnd(IntPtr.Zero)) {
          var hSrcDC = gSrc.GetHdc();
          var hDC = gDst.GetHdc();
          var retVal = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, location.X, location.Y, (int) CopyPixelOperation.SourceCopy);
          gDst.ReleaseHdc();
          gSrc.ReleaseHdc();
        }

        return screenPixel.GetPixel(0, 0);
      }
    }

    public static Color GetTaskbarColor() {
      return GetColorAt(GetTaskbarPosition().Location);
    }
  }
}