# Domain Model Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Tile/Group/BoardState model with types that cannot represent invalid tiles or groups, record what every joker stands for, and get value equality from compiler-generated record equality instead of hand-written hash codes.

**Architecture:** `Tile` becomes a one-byte record struct; `Group` becomes an abstract record with sealed `Run`/`Set` subtypes whose only fields are primitives and bitmasks. A single `EquatableArray<T>` helper provides sequence equality for `BoardState`, `Hand` and `SolverResult`. The DFS solver is adapted only as far as the new types force. The HTTP API and React client are redesigned around structural groups with joker meaning.

**Tech Stack:** .NET 8, C# latest, NUnit 3, ASP.NET Core 8 controllers, React 19 + Vite + TypeScript.

**Spec:** `docs/superpowers/specs/2026-10-14-domain-model-cleanup-design.md`

## Global Constraints

- Branch: `domain-model-cleanup` (already created; the spec is committed there).
- Colour order and bits: `Red = 0, Blue = 1, Yellow = 2, Black = 3`; mask bit is `1 << (int)color` (Red 1, Blue 2, Yellow 4, Black 8).
- Tile codes: `0` = default/uninitialised, `1..52` = `(int)color * 13 + value`, `53` = joker; `Id = code - 1`.
- At most 2 jokers per group; at least 3 tiles; at least one regular tile.
- Run: `3 <= Length <= 13`, `Start >= 1`, `Start + Length - 1 <= 13`, `JokerMask >> Length == 0`.
- Set: `1 <= Value <= 13`, `3 <= PopCount(Colors) <= 4`, `JokerColors ⊆ Colors`.
- `Run`/`Set` have private constructors, get-only properties; `Slots`, `Tiles`, `Count`, `JokerCount` are computed (no backing field).
- `Create` throws `ArgumentException` (trusted callers); `TryFromTiles` never throws (untrusted input).
- All domain types live in namespace `Rummikub.Core`; generic helpers in `Rummikub.Core.Common`.
- No `GetHashCode` override in `Rummikub.Core` except inside `EquatableArray<T>`.
- DFS search, `MultiSet` (except its equality overrides) and `StateHasher` are not changed in behaviour.
- No new compiler warnings relative to the baseline recorded in Task 1.

## Review Focus

- **Run inference at the low edge** — `R1 R2 J` must become start 1 with the joker as R3 (it cannot extend below 1). Test in Task 4.
- **Run inference at the high edge** — `R12 R13 J` must become start 11 with the joker as R11. Test in Task 4.
- **More than 2 jokers in the pool** (user enters 3) — the generator must skip shapes needing >2 jokers instead of letting `Create` throw. Test in Task 3.
- **Malformed tile JSON** — `joker: true` with a colour, a colour with no value, an unknown colour like `"Green"`, value 14 must each produce a 400 entry at the exact path, not a 500. Test in Task 6.
- **Missing lists / unknown group type** — `hand: null`, `board: null` treated as empty; `type: "pair"` reported at `board[i]`. Test in Task 6.

---

### Task 1: EquatableArray<T>

**Files:**
- Create: `Rummikub.Core/Common/EquatableArray.cs`
- Test: `Rummikub.Tests/Common/EquatableArrayTests.cs`

**Interfaces:**
- Produces:
  - `public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>` with `EquatableArray(ImmutableArray<T> items)`, `static EquatableArray<T> Empty`, `int Count`, `T this[int]`, `GetEnumerator()`, `==`, `!=`, `Equals`, `GetHashCode`.
  - `public static class EquatableArray { public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source); }`
  - `default(EquatableArray<T>)` behaves exactly like `Empty`.

- [ ] **Step 1: Record the warning baseline**

Run: `dotnet build RummikubSolver.sln --no-incremental 2>&1 | grep -c "warning"`
Write the number into the commit message of this task as `baseline-warnings: N`.

- [ ] **Step 2: Write the failing tests**

```csharp
using Rummikub.Core.Common;
namespace Rummikub.Tests.Common;

[TestFixture]
public class EquatableArrayTests
{
    [Test]
    public void Same_Sequence_Is_Equal_With_Same_Hash()
    {
        var a = new[] { 1, 2, 3 }.ToEquatableArray();
        var b = new[] { 1, 2, 3 }.ToEquatableArray();
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a == b, Is.True);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Different_Order_Is_Not_Equal()
    {
        Assert.That(new[] { 1, 2 }.ToEquatableArray() != new[] { 2, 1 }.ToEquatableArray(), Is.True);
    }

    [Test]
    public void Default_Behaves_As_Empty()
    {
        EquatableArray<int> d = default;
        Assert.That(d.Count, Is.EqualTo(0));
        Assert.That(d.ToList(), Is.Empty);
        Assert.That(d, Is.EqualTo(EquatableArray<int>.Empty));
        Assert.That(d.GetHashCode(), Is.EqualTo(EquatableArray<int>.Empty.GetHashCode()));
    }

    [Test]
    public void Indexer_And_Count_Reflect_Items()
    {
        var a = new[] { "x", "y" }.ToEquatableArray();
        Assert.That(a.Count, Is.EqualTo(2));
        Assert.That(a[1], Is.EqualTo("y"));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~EquatableArrayTests`
Expected: build FAIL, `EquatableArray` not found.

- [ ] **Step 4: Implement `EquatableArray<T>` in `Rummikub.Core/Common/EquatableArray.cs`**

