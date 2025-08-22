using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using sergiye.Common;

namespace TrayRAMBooster {
  
  internal static class Logger {
    
    private static readonly Enums.LogLevels level = Enums.LogLevels.Debug | Enums.LogLevels.Information |
                                                     Enums.LogLevels.Warning | Enums.LogLevels.Error;

    public static void Debug(string message, [CallerMemberName] string method = null) {
      Log(Enums.LogLevels.Debug, message, method);
    }

    public static void Error(Exception exception, string message = null, [CallerMemberName] string method = null) {
      if ((level & Enums.LogLevels.Debug) != 0) {
        try {
          var stackTrace = new StackTrace(exception, true);
          var frame = stackTrace.GetFrame(stackTrace.FrameCount - 1);
          var methodBase = frame.GetMethod();

          if (methodBase.DeclaringType != null)
            method = $"{methodBase.DeclaringType.Name}.{methodBase.Name}";
        }
        catch {
          // ignored
        }
      }

      if (string.IsNullOrWhiteSpace(message) && exception != null)
        message = exception.GetMessage();

      Log(Enums.LogLevels.Error, message, method);
    }

    public static void Error(string message, [CallerMemberName] string method = null) {
      Log(Enums.LogLevels.Error, message, method);
    }

    private static void Event(string message, EventLogEntryType type = EventLogEntryType.Information) {
      try {
        EventLog.WriteEntry(Updater.ApplicationTitle, message, type);
      }
      catch {
        // ignored
      }
    }

    public static void Information(string message, [CallerMemberName] string method = null) {
      Log(Enums.LogLevels.Information, message, method);
    }

    private static void Log(Enums.LogLevels logLevel, string message, [CallerMemberName] string method = null) {
      try {
        var log = new Log {
          DateTime = DateTime.Now,
          Level = logLevel,
          Method = method,
          Message = message
        };

        var traceMessage = $"{log.DateTime:yyyy-MM-dd HH:mm:ss.fff}\t{log.Level.ToString().ToUpper()}\t{(string.IsNullOrWhiteSpace(log.Method) ? log.Message : $"[{log.Method}] {log.Message}")}";

        switch (logLevel) {
          case Enums.LogLevels.Debug:
            if ((level & Enums.LogLevels.Debug) != 0) {
              Event(message);
              Trace.WriteLine(traceMessage);
            }
            break;
          case Enums.LogLevels.Information:
            if ((level & Enums.LogLevels.Information) != 0) {
              Event(message);
              Trace.TraceInformation(traceMessage);
            }
            break;
          case Enums.LogLevels.Warning:
            if ((level & Enums.LogLevels.Warning) != 0) {
              Event(message, EventLogEntryType.Warning);
              Trace.TraceWarning(traceMessage);
            }
            break;
          case Enums.LogLevels.Error:
            if ((level & Enums.LogLevels.Error) != 0) {
              Event(message, EventLogEntryType.Error);
              Trace.TraceError(traceMessage);
            }
            break;
        }
      }
      catch (Exception e) {
        try {
          Trace.TraceError(e.GetMessage());
        }
        catch {
          // ignored
        }

        Event($"Can not save the LOG: {message} (Exception: {e.GetMessage()})", EventLogEntryType.Error);
      }
    }

    public static void Warning(string message, [CallerMemberName] string method = null) {
      Log(Enums.LogLevels.Warning, message, method);
    }
  }
}
