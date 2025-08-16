using System;

namespace memoryOptimizer {

  internal class Log {

    public DateTime DateTime { get; set; }
    public Enums.LogLevels Level { get; set; }
    public string Method { get; set; }
    public string Message { get; set; }
    public override string ToString() => $"[{Level}] {Message}";
  }
}