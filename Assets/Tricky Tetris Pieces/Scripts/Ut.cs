/// Written by Timwi - repurposed from The Blue Button

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.Networking.Types;

static class Ut
{
    public static IEnumerable<int> SelectIndexWhere<T>(this IEnumerable<T> source, Predicate<T> predicate)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        return selectIndexWhereIterator(source, predicate);
    }

    private static IEnumerable<int> selectIndexWhereIterator<T>(IEnumerable<T> source, Predicate<T> predicate)
    {
        var i = 0;
        using (var e = source.GetEnumerator())
        {
            while (e.MoveNext())
            {
                if (predicate(e.Current))
                    yield return i;
                i++;
            }
        }
    }

    public static T[] NewArray<T>(params T[] parameters) { return parameters; }

    public static T[] NewArray<T>(int size, Func<int, T> initialiser)
    {
        if (initialiser == null)
            throw new ArgumentNullException(nameof(initialiser));
        var result = new T[size];
        for (int i = 0; i < size; i++)
        {
            result[i] = initialiser(i);
        }
        return result;
    }
}
