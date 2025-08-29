using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using sergiye.Common;

namespace TrayRAMBooster {
  
  internal class TrayApplicationContext : ApplicationContext {

    private readonly IContainer components = new Container();
    private const int AutoOptimizationMemoryUsageInterval = 5; // Minute

    private readonly Icon imageIcon;
    private readonly NotifyIcon notifyIcon;
    private readonly IconFactory iconFactory;
    private readonly SynchronizationContext uiContext;
    private readonly ComputerService computer;
    private readonly StartupManager startupManager;
    private readonly System.Windows.Forms.Timer autoUpdateTimer;
    private ToolStripLabel statusMenuLabel;
    private ToolStripMenuItem iconTypeMenu;
    private ToolStripMenuItem iconDoubleClickMenu;
    private ToolStripMenuItem autoOptimizationIntervalMenu;
    private ToolStripMenuItem autoOptimizeUsageMenu;
    private ToolStripMenuItem updateIntervalMenu;
    private ToolStripMenuItem optimizationTypesMenu;
    private bool isBusy;
    private string iconValue;
    private DateTimeOffset lastRun;
    private DateTimeOffset nextAutoOptimizationByInterval;
    private DateTimeOffset lastAutoOptimizationByInterval = DateTimeOffset.Now;
    private DateTimeOffset lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
    private byte optimizationProgressPercentage;

