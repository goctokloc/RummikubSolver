# ILP Solver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the exponential DFS solver with a pure-C# integer-programming solver (own simplex + branch-and-bound) that finds provably optimal plays in under a second and supports initial meld, points objective, keep-jokers and least-rearrangement.

**Architecture:** A generic MIP engine (`Mip/`: `IntegerModel`, `SimplexSolver`, `BranchAndBound`) knows nothing about Rummikub. `Model/` turns a position + options into an `IntegerModel` over a cached catalogue of 1173 legal groups, decodes and exactly verifies the solution. `RummikubSolver.Solve` composes them; the old DFS survives only as a test oracle.

**Tech Stack:** .NET 8, C# latest, NUnit 3, ASP.NET Core 8, React 19 + Vite.

**Spec:** `docs/superpowers/specs/2026-10-15-ilp-solver-design.md`

## Global Constraints

- Branch: `ilp-solver` (spec already committed there). No new NuGet packages.
- Engine arithmetic: `double`, tolerance `1e-9`; integrality tolerance `1e-6`; prune when `⌊lpObjective + 1e-6⌋ ≤ incumbent`.
- Pricing: Dantzig; Bland's rule after 50 consecutive degenerate pivots; > 50 000 pivots in one LP ⇒ `InvalidOperationException`.
- `long.MaxValue` as a variable upper bound means +∞.
- Catalogue: runs length 3–5, sets 3–4 colours, ≤ 2 jokers, duplicates by tile multiset collapsed keeping the highest-points reading; exactly 281 sets + 892 runs = 1173 (one tile + two jokers reads as a run for values 1–11, as a set for 12–13).
- Objective level order: Primary → JokersKept (only with `KeepJokers`) → GroupsKept. Points mode joker weight 30; `KeepJokers` joker weight 0 in Primary.
- Initial meld: board fixed (`k_b = mult_b`), `Σ points_g·x_g − InitialMeldPoints·m ≥ 0`, `Σ x_g − 36·m ≤ 0`.
- `SolverOptions` defaults: `InitialMeldDone = true`, `InitialMeldPoints = 30`, `Objective = TileCount`, `KeepJokers = false`, `MaxNodes = 200_000`.
- Every returned play passes `SolutionVerifier`; failure ⇒ `InvalidOperationException`.
- Mip and Model types are `internal` (tests see them via the existing `InternalsVisibleTo`); public API stays in namespace `Rummikub.Solver.Solver`.
- Build output is Polish-locale: warnings line is `Ostrzeżenia: N`, test summary `Powodzenie!` / `niepowodzenie:`. Baseline warnings: 0.

## Review Focus

- **Initial meld exactly at the threshold** — R9 R10 R11 (= 30) must meld (`≥`, not `>`). Test in Task 6.
- **Joker points in the initial meld** — R9 R10 J must meld with the joker as R11 (30), not R8 (27). Tests in Task 4 (catalogue keeps the 30-point reading) and Task 6.
- **Board run longer than 5 left alone** — board R1–R8 with an unplayable hand must come back as the identical board (a `k_b` column, not catalogue pieces). Test in Task 6.
- **More than 2 jokers with `KeepJokers`** — hand J J J R5 must not overflow the objective packing or throw; plays R5 J J. Test in Task 6.
- **Cancelled request** — an already-cancelled token returns the verified "keep everything" play with `IsOptimal = false`, not an exception. Test in Task 6.

---

### Task 1: IntegerModel

**Files:**
- Create: `Rummikub.Solver/Mip/IntegerModel.cs`
- Test: `Rummikub.Tests/Mip/IntegerModelTests.cs`

**Interfaces:**
- Produces (namespace `Rummikub.Solver.Mip`, all `internal`):
  - `enum Sense { LessOrEqual, Equal, GreaterOrEqual }`
  - `sealed record Variable(long Lower, long Upper, bool IsInteger, string Name)`
  - `sealed record Constraint(IReadOnlyList<(int Var, long Coef)> Terms, Sense Sense, long Rhs)`
  - `sealed class IntegerModel` with `IReadOnlyList<Variable> Variables`, `IReadOnlyList<Constraint> Constraints`, `IReadOnlyList<(int Var, long Coef)> Objective`, `int AddVariable(long lower, long upper, bool isInteger, string name)`, `void AddConstraint(IReadOnlyList<(int Var, long Coef)> terms, Sense sense, long rhs)`, `void SetObjective(IReadOnlyList<(int Var, long Coef)> terms)` (maximise; replaces previous), `long Evaluate(IReadOnlyList<long> values)`, `bool IsFeasible(IReadOnlyList<long> values)` (exact: bounds + every constraint, integer vars only checked as given).
  - `AddVariable` with `lower > upper` ⇒ `ArgumentException`; a term referencing an unknown variable ⇒ `ArgumentOutOfRangeException`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Rummikub.Solver.Mip;
namespace Rummikub.Tests.Mip;

[TestFixture]
public class IntegerModelTests
{
    static IntegerModel TwoVarModel()
    {
        var m = new IntegerModel();
        var x = m.AddVariable(0, 10, true, "x");
        var y = m.AddVariable(0, 10, true, "y");
        m.AddConstraint([(x, 1), (y, 1)], Sense.LessOrEqual, 4);
        m.AddConstraint([(x, 1)], Sense.GreaterOrEqual, 1);
        m.SetObjective([(x, 3), (y, 2)]);
        return m;
    }

    [Test] public void AddVariable_Returns_Sequential_Indices()
    {
        var m = new IntegerModel();
        Assert.That(m.AddVariable(0, 1, true, "a"), Is.EqualTo(0));
        Assert.That(m.AddVariable(0, 1, true, "b"), Is.EqualTo(1));
    }

    [Test] public void Evaluate_Computes_Objective() =>
        Assert.That(TwoVarModel().Evaluate([2, 1]), Is.EqualTo(8));

    [TestCase(new long[] { 1, 3 }, true)]
    [TestCase(new long[] { 0, 3 }, false)]   // x >= 1 violated
    [TestCase(new long[] { 3, 2 }, false)]   // x + y <= 4 violated
    [TestCase(new long[] { 11, 0 }, false)]  // bound violated
    public void IsFeasible_Checks_Bounds_And_Constraints(long[] values, bool expected) =>
        Assert.That(TwoVarModel().IsFeasible(values), Is.EqualTo(expected));

