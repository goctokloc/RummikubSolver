# Domain Model Cleanup — Design

Date: 2026-10-14
Status: Approved in conversation, pending written-spec review

## Goal

Replace the current `Tile` / `Group` / `BoardState` model with one where:

- invalid tiles and invalid groups cannot be represented,
- every joker records the colour and value it stands for,
- value equality comes from compiler-generated record equality over primitive
  fields — no hand-written `17 * 31` hash code anywhere in Core,
- canonical ordering is structural, not derived from hash codes.

This prepares the codebase for the later solver rewrite (ILP), which wants a
dense 0–52 tile id. The solver algorithm itself is **not** changed here.

## Non-goals

- No change to the DFS search, `MultiSet`, `StateHasher` or memoisation.
- No restriction of run length (runs up to 13 are still generated).
- No random-game invariant test (belongs to the solver rewrite).
- No game-rule features (initial meld, scoring, joker retrieval rules).

## Decisions (from brainstorming)

| Topic | Decision |
|---|---|
| Group validity | Valid by construction; invalid input rejected at the boundary |
| Joker meaning | Structural groups: position/colour slots, jokers fill slots |
| Representation | Records with primitive fields and bitmasks (approach A) |
| Rule violations | `Create` throws (trusted callers); `TryFromTiles` returns error (untrusted input) |
| Solver scope | Minimal adaptation; existing solver tests must stay green |
| API | Free to redesign; client updated in the same change |
| Board input jokers | Server infers placement; lowest valid start when ambiguous |

## 1. Core types (`Rummikub.Core`)

All domain types move to the single namespace `Rummikub.Core` (fixes the
`Rummikub.Core.Hand.Hand` clash). Folders may remain for file organisation.
`Rummikub.Core.Common` keeps generic helpers.

### Color

Unchanged enum, order is significant: `Red = 0, Blue = 1, Yellow = 2, Black = 3`.
Colour bitmask bit for colour `c` is `1 << (int)c`.

### Tile

`public readonly record struct Tile : IComparable<Tile>` with a single private
`byte _code` field.

- Codes: `0` = uninitialised (`default(Tile)`), `1..52` = regular tile
  `(int)color * 13 + value`, `53` = joker.
- `static Tile Of(Color color, int value)` — throws `ArgumentOutOfRangeException`
  if `value` is outside 1–13 or `color` is not a defined enum value.
- `static Tile Joker { get; }`
- `bool IsJoker`
- `Color Color`, `int Value` — throw `InvalidOperationException` on a joker.
- `int Id` — `_code - 1`, range 0–52 (dense index for future count arrays).
- `static IReadOnlyList<Tile> AllKinds` — the 53 distinct kinds, ordered by code.
- `CompareTo` compares codes.
- `ToString()` — `"R5"`, `"B13"`, `"Y1"`, `"K7"` (K = black), `"J"`.
- Every member except equality/`GetHashCode`/`ToString` throws
  `InvalidOperationException` on `default(Tile)`; `ToString()` returns `"?"`.

Equality and hashing are compiler-generated.

### GroupSlot

`public readonly record struct GroupSlot(Tile Tile, Color Color, int Value)` —
the physical tile in a position and what it represents. For a joker slot
`Tile == Tile.Joker`; for a regular slot `Tile == Tile.Of(Color, Value)`.

### Group

`public abstract record Group : IComparable<Group>`, with exactly two sealed
subtypes, `Run` and `Set`. Constructors are private; properties are get-only
(not `init`) so neither `new` nor `with` can bypass validation.

Common members:

- `GroupType Type` (`GroupType` enum is kept: `Run`, `Set`)
- `IReadOnlyList<GroupSlot> Slots` — ordered: runs by value ascending, sets by
  colour enum order
- `IEnumerable<Tile> Tiles` — `Slots.Select(s => s.Tile)`
- `int Count`, `int JokerCount`
- `CompareTo`: every `Set` sorts before every `Run`; sets by
  `(Value, Colors, JokerColors)`; runs by `(Color, Start, Length, JokerMask)`.