Store `ImmutableArray<T>`; treat `IsDefault` as empty everywhere. Equality via `EqualityComparer<T>.Default` element-wise; hash via `System.HashCode` with `Add` per element, then `ToHashCode()`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Rummikub.Tests`
Expected: all pass (17 existing + 4 new).

- [ ] **Step 6: Commit**

```bash
git add Rummikub.Core/Common/EquatableArray.cs Rummikub.Tests/Common/EquatableArrayTests.cs
git commit -m "Add EquatableArray<T> for sequence value equality" -m "baseline-warnings: N"
```

---

### Task 2: One-byte Tile, flattened namespace, dead code removed

**Files:**
- Modify: `Rummikub.Core/Tiles/Tile.cs` (rewrite), `Rummikub.Core/Tiles/Color.cs`, `Rummikub.Core/Groups/Group.cs`, `Rummikub.Core/Groups/GroupType.cs`, `Rummikub.Core/Board/BoardState.cs`, `Rummikub.Core/Hand/Hand.cs` — namespace → `Rummikub.Core`
- Delete: `Rummikub.Core/Tiles/TileExtensions.cs`, `Rummikub.Core/Groups/GroupValidator.cs`, `Rummikub.Core/Board/BoardValidator.cs`
- Modify callers: `Rummikub.Solver/Solver/GroupGenerator.cs`, `Rummikub.Solver/Solver/RummikubSolver.cs`, `Rummikub.Solver/Solver/SolverResult.cs`, `Rummikub.Solver/Memoization/StateHasher.cs`, `Rummikub.Server/Controllers/SolverController.cs`, `Rummikub.Runner/Program.cs`, `Rummikub.Tests/Solver/RummikubSolverTests.cs`
- Test: `Rummikub.Tests/Tiles/TileTests.cs` (rewrite)

**Interfaces:**
- Produces (namespace `Rummikub.Core`):
  - `public readonly record struct Tile : IComparable<Tile>` — single private field `byte _code`.
  - `static Tile Of(Color color, int value)` — `ArgumentOutOfRangeException` for value outside 1–13 or undefined colour.
  - `static Tile Joker { get; }`, `bool IsJoker`, `Color Color`, `int Value`, `int Id`, `static IReadOnlyList<Tile> AllKinds`, `int CompareTo(Tile)`, `string ToString()`.
  - Joker `Color`/`Value` throw `InvalidOperationException`; every member except `Equals`/`GetHashCode`/`ToString` throws `InvalidOperationException` on `default(Tile)`; `default(Tile).ToString()` is `"?"`.
  - `ToString()` letters: R, B, Y, K (black); joker `"J"`.

- [ ] **Step 1: Write the failing tests (replace `TileTests.cs`)**

```csharp
using Rummikub.Core;
namespace Rummikub.Tests.Tiles;

[TestFixture]
public class TileTests
{
    [Test]
    public void Of_Round_Trips_Color_Value_And_Id()
    {
        var t = Tile.Of(Color.Yellow, 7);
        Assert.Multiple(() =>
        {
            Assert.That(t.Color, Is.EqualTo(Color.Yellow));
            Assert.That(t.Value, Is.EqualTo(7));
            Assert.That(t.IsJoker, Is.False);
            Assert.That(t.Id, Is.EqualTo(2 * 13 + 7 - 1));
        });
    }

    [TestCase(0)] [TestCase(14)] [TestCase(-1)]
    public void Of_Rejects_Value_Out_Of_Range(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Tile.Of(Color.Red, value));