    [Test] public void Lower_Above_Upper_Throws() =>
        Assert.Throws<ArgumentException>(() => new IntegerModel().AddVariable(2, 1, true, "z"));

    [Test] public void Unknown_Variable_In_Term_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntegerModel().AddConstraint([(0, 1)], Sense.Equal, 0));
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~IntegerModelTests`
Expected: build FAIL (`IntegerModel` not found).

- [ ] **Step 3: Implement `IntegerModel` and its records in `Rummikub.Solver/Mip/IntegerModel.cs`**

- [ ] **Step 4: Run all tests**

Run: `dotnet test Rummikub.Tests`
Expected: all pass (91 existing + new).

- [ ] **Step 5: Commit**

```bash
git add Rummikub.Solver/Mip Rummikub.Tests/Mip
git commit -m "Add engine-agnostic IntegerModel"
```

---

### Task 2: SimplexSolver

**Files:**
- Create: `Rummikub.Solver/Mip/SimplexSolver.cs`, `Rummikub.Solver/Mip/LpResult.cs`
- Test: `Rummikub.Tests/Mip/SimplexSolverTests.cs`

**Interfaces:**
- Consumes: `IntegerModel` (Task 1).
- Produces (`internal`, namespace `Rummikub.Solver.Mip`):
  - `enum LpStatus { Optimal, Infeasible, Unbounded }`
  - `sealed record LpResult(LpStatus Status, double Objective, double[] Values)` — `Values` has one entry per model variable (structural only); empty array unless `Optimal`.
  - `static class SimplexSolver { public static LpResult Solve(IntegerModel model, double[]? lower = null, double[]? upper = null); }` — `lower`/`upper` override variable bounds (length = variable count) when given; `double.PositiveInfinity` for unbounded. Integrality is ignored.

- [ ] **Step 1: Write the failing tests**

```csharp
using Rummikub.Solver.Mip;
namespace Rummikub.Tests.Mip;

[TestFixture]
public class SimplexSolverTests
{
    const long Inf = long.MaxValue;

    // Wyndor: max 3x + 5y; x <= 4; 2y <= 12; 3x + 2y <= 18  -> 36 at (2, 6)
    static IntegerModel Wyndor()
    {
        var m = new IntegerModel();
        var x = m.AddVariable(0, Inf, false, "x");
        var y = m.AddVariable(0, Inf, false, "y");
        m.AddConstraint([(x, 1)], Sense.LessOrEqual, 4);
        m.AddConstraint([(y, 2)], Sense.LessOrEqual, 12);
        m.AddConstraint([(x, 3), (y, 2)], Sense.LessOrEqual, 18);
        m.SetObjective([(x, 3), (y, 5)]);
        return m;
    }

    [Test] public void Solves_Textbook_Lp()
    {
        var r = SimplexSolver.Solve(Wyndor());
        Assert.That(r.Status, Is.EqualTo(LpStatus.Optimal));
        Assert.That(r.Objective, Is.EqualTo(36).Within(1e-9));
        Assert.That(r.Values, Is.EqualTo(new[] { 2.0, 6.0 }).Within(1e-9));
    }

    [Test] public void Bound_Override_Is_Respected()
    {
        var r = SimplexSolver.Solve(Wyndor(), upper: [1, double.PositiveInfinity]);
        Assert.That(r.Objective, Is.EqualTo(33).Within(1e-9));   // x = 1, y = 6
    }

    [Test] public void Equality_And_GreaterOrEqual_Constraints()
    {
        // max x + 2y; x + y = 4; x >= 1; y <= 2 (variable bound)  -> 6 at (2, 2)
        var m = new IntegerModel();
        var x = m.AddVariable(0, Inf, false, "x");
        var y = m.AddVariable(0, 2, false, "y");
        m.AddConstraint([(x, 1), (y, 1)], Sense.Equal, 4);
        m.AddConstraint([(x, 1)], Sense.GreaterOrEqual, 1);
        m.SetObjective([(x, 1), (y, 2)]);
        var r = SimplexSolver.Solve(m);
        Assert.That(r.Objective, Is.EqualTo(6).Within(1e-9));
        Assert.That(r.Values, Is.EqualTo(new[] { 2.0, 2.0 }).Within(1e-9));
    }

    [Test] public void Detects_Infeasible()
    {
        var m = new IntegerModel();
        var x = m.AddVariable(0, Inf, false, "x");
        var y = m.AddVariable(0, Inf, false, "y");
        m.AddConstraint([(x, 1), (y, 1)], Sense.LessOrEqual, 1);
        m.AddConstraint([(x, 1), (y, 1)], Sense.GreaterOrEqual, 3);
        m.SetObjective([(x, 1)]);
        Assert.That(SimplexSolver.Solve(m).Status, Is.EqualTo(LpStatus.Infeasible));
    }

    [Test] public void Detects_Unbounded()
    {
        var m = new IntegerModel();
        var x = m.AddVariable(0, Inf, false, "x");
        var y = m.AddVariable(0, Inf, false, "y");
        m.AddConstraint([(x, 1), (y, -1)], Sense.LessOrEqual, 1);
        m.SetObjective([(x, 1)]);
        Assert.That(SimplexSolver.Solve(m).Status, Is.EqualTo(LpStatus.Unbounded));
    }