Shared rule: at most 2 jokers, at least 3 tiles, at least one regular tile.

The only instance fields of `Run` and `Set` are the primitive fields listed
below. `Slots`, `Tiles`, `Count` and `JokerCount` are computed properties with
no backing field — a stored list would be compared by reference by the
synthesized record equality and silently break it.

#### Run

Fields: `Color Color`, `int Start`, `int Length`, `ushort JokerMask`
(bit `i` set ⇒ position `i`, i.e. value `Start + i`, is a joker).

Rules: `3 <= Length <= 13`, `Start >= 1`, `Start + Length - 1 <= 13`,
`JokerMask >> Length == 0`, `PopCount(JokerMask) <= 2`.

`static Run Create(Color color, int start, int length, ushort jokerMask)` —
throws `ArgumentException` naming the violated rule.

#### Set

Fields: `int Value`, `byte Colors`, `byte JokerColors` (4-bit colour masks).

Rules: `1 <= Value <= 13`, `Colors` uses only the low 4 bits,
`3 <= PopCount(Colors) <= 4`, `(JokerColors & ~Colors) == 0`,
`PopCount(JokerColors) <= 2`.

`static Set Create(int value, byte colors, byte jokerColors)` — throws
`ArgumentException` naming the violated rule.

#### Group.TryFromTiles

`static bool TryFromTiles(GroupType type, IEnumerable<Tile> tiles,
[NotNullWhen(true)] out Group? group, [NotNullWhen(false)] out string? error)`

Never throws. Fails with a human-readable `error` when: any tile is
`default`, fewer than 3 tiles, more than 2 jokers, no regular tile, or the
type-specific rules below fail.

Run inference:

1. All regular tiles share one colour (else `"run tiles must share one color"`).
2. Regular values are distinct (else `"run contains duplicate value N"`).
3. `span = max - min + 1`; `gaps = span - regularCount`. If
   `gaps > jokers` ⇒ `"not enough jokers to fill gaps"`.
4. `extra = jokers - gaps`. `start = max(1, min - extra)`; remaining extras go
   after `max`. If the end exceeds 13 ⇒ `"run exceeds 13"`.
   (Example: R3 R4 J ⇒ start 2, joker represents R2.)
5. `JokerMask` has bits for every position without a regular tile.

Set inference:

1. All regular tiles share one value (else `"set tiles must share one value"`).
2. Regular colours are distinct (else `"set contains duplicate color C"`).
3. Total tiles ≤ 4 (else `"set has more than 4 tiles"`).
4. Jokers take the missing colours, lowest enum value first.

### EquatableArray<T>

`public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>,
IReadOnlyList<T>` in `Rummikub.Core.Common`, wrapping `ImmutableArray<T>`.
Sequence equality via `EqualityComparer<T>.Default`; hash built with
`System.HashCode.Add`. `default` behaves as empty. This is the only
hand-written equality in Core.

### BoardState

`public sealed record BoardState` with `EquatableArray<Group> Groups`, always
sorted by `Group.CompareTo`. Private constructor.

- `static BoardState Empty`
- `static BoardState Of(IEnumerable<Group> groups)`
- `BoardState Add(Group group)` — inserts keeping order
- `IEnumerable<Tile> Tiles`

### Hand

`public sealed record Hand` with `EquatableArray<Tile> Tiles`, sorted.
`static Hand Of(IEnumerable<Tile> tiles)`, `static Hand Empty`.
Throws `ArgumentException` on a `default` tile.

### Removed

- `GroupValidator` (rules live in factories), `BoardValidator` (empty),
  `TileExtensions.IsSameAs` (duplicate of record equality) — none have callers.
- `Equals` / `GetHashCode` overrides on `MultiSet<T>` (no callers; a mutable
  type should not have value-based hashing).
- All redundant `: IEquatable<T>` declarations on records and all hand-written
  hash codes in Core.

