# ILP Solver — Design

Date: 2026-10-15
Status: Approved in conversation, pending written-spec review

## Goal

Replace the exponential DFS solver with an exact integer-programming solver
written in pure C# (no native or third-party solver dependency) that:

- finds a provably optimal play for any legal position, including a full
  106-tile game, in under 1 second;
- supports the initial meld (30 points from hand only), a points-based
  objective, a "keep jokers" preference, and a least-rearrangement tie-break;
- is structured so a later strategy layer can add objective terms and
  constraints and ask the solver repeated "what-if" questions.

## Non-goals

- Strategic play (holding tiles back, opponent modelling). Later project.
- A public "list candidate plays" API. Belongs to the strategy project.
- Move-by-move rearrangement instructions.
- Warm-starting LPs between branch-and-bound nodes (possible later
  optimisation; not needed for the performance target).
- Validating physical tile counts (more than 2 copies of a tile).

## Decisions (from brainstorming)

| Topic | Decision |
|---|---|
| Engine | Own LP + branch-and-bound in C# (approach C) |
| Arithmetic | `double` with tolerances; integer solutions verified exactly |
| Anti-cycling | Dantzig pricing, switch to Bland's rule after 50 consecutive degenerate pivots |
| Objective | Lexicographic levels packed into one integer objective |
| Level order | 1. primary score, 2. jokers kept (if `KeepJokers`), 3. board groups kept intact |
| Joker value in `Points` mode | 30 |
| Old DFS | Kept as `internal DfsReferenceSolver`, used only as a test oracle |
| Strategy | Three structural hooks only (see §6) |

## 1. Mathematical model (summary)

Indices: tile kinds `t` (53, by `Tile.Id`), catalogue groups `g`, distinct
existing board groups `b`. `a_tg` = copies of tile kind `t` used by group `g`;
`board_t`, `hand_t` = counts of kind `t`; `mult_b` = copies of board group `b`.

Variables (all integer):

- `x_g ∈ [0, ub_g]` — copies of catalogue group `g` on the table.
- `k_b ∈ [0, mult_b]` — copies of board group `b` kept exactly as they are.
- `y_t ∈ [0, hand_t]` — tiles of kind `t` played from hand.
- `m ∈ {0,1}` — only when `InitialMeldDone == false`.

Tile balance for every `t`:
`Σ_g a_tg·x_g + Σ_b a_tb·k_b − y_t = board_t`.

"Keep the board, play nothing" (`k_b = mult_b`, everything else 0) is always
feasible, so every position has a valid answer.

## 2. Components (all in `Rummikub.Solver`)

| Unit | Responsibility |
|---|---|
| `TileCounts` | 53-slot count vector indexed by `Tile.Id`; `From(Hand)`, `From(BoardState)` |
| `GroupCatalog` | All legal groups as model columns (cached) |
| `Mip/IntegerModel` | Engine-agnostic model: variables, sparse constraints, objective |
| `Mip/SimplexSolver` | LP relaxation solver |
| `Mip/BranchAndBound` | Integer search over LP relaxations |
| `Model/RummikubModelBuilder` | Game rules + options → `RummikubModel` |
| `Model/RummikubModel` | `IntegerModel` + variable maps + objective levels; `Decode(MipSolution)` |
| `Model/SolutionVerifier` | Exact integer check of a decoded solution |
| `RummikubSolver` | Public facade: build → solve → verify → decode |
| `Reference/DfsReferenceSolver` | Old DFS (internal), test oracle only |

### GroupCatalog

- Runs: every colour, length 3–5, start `1..14-length`, every `JokerMask` with
  at most 2 bits. Sets: every value 1–13, every colour mask with 3 or 4 bits,
  every `JokerColors ⊆ Colors` with at most 2 bits. All built via
  `Run.Create` / `Set.Create`.
- Columns with identical tile multisets are collapsed to one (keep the first
  in enumeration order: sets before runs, lower start first).
- Runs longer than 5 are not catalogue columns: any run of length ≥ 6 splits
  into runs of length 3–5, so they add nothing to the optimum.
- Per solve, a column's upper bound is
  `ub_g = min(2, min over t with a_tg > 0 of ⌊(hand_t + board_t) / a_tg⌋)`;
  columns with `ub_g = 0` are omitted from the model.

## 3. Model builder and options

`SolverOptions` (record, `init` properties):

| Property | Type | Default |
|---|---|---|
| `InitialMeldDone` | `bool` | `true` |
| `InitialMeldPoints` | `int` | `30` |
| `Objective` | `SolverObjective` (`TileCount`, `Points`) | `TileCount` |
| `KeepJokers` | `bool` | `false` |
| `MaxNodes` | `int` | `200_000` |

Rules:

- Tile balance as in §1. Board groups become `k_b` columns (any length,
  identical board groups merged with `mult_b`).
- Initial meld (`InitialMeldDone == false`): fix `k_b = mult_b` (board
  untouched), add `m` and
  `Σ_g points_g·x_g − InitialMeldPoints·m ≥ 0` and `Σ_g x_g − 36·m ≤ 0`,
  where `points_g` = sum of the represented values of the group's slots.

Objective levels (highest priority first), each an `ObjectiveLevel(name,
terms, min, max)` with integer coefficients:

1. **Primary**: `Σ_t w_t·y_t`. `TileCount`: `w_t = 1`. `Points`: face value,
   joker 30. With `KeepJokers`: joker `w = 0`.
2. **JokersKept** (only with `KeepJokers`): `−y_joker` (range `−hand_joker..0`;
   the range comes from the actual hand, since physical counts are not validated).
3. **GroupsKept**: `Σ_b k_b` (range `0..Σ mult_b`).