    public TrayApplicationContext() {
      imageIcon = Icon.ExtractAssociatedIcon(Updater.CurrentFileLocation);
      notifyIcon = new NotifyIcon(components) {
        ContextMenuStrip = new ContextMenuStrip(),
        Icon = imageIcon,
        Text = Updater.ApplicationTitle,
        Visible = true
      };
      //notifyIcon.MouseUp += (s, e) => {
      //  if (e.Button != MouseButtons.Left) return;
      //  var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
      //  mi?.Invoke(notifyIcon, null);
      //};
      notifyIcon.MouseMove += (s, e) => {
        var iconText = GetTrayIconText();
        if (notifyIcon.Text != iconText)
          notifyIcon.Text = iconText.Length > 63 ? iconText.Substring(0, 63) : iconText;
      };
      notifyIcon.DoubleClick += (s, e) => {
        switch (Settings.DoubleClickAction) {
          case Enums.DoubleClickAction.Optimize:
            MenuItemOptimizeClick(s, e);
            break;
          case Enums.DoubleClickAction.TaskManager:
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
            break;
          case Enums.DoubleClickAction.ResourceMonitor:
            var currentSessionID = Process.GetCurrentProcess().SessionId;
            var process = Process.GetProcessesByName("perfmon").Where(p => p.SessionId == currentSessionID).FirstOrDefault();
            if (process == null) {
              Process.Start(new ProcessStartInfo("resmon.exe") { UseShellExecute = true });
            }
            else {
              WinApiHelper.ShowWindowAsync(process.MainWindowHandle, WinApiHelper.SW_RESTORE);
              WinApiHelper.ShowWindowAsync(process.MainWindowHandle, WinApiHelper.SW_SHOWNORMAL);
              WinApiHelper.SetForegroundWindow(process.MainWindowHandle);
            }
            break;
          case Enums.DoubleClickAction.None:
          default:
            break;
        }
      };
      notifyIcon.ContextMenuStrip.Renderer = new ThemedToolStripRenderer();
      uiContext = SynchronizationContext.Current;
      iconFactory = new IconFactory(Settings.TrayIconValueColor);

      Updater.Subscribe(
        (message, isError) => {
          MessageBox.Show(message, Updater.ApplicationName, MessageBoxButtons.OK,
            isError ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        },
        message => MessageBox.Show(message, Updater.ApplicationName, MessageBoxButtons.OKCancel,
          MessageBoxIcon.Question) == DialogResult.OK,
        Application.Exit
      );

      startupManager = new StartupManager();
      computer = new ComputerService();
      computer.OnOptimizeProgressUpdate += OnOptimizeProgressUpdate;

      autoUpdateTimer = new System.Windows.Forms.Timer(components);
      autoUpdateTimer.Tick += async (_, _) => {
        await Updater.CheckForUpdatesAsync(Updater.CheckUpdatesMode.AutoUpdate);
      };
      autoUpdateTimer.Interval = 6 * 60 * 60 * 1000; //every 6 hours
      autoUpdateTimer.Enabled = Settings.AutoUpdateApp;

      AddMenuItems();
      Theme.SetAutoTheme();

      Task.Factory.StartNew(MonitorComputer, TaskCreationOptions.LongRunning);
    }

    public bool IsBusy {
      get => isBusy;
      set {
        //Mouse.OverrideCursor = value ? Cursors.Wait : null;
        if (notifyIcon?.ContextMenuStrip != null)
          ExecuteInUiThread(() => notifyIcon.ContextMenuStrip.Enabled = !value);
        isBusy = value;
      }
    }

    private void ExecuteInUiThread(Action action) {
      if (uiContext != null)
        uiContext.Post(_ => action?.Invoke(), null);
      else
        action?.Invoke();
    }
    
    private string GetTrayIconText() {
      return Settings.TrayIconMode switch {
        Enums.TrayIconMode.MemoryAvailable => !Settings.ShowVirtualMemory
                    ? $"Memory available:\nPhysical: {computer.Memory.Physical.Free}"
                    : $"Memory available:\nPhysical: {computer.Memory.Physical.Free}\nVirtual: {computer.Memory.Virtual.Free}",
        _ => !Settings.ShowVirtualMemory
                    ? $"Memory used:\nPhysical: {computer.Memory.Physical.Used}"
                    : $"Memory used:\nPhysical: {computer.Memory.Physical.Used}\nVirtual: {computer.Memory.Virtual.Used}",
      };
    }

    private void Update(bool fetchMemoryState) {

      if (fetchMemoryState && !computer.UpdateMemoryState()) return;

      UpdateIcon(fetchMemoryState);

      if (statusMenuLabel.Visible)
        ExecuteInUiThread(() => { UpdateStatusMenuItem(false); });
    }

    private void SafeSetTrayIcon(Icon icon) {
      var prevIcon = notifyIcon.Icon;
      notifyIcon.Icon = icon;
      if (imageIcon != prevIcon)
        prevIcon?.Destroy();
    }

    private void UpdateIcon(bool force = false) {

      string newIconValue = Settings.TrayIconMode switch {
        Enums.TrayIconMode.MemoryAvailable => computer.Memory.Physical.Free.Value.ToTrayValue(),
        Enums.TrayIconMode.MemoryUsageValue => computer.Memory.Physical.Used.Value.ToTrayValue(),
        _ => $"{computer.Memory.Physical.Used.Percentage:0}",
      };
      if (!force && string.Equals(iconValue, newIconValue, StringComparison.OrdinalIgnoreCase))
        return;
      iconValue = newIconValue;
      try {
        if (optimizationProgressPercentage > 0) {
          SafeSetTrayIcon(iconFactory.CreatePercentagePieIcon(optimizationProgressPercentage));
        }
        else {
          switch (Settings.TrayIconMode) {
            case Enums.TrayIconMode.Image:
              SafeSetTrayIcon(imageIcon);
              break;
            case Enums.TrayIconMode.MemoryUsageBar:
              SafeSetTrayIcon(iconFactory.CreatePercentageIcon(computer.Memory.Physical.Used.Percentage));
              break;
            case Enums.TrayIconMode.MemoryUsagePie:
              SafeSetTrayIcon(iconFactory.CreatePercentagePieIcon((byte)computer.Memory.Physical.Used.Percentage));
              break;
            case Enums.TrayIconMode.MemoryUsagePercent:
            case Enums.TrayIconMode.MemoryUsageValue:
            case Enums.TrayIconMode.MemoryAvailable:
              SafeSetTrayIcon(iconFactory.CreateTransparentIcon(iconValue));
              break;
          }
        }
      }
      catch {
        SafeSetTrayIcon(imageIcon);
      }
    }

    private void OnOptimizeProgressUpdate(byte value, string step) {
      var stepsCount = GetEnabledMemoryAreasCount();
      if (value > stepsCount)
        optimizationProgressPercentage = 0;
      else
        optimizationProgressPercentage = (byte) (value * 100 / stepsCount);
      UpdateIcon(true);
    }

    private static Enums.MemoryAreas GetEnabledMemoryAreas() {
      if (!ComputerService.HasCombinedPageList)
        Settings.MemoryAreas &= ~Enums.MemoryAreas.CombinedPageList;
      if (!ComputerService.HasModifiedPageList)
        Settings.MemoryAreas &= ~Enums.MemoryAreas.ModifiedPageList;
      if (!ComputerService.HasProcessesWorkingSet)
        Settings.MemoryAreas &= ~Enums.MemoryAreas.ProcessesWorkingSet;
      if (!ComputerService.HasStandbyList) {
        Settings.MemoryAreas &= ~Enums.MemoryAreas.StandbyList;
        Settings.MemoryAreas &= ~Enums.MemoryAreas.StandbyListLowPriority;
      }
      if (!ComputerService.HasSystemWorkingSet)
        Settings.MemoryAreas &= ~Enums.MemoryAreas.SystemWorkingSet;
      return Settings.MemoryAreas;
    }

    private static int GetEnabledMemoryAreasCount() {
      var result = 0;
      if (Settings.MemoryAreas.HasFlag(Enums.MemoryAreas.CombinedPageList))
        result++;
      if (Settings.MemoryAreas.HasFlag(Enums.MemoryAreas.ModifiedPageList))
        result++;
      if (Settings.MemoryAreas.HasFlag(Enums.MemoryAreas.ProcessesWorkingSet))
        result++;
      if (Settings.MemoryAreas.HasFlag(Enums.MemoryAreas.StandbyList) ||
        Settings.MemoryAreas.HasFlag(Enums.MemoryAreas.StandbyListLowPriority))
        result++;
      if (Settings.MemoryAreas.HasFlag(Enums.MemoryAreas.SystemWorkingSet))
        result++;
      return result;
    }

    private void ToggleMemoryArea(Enums.MemoryAreas value) {
      try {
        IsBusy = true;
        if (Settings.MemoryAreas.HasFlag(value))
          Settings.MemoryAreas &= ~value;
        else
          Settings.MemoryAreas |= value;
        switch (value) {
          case Enums.MemoryAreas.StandbyList:
            if (Settings.MemoryAreas.HasFlag(Enums.MemoryAreas.StandbyListLowPriority))
              Settings.MemoryAreas &= ~Enums.MemoryAreas.StandbyListLowPriority;
            break;

          case Enums.MemoryAreas.StandbyListLowPriority:
            if (Settings.MemoryAreas.HasFlag(Enums.MemoryAreas.StandbyList))
              Settings.MemoryAreas &= ~Enums.MemoryAreas.StandbyList;
            break;
        }
      }
      finally {
        IsBusy = false;
        UpdateAreasMenuItems();
      }
    }

    private void UpdateAreasMenuItems() {
      foreach (var subItem in optimizationTypesMenu.DropDownItems) {
        if (subItem is ToolStripMenuItem subMenuItem)
          subMenuItem.Checked = subMenuItem.Tag is Enums.MemoryAreas area && Settings.MemoryAreas.HasFlag(area);
      }
    }

    private static void SetPriority(Enums.Priority priority) {
      bool priorityBoostEnabled;
      ProcessPriorityClass processPriorityClass;
      ThreadPriority threadPriority;
      ThreadPriorityLevel threadPriorityLevel;

      switch (priority) {
        case Enums.Priority.Low:
          priorityBoostEnabled = false;
          processPriorityClass = ProcessPriorityClass.Idle;
          threadPriority = ThreadPriority.Lowest;
          threadPriorityLevel = ThreadPriorityLevel.Idle;
          break;
        case Enums.Priority.Normal:
          priorityBoostEnabled = true;
          processPriorityClass = ProcessPriorityClass.Normal;
          threadPriority = ThreadPriority.Normal;
          threadPriorityLevel = ThreadPriorityLevel.Normal;
          break;
        case Enums.Priority.High:
          priorityBoostEnabled = true;
          processPriorityClass = ProcessPriorityClass.High;
          threadPriority = ThreadPriority.Highest;
          threadPriorityLevel = ThreadPriorityLevel.Highest;
          break;
        default:
          throw new NotImplementedException();
      }

      try {
        Thread.CurrentThread.Priority = threadPriority;
      }
      catch {
        // ignored
      }

      try {
        var process = Process.GetCurrentProcess();
        try {
          process.PriorityBoostEnabled = priorityBoostEnabled;
        }
        catch {
          // ignored
        }

        try {
          process.PriorityClass = processPriorityClass;
        }
        catch {
          // ignored
        }

        foreach (ProcessThread thread in process.Threads) {
          try {
            thread.PriorityBoostEnabled = priorityBoostEnabled;
          }
          catch {
            // ignored
          }

          try {
            thread.PriorityLevel = threadPriorityLevel;
          }
          catch {
            // ignored
          }
        }
      }
      catch {
        // ignored
      }
    }

    private async Task MonitorComputer() {
      SetPriority(Settings.RunOnPriority);
      while (true) {
        try {
          if (IsBusy) {
            await Task.Delay(1000).ConfigureAwait(false);
            continue;
          }
          if (GetEnabledMemoryAreas() != Enums.MemoryAreas.None) {
            if (nextAutoOptimizationByInterval != DateTimeOffset.MinValue && DateTimeOffset.Now >= nextAutoOptimizationByInterval) {
              Optimize(Enums.OptimizationReason.Scheduled);
              lastAutoOptimizationByInterval = DateTimeOffset.Now;
              nextAutoOptimizationByInterval = lastAutoOptimizationByInterval.AddHours(Settings.AutoOptimizationInterval);
            }
            else {
              computer.UpdateMemoryState();
              if (Settings.AutoOptimizationMemoryUsage > 0 &&
                  computer.Memory.Physical.Free.Percentage < Settings.AutoOptimizationMemoryUsage &&
                  DateTimeOffset.Now.Subtract(lastAutoOptimizationByMemoryUsage).TotalMinutes >= AutoOptimizationMemoryUsageInterval) {
                Optimize(Enums.OptimizationReason.Usage);
                lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
              }
            }
          }
          
          Update(true);

          await Task.Delay(Settings.UpdateIntervalSeconds * 1000).ConfigureAwait(false);
        }
        catch (Exception ex) {
          Logger.Debug(ex.GetMessage());
        }
      }
    }

    private void Optimize(Enums.OptimizationReason reason) {
      try {
        if (IsBusy)
          return;
        IsBusy = true;
        SetPriority(Settings.RunOnPriority);

        var tempPhysicalAvailable = computer.Memory.Physical.Free.Bytes;
        var tempVirtualAvailable = computer.Memory.Virtual.Free.Bytes;

        computer.Optimize(Settings.MemoryAreas, reason);
        lastRun = DateTimeOffset.Now;

        if (Settings.ShowOptimizationNotifications && computer.UpdateMemoryState()) {
          var physicalReleased = (computer.Memory.Physical.Free.Bytes > tempPhysicalAvailable
            ? computer.Memory.Physical.Free.Bytes - tempPhysicalAvailable
            : tempPhysicalAvailable - computer.Memory.Physical.Free.Bytes).ToMemoryUnit();
          var virtualReleased = (computer.Memory.Virtual.Free.Bytes > tempVirtualAvailable
            ? computer.Memory.Virtual.Free.Bytes - tempVirtualAvailable
            : tempVirtualAvailable - computer.Memory.Virtual.Free.Bytes).ToMemoryUnit();
          var message = $"Reason: {reason}\nPhysical: {physicalReleased.Key:0.#} {physicalReleased.Value}";
          if (Settings.ShowVirtualMemory)
            message += $"\nVirtual: {virtualReleased.Key:0.#} {virtualReleased.Value}";
          notifyIcon.ShowBalloonTip(5000, "Memory optimized", message, ToolTipIcon.Info);
        }
      }
      catch (Exception ex) {
        Logger.Error(ex);
      }
      finally {
        IsBusy = false;
      }
    }

    private void MenuItemOptimizeClick(object sender, EventArgs e) {
      if (IsBusy) return;
      Task.Run(() => {
        Optimize(Enums.OptimizationReason.Manual);
        Update(true);
      });
    } 
    
    private void AddMenuItems() {

      var menuImage = imageIcon.ToBitmap();
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Optimize now", menuImage, MenuItemOptimizeClick));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
      statusMenuLabel = new ToolStripLabel() { TextAlign = ContentAlignment.MiddleLeft };
      notifyIcon.ContextMenuStrip.Items.Add(statusMenuLabel);
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
      //auto-start
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Auto-start application", null, (sender, _) => {
        startupManager.Startup = !startupManager.Startup;
        ((ToolStripMenuItem) sender).Checked = startupManager.Startup;
      }) {
        Checked = startupManager.Startup,
      });
      //auto-optimize
      autoOptimizationIntervalMenu = new ToolStripMenuItem("Optimize every") {
        DropDownItems = {
          new ToolStripMenuItem("Never", null, (_, _) => { SetOptimizationIntervalType(0); }){Tag = 0},
          new ToolStripMenuItem("1 hour", null, (_, _) => { SetOptimizationIntervalType(1); }){Tag = 1},
          new ToolStripMenuItem("2 hours", null, (_, _) => { SetOptimizationIntervalType(2); }){Tag = 2},
          new ToolStripMenuItem("3 hours", null, (_, _) => { SetOptimizationIntervalType(3); }){Tag = 3},
          new ToolStripMenuItem("4 hours", null, (_, _) => { SetOptimizationIntervalType(4); }){Tag = 4},
          new ToolStripMenuItem("5 hours", null, (_, _) => { SetOptimizationIntervalType(5); }){Tag = 5},
          new ToolStripMenuItem("6 hours", null, (_, _) => { SetOptimizationIntervalType(6); }){Tag = 6},
          new ToolStripMenuItem("9 hours", null, (_, _) => { SetOptimizationIntervalType(9); }){Tag = 9},
          new ToolStripMenuItem("12 hours", null, (_, _) => { SetOptimizationIntervalType(12); }){Tag = 12},
          new ToolStripMenuItem("24 hours", null, (_, _) => { SetOptimizationIntervalType(24); }){Tag = 24},
        }
      };
      autoOptimizationIntervalMenu.DropDown.Closing += OnContextMenuStripClosing;
      notifyIcon.ContextMenuStrip.Items.Add(autoOptimizationIntervalMenu);
      SetOptimizationIntervalType(Settings.AutoOptimizationInterval);