    [Test] public void Beale_Cycling_Example_Terminates_At_Optimum()
    {
        // Beale's example scaled to integers: max 3a - 80b + 2c - 24d
        // a - 32b - 4c + 36d <= 0;  a - 24b - c + 6d <= 0;  c <= 1   -> 5 at a = 1, c = 1
        var m = new IntegerModel();
        var a = m.AddVariable(0, Inf, false, "a");
        var b = m.AddVariable(0, Inf, false, "b");
        var c = m.AddVariable(0, Inf, false, "c");
        var d = m.AddVariable(0, Inf, false, "d");
        m.AddConstraint([(a, 1), (b, -32), (c, -4), (d, 36)], Sense.LessOrEqual, 0);
        m.AddConstraint([(a, 1), (b, -24), (c, -1), (d, 6)], Sense.LessOrEqual, 0);
        m.AddConstraint([(c, 1)], Sense.LessOrEqual, 1);
        m.SetObjective([(a, 3), (b, -80), (c, 2), (d, -24)]);
        var r = SimplexSolver.Solve(m);
        Assert.That(r.Status, Is.EqualTo(LpStatus.Optimal));
        Assert.That(r.Objective, Is.EqualTo(5).Within(1e-9));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~SimplexSolverTests`
Expected: build FAIL (`SimplexSolver` not found).

- [ ] **Step 3: Implement `SimplexSolver.Solve`**

Algorithm (the tests do not determine it):
- Rows: one per constraint, `Σ a_ij x_j + s_i = rhs_i` with slack bounds `≤ → s ∈ [0, ∞)`, `≥ → s ∈ (−∞, 0]`, `= → s ∈ [0, 0]`.
- Every nonbasic variable sits at its lower or upper bound (all lower bounds are finite). Start with structurals and slacks at a finite bound (lower if finite, else upper, else 0); one artificial per row with sign chosen so its value `|residual|` ≥ 0 forms the initial basis.
- Phase 1 maximises `−Σ artificials`; if the optimum is below `−1e-9` ⇒ `Infeasible`. Pivot remaining zero-valued artificials out where possible, then fix artificials at 0.
- Phase 2 maximises the model objective on the same dense tableau.
- Bounded ratio test: the entering variable may also stop by reaching its own opposite bound (bound flip, no basis change). Unlimited step ⇒ `Unbounded`.
- Entering choice: Dantzig (largest improving reduced cost, sign-aware for variables at upper bound); after 50 consecutive degenerate pivots switch to Bland (lowest eligible index entering, lowest index among tied leaving rows) until a non-degenerate pivot.
- Pivot counter across both phases; > 50 000 ⇒ `InvalidOperationException("simplex pivot limit exceeded")`.

- [ ] **Step 4: Run all tests**

Run: `dotnet test Rummikub.Tests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Rummikub.Solver/Mip Rummikub.Tests/Mip
git commit -m "Add bounded two-phase simplex with Bland fallback"
```

---

### Task 3: BranchAndBound

**Files:**
- Create: `Rummikub.Solver/Mip/BranchAndBound.cs`, `Rummikub.Solver/Mip/MipSolution.cs`
- Test: `Rummikub.Tests/Mip/BranchAndBoundTests.cs`

**Interfaces:**
- Consumes: `IntegerModel`, `SimplexSolver`, `LpResult` (Tasks 1–2).
- Produces (`internal`):
  - `sealed record MipSolution(long[] Values, long Objective, bool IsOptimal)`
  - `static class BranchAndBound { public static MipSolution Solve(IntegerModel model, long[] incumbentValues, int maxNodes, CancellationToken cancellationToken); }`
  - `incumbentValues` must satisfy `model.IsFeasible` (else `ArgumentException`); its objective is computed with `model.Evaluate` (refines spec §4, which also passed the objective separately).
  - Node limit reached or token cancelled ⇒ best incumbent with `IsOptimal = false`. An already-cancelled token returns the given incumbent without solving.

- [ ] **Step 1: Write the failing tests**

```csharp
using Rummikub.Solver.Mip;
namespace Rummikub.Tests.Mip;

[TestFixture]
public class BranchAndBoundTests
{
    static IntegerModel Knapsack()
    {
        // max 5a + 4b + 3c; 2a + 3b + c <= 5; binary  -> 9 at (1, 1, 0)
        var m = new IntegerModel();
        var a = m.AddVariable(0, 1, true, "a");
        var b = m.AddVariable(0, 1, true, "b");
        var c = m.AddVariable(0, 1, true, "c");
        m.AddConstraint([(a, 2), (b, 3), (c, 1)], Sense.LessOrEqual, 5);
        m.SetObjective([(a, 5), (b, 4), (c, 3)]);
        return m;
    }

    static IntegerModel OddCycle()
    {
        // max x1 + x2 + x3; pairwise sums <= 1; binary. LP = 1.5, IP = 1
        var m = new IntegerModel();
        var x = Enumerable.Range(0, 3).Select(i => m.AddVariable(0, 1, true, $"x{i}")).ToArray();
        m.AddConstraint([(x[0], 1), (x[1], 1)], Sense.LessOrEqual, 1);
        m.AddConstraint([(x[1], 1), (x[2], 1)], Sense.LessOrEqual, 1);
        m.AddConstraint([(x[0], 1), (x[2], 1)], Sense.LessOrEqual, 1);
        m.SetObjective([(x[0], 1), (x[1], 1), (x[2], 1)]);
        return m;
    }

    [Test] public void Solves_Knapsack()
    {
        var s = BranchAndBound.Solve(Knapsack(), [0, 0, 0], 1000, CancellationToken.None);
        Assert.That(s.Objective, Is.EqualTo(9));
        Assert.That(s.Values, Is.EqualTo(new long[] { 1, 1, 0 }));
        Assert.That(s.IsOptimal, Is.True);
    }

    [Test] public void Resolves_Fractional_Root()
    {
        var s = BranchAndBound.Solve(OddCycle(), [0, 0, 0], 1000, CancellationToken.None);
        Assert.That(s.Objective, Is.EqualTo(1));
        Assert.That(s.IsOptimal, Is.True);
    }

    [Test] public void General_Integer_Bounds()
    {
        // max x + y; 2x + 2y <= 7; x, y in [0, 10] integer -> 3
        var m = new IntegerModel();
        var x = m.AddVariable(0, 10, true, "x");
        var y = m.AddVariable(0, 10, true, "y");
        m.AddConstraint([(x, 2), (y, 2)], Sense.LessOrEqual, 7);
        m.SetObjective([(x, 1), (y, 1)]);
        Assert.That(BranchAndBound.Solve(m, [0, 0], 1000, CancellationToken.None).Objective, Is.EqualTo(3));
    }

    [Test] public void Node_Limit_Returns_Feasible_Non_Optimal()
    {
        var m = OddCycle();
        var s = BranchAndBound.Solve(m, [0, 0, 0], 1, CancellationToken.None);
        Assert.That(s.IsOptimal, Is.False);
        Assert.That(m.IsFeasible(s.Values), Is.True);
    }

    [Test] public void Cancelled_Token_Returns_Incumbent()
    {
        var s = BranchAndBound.Solve(Knapsack(), [0, 0, 1], 1000, new CancellationToken(true));
        Assert.That(s.Values, Is.EqualTo(new long[] { 0, 0, 1 }));
        Assert.That(s.Objective, Is.EqualTo(3));
        Assert.That(s.IsOptimal, Is.False);
    }

    [Test] public void Infeasible_Incumbent_Throws() =>
        Assert.Throws<ArgumentException>(() => BranchAndBound.Solve(Knapsack(), [1, 1, 1], 1000, CancellationToken.None));
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~BranchAndBoundTests`
Expected: build FAIL (`BranchAndBound` not found).

- [ ] **Step 3: Implement `BranchAndBound.Solve`**

Depth-first stack of `(double[] lower, double[] upper)` bound sets, starting from the model bounds. Per node: count it (stop at `maxNodes`), check cancellation, solve LP; skip if `Infeasible`; prune if `⌊obj + 1e-6⌋ ≤ incumbent`; if all integer variables are within `1e-6` of an integer, round, check `model.IsFeasible` and `Evaluate`, keep if better; else branch on the most fractional integer variable (ties: lowest index), pushing the "≤ ⌊v⌋" child first so the "≥ ⌈v⌉" child is popped first. An `Unbounded` LP ⇒ `InvalidOperationException` (Rummikub models are bounded).

- [ ] **Step 4: Run all tests**

Run: `dotnet test Rummikub.Tests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Rummikub.Solver/Mip Rummikub.Tests/Mip
git commit -m "Add depth-first branch and bound over the simplex"
```

---

### Task 4: TileCounts and GroupCatalog

**Files:**
- Create: `Rummikub.Solver/Model/TileCounts.cs`, `Rummikub.Solver/Model/GroupCatalog.cs`
- Test: `Rummikub.Tests/Model/TileCountsTests.cs`, `Rummikub.Tests/Model/GroupCatalogTests.cs`

**Interfaces:**
- Consumes: `Tile`, `Group`, `Run.Create`, `Set.Create`, `Hand`, `BoardState` (Core).
- Produces (`internal`, namespace `Rummikub.Solver.Model`):
  - `sealed class TileCounts` — `const int Kinds = 53`; `static TileCounts From(IEnumerable<Tile> tiles)`; `int this[int id]`; `int Total`.
  - `static class GroupCatalog` — `IReadOnlyList<Group> All` (sets first, then runs; built once); `static int Points(Group g)` = sum of `Slots[i].Value`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Rummikub.Core;
using Rummikub.Core.Common;
using Rummikub.Solver.Model;
namespace Rummikub.Tests.Model;

[TestFixture]
public class TileCountsTests
{
    [Test] public void Counts_By_Tile_Id()
    {
        var c = TileCounts.From([Tile.Of(Color.Red, 5), Tile.Of(Color.Red, 5), Tile.Joker]);
        Assert.That(c[Tile.Of(Color.Red, 5).Id], Is.EqualTo(2));
        Assert.That(c[Tile.Joker.Id], Is.EqualTo(1));
        Assert.That(c.Total, Is.EqualTo(3));
    }
}

[TestFixture]
public class GroupCatalogTests
{
    [Test] public void Has_Expected_Counts()
    {
        Assert.That(GroupCatalog.All.OfType<Set>().Count(), Is.EqualTo(281));
        Assert.That(GroupCatalog.All.OfType<Run>().Count(), Is.EqualTo(892));
        Assert.That(GroupCatalog.All, Has.Count.EqualTo(1173));
    }

    [Test] public void Tile_Multisets_Are_Unique() =>
        Assert.That(GroupCatalog.All.Select(g => g.Tiles.Order().ToEquatableArray()), Is.Unique);

    [Test] public void Runs_Are_Three_To_Five_Long() =>
        Assert.That(GroupCatalog.All.OfType<Run>().Select(r => r.Length), Is.All.InRange(3, 5));

    [Test] public void Sets_Come_Before_Runs() =>
        Assert.That(GroupCatalog.All.Select(g => g is Set ? 0 : 1), Is.Ordered);

    [Test] public void Collapsed_Reading_Keeps_Highest_Points()
    {
        var key = new[] { Tile.Of(Color.Red, 9), Tile.Of(Color.Red, 10), Tile.Joker }.Order().ToEquatableArray();
        var g = GroupCatalog.All.Single(x => x.Tiles.Order().ToEquatableArray() == key);
        Assert.That(g, Is.EqualTo(Run.Create(Color.Red, 9, 3, 0b100)));
        Assert.That(GroupCatalog.Points(g), Is.EqualTo(30));
    }

    static Group Reading(params Tile[] tiles) =>
        GroupCatalog.All.Single(x => x.Tiles.Order().ToEquatableArray() == tiles.Order().ToEquatableArray());

    [Test] public void One_Tile_Two_Jokers_Uses_Highest_Points_Reading()
    {
        // B4 J J: run B4-B6 (15) beats set of 4s (12); K13 J J: set of 13s (39) beats run K11-K13 (36)
        Assert.That(Reading(Tile.Of(Color.Blue, 4), Tile.Joker, Tile.Joker), Is.EqualTo(Run.Create(Color.Blue, 4, 3, 0b110)));
        Assert.That(Reading(Tile.Of(Color.Black, 13), Tile.Joker, Tile.Joker), Is.InstanceOf<Set>());
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~Model`
Expected: build FAIL.

- [ ] **Step 3: Implement `TileCounts` and `GroupCatalog`**

Enumerate sets (values 1–13, colour masks with 3–4 bits, joker-colour subsets with ≤ 2 bits) then runs (colours, lengths 3–5, starts `1..14−length`, joker masks with ≤ 2 bits); group by tile multiset preserving first-seen order; within a group pick max `Points`, ties first seen. Use `Lazy<IReadOnlyList<Group>>`.

- [ ] **Step 4: Run all tests**

Run: `dotnet test Rummikub.Tests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Rummikub.Solver/Model Rummikub.Tests/Model
git commit -m "Add tile counts and the 1173-group catalogue"
```

---

### Task 5: Options, model builder, decoding and verification

**Files:**
- Modify: `Rummikub.Solver/Solver/SolverOptions.cs` (rewrite)
- Create: `Rummikub.Solver/Solver/SolverObjective.cs`, `Rummikub.Solver/Model/ObjectiveLevel.cs`, `Rummikub.Solver/Model/RummikubModel.cs`, `Rummikub.Solver/Model/RummikubModelBuilder.cs`, `Rummikub.Solver/Model/DecodedPlay.cs`, `Rummikub.Solver/Model/SolutionVerifier.cs`
- Test: `Rummikub.Tests/Model/RummikubModelBuilderTests.cs`, `Rummikub.Tests/Model/SolutionVerifierTests.cs`

**Interfaces:**
- Consumes: Tasks 1, 4; `Hand`, `BoardState`, `Group`.
- Produces:
  - Public (namespace `Rummikub.Solver.Solver`): `enum SolverObjective { TileCount, Points }`; `sealed record SolverOptions` with `init` properties and the Global Constraints defaults.
  - Internal (namespace `Rummikub.Solver.Model`):
    - `sealed record ObjectiveLevel(string Name, IReadOnlyList<(int Var, long Coef)> Terms, long Min, long Max)`
    - `sealed record DecodedPlay(IReadOnlyList<Group> KeptGroups, IReadOnlyList<Group> NewGroups, IReadOnlyList<Tile> PlayedTiles)`
    - `sealed class RummikubModel` — `IntegerModel Model`; `IReadOnlyList<(int Var, Group Group)> GroupVars`; `IReadOnlyList<(int Var, Group Group, int Copies)> KeptVars`; `IReadOnlyList<int> PlayedVars` (index = `Tile.Id`, 53 entries); `int? MeldVar`; `List<ObjectiveLevel> Levels` (appendable — strategy hook 1); `void PackObjective()`; `long[] KeepEverything()`; `DecodedPlay Decode(IReadOnlyList<long> values)`.
    - `static class RummikubModelBuilder { public static RummikubModel Build(Hand hand, BoardState board, SolverOptions options); }`
    - `static class SolutionVerifier { public static void Verify(Hand hand, BoardState board, SolverOptions options, DecodedPlay play); }` — throws `InvalidOperationException` naming the violated rule.
  - Level names: `"Primary"`, `"JokersKept"`, `"GroupsKept"`. `PackObjective` multiplies level `i` by `Π_{j>i}(Max_j − Min_j + 1)` and calls `Model.SetObjective`.
  - Primary `Max` = `Σ_t w_t·hand_t`; JokersKept range `−hand_joker..0`; GroupsKept range `0..Σ mult_b`.
  - Column upper bound `ub_g` per spec §2; columns with `ub_g = 0` are not added.

- [ ] **Step 1: Write the failing tests**

`RummikubModelBuilderTests.cs`:

```csharp
using Rummikub.Core;
using Rummikub.Solver.Model;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Model;

[TestFixture]
public class RummikubModelBuilderTests
{
    static Tile R(int v) => Tile.Of(Color.Red, v);
    static readonly SolverOptions Default = new();

    [Test] public void Default_Options()
    {
        var o = new SolverOptions();
        Assert.Multiple(() =>
        {
            Assert.That(o.InitialMeldDone, Is.True);
            Assert.That(o.InitialMeldPoints, Is.EqualTo(30));
            Assert.That(o.Objective, Is.EqualTo(SolverObjective.TileCount));
            Assert.That(o.KeepJokers, Is.False);
            Assert.That(o.MaxNodes, Is.EqualTo(200_000));
        });
    }

    [Test] public void Keep_Everything_Is_Feasible()
    {
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 8, 0), Set.Create(5, 0b0111, 0)]);
        var m = RummikubModelBuilder.Build(Hand.Of([R(3)]), board, Default);
        Assert.That(m.Model.IsFeasible(m.KeepEverything()), Is.True);
        Assert.That(m.Decode(m.KeepEverything()).KeptGroups, Is.EquivalentTo(board.Groups));
    }

    [Test] public void Identical_Board_Groups_Share_One_Kept_Column()
    {
        var run = Run.Create(Color.Red, 1, 3, 0);
        var m = RummikubModelBuilder.Build(Hand.Empty, BoardState.Of([run, run]), Default);
        Assert.That(m.KeptVars, Has.Count.EqualTo(1));
        Assert.That(m.KeptVars[0].Copies, Is.EqualTo(2));
    }

    [Test] public void Unavailable_Columns_Are_Omitted() =>
        Assert.That(RummikubModelBuilder.Build(Hand.Of([R(1), R(2), R(3)]), BoardState.Empty, Default)
            .GroupVars.Select(v => v.Group), Is.EquivalentTo(new Group[] { Run.Create(Color.Red, 1, 3, 0) }));

    [Test] public void Level_Order_Depends_On_KeepJokers()
    {
        var plain = RummikubModelBuilder.Build(Hand.Of([R(1)]), BoardState.Empty, Default);
        var keep = RummikubModelBuilder.Build(Hand.Of([R(1)]), BoardState.Empty, Default with { KeepJokers = true });
        Assert.That(plain.Levels.Select(l => l.Name), Is.EqualTo(new[] { "Primary", "GroupsKept" }));
        Assert.That(keep.Levels.Select(l => l.Name), Is.EqualTo(new[] { "Primary", "JokersKept", "GroupsKept" }));
    }

    [Test] public void Packing_Multiplies_Higher_Levels()
    {
        // hand: 2 tiles (Primary 0..2); board: 3 groups (GroupsKept 0..3) -> played tile coef 4, kept coef 1
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 3, 0), Run.Create(Color.Blue, 5, 3, 0), Run.Create(Color.Blue, 9, 3, 0)]);
        var m = RummikubModelBuilder.Build(Hand.Of([R(1), R(2)]), board, Default);
        m.PackObjective();
        var coef = m.Model.Objective.ToDictionary(t => t.Var, t => t.Coef);
        Assert.That(coef[m.PlayedVars[R(1).Id]], Is.EqualTo(4));
        Assert.That(m.KeptVars.Select(k => coef[k.Var]), Is.All.EqualTo(1));
    }

    [Test] public void Points_Weights_Joker_30_Unless_Kept()
    {
        var hand = Hand.Of([Tile.Of(Color.Red, 12), Tile.Joker]);
        var pts = RummikubModelBuilder.Build(hand, BoardState.Empty, Default with { Objective = SolverObjective.Points });
        var w = pts.Levels[0].Terms.ToDictionary(t => t.Var, t => t.Coef);
        Assert.That(w[pts.PlayedVars[Tile.Joker.Id]], Is.EqualTo(30));
        Assert.That(w[pts.PlayedVars[Tile.Of(Color.Red, 12).Id]], Is.EqualTo(12));

        var keep = RummikubModelBuilder.Build(hand, BoardState.Empty, Default with { Objective = SolverObjective.Points, KeepJokers = true });
        Assert.That(keep.Levels[0].Terms.Where(t => t.Var == keep.PlayedVars[Tile.Joker.Id]).Sum(t => t.Coef), Is.EqualTo(0));
    }

    [Test] public void Initial_Meld_Adds_Meld_Variable() =>
        Assert.That(RummikubModelBuilder.Build(Hand.Of([R(1)]), BoardState.Empty, Default with { InitialMeldDone = false }).MeldVar, Is.Not.Null);
}
```

`SolutionVerifierTests.cs`:

```csharp
using Rummikub.Core;
using Rummikub.Solver.Model;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Model;

[TestFixture]
public class SolutionVerifierTests
{
    static Tile R(int v) => Tile.Of(Color.Red, v);
    static readonly Group Board = Run.Create(Color.Red, 1, 3, 0);
    static readonly SolverOptions Default = new();

    [Test] public void Accepts_Valid_Play() =>
        Assert.DoesNotThrow(() => SolutionVerifier.Verify(Hand.Of([R(4)]), BoardState.Of([Board]), Default,
            new DecodedPlay([], [Run.Create(Color.Red, 1, 4, 0)], [R(4)])));

    [Test] public void Rejects_Tile_Not_In_Hand() =>
        Assert.Throws<InvalidOperationException>(() => SolutionVerifier.Verify(Hand.Empty, BoardState.Of([Board]), Default,
            new DecodedPlay([], [Run.Create(Color.Red, 1, 4, 0)], [R(4)])));

    [Test] public void Rejects_Dropped_Board_Tile() =>
        Assert.Throws<InvalidOperationException>(() => SolutionVerifier.Verify(Hand.Empty, BoardState.Of([Run.Create(Color.Red, 1, 4, 0)]), Default,
            new DecodedPlay([], [Board], [])));

    [Test] public void Rejects_Meld_Below_Threshold() =>
        Assert.Throws<InvalidOperationException>(() => SolutionVerifier.Verify(Hand.Of([R(4), R(5), R(6)]), BoardState.Empty,
            Default with { InitialMeldDone = false }, new DecodedPlay([], [Run.Create(Color.Red, 4, 3, 0)], [R(4), R(5), R(6)])));

    [Test] public void Rejects_Meld_That_Changes_Board() =>
        Assert.Throws<InvalidOperationException>(() => SolutionVerifier.Verify(Hand.Of([R(4)]), BoardState.Of([Board]),
            Default with { InitialMeldDone = false, InitialMeldPoints = 0 }, new DecodedPlay([], [Run.Create(Color.Red, 1, 4, 0)], [R(4)])));
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~Rummikub.Tests.Model`
Expected: build FAIL.

- [ ] **Step 3: Implement options, builder, model, decode and verifier**

Verifier works on domain objects only (independent of the model): tiles of `KeptGroups + NewGroups` must equal board tiles + `PlayedTiles` as multisets; `PlayedTiles` ⊆ hand; `KeptGroups` ⊆ board groups; with `InitialMeldDone == false`: `KeptGroups` equals all board groups and `NewGroups` is empty or `Σ GroupCatalog.Points ≥ InitialMeldPoints`.

- [ ] **Step 4: Run all tests**

Run: `dotnet test Rummikub.Tests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Rummikub.Solver Rummikub.Tests/Model
git commit -m "Add Rummikub model builder, options, decoding and exact verifier"
```

---

### Task 6: Solver facade, run merging, DFS demoted to reference

**Files:**
- Move: `Rummikub.Solver/Solver/RummikubSolver.cs` → `Rummikub.Solver/Reference/DfsReferenceSolver.cs` (class renamed, `internal`, keeps `SolveMaxTiles`); `GroupGenerator.cs`, `Memoization/StateHasher.cs` stay where they are.
- Create: `Rummikub.Solver/Solver/RummikubSolver.cs` (new facade), `Rummikub.Solver/Model/RunMerger.cs`
- Modify: `Rummikub.Solver/Solver/SolverResult.cs`, `Rummikub.Server/Controllers/SolverController.cs` (call `Solve`), `Rummikub.Runner/Program.cs`, `Rummikub.Tests/Solver/RummikubSolverTests.cs` (`SolveMaxTiles` → `Solve`)
- Test: `Rummikub.Tests/Model/RunMergerTests.cs`, `Rummikub.Tests/Solver/SolverOptionsBehaviourTests.cs`, `Rummikub.Tests/Solver/RandomGames.cs`, `Rummikub.Tests/Solver/SolverOracleTests.cs`, `Rummikub.Tests/Solver/SolverPerformanceTests.cs`

**Interfaces:**
- Consumes: Tasks 1–5.
- Produces:
  - `public sealed class RummikubSolver { public SolverResult Solve(Hand hand, BoardState board, SolverOptions? options = null, CancellationToken cancellationToken = default); }` — build → `PackObjective` → `BranchAndBound.Solve(model, KeepEverything(), options.MaxNodes, token)` → `Decode` → merge new runs → `SolutionVerifier.Verify` → result.
  - `public sealed record SolverResult(BoardState Board, EquatableArray<Tile> PlayedTiles, bool IsOptimal)`; `static SolverResult Create(BoardState board, IEnumerable<Tile> playedTiles, bool isOptimal)`; `Empty` is optimal.
  - `internal static class RunMerger { public static IReadOnlyList<Group> Merge(IEnumerable<Group> newGroups); }` — joins runs of one colour where one ends at `v` and the next starts at `v + 1`, only if the result has ≤ 2 jokers and length ≤ 13; repeats until stable; non-runs pass through.
  - `internal sealed class DfsReferenceSolver { public SolverResult SolveMaxTiles(Hand hand, BoardState board); }` (returns `IsOptimal = true`).
  - Test helper `static class RandomGames { public static (Hand Hand, BoardState Board) Deal(int seed, int boardTiles, int handTiles); }` — 106-tile deck, board built from random valid runs (length 3–5) and sets (3–4) until ≥ `boardTiles`, hand = next `handTiles` tiles of the shuffled rest.

- [ ] **Step 1: Write the failing tests**

`RunMergerTests.cs`:

```csharp
using Rummikub.Core;
using Rummikub.Solver.Model;
namespace Rummikub.Tests.Model;

[TestFixture]
public class RunMergerTests
{
    [Test] public void Merges_Touching_Runs() =>
        Assert.That(RunMerger.Merge([Run.Create(Color.Red, 1, 3, 0), Run.Create(Color.Red, 4, 4, 0)]),
            Is.EqualTo(new Group[] { Run.Create(Color.Red, 1, 7, 0) }));

    [Test] public void Keeps_Gapped_Or_Other_Colour_Runs() =>
        Assert.That(RunMerger.Merge([Run.Create(Color.Red, 1, 3, 0), Run.Create(Color.Red, 5, 3, 0), Run.Create(Color.Blue, 4, 3, 0)]),
            Has.Count.EqualTo(3));

    [Test] public void Does_Not_Exceed_Two_Jokers() =>
        Assert.That(RunMerger.Merge([Run.Create(Color.Red, 1, 3, 0b011), Run.Create(Color.Red, 4, 3, 0b001)]), Has.Count.EqualTo(2));
}
```

`SolverOptionsBehaviourTests.cs`:

```csharp
using Rummikub.Core;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Solver;

[TestFixture]
public class SolverOptionsBehaviourTests
{
    static Tile R(int v) => Tile.Of(Color.Red, v);
    static readonly SolverOptions Meld = new() { InitialMeldDone = false };
    readonly RummikubSolver _solver = new();

    [Test] public void Meld_At_Threshold_Plays()
    {
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 3, 0)]);
        var r = _solver.Solve(Hand.Of([R(9), R(10), R(11)]), board, Meld);
        Assert.That(r.PlayedTiles, Has.Count.EqualTo(3));
        Assert.That(r.Board.Groups, Does.Contain(Run.Create(Color.Blue, 1, 3, 0)));
    }

    [Test] public void Meld_Below_Threshold_Plays_Nothing()
    {
        var board = BoardState.Of([Run.Create(Color.Blue, 1, 3, 0)]);
        var r = _solver.Solve(Hand.Of([R(1), R(2), R(3)]), board, Meld);
        Assert.That(r.PlayedTiles, Is.Empty);
        Assert.That(r.Board, Is.EqualTo(board));
    }

    [Test] public void Meld_Joker_Counts_Best_Reading()
    {
        var r = _solver.Solve(Hand.Of([R(9), R(10), Tile.Joker]), BoardState.Empty, Meld);
        Assert.That(r.Board.Groups, Is.EqualTo(new Group[] { Run.Create(Color.Red, 9, 3, 0b100) }));
    }

    [Test] public void Meld_Cannot_Use_Board()
    {
        var board = BoardState.Of([Run.Create(Color.Red, 4, 3, 0)]);
        Assert.That(_solver.Solve(Hand.Of([R(7)]), board, Meld).PlayedTiles, Is.Empty);
        Assert.That(_solver.Solve(Hand.Of([R(7)]), board).PlayedTiles, Has.Count.EqualTo(1));
    }

    [Test] public void Points_And_TileCount_Choose_Differently()
    {
        var hand = Hand.Of([R(1), R(2), R(4), Tile.Joker, Tile.Of(Color.Blue, 13), Tile.Of(Color.Black, 13)]);
        var count = _solver.Solve(hand, BoardState.Empty);
        var points = _solver.Solve(hand, BoardState.Empty, new SolverOptions { Objective = SolverObjective.Points });
        Assert.That(count.PlayedTiles, Has.Count.EqualTo(4).And.Contains(R(4)));
        Assert.That(points.PlayedTiles, Is.EquivalentTo(new[] { Tile.Of(Color.Blue, 13), Tile.Of(Color.Black, 13), Tile.Joker }));
    }

    [Test] public void KeepJokers_Keeps_Unneeded_Joker()
    {
        var hand = Hand.Of([R(5), R(6), R(7), Tile.Joker]);
        Assert.That(_solver.Solve(hand, BoardState.Empty).PlayedTiles, Has.Count.EqualTo(4));
        var kept = _solver.Solve(hand, BoardState.Empty, new SolverOptions { KeepJokers = true });
        Assert.That(kept.PlayedTiles, Has.Count.EqualTo(3).And.Not.Contains(Tile.Joker));
    }

    [Test] public void KeepJokers_With_Three_Jokers_Does_Not_Overflow()
    {
        var r = _solver.Solve(Hand.Of([Tile.Joker, Tile.Joker, Tile.Joker, R(5)]), BoardState.Empty, new SolverOptions { KeepJokers = true });
        Assert.That(r.PlayedTiles, Has.Count.EqualTo(3));
        Assert.That(r.IsOptimal, Is.True);
    }

    [Test] public void Least_Rearrangement_Keeps_Board_Group()
    {
        var set = Set.Create(5, 0b1111, 0);
        var board = BoardState.Of([set, Run.Create(Color.Red, 6, 3, 0)]);
        var r = _solver.Solve(Hand.Of([R(9)]), board);
        Assert.That(r.PlayedTiles, Has.Count.EqualTo(1));
        Assert.That(r.Board.Groups, Does.Contain(set));
    }

    [Test] public void Long_Board_Run_Stays_Intact()
    {
        var board = BoardState.Of([Run.Create(Color.Red, 1, 8, 0)]);
        var r = _solver.Solve(Hand.Of([Tile.Of(Color.Blue, 13)]), board);
        Assert.That(r.Board, Is.EqualTo(board));
    }

    [Test] public void Cancelled_Token_Returns_Keep_Everything()
    {
        var board = BoardState.Of([Run.Create(Color.Red, 1, 3, 0)]);
        var r = _solver.Solve(Hand.Of([R(4)]), board, cancellationToken: new CancellationToken(true));
        Assert.That(r.PlayedTiles, Is.Empty);
        Assert.That(r.Board, Is.EqualTo(board));
        Assert.That(r.IsOptimal, Is.False);
    }
}
```

`SolverOracleTests.cs`:

```csharp
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Solver;

[TestFixture]
public class SolverOracleTests
{
    [Test] public void Matches_Dfs_On_Small_Random_Games([Range(0, 199)] int seed)
    {
        var (hand, board) = RandomGames.Deal(seed, boardTiles: 3 + seed % 18, handTiles: 1 + seed % 8);
        var expected = new DfsReferenceSolver().SolveMaxTiles(hand, board).PlayedTiles.Count;
        var actual = new RummikubSolver().Solve(hand, board);
        Assert.That(actual.PlayedTiles, Has.Count.EqualTo(expected));
        Assert.That(actual.IsOptimal, Is.True);
    }
}
```

`SolverPerformanceTests.cs`:

```csharp
using System.Diagnostics;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Solver;

[TestFixture, Category("Performance")]
public class SolverPerformanceTests
{
    [Test] public void Full_Game_Positions_Solve_Under_A_Second([Range(0, 4)] int seed)
    {
        var (hand, board) = RandomGames.Deal(1000 + seed, boardTiles: 60 + seed * 7, handTiles: 14);
        new RummikubSolver().Solve(hand, BoardState.Empty); // warm-up (JIT, catalogue)
        var sw = Stopwatch.StartNew();
        var r = new RummikubSolver().Solve(hand, board);
        sw.Stop();
        Assert.That(r.IsOptimal, Is.True);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000));
    }
}
```

(Add `using Rummikub.Core;` where `BoardState` is used.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Rummikub.Tests`
Expected: build FAIL (`Solve`, `RunMerger`, `DfsReferenceSolver`, `RandomGames` missing).

- [ ] **Step 3: Demote the DFS, build the facade and `RunMerger`, update callers**

Move/rename the old solver as listed; it constructs results with `SolverResult.Create(..., isOptimal: true)`. Port `RummikubSolverTests` to `Solve` (expectations unchanged). Runner and controller call `Solve`.

- [ ] **Step 4: Run all tests (Debug and Release)**

Run: `dotnet test Rummikub.Tests` then `dotnet test Rummikub.Tests -c Release --filter Category=Performance`
Expected: all pass in both runs.

- [ ] **Step 5: Commit**

```bash
git add -A Rummikub.Solver Rummikub.Server Rummikub.Runner Rummikub.Tests
git commit -m "Switch to the ILP solver; keep the DFS as a test oracle"
```

---

### Task 7: Options and optimality through the API and client

**Files:**
- Modify: `Rummikub.Server/Models/RummikubDtos.cs`, `Rummikub.Server/Mapping/DtoMapper.cs`, `Rummikub.Server/Controllers/SolverController.cs`, `Rummikub.Server/Rummikub.Server.http`, `Rummikub.Tests/Server/DtoMapperTests.cs`, `rummikub-client/src/App.tsx`

**Interfaces:**
- Consumes: `SolverOptions`, `SolverObjective`, `SolverResult.IsOptimal` (Tasks 5–6).
- Produces:
  - `SolveOptionsDto { bool? InitialMeldDone; string? Objective; bool? KeepJokers }`; `SolveRequest.Options` (`SolveOptionsDto?`); `SolveResponse.Optimal` (`bool`).
  - `DtoMapper.TryMapRequest(SolveRequest request, out Hand? hand, out BoardState? board, out SolverOptions? options, out IReadOnlyDictionary<string, string> errors)` — missing options/fields ⇒ `SolverOptions` defaults; objective parsed with the existing `TryParseEnum` (`"tilecount"`, `"Points"` …); unknown ⇒ error key `options.objective`, message `unknown objective 'x'`.
  - Controller: `_solver.Solve(hand, board, options, HttpContext.RequestAborted)`.

- [ ] **Step 1: Write the failing tests (add to `DtoMapperTests`, update existing calls to the new `out` parameter)**

```csharp
[Test]
public void Missing_Options_Use_Defaults()
{
    Assert.That(DtoMapper.TryMapRequest(new SolveRequest(), out _, out _, out var options, out _), Is.True);
    Assert.That(options, Is.EqualTo(new SolverOptions()));
}

[Test]
public void Maps_Options()
{
    var req = new SolveRequest { Options = new SolveOptionsDto { InitialMeldDone = false, Objective = "points", KeepJokers = true } };
    Assert.That(DtoMapper.TryMapRequest(req, out _, out _, out var options, out _), Is.True);
    Assert.That(options, Is.EqualTo(new SolverOptions { InitialMeldDone = false, Objective = SolverObjective.Points, KeepJokers = true }));
}

[Test]
public void Unknown_Objective_Is_Reported() =>
    Assert.That(Errors(new SolveRequest { Options = new SolveOptionsDto { Objective = "fun" } }),
        Is.EquivalentTo(new Dictionary<string, string> { ["options.objective"] = "unknown objective 'fun'" }));

[Test]
public void Response_Carries_Optimality() =>
    Assert.That(DtoMapper.ToResponse(SolverResult.Create(BoardState.Empty, [], isOptimal: false)).Optimal, Is.False);
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~DtoMapperTests`
Expected: build FAIL.

- [ ] **Step 3: Implement DTOs, mapping, controller, `.http` sample (add `"options": { "objective": "points" }`)**

- [ ] **Step 4: Update the client**

`SolveRequest` gains `options: { initialMeldDone: boolean; objective: 'TileCount' | 'Points'; keepJokers: boolean }`; `SolveResponse` gains `optimal: boolean`. Builder panel gets an "Initial meld made" checkbox (default checked), an Objective select (Tile count / Points), a "Keep jokers" checkbox. The result panel shows "Not proven optimal" when `optimal === false`.

- [ ] **Step 5: Verify**

Run: `dotnet test Rummikub.Tests` — all pass. Run (in `rummikub-client`): `npm run build` and `npm run lint` — both exit 0.

- [ ] **Step 6: Commit**

```bash
git add -A Rummikub.Server Rummikub.Tests rummikub-client/src
git commit -m "Expose solver options and optimality in API and client"
```

---

### Task 8: Final verification

- [ ] **Step 1:** `dotnet build RummikubSolver.sln --no-incremental` → `Ostrzeżenia: 0`, `Liczba błędów: 0`.
- [ ] **Step 2:** `dotnet test RummikubSolver.sln` → all pass; `dotnet test Rummikub.Tests -c Release --filter Category=Performance` → 5/5 pass.
- [ ] **Step 3:** `grep -rn "SolveMaxTiles" --include=*.cs Rummikub.*` → only `Rummikub.Solver/Reference/DfsReferenceSolver.cs` and `Rummikub.Tests/Solver/SolverOracleTests.cs`.
- [ ] **Step 4:** `dotnet run --project Rummikub.Runner` prints a tiles-played count and groups without an exception.
- [ ] **Step 5:** Smoke-test the API (`dotnet run --project Rummikub.Server --launch-profile http`, then POST the `.http` sample with curl): 200 with `"optimal":true`; with `"options":{"objective":"fun"}` → 400 keyed `options.objective`.
