using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
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
    private readonly SynchronizationContext uiContext;
    private readonly ComputerService computer;
    private readonly StartupManager startupManager;
    private ToolStripLabel statusMenuLabel;
    private ToolStripMenuItem iconTypeMenu;
    private ToolStripMenuItem iconDoubleClickMenu;
    private ToolStripMenuItem autoOptimizationIntervalMenu;
    private ToolStripMenuItem autoOptimizeUsageMenu;
    private ToolStripMenuItem updateIntervalMenu;
    private ToolStripMenuItem optimizationTypesMenu;
    private readonly Bitmap bitmap;
    private readonly Graphics graphics;
    private readonly Font font;
    private readonly Font smallFont;
    private bool isBusy;
    private string iconValue;
    private DateTimeOffset lastRun;
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
      notifyIcon.ContextMenuStrip.Opening += OnContextMenuStripOpening;
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
            Process.Start("taskmgr.exe");
            break;
          case Enums.DoubleClickAction.ResourceMonitor:
            var currentSessionID = Process.GetCurrentProcess().SessionId;
            var process = Process.GetProcessesByName("perfmon").Where(p => p.SessionId == currentSessionID).FirstOrDefault();
            if (process == null) {
              Process.Start("resmon.exe");
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
      
      float dpiX, dpiY;
      using (var b = new Bitmap(1, 1, PixelFormat.Format32bppArgb)) {
        dpiX = b.HorizontalResolution;
        dpiY = b.VerticalResolution;
      }
      var width = Math.Max(16, (int)Math.Round(16 * dpiX / 96));
      var height = Math.Max(16, (int)Math.Round(16 * dpiY / 96));
      bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
      graphics = Graphics.FromImage(bitmap);
      if (Environment.OSVersion.Version.Major > 5) {
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
      }
      font = new Font("Arial", width == 16 ? 10.0F : 8.0F * dpiX / 96);
      smallFont = new Font("Arial", width == 16 ? 8.0F : 6.0F * dpiX / 96);

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
        Enums.TrayIconMode.MemoryUsed => !Settings.ShowVirtualMemory
                    ? $"Memory used:\nPhysical: {computer.Memory.Physical.Used}"
                    : $"Memory used:\nPhysical: {computer.Memory.Physical.Used}\nVirtual: {computer.Memory.Virtual.Used}",
        Enums.TrayIconMode.MemoryAvailable => !Settings.ShowVirtualMemory
                    ? $"Memory available:\nPhysical: {computer.Memory.Physical.Free}"
                    : $"Memory available:\nPhysical: {computer.Memory.Physical.Free}\nVirtual: {computer.Memory.Virtual.Free}",
        _ => !Settings.ShowVirtualMemory
                    ? $"Memory usage:\nPhysical: {computer.Memory.Physical.Used.Percentage}%"
                    : $"Memory usage:\nPhysical: {computer.Memory.Physical.Used.Percentage}%\nVirtual: {computer.Memory.Virtual.Used.Percentage}%",
      };
    }

    private void Update() {

      if (!computer.UpdateMemoryState()) return;

      string newIconValue = null;
      switch (Settings.TrayIconMode) {
        case Enums.TrayIconMode.MemoryUsed:
          newIconValue = computer.Memory.Physical.Used.Value.ToTrayValue();
          break;
        case Enums.TrayIconMode.MemoryAvailable:
          newIconValue = computer.Memory.Physical.Free.Value.ToTrayValue();
          break;
        case Enums.TrayIconMode.Image:
        case Enums.TrayIconMode.MemoryUsage:
        default:
          if (Settings.TrayIconMode == Enums.TrayIconMode.MemoryUsage)
            newIconValue = $"{computer.Memory.Physical.Used.Percentage:0}";
          break;
      }

      if (iconValue != newIconValue) {
        iconValue = newIconValue;
        UpdateIcon();
      }

      if (statusMenuLabel.Visible)
        ExecuteInUiThread(() => { UpdateStatusMenuItem(); });
    }

    private void UpdateIcon() {
      if (string.IsNullOrEmpty(iconValue) && optimizationProgressPercentage == 0) {
        notifyIcon.Icon = imageIcon;
      }
      else {
        try {
          var color = Settings.TrayIconValueColor;
          var iconBackColor = Color.Transparent;
          graphics.Clear(iconBackColor);

          if (optimizationProgressPercentage > 0) {
            var rect = new Rectangle(1, 1, bitmap.Width - 2, bitmap.Height - 2);
            // var backColor = NativeMethods.GetTaskbarColor(); 
            // using (var b = new SolidBrush(backColor))
            //   graphics.FillEllipse(b, rect);
            var sweepAngle = 360f * optimizationProgressPercentage / 100f;
            if (sweepAngle > 0) {
              using (var b = new SolidBrush(color))
                graphics.FillPie(b, rect, -90, sweepAngle);
            }
            using (var p = new Pen(color, 1))
              graphics.DrawEllipse(p, rect);
      
            // var text = value.ToString(); //optimizationProgressPercentage.ToString();
            // using (var path = new GraphicsPath()) {
            //   var emSize = graphics.DpiY * smallFont.Size / 72;
            //   var format = new StringFormat {Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center};
            //   path.AddString(text, smallFont.FontFamily, (int) smallFont.Style, emSize, new RectangleF(0, 0, bitmap.Width, bitmap.Height), format);
            //   using (var outline = new Pen(fillColor, 2)) {
            //     outline.LineJoin = LineJoin.Round;
            //     graphics.DrawPath(outline, path);
            //   }
            //   using (var brush = new SolidBrush(backColor)) graphics.FillPath(brush, path);
            // }
          }
          else {
            var small = iconValue.Length > 2;
            if (small) {
              if (iconValue[1] == '.' || iconValue[1] == ',') {
                var bigPart = iconValue.Substring(0, 1);
                var smallPart = iconValue.Substring(1);
                TextRenderer.DrawText(graphics, bigPart, font, new Point(-bitmap.Width / 4, bitmap.Height / 2), color,
                  iconBackColor, TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(graphics, smallPart, smallFont, new Point(bitmap.Width / 4, bitmap.Height), color,
                  iconBackColor, TextFormatFlags.Bottom);
              }
              else {
                var size = TextRenderer.MeasureText(iconValue, smallFont);
                TextRenderer.DrawText(graphics, iconValue, smallFont,
                  new Point((bitmap.Width - size.Width) / 2, bitmap.Height / 2), color, iconBackColor,
                  TextFormatFlags.VerticalCenter);
              }
            }
            else {
              var size = TextRenderer.MeasureText(iconValue, font);
              TextRenderer.DrawText(graphics, iconValue, font,
                new Point((bitmap.Width - size.Width) / 2, bitmap.Height / 2), color, iconBackColor,
                TextFormatFlags.VerticalCenter);
            }
          }

          var handle = bitmap.GetHicon();
          using (var icon = Icon.FromHandle(handle))
            notifyIcon.Icon = (Icon) icon.Clone();
          NativeMethods.DestroyIcon(handle);
        }
        catch {
          notifyIcon.Icon = imageIcon;
        }
      }
    }
    
    private void OnOptimizeProgressUpdate(byte value, string step) {
      var stepsCount = GetEnabledMemoryAreasCount();
      if (value > stepsCount)
        optimizationProgressPercentage = 0;
      else
        optimizationProgressPercentage = (byte) (value * 100 / stepsCount);
      UpdateIcon();
    }

    private static Enums.MemoryAreas GetEnabledMemoryAreas() {
      if (!OperatingSystem.HasCombinedPageList)
        Settings.MemoryAreas &= ~Enums.MemoryAreas.CombinedPageList;
      if (!OperatingSystem.HasModifiedPageList)
        Settings.MemoryAreas &= ~Enums.MemoryAreas.ModifiedPageList;
      if (!OperatingSystem.HasProcessesWorkingSet)
        Settings.MemoryAreas &= ~Enums.MemoryAreas.ProcessesWorkingSet;
      if (!OperatingSystem.HasStandbyList) {
        Settings.MemoryAreas &= ~Enums.MemoryAreas.StandbyList;
        Settings.MemoryAreas &= ~Enums.MemoryAreas.StandbyListLowPriority;
      }
      if (!OperatingSystem.HasSystemWorkingSet)
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
          if (IsBusy)
            continue;

          if (GetEnabledMemoryAreas() != Enums.MemoryAreas.None) {
            if (Settings.AutoOptimizationInterval > 0 &&
                DateTimeOffset.Now.Subtract(lastAutoOptimizationByInterval).TotalHours >= Settings.AutoOptimizationInterval) {
              Optimize();
              lastAutoOptimizationByInterval = DateTimeOffset.Now;
            }
            else {
              computer.UpdateMemoryState();
              if (Settings.AutoOptimizationMemoryUsage > 0 &&
                  computer.Memory.Physical.Free.Percentage < Settings.AutoOptimizationMemoryUsage &&
                  DateTimeOffset.Now.Subtract(lastAutoOptimizationByMemoryUsage).TotalMinutes >= AutoOptimizationMemoryUsageInterval) {
                Optimize();
                lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
              }
            }
          }
          
          Update();

          await Task.Delay(Settings.UpdateIntervalSeconds * 1000).ConfigureAwait(false);
        }
        catch (Exception ex) {
          Logger.Debug(ex.GetMessage());
        }
      }
    }

    private void Optimize() {
      try {
        if (IsBusy)
          return;
        IsBusy = true;
        SetPriority(Settings.RunOnPriority);

        var tempPhysicalAvailable = computer.Memory.Physical.Free.Bytes;
        var tempVirtualAvailable = computer.Memory.Virtual.Free.Bytes;

        computer.Optimize(Settings.MemoryAreas);
        lastRun = DateTimeOffset.Now;

        if (Settings.ShowOptimizationNotifications && computer.UpdateMemoryState()) {
          var physicalReleased = (computer.Memory.Physical.Free.Bytes > tempPhysicalAvailable
            ? computer.Memory.Physical.Free.Bytes - tempPhysicalAvailable
            : tempPhysicalAvailable - computer.Memory.Physical.Free.Bytes).ToMemoryUnit();
          var virtualReleased = (computer.Memory.Virtual.Free.Bytes > tempVirtualAvailable
            ? computer.Memory.Virtual.Free.Bytes - tempVirtualAvailable
            : tempVirtualAvailable - computer.Memory.Virtual.Free.Bytes).ToMemoryUnit();
          var message = Settings.ShowVirtualMemory
            ? $"Memory optimized{Environment.NewLine}{Environment.NewLine}Physical: {physicalReleased.Key:0.#} {physicalReleased.Value}{Environment.NewLine}Virtual: {virtualReleased.Key:0.#} {virtualReleased.Value}"
            : $"Memory optimized{Environment.NewLine}{Environment.NewLine}Physical: {physicalReleased.Key:0.#} {physicalReleased.Value}";
          notifyIcon.ShowBalloonTip(5000, Updater.ApplicationTitle, message, ToolTipIcon.Info);
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
        Optimize();
        Update();
      });
    } 
    
    private void AddMenuItems() {

      var menuImage = imageIcon.ToBitmap();
      //notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem(Updater.ApplicationTitle, menuImage, (_, _) => { Updater.VisitAppSite(); }));
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
      if (OperatingSystem.HasProcessesWorkingSet) 
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("Processes working set", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.ProcessesWorkingSet);
        }) { Tag = Enums.MemoryAreas.ProcessesWorkingSet });
      if (OperatingSystem.HasSystemWorkingSet)
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("System working set", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.SystemWorkingSet);
        }) { Tag = Enums.MemoryAreas.SystemWorkingSet });
      if (OperatingSystem.HasCombinedPageList)
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("Combined page list", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.CombinedPageList);
        }) { Tag = Enums.MemoryAreas.CombinedPageList });
      if (OperatingSystem.HasModifiedPageList) 
        optimizationTypesMenu.DropDownItems.Add(new ToolStripMenuItem("Modified page list", null, (_, _) => {
          ToggleMemoryArea(Enums.MemoryAreas.ModifiedPageList);
        }) { Tag = Enums.MemoryAreas.ModifiedPageList });
      if (OperatingSystem.HasStandbyList) {
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
        ((ToolStripMenuItem)sender).Checked = Settings.ShowOptimizationNotifications;
      }) {
        Checked = Settings.ShowOptimizationNotifications,
      });
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Show virtual memory", null, (sender, _) => {
        Settings.ShowVirtualMemory = !Settings.ShowVirtualMemory;
        ((ToolStripMenuItem)sender).Checked = Settings.ShowVirtualMemory;
      }) {
        Checked = Settings.ShowVirtualMemory,
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
          new ToolStripMenuItem("Memory usage", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryUsage); }),
          new ToolStripMenuItem("Memory available", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryAvailable); }),
          new ToolStripMenuItem("Memory used", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryUsed); }),
        }
      };
      iconTypeMenu.DropDown.Closing += OnContextMenuStripClosing;
      notifyIcon.ContextMenuStrip.Items.Add(iconTypeMenu);
      SetIconType(Settings.TrayIconMode);

      iconDoubleClickMenu = new ToolStripMenuItem("Icon double click action") {
        DropDownItems = {
          new ToolStripMenuItem("None", null, (_, _) => { SetIconDoubleClickAction(Enums.DoubleClickAction.None); }),
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
          if (dialog.ShowDialog() != DialogResult.OK) return;
          Settings.TrayIconValueColor = dialog.Color;
          UpdateIcon();
        }
      }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
      //about
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Check for updates", null,
        (_, _) => { Updater.CheckForUpdates(false); }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Site", null, (_, _) => { Updater.VisitAppSite(); }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("About", null, (_, _) => { Updater.ShowAbout(); }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => { ExitThread(); }));
    }

    private void SetIconType(Enums.TrayIconMode trayIconMode) {
      Settings.TrayIconMode = trayIconMode;
      Update();
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
      foreach (var subItem in autoOptimizationIntervalMenu.DropDownItems) {
        if (subItem is ToolStripMenuItem subMenuItem)
          subMenuItem.Checked = subMenuItem.Tag is int intTag && intTag == interval;
      }
      UpdateStatusMenuItem();
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
    
    private void OnContextMenuStripOpening(object sender, CancelEventArgs e) {
      UpdateStatusMenuItem();
    }

    private void UpdateStatusMenuItem() {
      string iconText = GetTrayIconText();
      if (lastRun != DateTimeOffset.MinValue) {
        iconText += $"{Environment.NewLine}Last run: {lastRun:G}";
      }
      if (Settings.AutoOptimizationInterval > 0) {
        var nextRun = lastAutoOptimizationByInterval.AddHours(Settings.AutoOptimizationInterval);
        iconText += $"{Environment.NewLine}Next run: {nextRun:G}";
      }
      if (iconText != statusMenuLabel.Text)
        statusMenuLabel.Text = iconText;
    }

    protected override void Dispose(bool disposing) {
      if (!disposing || components == null) return;
      components.Dispose();
      imageIcon?.Dispose();
      notifyIcon?.Dispose();
      graphics?.Dispose();
      bitmap?.Dispose();
      font?.Dispose();
      smallFont?.Dispose();
      GC.SuppressFinalize(this);
    }

    protected override void ExitThreadCore() {
      notifyIcon.Visible = false;
      Settings.Save();
      base.ExitThreadCore();
    }
  }
}