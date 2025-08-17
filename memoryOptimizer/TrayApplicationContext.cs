using sergiye.Common;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace memoryOptimizer {
  
  internal class TrayApplicationContext : ApplicationContext {

    private readonly IContainer components = new Container();
    private const int AutoOptimizationMemoryUsageInterval = 5; // Minute

    private readonly Icon imageIcon;
    private readonly NotifyIcon notifyIcon;
    private readonly Computer computer;
    private readonly ComputerService computerService;
    private readonly StartupManager startupManager;
    private ToolStripMenuItem iconTypeMenu;
    private ToolStripMenuItem autoOptimizeEveryMenu;
    private ToolStripMenuItem autoOptimizeUsageMenu;
    private ToolStripMenuItem updateIntervalMenu;
    private Brush iconValueBrush;
    private bool isBusy;

    private BackgroundWorker monitorAppWorker;
    private BackgroundWorker monitorComputerWorker;

    private DateTimeOffset lastAutoOptimizationByInterval = DateTimeOffset.Now;
    private DateTimeOffset lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;

    // private byte optimizationProgressTotal = byte.MaxValue;
    // private byte optimizationProgressPercentage;
    // private byte optimizationProgressValue = byte.MinValue;
    // private string optimizationProgressStep = "Optimize";

    public TrayApplicationContext() {
      imageIcon = Icon.ExtractAssociatedIcon(Updater.CurrentFileLocation);
      notifyIcon = new NotifyIcon(components) {
        ContextMenuStrip = new ContextMenuStrip(),
        Icon = imageIcon,
        Text = Updater.ApplicationTitle,
        Visible = true
      };
      notifyIcon.ContextMenuStrip.Opening += OnContextMenuStripOpening;
      notifyIcon.MouseUp += OnNotifyIconMouseUp;
      notifyIcon.ContextMenuStrip.Renderer = new ThemedToolStripRenderer();
      iconValueBrush = new SolidBrush(Settings.TrayIconValueColor);

      Updater.Subscribe(
        (message, isError) => {
          MessageBox.Show(message, Updater.ApplicationName, MessageBoxButtons.OK,
            isError ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        },
        message => MessageBox.Show(message, Updater.ApplicationName, MessageBoxButtons.OKCancel,
          MessageBoxIcon.Question) == DialogResult.OK,
        Application.Exit
      );

      var timer = new System.Windows.Forms.Timer(components);
      timer.Tick += async (_, _) => {
        timer.Enabled = false;
        timer.Enabled = !await Updater.CheckForUpdatesAsync(true);
      };
      timer.Interval = 3000;
      timer.Enabled = true;

      startupManager = new StartupManager();
      computerService = new ComputerService();
      computerService.OnOptimizeProgressUpdate += OnOptimizeProgressUpdate;
      computer = new Computer {
        OperatingSystem = computerService.OperatingSystem
      };

      AddMenuItems();
      Theme.SetAutoTheme();

      MonitorAsync();
    }

    public event Action OnOptimizeCommandCompleted;

    public bool IsBusy {
      get => isBusy;
      set {
        try {
          Loading(value);
        }
        catch {
          // ignored
        }

        isBusy = value;
      }
    }

    private void Loading(bool running) {
      //Mouse.OverrideCursor = running ? Cursors.Wait : null;
      if (notifyIcon?.ContextMenuStrip != null)
        notifyIcon.ContextMenuStrip.Enabled = !running;
    }

    private void Notify(string message, string title = null, int timeout = 5, ToolTipIcon icon = ToolTipIcon.None) {
      notifyIcon?.ShowBalloonTip(timeout * 1000, title, message, icon);
    }

    private void Update() {
      if (notifyIcon == null)
        return;

      try {
        if (computer?.Memory == null)
          throw new ArgumentNullException(nameof(computer.Memory));

        var text = Settings.ShowVirtualMemory
          ? $"{"Memory usage".ToUpper()}{Environment.NewLine}Physical: {computer.Memory.Physical.Used.Percentage}%{Environment.NewLine}Virtual: {computer.Memory.Virtual.Used.Percentage}%"
          : $"{"Memory usage".ToUpper()}{Environment.NewLine}Physical: {computer.Memory.Physical.Used.Percentage}%";
        // var text = $"Physical: {memory.Physical.Used} / {memory.Physical.Free}";
        // if (Settings.ShowVirtualMemory)
        //   text += $"{Environment.NewLine}Virtual: {memory.Virtual.Used} / {memory.Virtual.Free}";

        notifyIcon.Text = text;
      }
      catch (Exception ex) {
        notifyIcon.Text = string.Empty;
        Debug.WriteLine(ex.Message);
      }

      string iconValue = null;
      if (computer?.Memory != null) {
        switch (Settings.TrayIconMode) {
          case Enums.TrayIconMode.MemoryUsage:
            iconValue = $"{computer.Memory.Physical.Used.Percentage:00}";
            break;
          case Enums.TrayIconMode.MemoryUsed:
            iconValue = computer.Memory.Physical.Used.Value >= 10 ? $"{computer.Memory.Physical.Used.Value:00}" : $"{computer.Memory.Physical.Used.Value:0.0}";
            break;
          case Enums.TrayIconMode.MemoryAvailable:
            iconValue = computer.Memory.Physical.Free.Value >= 10 ? $"{computer.Memory.Physical.Free.Value:00}" : $"{computer.Memory.Physical.Free.Value:0.0}";
            break;
        }
      }

      if (string.IsNullOrEmpty(iconValue)) {
        notifyIcon.Icon = imageIcon;
      }
      else {
        try {
            float dpiX, dpiY;
            using (var b = new Bitmap(1, 1, PixelFormat.Format32bppArgb)) {
              dpiX = b.HorizontalResolution;
              dpiY = b.VerticalResolution;
            }
            var width = Math.Max(16, (int)Math.Round(16 * dpiX / 96));
            var height = Math.Max(16, (int)Math.Round(16 * dpiY / 96));
            var fontSize = iconValue.Length > 2 ? 7.0F : 9.0F;
            using (var image = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font("Arial", width == 16 ? fontSize : (fontSize - 1) * dpiX / 96))
            using (var format = new StringFormat()) {
              format.Alignment = StringAlignment.Center;
              format.LineAlignment = StringAlignment.Center;

              graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
              graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
              graphics.SmoothingMode = SmoothingMode.AntiAlias;
              graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

              graphics.FillRectangle(Brushes.Transparent, 0, 0, image.Width, image.Height);
              graphics.DrawString(iconValue, font, iconValueBrush, (float) image.Width / 2, (float) image.Height / 2, format);

              var handle = image.GetHicon();
              using (var icon = Icon.FromHandle(handle))
                notifyIcon.Icon = (Icon) icon.Clone();
              NativeMethods.DestroyIcon(handle);
            }
        }
        catch {
          notifyIcon.Icon = imageIcon;
        }
      }
    }

    private void OnOptimizeProgressUpdate(byte value, string step) {
      // optimizationProgressPercentage = (byte) (value * 100 / optimizationProgressTotal);
      // optimizationProgressStep = step;
      // optimizationProgressValue = value;
      // if (Settings.ShowOptimizationNotifications)
      //   Notify($"Step: {step}", $"{optimizationProgressPercentage}% optimized", 1, Enums.Icon.Notification.Information);
    }

    public Enums.Memory.Areas MemoryAreas {
      get {
        if (!computer.OperatingSystem.HasCombinedPageList)
          Settings.MemoryAreas &= ~Enums.Memory.Areas.CombinedPageList;

        if (!computer.OperatingSystem.HasModifiedPageList)
          Settings.MemoryAreas &= ~Enums.Memory.Areas.ModifiedPageList;

        if (!computer.OperatingSystem.HasProcessesWorkingSet)
          Settings.MemoryAreas &= ~Enums.Memory.Areas.ProcessesWorkingSet;

        if (!computer.OperatingSystem.HasStandbyList) {
          Settings.MemoryAreas &= ~Enums.Memory.Areas.StandbyList;
          Settings.MemoryAreas &= ~Enums.Memory.Areas.StandbyListLowPriority;
        }

        if (!computer.OperatingSystem.HasSystemWorkingSet)
          Settings.MemoryAreas &= ~Enums.Memory.Areas.SystemWorkingSet;

        return Settings.MemoryAreas;
      }
      set {
        try {
          IsBusy = true;

          if ((Settings.MemoryAreas & value) != 0)
            Settings.MemoryAreas &= ~value;
          else
            Settings.MemoryAreas |= value;

          switch (value) {
            case Enums.Memory.Areas.StandbyList:
              if ((Settings.MemoryAreas & Enums.Memory.Areas.StandbyListLowPriority) != 0)
                Settings.MemoryAreas &= ~Enums.Memory.Areas.StandbyListLowPriority;
              break;

            case Enums.Memory.Areas.StandbyListLowPriority:
              if ((Settings.MemoryAreas & Enums.Memory.Areas.StandbyList) != 0)
                Settings.MemoryAreas &= ~Enums.Memory.Areas.StandbyList;
              break;
          }

          Settings.Save();
        }
        finally {
          IsBusy = false;
        }
      }
    }

    public bool CanOptimize => MemoryAreas != Enums.Memory.Areas.None;

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

    private void MonitorApp(object sender, DoWorkEventArgs e) {
      SetPriority(Settings.RunOnPriority);
      while (!monitorAppWorker.CancellationPending) {
        try {
          if (IsBusy)
            continue;
          if (CanOptimize) {
            if (Settings.AutoOptimizationInterval > 0 &&
                DateTimeOffset.Now.Subtract(lastAutoOptimizationByInterval).TotalHours >= Settings.AutoOptimizationInterval) {
              OptimizeAsync();
              lastAutoOptimizationByInterval = DateTimeOffset.Now;
            }
            else {
              if (Settings.AutoOptimizationMemoryUsage > 0 &&
                  computer.Memory.Physical.Free.Percentage < Settings.AutoOptimizationMemoryUsage &&
                  DateTimeOffset.Now.Subtract(lastAutoOptimizationByMemoryUsage).TotalMinutes >= AutoOptimizationMemoryUsageInterval) {
                OptimizeAsync();
                lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
              }
            }
          }
          Thread.Sleep(Settings.UpdateIntervalSeconds * 1000);
        }
        catch (Exception ex) {
          Logger.Debug(ex.GetMessage());
        }
      }
    }

    private void MonitorAsync() {
      try {
        using (monitorAppWorker = new BackgroundWorker()) {
          monitorAppWorker.DoWork += MonitorApp;
          monitorAppWorker.WorkerSupportsCancellation = true;
          monitorAppWorker.RunWorkerAsync();
        }
      }
      catch (Exception e) {
        Logger.Error(e);
      }

      try {
        using (monitorComputerWorker = new BackgroundWorker()) {
          monitorComputerWorker.DoWork += MonitorComputer;
          monitorComputerWorker.WorkerSupportsCancellation = true;
          monitorComputerWorker.RunWorkerAsync();
        }
      }
      catch (Exception e) {
        Logger.Error(e);
      }
    }

    private void MonitorComputer(object sender, DoWorkEventArgs e) {
      SetPriority(Settings.RunOnPriority);
      while (!monitorComputerWorker.CancellationPending) {
        try {
          if (IsBusy)
            continue;
          computer.Memory = computerService.Memory;
          Update();
          Thread.Sleep(5000);
        }
        catch (Exception ex) {
          Logger.Debug(ex.GetMessage());
        }
      }
    }

    private void Optimize(object sender, DoWorkEventArgs e) {
      try {
        IsBusy = true;
        SetPriority(Settings.RunOnPriority);

        var tempPhysicalAvailable = computer.Memory.Physical.Free.Bytes;
        var tempVirtualAvailable = computer.Memory.Virtual.Free.Bytes;

        computerService.Optimize(Settings.MemoryAreas);
        computer.Memory = computerService.Memory;

        if (Settings.ShowOptimizationNotifications) {
          var physicalReleased = (computer.Memory.Physical.Free.Bytes > tempPhysicalAvailable
              ? computer.Memory.Physical.Free.Bytes - tempPhysicalAvailable
              : tempPhysicalAvailable - computer.Memory.Physical.Free.Bytes).ToMemoryUnit();
          var virtualReleased = (computer.Memory.Virtual.Free.Bytes > tempVirtualAvailable
              ? computer.Memory.Virtual.Free.Bytes - tempVirtualAvailable
              : tempVirtualAvailable - computer.Memory.Virtual.Free.Bytes).ToMemoryUnit();

          var message = Settings.ShowVirtualMemory
            ? $"Memory optimized{Environment.NewLine}{Environment.NewLine}Physical: {physicalReleased.Key:0.#} {physicalReleased.Value} | Virtual: {virtualReleased.Key:0.#} {virtualReleased.Value}"
            : $"Memory optimized{Environment.NewLine}{Environment.NewLine}Physical: {physicalReleased.Key:0.#} {physicalReleased.Value}";

          Notify(message);
        }

        OnOptimizeCommandCompleted?.Invoke();
      }
      finally {
        IsBusy = false;
      }
    }

    private void OptimizeAsync() {
      try {
        // optimizationProgressStep = "Optimize";
        // optimizationProgressValue = 0;
        // optimizationProgressTotal = (byte) (new BitArray(new[] {(int) Settings.MemoryAreas}).OfType<bool>().Count(x => x) + 1);

        using (var worker = new BackgroundWorker()) {
          worker.DoWork += Optimize;
          worker.RunWorkerAsync();
        }
      }
      catch (Exception e) {
        Logger.Error(e);
      }
    }

    private void AddMenuItems() {
      
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Optimize now", null, (_, _) => {
        OptimizeAsync();
      }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
      //auto-start
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Auto-start application", null, (sender, _) => {
        startupManager.Startup = !startupManager.Startup;
        ((ToolStripMenuItem) sender).Checked = startupManager.Startup;
      }) {
        Checked = startupManager.Startup,
      });
      //auto-optimize
      autoOptimizeEveryMenu = new ToolStripMenuItem("Optimize every...") {
        DropDownItems = {
          new ToolStripMenuItem("Never", null, (_, _) => { Settings.AutoOptimizationInterval = 0; }),
        }
      };
      for (var i = 1; i <= 24; i++) {
        var interval = i;
        autoOptimizeEveryMenu.DropDownItems.Add(new ToolStripMenuItem($"{i} hour(s)", null,
          (_, _) => { Settings.AutoOptimizationInterval = interval; }));
      }
      notifyIcon.ContextMenuStrip.Items.Add(autoOptimizeEveryMenu);

      autoOptimizeUsageMenu = new ToolStripMenuItem("Optimize when free memory is below...") {
        DropDownItems = {
          new ToolStripMenuItem("Never", null, (_, _) => { Settings.AutoOptimizationMemoryUsage = 0; }),
        }
      };
      for (var i = 10; i < 100; i+=10) {
        var percents = i;
        autoOptimizeUsageMenu.DropDownItems.Add(new ToolStripMenuItem($"{i}%", null,
          (_, _) => { Settings.AutoOptimizationMemoryUsage = percents; }));
      }
      notifyIcon.ContextMenuStrip.Items.Add(autoOptimizeUsageMenu);

      updateIntervalMenu = new ToolStripMenuItem("Update interval...") {
        DropDownItems = {
          new ToolStripMenuItem("1 sec", null, (_, _) => { Settings.UpdateIntervalSeconds = 1; }){Tag = 1},
          new ToolStripMenuItem("2 sec", null, (_, _) => { Settings.UpdateIntervalSeconds = 2; }){Tag = 2},
          new ToolStripMenuItem("3 sec", null, (_, _) => { Settings.UpdateIntervalSeconds = 3; }){Tag = 3},
          new ToolStripMenuItem("5 sec", null, (_, _) => { Settings.UpdateIntervalSeconds = 5; }){Tag = 5},
          new ToolStripMenuItem("10 sec", null, (_, _) => { Settings.UpdateIntervalSeconds = 10; }){Tag = 10},
          new ToolStripMenuItem("30 sec", null, (_, _) => { Settings.UpdateIntervalSeconds = 30; }){Tag = 30},
          new ToolStripMenuItem("60 sec", null, (_, _) => { Settings.UpdateIntervalSeconds = 60; }){Tag = 60},
        }
      };
      notifyIcon.ContextMenuStrip.Items.Add(updateIntervalMenu);

      //settings
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Show optimization notifications", null, (sender, _) => {
        Settings.ShowOptimizationNotifications = !Settings.ShowOptimizationNotifications;
        ((ToolStripMenuItem) sender).Checked = Settings.ShowOptimizationNotifications;
      }) {
        Checked = Settings.ShowOptimizationNotifications,
      });
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Show virtual memory", null, (sender, _) => {
        Settings.ShowVirtualMemory = !Settings.ShowVirtualMemory;
        ((ToolStripMenuItem) sender).Checked = Settings.ShowVirtualMemory;
      }) {
        Checked = Settings.ShowVirtualMemory,
      });
      iconTypeMenu = new ToolStripMenuItem("Icon type") {
        DropDownItems = {
          new ToolStripMenuItem("Image", null, (_, _) => { SetIconType(Enums.TrayIconMode.Image); }),
          new ToolStripMenuItem("Memory usage", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryUsage); }),
          new ToolStripMenuItem("Memory available", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryAvailable); }),
          new ToolStripMenuItem("Memory used", null, (_, _) => { SetIconType(Enums.TrayIconMode.MemoryUsed); }),
        }
      };
      notifyIcon.ContextMenuStrip.Items.Add(iconTypeMenu);
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Set icon color...", null, (_, _) => {
        using (var dialog = new ColorDialog()) {
          dialog.Color = Settings.TrayIconValueColor;
          if (dialog.ShowDialog() != DialogResult.OK) return;
          Settings.TrayIconValueColor = dialog.Color;
          iconValueBrush = new SolidBrush(Settings.TrayIconValueColor);
          Update();
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

    private void SetIconType(Enums.TrayIconMode trayIconЬщву) {
      Settings.TrayIconMode = trayIconЬщву;
      Update();
    }
    
    private void OnContextMenuStripOpening(object sender, CancelEventArgs e) {

      foreach (Enums.TrayIconMode trayType in Enum.GetValues(typeof(Enums.TrayIconMode))) {
        ((ToolStripMenuItem) iconTypeMenu.DropDownItems[(int)trayType]).Checked = Settings.TrayIconMode == trayType;
      }
      for (var i = 0; i <=24; i ++)
        ((ToolStripMenuItem) autoOptimizeEveryMenu.DropDownItems[i]).Checked = Settings.AutoOptimizationInterval == i;
      for (var i = 0; i < 10; i ++)
        ((ToolStripMenuItem) autoOptimizeUsageMenu.DropDownItems[i]).Checked = Settings.AutoOptimizationMemoryUsage == i * 10;
      
      foreach (var subItem in updateIntervalMenu.DropDownItems) {
        if (subItem is not ToolStripMenuItem subMenuItem) continue;
        subMenuItem.Checked = subMenuItem.Tag is int intTag && intTag == Settings.UpdateIntervalSeconds;
      }  
    }

    private void OnNotifyIconMouseUp(object sender, MouseEventArgs e) {
      if (e.Button != MouseButtons.Left) return;
      var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
      mi?.Invoke(notifyIcon, null);
    }

    protected override void Dispose(bool disposing) {
      if (!disposing || components == null) return;
      components.Dispose();
      if (monitorAppWorker != null) {
        try {
          monitorAppWorker.CancelAsync();
        }
        catch {
          // ignored
        }

        try {
          monitorAppWorker.Dispose();
        }
        catch {
          // ignored
        }
      }

      if (monitorComputerWorker != null) {
        try {
          monitorComputerWorker.CancelAsync();
        }
        catch {
          // ignored
        }

        try {
          monitorComputerWorker.Dispose();
        }
        catch {
          // ignored
        }
      }

      if (imageIcon != null) {
        imageIcon.Dispose();
      }

      if (notifyIcon != null) {
        notifyIcon.Dispose();
      }

      GC.SuppressFinalize(this);
    }

    protected override void ExitThreadCore() {
      notifyIcon.Visible = false;
      Settings.Save();
      base.ExitThreadCore();
    }
  }
}