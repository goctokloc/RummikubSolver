using Rummikub.Core;

namespace Rummikub.Solver.Model;

internal static class RunMerger
{
    public static IReadOnlyList<Group> Merge(IEnumerable<Group> newGroups)
    {
        var runs = new List<Run>();
        var result = new List<Group>();
        foreach (var g in newGroups)
        {
            if (g is Run r) runs.Add(r); else result.Add(g);
        }

        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < runs.Count && !changed; i++)
            {
                for (int j = 0; j < runs.Count && !changed; j++)
                {
                    if (i == j) continue;
                    var a = runs[i];
                    var b = runs[j];
                    if (a.Color != b.Color || a.Start + a.Length != b.Start) continue;
                    if (a.Length + b.Length > Run.MaxValue || a.JokerCount + b.JokerCount > 2) continue;
                    var merged = Run.Create(a.Color, a.Start, a.Length + b.Length,
                        (ushort)(a.JokerMask | (b.JokerMask << a.Length)));
                    runs[i] = merged;
                    runs.RemoveAt(j);
                    changed = true;
                }
            }
        } while (changed);

        result.AddRange(runs);
        return result;
    }
}
