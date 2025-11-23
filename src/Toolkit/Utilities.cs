using System.Collections;

namespace Toolkit;

public static class Utilities
{
  public static object? GetByPath(object root, string path)
  {
    var current = root;
    var segments = path.Split('.');

    foreach (var segment in segments)
    {
      if (current == null) { return null; }

      var (propName, index) = ParseSegment(segment);

      var type = current.GetType();
      var prop = type.GetProperty(propName);
      if (prop == null) { return null; }

      current = prop.GetValue(current);
      if (current == null) { return null; }

      if (index == null) { continue; }

      if (current is IList list)
      {
        if (index >= list.Count) { return null; }
        current = list[index.Value];
      }
      else
      {
        return null;
      }
    }

    return current;
  }

  private static (string propName, int? index) ParseSegment(string segment)
  {
    // Example: "Orders[3]"
    int start = segment.IndexOf('[');
    if (start < 0)
      return (segment, null);

    int end = segment.IndexOf(']', start);
    if (end < 0)
      return (segment, null);

    var propName = segment.Substring(0, start);
    var indexText = segment.Substring(start + 1, end - start - 1);

    return int.TryParse(indexText, out var index)
        ? (propName, index)
        : (propName, null);
  }
}