namespace memoryOptimizer {
  
  internal class Memory {
    
    public Memory(WindowsStructs.MemoryStatusEx memoryStatusEx) {

      Physical = new MemoryStats(memoryStatusEx.AvailPhys, memoryStatusEx.TotalPhys, memoryStatusEx.MemoryLoad);
      Virtual = new MemoryStats(memoryStatusEx.AvailPageFile, memoryStatusEx.TotalPageFile);
    }

    public MemoryStats Physical { get; }
    public MemoryStats Virtual { get; }

    public override string ToString() => $"Physical: {Physical.Used} / {Physical.Free}\nVirtual: {Virtual.Used} / {Virtual.Free}";
  }
}