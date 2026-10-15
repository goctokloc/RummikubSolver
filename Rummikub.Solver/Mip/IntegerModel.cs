namespace Rummikub.Solver.Mip;

internal enum Sense
{
    LessOrEqual,
    Equal,
    GreaterOrEqual,
}

internal sealed record Variable(long Lower, long Upper, bool IsInteger, string Name);

internal sealed record Constraint(IReadOnlyList<(int Var, long Coef)> Terms, Sense Sense, long Rhs);

/// <summary>Engine-agnostic description of a (mixed) integer linear program. Objective is maximised.</summary>
internal sealed class IntegerModel
{
    readonly List<Variable> _variables = [];
    readonly List<Constraint> _constraints = [];
    IReadOnlyList<(int Var, long Coef)> _objective = [];

    public IReadOnlyList<Variable> Variables => _variables;

    public IReadOnlyList<Constraint> Constraints => _constraints;

    public IReadOnlyList<(int Var, long Coef)> Objective => _objective;

    public int AddVariable(long lower, long upper, bool isInteger, string name)
    {
        if (lower > upper)
            throw new ArgumentException($"Lower bound {lower} exceeds upper bound {upper} for variable '{name}'.", nameof(lower));

        _variables.Add(new Variable(lower, upper, isInteger, name));
        return _variables.Count - 1;
    }

    public void AddConstraint(IReadOnlyList<(int Var, long Coef)> terms, Sense sense, long rhs)
    {
        ValidateTerms(terms);
        _constraints.Add(new Constraint(terms.ToArray(), sense, rhs));
    }

    public void SetObjective(IReadOnlyList<(int Var, long Coef)> terms)
    {
        ValidateTerms(terms);
        _objective = terms.ToArray();
    }

    public long Evaluate(IReadOnlyList<long> values)
    {
        long total = 0;
        foreach (var (index, coef) in _objective)
            total += coef * values[index];
        return total;
    }

    public bool IsFeasible(IReadOnlyList<long> values)
    {
        for (var i = 0; i < _variables.Count; i++)
        {
            if (values[i] < _variables[i].Lower || values[i] > _variables[i].Upper)
                return false;
        }

        foreach (var constraint in _constraints)
        {
            long lhs = 0;
            foreach (var (index, coef) in constraint.Terms)
                lhs += coef * values[index];

            var satisfied = constraint.Sense switch
            {
                Sense.LessOrEqual => lhs <= constraint.Rhs,
                Sense.Equal => lhs == constraint.Rhs,
                Sense.GreaterOrEqual => lhs >= constraint.Rhs,
                _ => throw new InvalidOperationException($"Unknown sense {constraint.Sense}."),
            };
            if (!satisfied)
                return false;
        }

        return true;
    }

    void ValidateTerms(IReadOnlyList<(int Var, long Coef)> terms)
    {
        foreach (var (index, _) in terms)
        {
            if (index < 0 || index >= _variables.Count)
                throw new ArgumentOutOfRangeException(nameof(terms), index, "Term references an unknown variable.");
        }
    }
}