    [Test]
    public void Of_Rejects_Undefined_Color() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Tile.Of((Color)9, 5));

    [Test]
    public void Joker_Has_No_Color_Or_Value()
    {
        Assert.That(Tile.Joker.IsJoker, Is.True);
        Assert.That(Tile.Joker.Id, Is.EqualTo(52));
        Assert.Throws<InvalidOperationException>(() => _ = Tile.Joker.Color);
        Assert.Throws<InvalidOperationException>(() => _ = Tile.Joker.Value);
    }

    [Test]
    public void Default_Tile_Is_Unusable()
    {
        Tile d = default;
        Assert.Throws<InvalidOperationException>(() => _ = d.IsJoker);
        Assert.Throws<InvalidOperationException>(() => _ = d.Id);
        Assert.That(d.ToString(), Is.EqualTo("?"));
    }

    [Test]
    public void Equal_Tiles_Are_Equal() =>
        Assert.That(Tile.Of(Color.Blue, 10), Is.EqualTo(Tile.Of(Color.Blue, 10)));

    [Test]
    public void Ordering_Follows_Code()
    {
        Assert.That(Tile.Of(Color.Red, 13).CompareTo(Tile.Of(Color.Blue, 1)), Is.LessThan(0));
        Assert.That(Tile.Of(Color.Black, 13).CompareTo(Tile.Joker), Is.LessThan(0));
    }

    [Test]
    public void AllKinds_Has_53_Distinct_Ordered_Tiles()
    {
        Assert.That(Tile.AllKinds, Has.Count.EqualTo(53));
        Assert.That(Tile.AllKinds, Is.Unique);
        Assert.That(Tile.AllKinds, Is.Ordered);
        Assert.That(Tile.AllKinds[^1], Is.EqualTo(Tile.Joker));
    }

    [TestCase(Color.Red, 5, "R5")] [TestCase(Color.Blue, 13, "B13")]
    [TestCase(Color.Yellow, 1, "Y1")] [TestCase(Color.Black, 7, "K7")]
    public void ToString_Uses_Short_Code(Color c, int v, string expected) =>
        Assert.That(Tile.Of(c, v).ToString(), Is.EqualTo(expected));

    [Test]
    public void Joker_ToString_Is_J() => Assert.That(Tile.Joker.ToString(), Is.EqualTo("J"));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~TileTests`
Expected: build FAIL (`Tile.Of` missing).

- [ ] **Step 3: Rewrite `Tile` in `Rummikub.Core/Tiles/Tile.cs`**

Private constructor taking the code; one private guard method that throws on code 0, used by all accessors except `ToString`.

- [ ] **Step 4: Flatten namespaces and delete dead code**

Change the namespace of `Color`, `Group`, `GroupType`, `BoardState`, `Hand` to `Rummikub.Core` (file-scoped). Delete the three dead files listed above. Old `Group` constructor: replace the `OrderBy(IsJoker).ThenBy(Color).ThenBy(Value)` chain with `OrderBy(t => t)` (joker code 53 sorts last, as before).

- [ ] **Step 5: Migrate callers**

Replace usings `Rummikub.Core.Tiles/Groups/Board/Hand` with `Rummikub.Core`. `new Tile(c, v, false)` → `Tile.Of(c, v)`; `new Tile(null, null, true)` → `Tile.Joker`; `t.Value!.Value` → `t.Value`; `t.Color!.Value` → `t.Color`. In `SolverController.MapTile` (temporary until Task 6): `dto.IsJoker ? Tile.Joker : Tile.Of(Enum.Parse<Color>(dto.Color!, true), dto.Value!.Value)`; `MapTileDto` emits null colour/value for jokers.

- [ ] **Step 6: Run all tests**

Run: `dotnet test Rummikub.Tests`
Expected: all pass. Also `dotnet build RummikubSolver.sln` succeeds (Server and Runner compile).

- [ ] **Step 7: Commit**

```bash
git add -A Rummikub.Core Rummikub.Solver Rummikub.Server Rummikub.Runner Rummikub.Tests
git commit -m "Make Tile a one-byte value, flatten Core namespace, drop dead validators"
```

---

### Task 3: Structural Run/Set groups built via Create

**Files:**
- Create: `Rummikub.Core/Groups/GroupSlot.cs`, `Rummikub.Core/Groups/Run.cs`, `Rummikub.Core/Groups/Set.cs`
- Modify: `Rummikub.Core/Groups/Group.cs` (rewrite as abstract record), `Rummikub.Core/Board/BoardState.cs` (ordering only), `Rummikub.Solver/Solver/GroupGenerator.cs`, `Rummikub.Server/Controllers/SolverController.cs` (`MapBoard` only), `Rummikub.Runner/Program.cs`, `Rummikub.Tests/Solver/RummikubSolverTests.cs`
- Test: `Rummikub.Tests/Groups/RunTests.cs`, `Rummikub.Tests/Groups/SetTests.cs`, `Rummikub.Tests/Groups/GroupOrderingTests.cs`

**Interfaces:**
- Consumes: `Tile`, `Color` (Task 2).
- Produces (namespace `Rummikub.Core`):
  - `public readonly record struct GroupSlot(Tile Tile, Color Color, int Value)`
  - `public abstract record Group : IComparable<Group>` — `GroupType Type`, `IReadOnlyList<GroupSlot> Slots`, `IEnumerable<Tile> Tiles`, `int Count`, `int JokerCount`, `int CompareTo(Group? other)`. Sets sort before runs; sets by `(Value, Colors, JokerColors)`, runs by `(Color, Start, Length, JokerMask)`.
  - `public sealed record Run : Group` — `Color Color`, `int Start`, `int Length`, `ushort JokerMask`; `static Run Create(Color color, int start, int length, ushort jokerMask)`.
  - `public sealed record Set : Group` — `int Value`, `byte Colors`, `byte JokerColors`; `static Set Create(int value, byte colors, byte jokerColors)`.
  - `Create` throws `ArgumentException` whose message names the rule.
  - Run slots ordered by value; set slots ordered by colour enum order.

- [ ] **Step 1: Write the failing tests**

`RunTests.cs`:

```csharp
using Rummikub.Core;
namespace Rummikub.Tests.Groups;

[TestFixture]
public class RunTests
{
    [Test]
    public void Slots_Report_Joker_Meaning()
    {
        var run = Run.Create(Color.Red, 3, 3, 0b010);
        Assert.That(run.Slots, Is.EqualTo(new[]
        {
            new GroupSlot(Tile.Of(Color.Red, 3), Color.Red, 3),
            new GroupSlot(Tile.Joker, Color.Red, 4),
            new GroupSlot(Tile.Of(Color.Red, 5), Color.Red, 5),
        }));
        Assert.That(run.JokerCount, Is.EqualTo(1));
        Assert.That(run.Count, Is.EqualTo(3));
        Assert.That(run.Type, Is.EqualTo(GroupType.Run));
    }

    [Test]
    public void Equal_Structure_Is_Equal_With_Same_Hash()
    {
        var a = Run.Create(Color.Blue, 5, 4, 0);
        var b = Run.Create(Color.Blue, 5, 4, 0);
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [TestCase(1, 2, (ushort)0)]          // too short
    [TestCase(1, 14, (ushort)0)]         // too long
    [TestCase(0, 3, (ushort)0)]          // start below 1
    [TestCase(12, 3, (ushort)0)]         // runs past 13
    [TestCase(1, 3, (ushort)0b1000)]     // mask bit beyond length
    [TestCase(1, 4, (ushort)0b0111)]     // 3 jokers
    [TestCase(1, 3, (ushort)0b0111)]     // no regular tile (and 3 jokers)
    public void Create_Rejects_Invalid(int start, int length, ushort mask) =>
        Assert.Throws<ArgumentException>(() => Run.Create(Color.Red, start, length, mask));

    [Test]
    public void Max_Length_Run_Is_Valid() =>
        Assert.That(Run.Create(Color.Black, 1, 13, 0b1_0000_0000_0001).Count, Is.EqualTo(13));
}
```

`SetTests.cs`:

```csharp
using Rummikub.Core;
namespace Rummikub.Tests.Groups;

[TestFixture]
public class SetTests
{
    const byte R = 1, B = 2, Y = 4, K = 8;

    [Test]
    public void Slots_Ordered_By_Color_With_Joker_Meaning()
    {
        var set = Set.Create(7, R | B | K, K);
        Assert.That(set.Slots, Is.EqualTo(new[]
        {
            new GroupSlot(Tile.Of(Color.Red, 7), Color.Red, 7),
            new GroupSlot(Tile.Of(Color.Blue, 7), Color.Blue, 7),
            new GroupSlot(Tile.Joker, Color.Black, 7),
        }));
        Assert.That(set.Type, Is.EqualTo(GroupType.Set));
    }

