namespace memoryOptimizer {

  internal class Computer {
  
    public Computer() {
      Memory = new Memory();
      OperatingSystem = new OperatingSystem();
    }

    public Memory Memory { get; set; }
    public OperatingSystem OperatingSystem { get; set; }
  }
}