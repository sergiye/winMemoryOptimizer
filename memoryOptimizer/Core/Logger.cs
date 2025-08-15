using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using memoryOptimizer.Model;
using sergiye.Common;

namespace memoryOptimizer {
  
  public static class Logger {
    
    private static readonly Enums.Log.Levels level = Enums.Log.Levels.Debug | Enums.Log.Levels.Information |
                                                     Enums.Log.Levels.Warning | Enums.Log.Levels.Error;

    public static void Debug(string message, [CallerMemberName] string method = null) {
      Log(Enums.Log.Levels.Debug, message, method);
    }

    public static void Error(Exception exception, string message = null, [CallerMemberName] string method = null) {
      if ((level & Enums.Log.Levels.Debug) != 0) {
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

      Log(Enums.Log.Levels.Error, message, method);
    }

    public static void Error(string message, [CallerMemberName] string method = null) {
      Log(Enums.Log.Levels.Error, message, method);
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
      Log(Enums.Log.Levels.Information, message, method);
    }

    private static void Log(Enums.Log.Levels level, string message, [CallerMemberName] string method = null) {
      try {
        var log = new Log {
          DateTime = DateTime.Now,
          Level = level,
          Method = method,
          Message = message
        };

        var traceMessage = $"{log.DateTime:yyyy-MM-dd HH:mm:ss.fff}\t{log.Level.ToString().ToUpper()}\t{(string.IsNullOrWhiteSpace(log.Method) ? log.Message : $"[{log.Method}] {log.Message}")}";

        switch (level) {
          case Enums.Log.Levels.Debug:
            if ((Logger.level & Enums.Log.Levels.Debug) != 0) {
              Event(message);
              Trace.WriteLine(traceMessage);
            }
            break;
          case Enums.Log.Levels.Information:
            if ((Logger.level & Enums.Log.Levels.Information) != 0) {
              Event(message);
              Trace.TraceInformation(traceMessage);
            }
            break;
          case Enums.Log.Levels.Warning:
            if ((Logger.level & Enums.Log.Levels.Warning) != 0) {
              Event(message, EventLogEntryType.Warning);
              Trace.TraceWarning(traceMessage);
            }
            break;
          case Enums.Log.Levels.Error:
            if ((Logger.level & Enums.Log.Levels.Error) != 0) {
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
      Log(Enums.Log.Levels.Warning, message, method);
    }
  }
}