      autoOptimizeUsageMenu = new ToolStripMenuItem("Optimize when free memory is below") {
        DropDownItems = {
          new ToolStripMenuItem("Never", null, (_, _) => { SetOptimizationUsage(0); }),
        }
      };
      for (var i = 10; i < 100; i+=10) {
        var percent = i;
        autoOptimizeUsageMenu.DropDownItems.Add(new ToolStripMenuItem($"{i}%", null, (_, _) => { SetOptimizationUsage(percent); }));
      }
      autoOptimizeUsageMenu.DropDown.Closing += OnContextMenuStripClosing;
      notifyIcon.ContextMenuStrip.Items.Add(autoOptimizeUsageMenu);
      SetOptimizationUsage(Settings.AutoOptimizationMemoryUsage);

      #region Optimization types
      optimizationTypesMenu = new ToolStripMenuItem("Optimization types");
      if (ComputerService.HasProcessesWorkingSet) 
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("Processes working set", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.ProcessesWorkingSet);
        }) { Tag = Enums.MemoryAreas.ProcessesWorkingSet });
      if (ComputerService.HasSystemWorkingSet)
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("System working set", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.SystemWorkingSet);
        }) { Tag = Enums.MemoryAreas.SystemWorkingSet });
      if (ComputerService.HasCombinedPageList)
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("Combined page list", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.CombinedPageList);
        }) { Tag = Enums.MemoryAreas.CombinedPageList });
      if (ComputerService.HasModifiedPageList) 
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("Modified page list", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.ModifiedPageList);
        }) { Tag = Enums.MemoryAreas.ModifiedPageList });
      if (ComputerService.HasStandbyList) {
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("Standby list", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.StandbyList);
        }) { Tag = Enums.MemoryAreas.StandbyList });
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("Standby list (low priority)", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.StandbyListLowPriority);
        }) { Tag = Enums.MemoryAreas.StandbyListLowPriority });
      }
      optimizationTypesMenu.DropDown.Closing += OnContextMenuStripClosing;
      UpdateAreasMenuItems();
      notifyIcon.ContextMenuStrip.Items.Add(optimizationTypesMenu);
      #endregion

      //settings
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Show optimization notifications", null, (sender, _) => {
        Settings.ShowOptimizationNotifications = !Settings.ShowOptimizationNotifications;
      }) {
        Checked = Settings.ShowOptimizationNotifications,
        CheckOnClick = true,
      });
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Show virtual memory", null, (sender, _) => {
        Settings.ShowVirtualMemory = !Settings.ShowVirtualMemory;
      }) {
        Checked = Settings.ShowVirtualMemory,
        CheckOnClick = true,
      });

      updateIntervalMenu = new ToolStripMenuItem("Update interval") {
        DropDownItems = {
          new ToolStripMenuItem("1 sec", null, (_, _) => { SetUpdateInterval(1); }){Tag = 1},
          new ToolStripMenuItem("2 sec", null, (_, _) => { SetUpdateInterval(2); }){Tag = 2},
          new ToolStripMenuItem("3 sec", null, (_, _) => { SetUpdateInterval(3); }){Tag = 3},
          new ToolStripMenuItem("5 sec", null, (_, _) => { SetUpdateInterval(5); }){Tag = 5},
          new ToolStripMenuItem("10 sec", null, (_, _) => { SetUpdateInterval(10); }){Tag = 10},
          new ToolStripMenuItem("30 sec", null, (_, _) => { SetUpdateInterval(30); }){Tag = 30},
          new ToolStripMenuItem("60 sec", null, (_, _) => { SetUpdateInterval(60); }){Tag = 60},
        }
      };
      updateIntervalMenu.DropDown.Closing += OnContextMenuStripClosing;
      notifyIcon.ContextMenuStrip.Items.Add(updateIntervalMenu);
      SetUpdateInterval(Settings.UpdateIntervalSeconds);

      iconTypeMenu = new ToolStripMenuItem("Icon type") {
        DropDownItems = {
          new ToolStripMenuItem("Image", null, (_, _) => { SetIconType(Enums.TrayIconMode.Image); }),
          new ToolStripMenuItem("Memory usage (Bar)", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryUsageBar); }),
          new ToolStripMenuItem("Memory usage (Pie)", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryUsagePie); }),
          new ToolStripMenuItem("Memory usage (%)", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryUsagePercent); }),
          new ToolStripMenuItem("Memory usage (Value)", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryUsageValue); }),
          new ToolStripMenuItem("Memory available", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryAvailable); }),
        }
      };
      iconTypeMenu.DropDown.Closing += OnContextMenuStripClosing;
      notifyIcon.ContextMenuStrip.Items.Add(iconTypeMenu);
      SetIconType(Settings.TrayIconMode);

      iconDoubleClickMenu = new ToolStripMenuItem("Icon double click action") {
        DropDownItems = {
          new ToolStripMenuItem("Nothing", null, (_, _) => { SetIconDoubleClickAction(Enums.DoubleClickAction.None); }),
          new ToolStripMenuItem("Optimize", null, (_, _) => { SetIconDoubleClickAction(Enums.DoubleClickAction.Optimize); }),
          new ToolStripMenuItem("Task Manager", null, (_, _) => { SetIconDoubleClickAction(Enums.DoubleClickAction.TaskManager); }),
          new ToolStripMenuItem("Resource Monitor", null, (_, _) => { SetIconDoubleClickAction(Enums.DoubleClickAction.ResourceMonitor); }),
        }
      };
      iconDoubleClickMenu.DropDown.Closing += OnContextMenuStripClosing;
      notifyIcon.ContextMenuStrip.Items.Add(iconDoubleClickMenu);
      SetIconDoubleClickAction(Settings.DoubleClickAction);

      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Icon color", null, (_, _) => {
        using (var dialog = new ColorDialog()) {
          dialog.Color = Settings.TrayIconValueColor;
          if (dialog.ShowDialog() != DialogResult.OK || Settings.TrayIconValueColor == dialog.Color) return;
          iconFactory.Color = Settings.TrayIconValueColor = dialog.Color;
          UpdateIcon(true);
        }
      }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
      //about
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Auto update app", null, (sender, _) => {
        Settings.AutoUpdateApp = !Settings.AutoUpdateApp;
        autoUpdateTimer.Enabled = Settings.AutoUpdateApp;
      }) {
        Checked = Settings.AutoUpdateApp,
        CheckOnClick = true,
      });
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Check for updates", null,
        (_, _) => { Updater.CheckForUpdates(Updater.CheckUpdatesMode.AllMessages); }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Site", null, (_, _) => { Updater.VisitAppSite(); }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("About", null, (_, _) => { Updater.ShowAbout(); }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => { ExitThread(); }));
    }

    private void SetIconType(Enums.TrayIconMode trayIconMode) {
      Settings.TrayIconMode = trayIconMode;
      Update(true);
      foreach (Enums.TrayIconMode trayType in Enum.GetValues(typeof(Enums.TrayIconMode))) {
        ((ToolStripMenuItem)iconTypeMenu.DropDownItems[(int)trayType]).Checked = Settings.TrayIconMode == trayType;
      }
    }

    private void SetIconDoubleClickAction(Enums.DoubleClickAction clickAction) {
      Settings.DoubleClickAction = clickAction;
      foreach (Enums.DoubleClickAction action in Enum.GetValues(typeof(Enums.DoubleClickAction))) {
        ((ToolStripMenuItem)iconDoubleClickMenu.DropDownItems[(int)action]).Checked = Settings.DoubleClickAction == action;
      }
    }

    private void SetOptimizationIntervalType(int interval) {
      Settings.AutoOptimizationInterval = interval;
      nextAutoOptimizationByInterval = interval == 0 ? DateTimeOffset.MinValue 
        : lastAutoOptimizationByInterval.AddHours(Settings.AutoOptimizationInterval);
      foreach (var subItem in autoOptimizationIntervalMenu.DropDownItems) {
        if (subItem is ToolStripMenuItem subMenuItem)
          subMenuItem.Checked = subMenuItem.Tag is int intTag && intTag == interval;
      }
      UpdateStatusMenuItem(true);
    }

    private void SetOptimizationUsage(int percent) {
      Settings.AutoOptimizationMemoryUsage = percent;
      for (var i = 0; i < 10; i++)
        ((ToolStripMenuItem)autoOptimizeUsageMenu.DropDownItems[i]).Checked = percent == i * 10;
    }

    private void SetUpdateInterval(int interval) {
      Settings.UpdateIntervalSeconds = interval;
      foreach (var subItem in updateIntervalMenu.DropDownItems) {
        if (subItem is ToolStripMenuItem subMenuItem)
          subMenuItem.Checked = subMenuItem.Tag is int intTag && intTag == interval;
      }
    }

    private void OnContextMenuStripClosing(object sender, ToolStripDropDownClosingEventArgs e) {
      if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) {
        e.Cancel = true;
      }
    }
    
    private void UpdateStatusMenuItem(bool force) {

      if (!force && !statusMenuLabel.Visible) return;
      string iconText = GetTrayIconText();
      if (lastRun != DateTimeOffset.MinValue) {
        iconText += $"\nLast run: {lastRun:G}";
      }
      if (nextAutoOptimizationByInterval != DateTimeOffset.MinValue) {
        iconText += $"\nNext run: {nextAutoOptimizationByInterval:G}";
      }
      if (iconText != statusMenuLabel.Text)
        statusMenuLabel.Text = iconText;
    }

    protected override void Dispose(bool disposing) {
      if (!disposing || components == null) return;
      components.Dispose();
      imageIcon?.Dispose();
      notifyIcon?.Dispose();
      iconFactory?.Dispose();
      GC.SuppressFinalize(this);
    }

    protected override void ExitThreadCore() {
      notifyIcon.Visible = false;
      Settings.Save();
      base.ExitThreadCore();
    }
  }
}