Packing: level `i` is multiplied by `Π_{j>i} (max_j − min_j + 1)`; the sum is
the single integer objective. Values stay below 2^31 for every legal position.

## 4. MIP engine

### IntegerModel

- `int AddVariable(long lower, long upper, bool isInteger, string name)`
- `void AddConstraint(IReadOnlyList<(int Var, long Coef)> terms, Sense sense, long rhs)` (`Sense`: `LessOrEqual`, `Equal`, `GreaterOrEqual`)
- `void SetObjective(IReadOnlyList<(int Var, long Coef)> terms)` (maximise)
- Coefficients and bounds are integers; the engine converts to `double`.

### SimplexSolver

- Two-phase, bounded-variable primal simplex on a dense tableau.
- Input: `IntegerModel` plus per-variable bound overrides (from branching).
- Output: `LpResult(LpStatus Status, double Objective, double[] Values)`,
  `LpStatus`: `Optimal`, `Infeasible`, `Unbounded`.
- Tolerance `1e-9`. Dantzig pricing; Bland's rule after 50 consecutive
  degenerate pivots, back to Dantzig after a non-degenerate pivot.
- More than 50 000 pivots in one solve ⇒ `InvalidOperationException`.

### BranchAndBound

- `MipSolution Solve(IntegerModel model, long incumbentObjective, long[] incumbentValues, int maxNodes, CancellationToken ct)`
- Depth-first; branch on the integer variable whose LP value is most
  fractional (ties: lowest index); explore the "≥ ⌈v⌉" child first.
- Prune when `⌊lpObjective + 1e-6⌋ ≤ incumbent`.
- An LP solution is integral when every integer variable is within `1e-6`
  of an integer; it is rounded and becomes the incumbent if better.
- `MipSolution(long[] Values, long Objective, bool IsOptimal)`. Node limit or
  cancellation ⇒ return the incumbent with `IsOptimal = false`.

### Failure handling

- `SolutionVerifier` checks the decoded solution exactly (tile balance per
  kind, all bounds, meld constraints, played tiles ⊆ hand). Failure ⇒
  `InvalidOperationException` — never return an unverified play.
- Cancellation is not an error: best verified incumbent, `IsOptimal = false`.

## 5. Public API

- `RummikubSolver.Solve(Hand hand, BoardState board, SolverOptions? options = null, CancellationToken cancellationToken = default) → SolverResult`
  (replaces `SolveMaxTiles`; all callers updated).
- `SolverResult(BoardState Board, EquatableArray<Tile> PlayedTiles, bool IsOptimal)`;
  `SolverResult.Create(board, played, isOptimal)` keeps played tiles sorted.
- Output board = kept board groups (unchanged) + chosen catalogue groups. New
  runs of the same colour where one ends at `v` and the other starts at `v+1`
  are merged (repeat until none) via `Run.Create`, but only when the merged
  run has at most 2 jokers and length ≤ 13; kept groups never merge.

## 6. Strategy hooks (structure only)

1. Rules (constraints) and preferences (`ObjectiveLevel` list) are built
   separately inside `RummikubModelBuilder`; a later caller can append levels.
2. `RummikubModelBuilder.Build` → `RummikubModel` is separate from solving
   (`internal`), so callers can add constraints (e.g. `Σ y_t ≤ k`, `y_t = 0`,
   no-good cuts) before calling the engine.
3. `Mip/*` knows nothing about Rummikub.

## 7. Server and client

- `SolveRequest.Options`: `SolveOptionsDto { bool? InitialMeldDone; string? Objective; bool? KeepJokers }`, all optional (server defaults).
  Unknown objective ⇒ 400 at `options.objective`: `"unknown objective 'x'"`.
- `SolveResponse.Optimal` (`bool`).
- Controller passes `HttpContext.RequestAborted` as the cancellation token.
- Client: "Initial meld made" checkbox (default checked), objective selector
  (Tile count / Points), "Keep jokers" checkbox; shows "Not proven optimal"
  when `optimal == false`.

## 8. Testing

- **Simplex**: textbook LPs with known optimum; infeasible; unbounded;
  Beale's cycling example terminates with the correct optimum; equality and
  `≥` constraints; variable upper bounds.
- **Branch and bound**: small knapsack and assignment IPs with known integer
  optima; fractional LP root (e.g. odd-cycle cover) resolved to the integer
  optimum; node limit returns `IsOptimal = false` with a valid incumbent.
- **Catalogue**: every column legal; no two columns with the same tile
  multiset; expected counts (asserted from an independent count in tests).
- **Solver behaviour**: all existing `RummikubSolverTests` pass against
  `Solve`; plus
  - initial meld: hand R10 R11 R12 plays 3 tiles; hand R1 R2 R3 plays 0;
    board unchanged in both;
  - `Points` vs `TileCount` choose different plays on a constructed hand;
  - `KeepJokers` keeps a joker that `TileCount` would play when it does not
    increase the primary score;
  - least rearrangement: of two equally good plays, the one keeping a board
    group intact is returned.
- **Oracle**: ≥ 200 random small games (board ≤ 20 tiles, hand ≤ 8) give the
  same tiles-played count as `DfsReferenceSolver`, and pass
  `SolutionVerifier`.
- **Performance** (`[Category("Performance")]`): random positions with 60–90
  board tiles and a 14-tile hand each solve in < 1 s with `IsOptimal == true`.
- **Server**: `DtoMapper` maps options, rejects unknown objective, emits
  `optimal`.

## Done when

- `dotnet build` has no new warnings; all tests pass, including the oracle
  and performance categories.
- Server and client expose the options and the optimality flag.
- `SolveMaxTiles` and the DFS are no longer used outside
  `DfsReferenceSolver` and its tests.
