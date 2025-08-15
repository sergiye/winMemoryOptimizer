using memoryOptimizer.Model;
using memoryOptimizer.Service;
using sergiye.Common;
using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace memoryOptimizer {
  public class TrayApplicationContext : ApplicationContext {

    private readonly IContainer components = new Container();
    private const int AutoOptimizationMemoryUsageInterval = 5; // Minute

    private readonly Icon imageIcon;
    private readonly NotifyIcon notifyIcon;
    private readonly Computer computer;
    private readonly ComputerService computerService;
    private ToolStripMenuItem iconTypeImageMenu;
    private ToolStripMenuItem iconTypeUsageMenu;
    private ToolStripMenuItem autoOptimizeEveryMenu;
    private ToolStripMenuItem autoOptimizeUsageMenu;
    private bool isBusy;

    private BackgroundWorker monitorAppWorker;
    private BackgroundWorker monitorComputerWorker;

    private DateTimeOffset lastAutoOptimizationByInterval = DateTimeOffset.Now;
    private DateTimeOffset lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;

    private byte optimizationProgressTotal = byte.MaxValue;
    private byte optimizationProgressPercentage;
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

      Updater.Subscribe(
        (message, isError) => {
          MessageBox.Show(message, Updater.ApplicationName, MessageBoxButtons.OK,
            isError ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        },
        (message) => {
          return MessageBox.Show(message, Updater.ApplicationName, MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question) == DialogResult.OK;
        },
        Application.Exit
      );

      var timer = new System.Windows.Forms.Timer(components);
      timer.Tick += async (_, _) => {
        timer.Enabled = false;
        timer.Enabled = !await Updater.CheckForUpdatesAsync(true);
      };
      timer.Interval = 3000;
      timer.Enabled = true;

      computerService = new ComputerService();
      computerService.OnOptimizeProgressUpdate += OnOptimizeProgressUpdate;
      computer = new Computer {
        OperatingSystem = computerService.OperatingSystem
      };
      MonitorAsync();

      AddMenuItems();
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
      if (notifyIcon != null && notifyIcon.ContextMenuStrip != null)
        notifyIcon.ContextMenuStrip.Enabled = !running;
    }

    private void Notify(string message, string title = null, int timeout = 5, Enums.Icon.Notification icon = Enums.Icon.Notification.None) {
      notifyIcon?.ShowBalloonTip(timeout * 1000, title, message, (ToolTipIcon) icon);
    }

    private void Update(Model.Memory.Memory memory) {
      if (notifyIcon == null)
        return;

      try {
        if (memory == null)
          throw new ArgumentNullException("memory");

        notifyIcon.Text = Settings.ShowVirtualMemory
          ? $"{"Memory usage".ToUpper()}{Environment.NewLine}Physical: {memory.Physical.Used.Percentage}%{Environment.NewLine}Virtual: {memory.Virtual.Used.Percentage}%"
          : $"{"Memory usage".ToUpper()}{Environment.NewLine}Physical: {memory.Physical.Used.Percentage}%";
      }
      catch {
        if (notifyIcon != null)
          notifyIcon.Text = string.Empty;
      }

      try {
        switch (Settings.TrayIcon) {
          case Enums.Icon.Tray.Image:
            notifyIcon.Icon = imageIcon;
            break;

          case Enums.Icon.Tray.MemoryUsage:
            if (memory == null)
              throw new ArgumentNullException("memory");

            if (memory.Physical.Used.Percentage == 0)
              return;

            using (var image = new Bitmap(16, 16))
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font("Arial", 9F))
            using (var format = new StringFormat()) {
              format.Alignment = StringAlignment.Center;
              format.LineAlignment = StringAlignment.Center;

              graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
              graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
              graphics.SmoothingMode = SmoothingMode.AntiAlias;
              graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

              graphics.FillRectangle(
                memory.Physical.Used.Percentage >= 90 ? Brushes.Red :
                memory.Physical.Used.Percentage >= 80 ? Brushes.DarkOrange : Brushes.Black, 0, 0, 16, 15);
              graphics.DrawString($"{(memory.Physical.Used.Percentage == 100 ? 0 : memory.Physical.Used.Percentage):00}",
                font, Brushes.WhiteSmoke, 8, 8, format);

              var handle = image.GetHicon();
              using (var icon = Icon.FromHandle(handle))
                notifyIcon.Icon = (Icon) icon.Clone();
              NativeMethods.DestroyIcon(handle);
            }
            break;
        }
      }
      catch {
        if (notifyIcon != null)
          notifyIcon.Icon = imageIcon;
      }
    }

    private void OnOptimizeProgressUpdate(byte value, string step) {
      optimizationProgressPercentage = (byte) (value * 100 / optimizationProgressTotal);
      // optimizationProgressStep = step;
      // optimizationProgressValue = value;
      if (Settings.ShowOptimizationNotifications)
        Notify($"Step: {step}", $"{optimizationProgressPercentage}% optimized", 1, Enums.Icon.Notification.Information);
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
          Thread.Sleep(10000);
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
          Update(computer.Memory);
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
        optimizationProgressTotal = (byte) (new BitArray(new[] {(int) Settings.MemoryAreas}).OfType<bool>().Count(x => x) + 1);

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
      //settings
      var iconTypeMenu = new ToolStripMenuItem("Icon type");
      iconTypeImageMenu = new ToolStripMenuItem("Image", null, (_, _) => {
        SetIconType(Enums.Icon.Tray.Image);
      });
      iconTypeUsageMenu = new ToolStripMenuItem("Memory usage", null, (_, _) => {
        SetIconType(Enums.Icon.Tray.MemoryUsage);
      });
      iconTypeMenu.DropDownItems.Add(iconTypeImageMenu);
      iconTypeMenu.DropDownItems.Add(iconTypeUsageMenu);
      notifyIcon.ContextMenuStrip.Items.Add(iconTypeMenu);
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
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
      //about
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Check for updates", null,
        (_, _) => { Updater.CheckForUpdates(false); }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Site", null, (_, _) => { Updater.VisitAppSite(); }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("About", null, (_, _) => { Updater.ShowAbout(); }));
      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => { ExitThread(); }));
    }

    private void SetIconType(Enums.Icon.Tray tray, bool update = true) {
      Settings.TrayIcon = tray;
      if (update)
        Update(computer.Memory);
    }
    
    private void OnContextMenuStripOpening(object sender, CancelEventArgs e) {
      
      iconTypeImageMenu.Checked = Settings.TrayIcon == Enums.Icon.Tray.Image;
      iconTypeUsageMenu.Checked = Settings.TrayIcon == Enums.Icon.Tray.MemoryUsage;
      for (var i = 0; i <=24; i ++)
        ((ToolStripMenuItem) autoOptimizeEveryMenu.DropDownItems[i]).Checked = Settings.AutoOptimizationInterval == i;
      for (var i = 0; i < 10; i ++)
        ((ToolStripMenuItem) autoOptimizeUsageMenu.DropDownItems[i]).Checked = Settings.AutoOptimizationMemoryUsage == i * 10;
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