namespace TrayRAMBooster {

  internal class MemorySize {
  
    public void Update(ulong bytes) {
      Bytes = bytes;
      var memory = bytes.ToMemoryUnit();
      Unit = memory.Value;
      Value = memory.Key;
    }

    public ulong Bytes { get; private set; }
    public uint Percentage { get; set; }
    public Enums.MemoryUnit Unit { get; private set; }
    public double Value { get; private set; }
    public override string ToString() => $"{Value:0.00} {Unit} ({Percentage}%)";
  }
}