    [TestCase(0, R | B | Y, 0)]          // value below 1
    [TestCase(14, R | B | Y, 0)]         // value above 13
    [TestCase(5, R | B, 0)]              // 2 colours
    [TestCase(5, 0b1_0111, 0)]           // bit outside low 4
    [TestCase(5, R | B | Y, K)]          // joker colour not in Colors
    [TestCase(5, R | B | Y | K, R | B | Y)] // 3 jokers
    public void Create_Rejects_Invalid(int value, int colors, int jokers) =>
        Assert.Throws<ArgumentException>(() => Set.Create(value, (byte)colors, (byte)jokers));

    [Test]
    public void Equal_Structure_Is_Equal_With_Same_Hash()
    {
        Assert.That(Set.Create(9, R | Y | K, 0), Is.EqualTo(Set.Create(9, R | Y | K, 0)));
        Assert.That(Set.Create(9, R | Y | K, 0).GetHashCode(), Is.EqualTo(Set.Create(9, R | Y | K, 0).GetHashCode()));
    }
}
```

`GroupOrderingTests.cs`:

```csharp
using Rummikub.Core;
namespace Rummikub.Tests.Groups;

[TestFixture]
public class GroupOrderingTests
{
    [Test]
    public void Sets_Sort_Before_Runs_Then_By_Fields()
    {
        Group[] groups =
        [
            Run.Create(Color.Blue, 1, 3, 0),
            Set.Create(5, 0b0111, 0),
            Run.Create(Color.Red, 4, 3, 0),
            Set.Create(3, 0b0111, 0),
            Run.Create(Color.Red, 2, 3, 0),
        ];
        Assert.That(groups.Order().ToArray(), Is.EqualTo(new Group[]
        {
            Set.Create(3, 0b0111, 0), Set.Create(5, 0b0111, 0),
            Run.Create(Color.Red, 2, 3, 0), Run.Create(Color.Red, 4, 3, 0),
            Run.Create(Color.Blue, 1, 3, 0),
        }));
    }

