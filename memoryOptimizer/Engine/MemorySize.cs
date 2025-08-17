namespace memoryOptimizer {

  internal class MemorySize {
  
    public MemorySize(ulong bytes) {
      Bytes = bytes;
      var memory = bytes.ToMemoryUnit();
      Unit = memory.Value;
      Value = memory.Key;
    }

    public ulong Bytes { get; private set; }
    public uint Percentage { get; set; }
    public Enums.MemoryUnit Unit { get; }
    public double Value { get; }
    public override string ToString() => $"{Value:0.#} {Unit} ({Percentage}%)";
  }
}