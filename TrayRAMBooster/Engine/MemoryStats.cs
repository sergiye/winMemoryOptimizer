namespace TrayRAMBooster {

  internal class MemoryStats {
  
    public void Update(ulong free, ulong total, uint? used = null) {
      Free.Update(free);
      Total.Update(total);
      Used.Update(total >= free ? total - free : free - total);
      used ??= Used.Value > 0 && Total.Value > 0 ? (uint) (Used.Value * 100 / Total.Value) : 0;
      Free.Percentage = (uint) (100 - used);
      Used.Percentage = (uint) used;
    }

    public MemorySize Free { get; } = new MemorySize();
    public MemorySize Total { get; } = new MemorySize();
    public MemorySize Used { get; } = new MemorySize();
    public override string ToString() => $"({Total.Value:0.#} {Total.Unit}) Used | {Used} - Free | {Free}";
  }
}