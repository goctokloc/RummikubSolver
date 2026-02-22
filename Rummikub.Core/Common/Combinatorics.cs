namespace Rummikub.Core.Common;

public static class Combinatorics
{
    /// <summary>
    /// Generates all k-combinations from the source sequence.
    /// Order does not matter.
    /// </summary>
    public static IEnumerable<IReadOnlyList<T>> Combinations<T>(
        IReadOnlyList<T> source,
        int k)
    {
        if (k <= 0 || k > source.Count)
            yield break;

        var buffer = new T[k];

        IEnumerable<IReadOnlyList<T>> Inner(int start, int depth)
        {
            if (depth == k)
            {
                yield return buffer.ToArray();
                yield break;
            }

            for (var i = start; i <= source.Count - (k - depth); i++)
            {
                buffer[depth] = source[i];
                foreach (var c in Inner(i + 1, depth + 1))
                    yield return c;
            }
        }

        foreach (var combo in Inner(0, 0))
            yield return combo;
    }

    /// <summary>
    /// Generates all combinations with size >= minSize
    /// </summary>
    public static IEnumerable<IReadOnlyList<T>> CombinationsAtLeast<T>(
        IReadOnlyList<T> source,
        int minSize)
    {
        for (var k = minSize; k <= source.Count; k++)
            foreach (var c in Combinations(source, k))
                yield return c;
    }
}
