using System.Collections;

namespace Rummikub.Core.Common;

public sealed class MultiSet<T> : IEnumerable<T> where T : notnull
{
    private readonly Dictionary<T, int> _counts;

    public MultiSet(IEnumerable<T> items)
    {
        _counts = items
            .GroupBy(item => item)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private MultiSet(Dictionary<T, int> counts)
    {
        _counts = counts;
    }

    public MultiSet()
    {
        _counts = [];
    }


    /// <summary>Total number of elements including duplicates</summary>
    public int Count => _counts.Values.Sum();

    /// <summary>Distinct elements (keys)</summary>
    public IEnumerable<T> DistinctItems => _counts.Keys;

    /// <summary>Flattened enumeration (with multiplicity)</summary>
    public IEnumerable<T> Elements
    {
        get
        {
            foreach (var (item, count) in _counts)
                for (var i = 0; i < count; i++)
                    yield return item;
        }
    }

    public int CountOf(T item) => _counts.TryGetValue(item, out var count) ? count : 0;

    public bool Contains(T item) => CountOf(item) > 0;

    public void Add(T item)
    {
        _counts[item] = CountOf(item) + 1;
    }
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public void Remove(T item)
    {
        int count = CountOf(item);
        if (count == 0)
            throw new InvalidOperationException("Item not found in multiset.");

        if (count == 1)
            _counts.Remove(item);
        else
            _counts[item] = count - 1;
    }

    public void Remove(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            Remove(item);
        }
    }

    public MultiSet<T> Except(IEnumerable<T> items)
    {
        var result = Clone();

        result.Remove(items);

        return result;
    }

    public MultiSet<T> Clone() => new(new Dictionary<T, int>(_counts));

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var (item, count) in _counts)
        {
            for (int i = 0; i < count; i++)
            {
                yield return item;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString()
    {
        return "{" + string.Join(", ", _counts.Select(kvp => $"{kvp.Key}: {kvp.Value}")) + "}";
    }
}
