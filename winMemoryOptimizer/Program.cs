using System;
using System.Windows.Forms;
using sergiye.Common;

namespace winMemoryOptimizer {

  internal class Program {

    public static void Main(string[] args) {

      Crasher.Listen();

      if (!OSHelper.IsCompatible(true, out var errorMessage, out var fixAction)) {
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

      var applicationContext = new TrayApplicationContext();
      Application.Run(applicationContext);
    }
  }
}