    [Test]
    public void Run_Never_Equals_Set() =>
        Assert.That(Run.Create(Color.Red, 1, 3, 0), Is.Not.EqualTo((Group)Set.Create(1, 0b0111, 0)));
}
```

Add to `RummikubSolverTests.cs` (Review Focus: >2 jokers):

```csharp
[Test]
public void Solver_Does_Not_Throw_With_Three_Jokers_In_Pool()
{
    var hand = new Hand([Tile.Joker, Tile.Joker, Tile.Joker, Tile.Of(Color.Red, 5)]);
    var result = _solver.SolveMaxTiles(hand, BoardState.Empty);
    Assert.That(result.PlayedTiles, Has.Count.EqualTo(3));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Rummikub.Tests`
Expected: build FAIL (`Run`, `Set`, `GroupSlot` missing).

- [ ] **Step 3: Implement `GroupSlot`, abstract `Group`, `Run`, `Set`**

`Group` has an `internal`/`private protected` constructor so no other subtype can exist. Use `BitOperations.PopCount` for mask checks. Delete the old `Group(GroupType, IEnumerable<Tile>)` constructor and its hand-written `Equals`/`GetHashCode`.

- [ ] **Step 4: Migrate `GroupGenerator` to `Create`**

Sets: for each regular-tile subset (same loops as today), `colors = subset mask | jokerColors` where `jokerColors` = the `neededJokers` lowest colours not in the subset mask; skip when `neededJokers > 2`. Runs: same `startVal`/`length` loops; a missing value sets bit `step` of `JokerMask`; abandon the shape once more than `min(jokerCount, 2)` jokers are needed. Remove the `distinctJokers` loops.

- [ ] **Step 5: Migrate remaining callers**

- `BoardState` constructor: replace the `OrderBy(Count).ThenBy(Type).ThenBy(string.Join(...hash...))` chain with `OrderBy(g => g)`.
- `RummikubSolverTests`: build board groups with `Run.Create(Color.Red, 3, 5, 0)` and `Set.Create(5, 0b1111, 0)`. Delete `Solver_Invalid_Board_Yields_Empty_If_Cannot_Fix` — an invalid board is no longer representable; its replacement is the invalid-group mapper test in Task 6.
- `Runner/Program.cs`: `Run.Create(Color.Blue, 5, 5, 0)`, `Run.Create(Color.Red, 5, 4, 0)`, `Set.Create(9, 0b1101, 0b0001)` (Yellow, Black, joker as Red).
- `SolverController.MapBoard` (temporary until Task 4): `throw new NotSupportedException("board input is re-enabled in Task 4")` when `dtos.Count > 0`, else `BoardState.Empty`.

- [ ] **Step 6: Run all tests**

Run: `dotnet test Rummikub.Tests` then `dotnet build RummikubSolver.sln`
Expected: all pass; solution builds.

- [ ] **Step 7: Commit**

```bash
git add -A Rummikub.Core Rummikub.Solver Rummikub.Server Rummikub.Runner Rummikub.Tests
git commit -m "Replace Group with structural Run/Set records"
```

---

### Task 4: Group.TryFromTiles (joker inference for input)

**Files:**
- Modify: `Rummikub.Core/Groups/Group.cs` (add `TryFromTiles`), `Rummikub.Server/Controllers/SolverController.cs` (`MapBoard`)
- Test: `Rummikub.Tests/Groups/GroupFromTilesTests.cs`

**Interfaces:**
- Consumes: `Run.Create`, `Set.Create` (Task 3).
- Produces: `public static bool TryFromTiles(GroupType type, IEnumerable<Tile> tiles, [NotNullWhen(true)] out Group? group, [NotNullWhen(false)] out string? error)` on `Group`. Never throws.
- Error messages (exact), checked in this order:
  1. `"group contains an uninitialized tile"`
  2. `"group needs at least 3 tiles"`
  3. `"group has more than 2 jokers"`
  4. `"group needs at least one regular tile"`
  5. Run: `"run tiles must share one color"`, `"run contains duplicate value {v}"`, `"not enough jokers to fill gaps"`, `"run exceeds 13"`
  6. Set: `"set tiles must share one value"`, `"set contains duplicate color {Color}"`, `"set has more than 4 tiles"`
- Inference algorithm: exactly spec §1 "Group.TryFromTiles" (run: `start = max(1, min - extra)`, rest after max; set: jokers take lowest missing colours).

- [ ] **Step 1: Write the failing tests**

```csharp
using Rummikub.Core;
namespace Rummikub.Tests.Groups;

[TestFixture]
public class GroupFromTilesTests
{
    static Tile R(int v) => Tile.Of(Color.Red, v);
    static readonly Tile J = Tile.Joker;

    static Group Ok(GroupType type, params Tile[] tiles)
    {
        Assert.That(Group.TryFromTiles(type, tiles, out var g, out var err), Is.True, err);
        return g!;
    }

    static string Fail(GroupType type, params Tile[] tiles)
    {
        Assert.That(Group.TryFromTiles(type, tiles, out _, out var err), Is.False);
        return err!;
    }

    [Test] public void Run_With_Inner_Joker() =>
        Assert.That(Ok(GroupType.Run, R(3), J, R(5)), Is.EqualTo(Run.Create(Color.Red, 3, 3, 0b010)));

    [Test] public void Run_Shuffled_Input_Equals_Create() =>
        Assert.That(Ok(GroupType.Run, J, R(5), R(3)), Is.EqualTo(Run.Create(Color.Red, 3, 3, 0b010)));

    [Test] public void Run_Extra_Joker_Goes_Below() =>
        Assert.That(Ok(GroupType.Run, R(3), R(4), J), Is.EqualTo(Run.Create(Color.Red, 2, 3, 0b001)));

    [Test] public void Run_Low_Edge_Joker_Goes_Above() =>
        Assert.That(Ok(GroupType.Run, R(1), R(2), J), Is.EqualTo(Run.Create(Color.Red, 1, 3, 0b100)));

    [Test] public void Run_High_Edge() =>
        Assert.That(Ok(GroupType.Run, R(12), R(13), J), Is.EqualTo(Run.Create(Color.Red, 11, 3, 0b001)));

    [Test] public void Run_Two_Extra_Jokers() =>
        Assert.That(Ok(GroupType.Run, R(5), J, J), Is.EqualTo(Run.Create(Color.Red, 3, 3, 0b011)));

    [Test] public void Set_Jokers_Take_Lowest_Missing_Colors() =>
        Assert.That(Ok(GroupType.Set, Tile.Of(Color.Red, 7), Tile.Of(Color.Blue, 7), J),
            Is.EqualTo(Set.Create(7, 0b0111, 0b0100)));

    [Test] public void Set_Three_Reals_Plus_Joker() =>
        Assert.That(Ok(GroupType.Set, Tile.Of(Color.Red, 2), Tile.Of(Color.Yellow, 2), Tile.Of(Color.Black, 2), J),
            Is.EqualTo(Set.Create(2, 0b1111, 0b0010)));

    [Test] public void Slots_Of_Inferred_Run_Report_Joker_Meaning() =>
        Assert.That(Ok(GroupType.Run, R(3), R(4), J).Slots[0], Is.EqualTo(new GroupSlot(J, Color.Red, 2)));

    [Test] public void Rejects_Default_Tile() =>
        Assert.That(Fail(GroupType.Run, R(1), R(2), default), Is.EqualTo("group contains an uninitialized tile"));
    [Test] public void Rejects_Two_Tiles() =>
        Assert.That(Fail(GroupType.Run, R(1), R(2)), Is.EqualTo("group needs at least 3 tiles"));
    [Test] public void Rejects_Three_Jokers() =>
        Assert.That(Fail(GroupType.Run, R(1), J, J, J), Is.EqualTo("group has more than 2 jokers"));
    [Test] public void Rejects_All_Jokers() =>
        Assert.That(Fail(GroupType.Set, J, J, J), Is.EqualTo("group has more than 2 jokers"));
    [Test] public void Rejects_Mixed_Run_Colors() =>
        Assert.That(Fail(GroupType.Run, R(1), Tile.Of(Color.Blue, 2), R(3)), Is.EqualTo("run tiles must share one color"));
    [Test] public void Rejects_Duplicate_Run_Value() =>
        Assert.That(Fail(GroupType.Run, R(4), R(4), R(5)), Is.EqualTo("run contains duplicate value 4"));
    [Test] public void Rejects_Unfillable_Gap() =>
        Assert.That(Fail(GroupType.Run, R(1), R(5), J), Is.EqualTo("not enough jokers to fill gaps"));
    [Test] public void Rejects_Mixed_Set_Values() =>
        Assert.That(Fail(GroupType.Set, R(1), Tile.Of(Color.Blue, 2), Tile.Of(Color.Black, 1)), Is.EqualTo("set tiles must share one value"));
    [Test] public void Rejects_Duplicate_Set_Color() =>
        Assert.That(Fail(GroupType.Set, R(5), R(5), Tile.Of(Color.Blue, 5)), Is.EqualTo("set contains duplicate color Red"));
    [Test] public void Rejects_Five_Tile_Set() =>
        Assert.That(Fail(GroupType.Set, R(5), Tile.Of(Color.Blue, 5), Tile.Of(Color.Yellow, 5), Tile.Of(Color.Black, 5), J),
            Is.EqualTo("set has more than 4 tiles"));
}
```

Note: `Rejects_All_Jokers` hits rule 3 first, which is the intended ordering. Rule 4 cannot trigger once rules 2–3 pass (≥3 tiles with ≤2 jokers always includes a regular tile); keep it as a defensive check, untested.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~GroupFromTilesTests`
Expected: build FAIL (`TryFromTiles` missing).

- [ ] **Step 3: Implement `Group.TryFromTiles`**

Materialise `tiles` once; check shared rules in the listed order; delegate to private `TryRun` / `TrySet` helpers that compute the structure and call `Create` (which cannot throw for shapes the helpers produce).

- [ ] **Step 4: Re-enable board input in the controller**

`SolverController.MapBoard`: `Enum.Parse<GroupType>(dto.Type, true)`, then `Group.TryFromTiles`; on failure `throw new ArgumentException(error)` (still caught by the existing catch-all; replaced in Task 6).

- [ ] **Step 5: Run all tests**

Run: `dotnet test Rummikub.Tests`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add Rummikub.Core Rummikub.Server Rummikub.Tests
git commit -m "Add Group.TryFromTiles with joker placement inference"
```

---

### Task 5: BoardState, Hand, SolverResult on EquatableArray

**Files:**
- Modify: `Rummikub.Core/Board/BoardState.cs`, `Rummikub.Core/Hand/Hand.cs`, `Rummikub.Core/Common/Multiset.cs` (delete `Equals`/`GetHashCode` overrides), `Rummikub.Solver/Solver/SolverResult.cs`, `Rummikub.Solver/Solver/RummikubSolver.cs`, `Rummikub.Server/Controllers/SolverController.cs`, `Rummikub.Runner/Program.cs`, `Rummikub.Tests/Solver/RummikubSolverTests.cs`
- Test: `Rummikub.Tests/Board/BoardStateTests.cs`, `Rummikub.Tests/Hands/HandTests.cs`, `Rummikub.Tests/Solver/SolverResultTests.cs`

**Interfaces:**
- Consumes: `EquatableArray<T>` (Task 1), `Group` (Task 3).
- Produces:
  - `public sealed record BoardState` — `EquatableArray<Group> Groups` (sorted), private ctor, `static BoardState Empty`, `static BoardState Of(IEnumerable<Group> groups)`, `BoardState Add(Group group)`, `IEnumerable<Tile> Tiles`.
  - `public sealed record Hand` — `EquatableArray<Tile> Tiles` (sorted), private ctor, `static Hand Empty`, `static Hand Of(IEnumerable<Tile> tiles)` (throws `ArgumentException` on a `default` tile).
  - `public sealed record SolverResult(BoardState Board, EquatableArray<Tile> PlayedTiles)` with `static SolverResult Empty` and `static SolverResult Create(BoardState board, IEnumerable<Tile> playedTiles)` (sorts `playedTiles`).
  - Removed: `BoardState(IEnumerable<Group>)` public ctor, `BoardState.AddGroup`, `new Hand(...)`, custom `Equals`/`GetHashCode` on all three, `MultiSet` equality overrides.

- [ ] **Step 1: Write the failing tests**

```csharp
// BoardStateTests.cs
using Rummikub.Core;
namespace Rummikub.Tests.Board;

[TestFixture]
public class BoardStateTests
{
    static readonly Group A = Set.Create(5, 0b0111, 0);
    static readonly Group B = Run.Create(Color.Red, 1, 3, 0);
    static readonly Group C = Run.Create(Color.Blue, 4, 4, 0b0010);

    [Test]
    public void Same_Groups_In_Any_Order_Are_Equal_With_Same_Hash()
    {
        var x = BoardState.Of([C, A, B]);
        var y = BoardState.Of([B, C, A]);
        Assert.That(x, Is.EqualTo(y));
        Assert.That(x.GetHashCode(), Is.EqualTo(y.GetHashCode()));
    }

    [Test]
    public void Add_Keeps_Sorted_Order() =>
        Assert.That(BoardState.Of([C, B]).Add(A).Groups, Is.EqualTo(new[] { A, B, C }));

    [Test]
    public void Tiles_Flattens_Groups() =>
        Assert.That(BoardState.Of([A, B]).Tiles.Count(), Is.EqualTo(6));

    [Test]
    public void Empty_Has_No_Groups() => Assert.That(BoardState.Empty.Groups, Is.Empty);
}

// HandTests.cs
using Rummikub.Core;
namespace Rummikub.Tests.Hands;

[TestFixture]
public class HandTests
{
    [Test]
    public void Of_Sorts_So_Order_Does_Not_Matter() =>
        Assert.That(Hand.Of([Tile.Joker, Tile.Of(Color.Red, 2)]),
            Is.EqualTo(Hand.Of([Tile.Of(Color.Red, 2), Tile.Joker])));

    [Test]
    public void Of_Rejects_Default_Tile() =>
        Assert.Throws<ArgumentException>(() => Hand.Of([default(Tile)]));
}

// SolverResultTests.cs
using Rummikub.Core;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Solver;

[TestFixture]
public class SolverResultTests
{
    [Test]
    public void Played_Tile_Order_Does_Not_Affect_Equality()
    {
        var a = SolverResult.Create(BoardState.Empty, [Tile.Of(Color.Red, 1), Tile.Joker]);
        var b = SolverResult.Create(BoardState.Empty, [Tile.Joker, Tile.Of(Color.Red, 1)]);
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }
}
```

(Test namespace is `Rummikub.Tests.Hands`, plural: a namespace named `Rummikub.Tests.Hand` would shadow the `Hand` type in every other `Rummikub.Tests.*` file.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Rummikub.Tests`
Expected: build FAIL (`BoardState.Of`, `Hand.Of`, `SolverResult.Create` missing).

- [ ] **Step 3: Implement `BoardState`, `Hand`, `SolverResult`; delete `MultiSet` equality overrides**

`BoardState.Add` may simply re-sort (`Of(Groups.Append(group))`); groups per board are few.

- [ ] **Step 4: Migrate callers**

Solver: `subResult.Board.AddGroup(g.Group)` → `.Add(g.Group)`; build results with `SolverResult.Create(board, subResult.PlayedTiles.Concat(handTilesUsed))`; compare `PlayedTiles.Count` as before. Tests/Runner/Controller: `new Hand([...])` → `Hand.Of([...])`, `new BoardState([...])` → `BoardState.Of([...])`.

- [ ] **Step 5: Run all tests and check hash-code removal**

Run: `dotnet test Rummikub.Tests`
Expected: all pass.
Run: `grep -rn "override int GetHashCode" Rummikub.Core --include=*.cs`
Expected: only `Rummikub.Core/Common/EquatableArray.cs`.

- [ ] **Step 6: Commit**

```bash
git add -A Rummikub.Core Rummikub.Solver Rummikub.Server Rummikub.Runner Rummikub.Tests
git commit -m "Base BoardState, Hand and SolverResult on EquatableArray"
```

---

### Task 6: API redesign with DtoMapper and validation errors

**Files:**
- Modify: `Rummikub.Server/Models/RummikubDtos.cs` (rewrite), `Rummikub.Server/Controllers/SolverController.cs` (rewrite), `Rummikub.Server/Rummikub.Server.http` (update sample request), `Rummikub.Tests/Rummikub.Tests.csproj` (add Server reference)
- Create: `Rummikub.Server/Mapping/DtoMapper.cs`
- Test: `Rummikub.Tests/Server/DtoMapperTests.cs`

**Interfaces:**
- Consumes: `Hand.Of`, `BoardState.Of`, `Group.TryFromTiles`, `SolverResult` (Tasks 4–5).
- Produces (namespace `Rummikub.Server.Models`, classes with settable properties and empty-collection defaults):
  - `TileDto { string? Color; int? Value; bool? Joker }`
  - `GroupInputDto { string Type = ""; List<TileDto>? Tiles }`
  - `SolveRequest { List<TileDto>? Hand; List<GroupInputDto>? Board }`
  - `SlotDto { string Color; int Value; bool Joker }`
  - `GroupDto { string Type; List<SlotDto> Slots }`
  - `SolveResponse { List<TileDto> PlayedTiles; List<GroupDto> Board; int TilesPlayed }`
- Produces (namespace `Rummikub.Server.Mapping`):
  - `public static class DtoMapper`
  - `static bool TryMapRequest(SolveRequest request, [NotNullWhen(true)] out Hand? hand, [NotNullWhen(true)] out BoardState? board, out IReadOnlyDictionary<string, string> errors)`
  - `static SolveResponse ToResponse(SolverResult result)`
  - `static TileDto ToDto(Tile tile)` — joker: `{ Joker = true }`, colour/value null; regular: colour name, value, `Joker = false`.
  - `static GroupDto ToDto(Group group)` — `Type` = enum name, one `SlotDto` per `Group.Slots`.
- Error keys: `hand[i]`, `board[i]`, `board[i].tiles[j]`. Exact tile messages: `"joker must not have color or value"`, `"color and value are required"`, `"unknown color '{text}'"`, `"value must be between 1 and 13"`. Group type message: `"unknown group type '{text}'"`. Group rule failures use `TryFromTiles`' error at `board[i]`; a group with any tile error is not checked further. Null `Hand`/`Board`/`Tiles` lists are treated as empty.
- Controller: `POST api/solver/solve`; on mapping errors add each to `ModelState` and `return ValidationProblem(ModelState)`; otherwise `Ok(DtoMapper.ToResponse(...))`. No `try/catch`.

- [ ] **Step 1: Add the Server project reference to the tests project**

In `Rummikub.Tests.csproj` add `<ProjectReference Include="..\Rummikub.Server\Rummikub.Server.csproj" />`.

- [ ] **Step 2: Write the failing tests**

```csharp
using Rummikub.Core;
using Rummikub.Server.Mapping;
using Rummikub.Server.Models;
using Rummikub.Solver.Solver;
namespace Rummikub.Tests.Server;

[TestFixture]
public class DtoMapperTests
{
    static TileDto T(string c, int v) => new() { Color = c, Value = v };
    static TileDto J() => new() { Joker = true };

    static IReadOnlyDictionary<string, string> Errors(SolveRequest req)
    {
        Assert.That(DtoMapper.TryMapRequest(req, out _, out _, out var errors), Is.False);
        return errors;
    }

    [Test]
    public void Maps_Valid_Request()
    {
        var req = new SolveRequest
        {
            Hand = [T("red", 5), J()],
            Board = [new GroupInputDto { Type = "run", Tiles = [T("Blue", 3), J(), T("Blue", 5)] }],
        };
        Assert.That(DtoMapper.TryMapRequest(req, out var hand, out var board, out var errors), Is.True);
        Assert.That(errors, Is.Empty);
        Assert.That(hand, Is.EqualTo(Hand.Of([Tile.Of(Color.Red, 5), Tile.Joker])));
        Assert.That(board, Is.EqualTo(BoardState.Of([Run.Create(Color.Blue, 3, 3, 0b010)])));
    }

    [Test]
    public void Null_Lists_Are_Empty()
    {
        Assert.That(DtoMapper.TryMapRequest(new SolveRequest(), out var hand, out var board, out _), Is.True);
        Assert.That(hand, Is.EqualTo(Hand.Empty));
        Assert.That(board, Is.EqualTo(BoardState.Empty));
    }

    [Test]
    public void Bad_Tiles_Report_Exact_Paths()
    {
        var errors = Errors(new SolveRequest
        {
            Hand = [new TileDto { Joker = true, Color = "Red" }, new TileDto { Color = "Red" },
                    T("Green", 3), T("Red", 14)],
        });
        Assert.That(errors, Is.EquivalentTo(new Dictionary<string, string>
        {
            ["hand[0]"] = "joker must not have color or value",
            ["hand[1]"] = "color and value are required",
            ["hand[2]"] = "unknown color 'Green'",
            ["hand[3]"] = "value must be between 1 and 13",
        }));
    }

    [Test]
    public void Unknown_Group_Type_Is_Reported() =>
        Assert.That(Errors(new SolveRequest { Board = [new GroupInputDto { Type = "pair", Tiles = [T("Red", 1), T("Red", 2), T("Red", 3)] }] }),
            Is.EquivalentTo(new Dictionary<string, string> { ["board[0]"] = "unknown group type 'pair'" }));

    [Test]
    public void Invalid_Group_Uses_Domain_Message() =>
        Assert.That(Errors(new SolveRequest { Board = [new GroupInputDto { Type = "set", Tiles = [T("Red", 1), T("Blue", 2), T("Black", 3)] }] }),
            Is.EquivalentTo(new Dictionary<string, string> { ["board[0]"] = "set tiles must share one value" }));

    [Test]
    public void Tile_Error_Inside_Group_Has_Nested_Path() =>
        Assert.That(Errors(new SolveRequest { Board = [new GroupInputDto { Type = "run", Tiles = [T("Red", 1), T("Red", 2), T("Pink", 3)] }] }),
            Is.EquivalentTo(new Dictionary<string, string> { ["board[0].tiles[2]"] = "unknown color 'Pink'" }));

    [Test]
    public void Response_Slots_Carry_Joker_Meaning()
    {
        var result = SolverResult.Create(BoardState.Of([Run.Create(Color.Red, 3, 3, 0b010)]), [Tile.Joker]);
        var dto = DtoMapper.ToResponse(result);
        Assert.That(dto.TilesPlayed, Is.EqualTo(1));
        Assert.That(dto.PlayedTiles[0].Joker, Is.True);
        Assert.That(dto.Board[0].Type, Is.EqualTo("Run"));
        var slot = dto.Board[0].Slots[1];
        Assert.That((slot.Color, slot.Value, slot.Joker), Is.EqualTo(("Red", 4, true)));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test Rummikub.Tests --filter FullyQualifiedName~DtoMapperTests`
Expected: build FAIL (`DtoMapper`, new DTOs missing).

- [ ] **Step 4: Implement DTOs, `DtoMapper`, and the new controller**

Parse colour/type with `Enum.TryParse(text, ignoreCase: true, out ...)` and also reject numeric strings (`Enum.IsDefined`). Update the sample request in `Rummikub.Server.http` to the new shape.

- [ ] **Step 5: Run all tests**

Run: `dotnet test Rummikub.Tests`
Expected: all pass.

- [ ] **Step 6: Smoke-test the running server**

Run: `dotnet run --project Rummikub.Server --launch-profile http` (background), then
`curl -s -X POST http://localhost:5155/api/solver/solve -H "Content-Type: application/json" -d '{"hand":[{"color":"Red","value":3},{"color":"Red","value":4},{"joker":true}],"board":[]}'`
Expected: 200 with `"tilesPlayed":3` and a slot with `"joker":true`.
Then the same with `"board":[{"type":"set","tiles":[{"color":"Red","value":1}]}]`
Expected: 400 JSON whose `errors` has key `board[0]`. Stop the server.

- [ ] **Step 7: Commit**

```bash
git add -A Rummikub.Server Rummikub.Tests
git commit -m "Redesign solve API around structural groups with validation errors"
```

---

### Task 7: Client update

**Files:**
- Modify: `rummikub-client/src/App.tsx`, `rummikub-client/src/index.css` (one new class)
- Create (only if `tsc` rejects `import.meta.env`): `rummikub-client/src/vite-env.d.ts` containing `/// <reference types="vite/client" />`

**Interfaces:**
- Consumes: the JSON contract from Task 6 (camelCase): request `{ hand: TileDto[], board: { type, tiles: TileDto[] }[] }`; response `{ playedTiles, board: { type, slots: { color, value, joker }[] }[], tilesPlayed }`; 400 body `{ errors: Record<string, string[]> }`.

- [ ] **Step 1: Update types and request building**

`TileDto = { color?: string; value?: number; joker?: boolean }`; jokers are sent as `{ joker: true }` only. Add `SlotDto`, `GroupDto`, `GroupInputDto`, `SolveResponse` matching Task 6. API base: `const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5155'`.

- [ ] **Step 2: Render result groups from slots**

`renderSlot(slot)`: a joker slot shows `☻` plus a small label `${slot.color[0]}${slot.value}` (use `K` for Black, matching `Tile.ToString`) in a new `.joker-face` span; regular slots render like regular tiles. Result count reads `result.tilesPlayed`.

- [ ] **Step 3: Show validation errors**

New state `errors: string[]`. On `response.status === 400`, set it to `Object.entries(body.errors).map(([k, v]) => \`${k}: ${v.join(', ')}\`)`; on any other non-OK status set `[\`Server error (${response.status})\`]`; clear it on each solve. Render the list in a red panel above the result area.

- [ ] **Step 4: Build and lint**

Run (in `rummikub-client`): `npm run build` and `npm run lint`
Expected: both succeed with no errors.

- [ ] **Step 5: Manual check**

Run the server (`dotnet run --project Rummikub.Server --launch-profile http`) and `npm run dev`; in the browser add hand R3, R4, Joker and solve → a run is shown with the joker labelled `R5` or `R2` as the solver chose; add a board set of R1 only → the red panel shows `board[0]: group needs at least 3 tiles`.

- [ ] **Step 6: Commit**

```bash
git add rummikub-client/src
git commit -m "Update client for structural groups and validation errors"
```

---

### Task 8: Final verification

- [ ] **Step 1: Full build, warning count against baseline**

Run: `dotnet build RummikubSolver.sln --no-incremental 2>&1 | grep -c "warning"`
Expected: ≤ the `baseline-warnings` number from Task 1's commit message.

- [ ] **Step 2: Full test run**

Run: `dotnet test RummikubSolver.sln`
Expected: all pass, 0 failed.

- [ ] **Step 3: Leftover checks**

Run: `grep -rn -E "override int GetHashCode|Rummikub\.Core\.(Tiles|Groups|Board|Hand)\b|GroupValidator|IsSameAs|AddGroup" --include=*.cs Rummikub.*`
Expected: only `Rummikub.Core/Common/EquatableArray.cs` (GetHashCode) and `Rummikub.Solver/Memoization/StateHasher.cs` (out of scope).

- [ ] **Step 4: Runner still works**

Run: `dotnet run --project Rummikub.Runner`
Expected: prints a tiles-played count and groups, no exception.
