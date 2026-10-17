using System.Collections;
using System.Collections.Generic;

namespace Haavk;

// Mono 版：Il2Cpp 集合在 Mono 下本身就是标准 .NET 集合，这里退化为普通枚举转换。
internal static class Il2CppEnum
{
    internal static List<T> ToList<T>(object source)
    {
        var list = new List<T>();
        if (source is IEnumerable en)
        {
            foreach (object item in en)
            {
                if (item is T typed)
                {
                    list.Add(typed);
                }
            }
        }
        return list;
    }

    internal static int Count(object source)
    {
        if (source is ICollection collection)
        {
            return collection.Count;
        }
        if (source is IEnumerable en)
        {
            int n = 0;
            foreach (object item in en)
            {
                n++;
            }
            return n;
        }
        return 0;
    }
}
