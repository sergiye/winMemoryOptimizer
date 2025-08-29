using System;
using System.Collections.Generic;
using System.Linq;

namespace TrayRAMBooster {

  internal static class ExtensionMethods {

    public static string GetMessage(this Exception value) {
      var exception = value;
      var messages = new List<string>();
      do {
        messages.Add(exception.Message.Trim());
        exception = exception.InnerException;
      } while (exception != null);
      return string.Join(". ", messages.Distinct());
    }

    public static string RemoveWhitespaces(this string value) {
      return new string(value.ToCharArray().Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    public static KeyValuePair<double, Enums.MemoryUnit> ToMemoryUnit(this ulong value) {
      if (value < 1024)
        return new KeyValuePair<double, Enums.MemoryUnit>(value, Enums.MemoryUnit.B);
      var mag = (int) Math.Log(value, 1024);
      return new KeyValuePair<double, Enums.MemoryUnit>(value / Math.Pow(1024, mag), (Enums.MemoryUnit) mag);
    }
  }
}