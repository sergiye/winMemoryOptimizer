namespace winMemoryOptimizer {
  
  internal class Memory {
    
    public void Update(WindowsStructs.MemoryStatusEx memoryStatusEx) {

      Physical.Update(memoryStatusEx.AvailPhys, memoryStatusEx.TotalPhys, memoryStatusEx.MemoryLoad);
      Virtual.Update(memoryStatusEx.AvailPageFile, memoryStatusEx.TotalPageFile);
    }

    public MemoryStats Physical { get; } = new MemoryStats();
    public MemoryStats Virtual { get; } = new MemoryStats();

    public override string ToString() => $"Physical: {Physical.Used} / {Physical.Free}\nVirtual: {Virtual.Used} / {Virtual.Free}";
  }
}