using System.Collections;
using System.Reflection;

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

  public static bool AddToPath(object root, string path, object? value)
  {
    if (root == null)
    {
      throw new ArgumentNullException(nameof(root));
    }
    if (string.IsNullOrWhiteSpace(path))
    {
      throw new ArgumentException("Path cannot be empty.", nameof(path));
    }

    var current = root;
    var segments = path.Split('.');

    for (int i = 0; i < segments.Length; i++)
    {
      var isLast = i == segments.Length - 1;
      var (propName, index) = ParseSegment(segments[i]);

      var type = current.GetType();
      var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
      if (
        prop == null ||
        (
          isLast == false &&
          IsReferenceOrCollection(prop.PropertyType) == false
        )
      )
      {
        // Property not found or cannot navigate through it
        return false;
      }

      if (index == null)
      {
        // No index: simple property, not an iterable
        if (isLast)
        {
          prop.SetValue(current, value);
          return true;
        }

        // Intermediate segment: ensure object exists
        var next = prop.GetValue(current);
        if (next == null)
        {
          next = Activator.CreateInstance(prop.PropertyType);
          if (next == null) { return false; }
          prop.SetValue(current, next);
        }

        current = next;
      }
      else
      {
        // Indexed segment: property must be a list (for now)
        var listObj = prop.GetValue(current);
        if (listObj == null)
        {
          listObj = CreateListInstance(prop.PropertyType);
          if (listObj == null) { return false; }
          prop.SetValue(current, listObj);
        }

        if (listObj is not IList list) { return false; }

        EnsureListSize(list, index.Value);

        if (isLast)
        {
          list[index.Value] = value;
          return true;
        }
        else
        {
          // Navigate into the list element (creating it if null)
          var element = list[index.Value];
          if (element == null)
          {
            var elementType = GetListElementType(list.GetType()) ?? typeof(object);
            element = Activator.CreateInstance(elementType);
            if (element == null) { return false; }
            list[index.Value] = element;
          }

          current = element;
        }
      }
    }

    return false;
  }

  private static (string propName, int? index) ParseSegment(string segment)
  {
    int start = segment.IndexOf('[');
    if (start < 0) { return (segment, null); }

    int end = segment.IndexOf(']', start);
    if (end < 0) { return (segment, null); }

    var propName = segment.Substring(0, start);
    var indexText = segment.Substring(start + 1, end - start - 1);

    return int.TryParse(indexText, out var index)
      ? (propName, index)
      : (propName, null);
  }

  private static bool IsReferenceOrCollection(Type t)
  {
    return t.IsValueType == false || typeof(IEnumerable).IsAssignableFrom(t);
  }

  private static object? CreateListInstance(Type type)
  {
    if (typeof(IList).IsAssignableFrom(type) == false)
    {
      // If it's an interface like IList<T> / IEnumerable<T>, fall back to List<T>
      if (type.IsInterface && type.IsGenericType)
      {
        var genericDef = type.GetGenericTypeDefinition();
        if (genericDef == typeof(IList<>) || genericDef == typeof(IEnumerable<>))
        {
          var elementType = type.GetGenericArguments()[0];
          var listType = typeof(List<>).MakeGenericType(elementType);
          return Activator.CreateInstance(listType);
        }
      }

      return null;
    }

    // Concrete IList implementation
    return Activator.CreateInstance(type);
  }

  private static void EnsureListSize(IList list, int index)
  {
    var elementType = GetListElementType(list.GetType());

    while (list.Count <= index)
    {
      if (elementType != null && elementType.IsValueType)
      {
        list.Add(Activator.CreateInstance(elementType)); // default(T)
      }
      else
      {
        list.Add(null);
      }
    }
  }

  private static Type? GetListElementType(Type listType)
  {
    if (listType.IsArray)
    {
      return listType.GetElementType();
    }

    if (listType.IsGenericType)
    {
      return listType.GetGenericArguments().FirstOrDefault();
    }

    // Try to find IList<T> in interfaces
    var iListInterface = listType
      .GetInterfaces()
      .FirstOrDefault(
        i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)
      );

    return iListInterface?.GetGenericArguments().FirstOrDefault();
  }
}