## 2. Solver adaptation (minimal)

- `GroupGenerator` builds groups via `Run.Create` / `Set.Create`. Runs: same
  loops as today; positions whose value is not in the pool set a `JokerMask`
  bit. Sets: same subsets as today; missing colours become `JokerColors`,
  lowest first. It only enumerates legal shapes, so `Create` never throws.
  Joker-count limits are enforced by the existing `jokerCount` checks; the
  generator additionally skips shapes needing more than 2 jokers.
- `RummikubSolver`: `GroupInfo` counts tiles from `group.Tiles`;
  `Board.AddGroup` → `Board.Add`. DFS, `MultiSet`, `StateHasher` unchanged.
- `SolverResult` becomes `sealed record SolverResult(BoardState Board,
  EquatableArray<Tile> PlayedTiles)` with `PlayedTiles` sorted; custom
  `Equals`/`GetHashCode` removed. `SolverResult.Empty` kept.
- `SolverOptions` kept as is.
- Search behaviour is unchanged: same groups, same results (modulo tile
  ordering in `PlayedTiles`).

## 3. API and client

### DTOs (`Rummikub.Server/Models`)

```
TileDto        { color?: string, value?: int, joker?: bool }
GroupInputDto  { type: string, tiles: TileDto[] }
SolveRequest   { hand: TileDto[], board: GroupInputDto[] }

SlotDto        { color: string, value: int, joker: bool }
GroupDto       { type: string, slots: SlotDto[] }
SolveResponse  { playedTiles: TileDto[], board: GroupDto[], tilesPlayed: int }
```

Enum strings are parsed case-insensitively and emitted as enum names
(`"Red"`, `"Run"`). A `TileDto` is a joker iff `joker == true`; then `color`
and `value` must be absent. Otherwise both are required and valid.

### Mapping and errors

- New static `DtoMapper` with pure functions: request → `(Hand, BoardState)` or
  a dictionary of errors keyed by path (`hand[3]`, `board[2]`,
  `board[2].tiles[0]`); domain → response DTOs.
- Board groups go through `Group.TryFromTiles`; its `error` becomes the message
  for `board[i]`.
- Controller returns `ValidationProblem` (400) when there are errors. The
  catch-all `catch (Exception)` is removed; unexpected failures are 500s.

### Client (`rummikub-client`)

- Types updated to the DTOs above; renders `board` groups from `slots`.
- A joker slot shows the joker glyph plus the represented tile (e.g. small
  "R5").
- 400 responses show the validation messages instead of rendering as a result.
- API base URL from `import.meta.env.VITE_API_URL`, default
  `http://localhost:5155`.

## 4. Testing

- `TileTests`: `Of` round-trips colour/value and `Id`; value 0 and 14 throw;
  joker `Color`/`Value` throw; `default(Tile)` members throw; ordering by code;
  `AllKinds` has 53 distinct entries.
- `RunTests` / `SetTests`: `Create` rejects each rule violation (length < 3,
  start 0, past 13, mask bits beyond length, 3 jokers; sets with 2 or 5
  colours, joker colours outside `Colors`). `TryFromTiles` accepts R3-J-R5;
  infers R3 R4 J as start 2; fills set jokers with lowest missing colours;
  rejects mixed colour, duplicate colour/value, all-joker, too-few tiles,
  each with its message. Groups built from shuffled tiles equal those from
  `Create`, with equal hash codes. `Slots` report joker meaning.
- `BoardStateTests`: same groups in different order ⇒ equal and same hash;
  `Add` keeps sorted order.
- Existing `RummikubSolverTests`: ported to the new API, all must pass.
- `DtoMapperTests` (Tests project gains a reference to Server): round-trip,
  error keys for bad tile, bad joker DTO, invalid group.

## Done when

- Solution builds with no warnings introduced by this change.
- All tests pass, including the ported solver tests.
- No `GetHashCode` override remains in Core except inside `EquatableArray<T>`.
- Client builds (`npm run build`) and talks to the new API.
