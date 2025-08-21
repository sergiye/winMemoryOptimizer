namespace memoryOptimizer {
  
  internal struct Memory {
    
    public Memory(WindowsStructs.MemoryStatusEx memoryStatusEx) {

      Physical = new MemoryStats(memoryStatusEx.AvailPhys, memoryStatusEx.TotalPhys, memoryStatusEx.MemoryLoad);
      Virtual = new MemoryStats(memoryStatusEx.AvailPageFile, memoryStatusEx.TotalPageFile);
    }

    public MemoryStats Physical { get; private set; }
    public MemoryStats Virtual { get; private set; }
  }
}