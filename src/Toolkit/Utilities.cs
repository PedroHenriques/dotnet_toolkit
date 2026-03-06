using System.Collections;
using System.Dynamic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Toolkit;

public static class Utilities
{
  public static object? GetByPath(object root, string path)
  {
    if (root == null) { return null; }
    if (string.IsNullOrWhiteSpace(path)) { return null; }

    object? current = root;
    var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);

    foreach (var segment in segments)
    {
      if (current == null) { return null; }

      var (propName, index) = ParseSegment(segment);

      // 1) Newtonsoft.Json dynamic: JObject / JArray / JValue via JToken
      if (current is JToken jt)
      {
        if (!string.IsNullOrEmpty(propName))
        {
          if (jt is JObject jo)
          {
            jt = jo.TryGetValue(propName, out var child) ? child : null;
          }
          else
          {
            return null;
          }
        }

        if (jt == null) { return null; }

        if (index != null)
        {
          if (jt is JArray ja)
          {
            if (index.Value < 0 || index.Value >= ja.Count) { return null; }
            jt = ja[index.Value];
          }
          else
          {
            return null;
          }
        }

        current = jt is JValue jv ? jv.Value : jt;
        continue;
      }

      // 2) ExpandoObject / dictionary-like dynamics
      if (current is IDictionary<string, object?> dict)
      {
        if (!dict.TryGetValue(propName, out var next)) { return null; }
        current = next;

        if (index != null)
        {
          if (current is IList eoList)
          {
            if (index.Value < 0 || index.Value >= eoList.Count) { return null; }
            current = eoList[index.Value];
          }
          else
          {
            return null;
          }
        }

        continue;
      }

      // 3) Regular POCO via reflection
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

    object? current = root;
    var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);

    for (int i = 0; i < segments.Length; i++)
    {
      if (current == null) { return false; }

      var isLast = i == segments.Length - 1;
      var (propName, index) = ParseSegment(segments[i]);

      // 1) Newtonsoft.Json tokens
      if (current is JToken jt)
      {
        if (!AddToPath_JToken(ref jt, segments, i, value)) { return false; }
        if (isLast) { return true; }

        current = jt;
        continue;
      }

      // 2) ExpandoObject / dictionary-like dynamics
      if (current is IDictionary<string, object?> dict)
      {
        if (!AddToPath_Dictionary(ref current, dict, segments, i, value)) { return false; }
        if (isLast) { return true; }

        continue;
      }

      // 3) Regular POCO via reflection
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

  private static bool AddToPath_JToken(
    ref JToken current, string[] segments, int i, object? value
  )
  {
    static JToken ToToken(object? v) => v == null ? JValue.CreateNull() : JToken.FromObject(v);

    // Normalize: if current is a property, work on its value
    if (current is JProperty jp)
    {
      current = jp.Value;
    }

    var isLast = i == segments.Length - 1;
    var (propName, index) = ParseSegment(segments[i]);

    if (current is not JObject obj) { return false; }
    if (string.IsNullOrWhiteSpace(propName)) { return false; }
    if (i == 0 && obj.Property(propName) == null) { return false; }

    if (index == null)
    {
      if (isLast)
      {
        obj[propName] = ToToken(value);
        current = obj;
        return true;
      }

      var child = obj[propName];
      if (child == null || child.Type == JTokenType.Null)
      {
        child = new JObject();
        obj[propName] = child;
      }
      else if (child is not JObject)
      {
        return false;
      }

      current = child; // this is a JObject
      return true;
    }

    // Prop[index] => array
    var arrToken = obj[propName];
    if (arrToken == null || arrToken.Type == JTokenType.Null)
    {
      arrToken = new JArray();
      obj[propName] = arrToken;
    }
    if (arrToken is not JArray arr) { return false; }

    while (arr.Count <= index.Value)
    {
      arr.Add(JValue.CreateNull());
    }

    if (isLast)
    {
      arr[index.Value] = ToToken(value);
      current = arr;
      return true;
    }

    var elem = arr[index.Value];
    if (elem == null || elem.Type == JTokenType.Null)
    {
      elem = new JObject();
      arr[index.Value] = elem;
    }
    else if (elem is not JObject)
    {
      return false;
    }

    current = elem;
    return true;
  }

  private static bool AddToPath_Dictionary(
    ref object? current, IDictionary<string, object?> dict, string[] segments,
    int i, object? value
  )
  {
    var isLast = i == segments.Length - 1;
    var (propName, index) = ParseSegment(segments[i]);

    if (string.IsNullOrWhiteSpace(propName)) { return false; }

    if (index == null)
    {
      if (isLast)
      {
        dict[propName] = value;
        current = dict;
        return true;
      }

      if (dict.TryGetValue(propName, out var child) == false || child == null)
      {
        child = new ExpandoObject();
        dict[propName] = child;
      }
      else if (child is IDictionary<string, object?> == false)
      {
        return false;
      }

      current = child;
      return true;
    }

    if (dict.TryGetValue(propName, out var listObj) == false || listObj == null)
    {
      listObj = new List<object?>();
      dict[propName] = listObj;
    }

    if (listObj is not IList list) { return false; }

    while (list.Count <= index.Value)
    {
      list.Add(null);
    }

    if (isLast)
    {
      list[index.Value] = value;
      current = list;
      return true;
    }

    var elem = list[index.Value];
    if (elem == null)
    {
      elem = new ExpandoObject();
      list[index.Value] = elem;
    }
    else if (elem is IDictionary<string, object?> == false)
    {
      return false;
    }

    current = elem;
    return true;
  }
}
