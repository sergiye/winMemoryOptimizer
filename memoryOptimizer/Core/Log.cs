using System;

namespace memoryOptimizer.Model {

  public class Log {

    public DateTime DateTime { get; set; }
    public Enums.Log.Levels Level { get; set; }
    public string Method { get; set; }
    public string Message { get; set; }
    public override string ToString() => $"[{Level}] {Message}";
  }
}