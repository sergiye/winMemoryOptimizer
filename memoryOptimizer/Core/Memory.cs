using System;

namespace memoryOptimizer.Model.Memory {
  
  public class Memory {
    
    public Memory() {
      Physical = new MemoryStats(0, 0, 0);
      Virtual = new MemoryStats(0, 0, 0);
    }

    public Memory(Structs.Windows.MemoryStatusEx memoryStatusEx) {
      if (memoryStatusEx == null)
        throw new ArgumentNullException("memoryStatusEx");

      Physical = new MemoryStats(memoryStatusEx.AvailPhys, memoryStatusEx.TotalPhys, memoryStatusEx.MemoryLoad);
      Virtual = new MemoryStats(memoryStatusEx.AvailPageFile, memoryStatusEx.TotalPageFile);
    }

    public MemoryStats Physical { get; private set; }
    public MemoryStats Virtual { get; private set; }
  }
}