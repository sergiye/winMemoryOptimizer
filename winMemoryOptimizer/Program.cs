using System;
using System.Windows.Forms;
using sergiye.Common;

namespace winMemoryOptimizer {
  
  internal class Program {
    
    public static void Main(string[] args) {
      
      Crasher.Listen();

      if (!OperatingSystemHelper.IsCompatible(true, out var errorMessage, out var fixAction)) {
        if (fixAction != null) {
          if (MessageBox.Show(errorMessage, Updater.ApplicationName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
            fixAction.Invoke();
          }
        }
        else {
          MessageBox.Show(errorMessage, Updater.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Environment.Exit(0);
      }

      if (WinApiHelper.CheckRunningInstances(true, false)) {
        MessageBox.Show($"{Updater.ApplicationName} is already running.", Updater.ApplicationName,
          MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        return;
      }

      // if (Environment.Is64BitOperatingSystem != Environment.Is64BitProcess) {
      //   if (MessageBox.Show("You are running an application build made for a different OS architecture.\nIt is not compatible!\nWould you like to download correct version?", Updater.ApplicationName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
      //     Updater.VisitAppSite("releases");
      //   }
      //   Environment.Exit(0);
      // }

      var applicationContext = new TrayApplicationContext();
      Application.Run(applicationContext);
    }
  }
}