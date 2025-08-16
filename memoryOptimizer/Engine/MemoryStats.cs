namespace memoryOptimizer {

  internal class MemoryStats {
  
    public MemoryStats(ulong free, ulong total, uint? used = null) {
      Free = new MemorySize(free);
      Total = new MemorySize(total);
      Used = new MemorySize(total >= free ? total - free : free - total);
      used ??= Used.Value > 0 && Total.Value > 0 ? (uint) (Used.Value * 100 / Total.Value) : 0;
      Free.Percentage = (uint) (100 - used);
      Used.Percentage = (uint) used;
    }

    public MemorySize Free { get; }
    public MemorySize Total { get; }
    public MemorySize Used { get; }
    public override string ToString() => $"({Total.Value:0.#} {Total.Unit}) Used | {Used} - Free | {Free}";
  }
}