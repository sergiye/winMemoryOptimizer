namespace memoryOptimizer.Model {

  public class Computer {
  
    public Computer() {
      Memory = new Memory.Memory();
      OperatingSystem = new OperatingSystem();
    }

    public Memory.Memory Memory { get; set; }
    public OperatingSystem OperatingSystem { get; set; }
  }
}