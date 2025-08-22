using System;
using System.Collections.Generic;
using System.Drawing;
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

    public static bool IsValid(this Enum value) {
      if (value == null)
        return false;
      var firstDigit = value.ToString()[0];
      return !char.IsDigit(firstDigit) && firstDigit != '-';
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

    public static bool IsDark(this Color color) {
      var brightness = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B);
      return brightness < 186;
    }

    public static string ToTrayValue(this double value) {
      double rounded1 = Math.Round(value, 1);
      return rounded1 < 10 ? rounded1.ToString("0.0") : Math.Round(value).ToString("0");
    }
  }
}