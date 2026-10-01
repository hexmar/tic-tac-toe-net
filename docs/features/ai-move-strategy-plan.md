# Implementation Plan — `IAiMoveStrategy`

**Status:** Proposed
**Date:** 2026-10-01
**Scope:** `TicTacToeNet.TelegramBot` project only

---

## 1. Motivation

Today the bot's move is an inline `Random.Shared` call buried deep inside
`TelegramBackgroundService.ExecuteAsync`:

```csharp
// TicTacToeNet.TelegramBot/TelegramBackgroundService.cs (~line 365)
#pragma warning disable CA5394 // Do not use insecure randomness
var botSelectedIndex = Random.Shared.Next(emptyCellsIndexes.Count);
#pragma warning restore CA5394 // Do not use insecure randomness
state[emptyCellsIndexes[botSelectedIndex]] = 'O';
```

Problems:

- **Hardcoded intelligence** — the only way to make the bot smarter is to edit the
  background service. The decision policy is not an interchangeable unit.
- **Untestable in isolation** — to test "does the bot ever pick a filled cell?" you
  must run the whole hosted service with a fake Telegram API.
- **Hidden coupling** — the service knows *how* the bot thinks, not just that
  *the bot makes a move*.

The Strategy pattern turns the move policy into a swappable, injectable
dependency of a single method.

---

## 2. Goals / Non-Goals

### Goals
1. Extract move selection behind an `IAiMoveStrategy` interface.
2. Provide a `RandomAiMoveStrategy` that **preserves current behaviour exactly**.
3. Register the strategy in DI so it can be swapped per-environment via
   configuration (no code change to swap policies).
4. Move the relevant `CA5394` suppression from the service into the strategy.
5. Make the strategy unit-testable without the hosted service.

### Non-Goals (this iteration)
- Implementing an unbeatable (minimax) bot — planned as a follow-up feature that
  this abstraction is explicitly designed to absorb.
- Refactoring `CheckWin` / introducing a `GameBoard` domain value object
  (separate feature; the strategy consumes the raw `char[]` for now).
- Persisting game state; guest-mode vs private-chat differences (transparent to
  the strategy — both paths use the same `'O'` bot mark).

---

## 3. Design

### 3.1 Folder & naming

Following repo conventions (PascalCase folders, namespace mirrors folder path
from repo root, all types `internal`):

```
TicTacToeNet.TelegramBot/
  Ai/
    IAiMoveStrategy.cs
    RandomAiMoveStrategy.cs
```

Namespace: `TicTacToeNet.TelegramBot.Ai`.

### 3.2 The interface

```csharp
namespace TicTacToeNet.TelegramBot.Ai;

/// <summary>
/// Decides which empty cell the bot ('O') plays on.
/// </summary>
internal interface IAiMoveStrategy
{
    /// <summary>
    /// Picks a cell index (0..8) for the given board state.
    /// </summary>
    /// <param name="state">
    /// The 9-element board. Indices 0–8, row-major.
    /// Values: '-' = empty, 'X' = human, 'O' = bot.
    /// Must contain at least one '-' when called
    /// (the caller handles the full-board edge case before calling).
    /// Empty cells are derivable from the state — the strategy scans it itself.
    /// </param>
    /// <returns>An index where <c>state[index] == '-'</c>.</returns>
    int PickMove(char[] state);
}
```

**Contract:**
- Return value **must** be an index where `state[index] == '-'`.
- The caller guarantees at least one empty cell; a strategy may throw
  `ArgumentException` (or equivalent) if it is ever called on a
  full board, but must not do so for any valid state.
- `state` may be treated as read-only by implementations (the service does
  not expect it mutated); implementations may allocate internally.
- Method must be **synchronous** — no I/O, so a plain `int` return keeps the
  strategy trivially testable. (If a future strategy needs model loading, it can
  take it in the constructor via DI; no `Task` needed on the hot path.)
- Implementation must be **stateless/thread-safe** — it's registered as a
  singleton and the background service is single-threaded, but statelessness
  keeps future parallelism free.

**Why not `Task<int>`, a `GameBoard` parameter, or a context object?**

| Alternative | Rejected because |
|---|---|
| `Task<int> PickMoveAsync(...)` | No implementation needs I/O; async would force `await`/`GetAwaiter` plumbing everywhere for zero benefit. |
| `PickMove(char[] state, int[] emptyCellIndexes)` | Duplicates information already in the state — the empty cells *are* the `'-'` cells. Deriving them once per call (8 comparisons) is cheaper than keeping a parallel array in sync, and every strategy (random, heuristic, minimax) needs the full state anyway. |
| `PickMove(GameBoard board)` | `GameBoard` value object is a separate planned refactor; coupling to it now blocks landing this feature. Re-evaluate when `GameBoard` lands. |
| `PickMove(AiMoveContext ctx)` | A context bag is YAGNI for tic-tac-toe — the full game state *is* the board; there are no hidden state channels (no decks, no power-ups). |

The minimal `PickMove(char[] state)` signature is the narrowest seam that
still supports random, heuristic, and minimax strategies.

### 3.3 First implementation — `RandomAiMoveStrategy`

Faithful extraction of the current behaviour (uniform random over empty cells):

```csharp
namespace TicTacToeNet.TelegramBot.Ai;

internal sealed class RandomAiMoveStrategy : IAiMoveStrategy
{
    public int PickMove(char[] state)
    {
        var empty = new List<int>(9);
        for (var i = 0; i < state.Length; i++)
        {
            if (state[i] == '-')
            {
                empty.Add(i);
            }
        }

#pragma warning disable CA5394 // Do not use insecure randomness
        return empty[Random.Shared.Next(empty.Count)];
#pragma warning restore CA5394 // Do not use insecure randomness
    }
}
```

Notes:
- The `CA5394` pragma **moves here** and is removed from
  `TelegramBackgroundService.cs` (line ~365–367). Only that one pragma is in
  scope; the other two in the service (turn-order coin flip and inline message
  ID) are unrelated and stay put.
- `state` is now the *only* input — the strategy derives empty cells itself
  (`'-'` cells), which is what the interface contract promises. This is a
  faithful, uniform re-expression of today's inline random pick.
- The per-call `List<int>` (≤ 9 items) is negligible; if profiling ever says
  otherwise, swap for a stack-allocated `Span`/rented buffer without touching
  the interface.

### 3.4 DI registration & injection

`Program.cs`:

```csharp
builder.Services.AddSingleton<IAiMoveStrategy, RandomAiMoveStrategy>();
```

(`AddSingleton` matches the existing DI style — singletons/factories only, no
`AddTransient`/`AddScoped`.)

`TelegramBackgroundService` constructor gains one parameter:

```csharp
internal sealed partial class TelegramBackgroundService(
    ITelegramUpdateQueue telegramUpdateQueue,
    ITelegramBotApiClient apiClient,
    IAiMoveStrategy aiMoveStrategy,          // ← new
    ILogger<TelegramBackgroundService> logger)
    : BackgroundService
```

### 3.5 Call-site changes in `ExecuteAsync`

The bot move happens in the **callback_query → private message** branch.
There are two places where the bot's mark is placed:

1. **Forced final move** (~line 325, `emptyCellsIndexes.Count == 1`):
   ```csharp
   // before
   state[emptyCellsIndexes[0]] = 'O';
   // after
   state[aiMoveStrategy.PickMove(state)] = 'O';
   ```
   When exactly one cell is empty, `PickMove` is trivially forced to return it,
   so routing the single-candidate case through the strategy keeps the contract
   uniform ("the service never chooses for the bot") at zero cost.
   **Alternative:** keep the direct `state[emptyCellsIndexes[0]] = 'O'` and
   document that the strategy is only called for `count >= 2` — also fine,
   one fewer call. *(Flagged as an explicit decision point in the PR; default
   is to call `PickMove` for uniformity.)*

2. **Normal move** (~line 365):
   ```csharp
   // before
   #pragma warning disable CA5394 // Do not use insecure randomness
   var botSelectedIndex = Random.Shared.Next(emptyCellsIndexes.Count);
   #pragma warning restore CA5394 // Do not use insecure randomness
   state[emptyCellsIndexes[botSelectedIndex]] = 'O';

   // after
   state[aiMoveStrategy.PickMove(state)] = 'O';
   ```
   The `emptyCellsIndexes` local **stays** in the service: the surrounding
   code still needs the empty-cell *count* for the `== 0` / `== 1` edge-case
   branches above it. It is simply no longer passed into the strategy — each
   strategy derives the empty set from `state` itself, which is why the
   interface can be one parameter.

**Guest-mode path** (`guest_message` → inline game): no bot move happens there
— both players are humans, so no change. This is a deliberate property of the
design: the AI strategy only participates in private-chat bot games. Document
this in the PR description because it's a subtle, easy-to-miss fact.

---

## 4. Implementation Steps (ordered)

### Phase 1 — Core extraction (behaviour-preserving, one PR)

1. Create `TicTacToeNet.TelegramBot/Ai/IAiMoveStrategy.cs`.
2. Create `TicTacToeNet.TelegramBot/Ai/RandomAiMoveStrategy.cs` (exact
   behaviour of today's inline random pick, pragma included).
3. `Program.cs`: register `AddSingleton<IAiMoveStrategy, RandomAiMoveStrategy>()`.
4. `TelegramBackgroundService`:
   - Add `IAiMoveStrategy aiMoveStrategy` to the primary constructor.
   - Replace the `Random.Shared` block with the `PickMove` call (call site 2).
   - Decide call-site 1 treatment per §3.5.
   - Keep the `emptyCellsIndexes` local (still used for the `== 0` / `== 1`
     edge-case branches) but stop passing it into the strategy.
   - Remove the now-relocated `CA5394` pragma.
5. Build & verify:
   - `dotnet build TicTacToeNet.slnx` — must be warning-clean
     (TelegramBot runs with `<AnalysisMode>All</AnalysisMode>`).
   - Manual smoke test with a Telegram test bot:
     - `/start` → `/newgame` → play to completion, bot still plays randomly
       and wins/draws exactly as before.
     - Guest-mode game via inline query — unaffected, both humans.

### Phase 2 — Test project + strategy tests (second PR)

Per `AGENTS.md`: no test project exists yet — create a **separate xUnit
project** `TicTacToeNet.TelegramBot.Tests` and confirm with the owner before
wiring it into `TicTacToeNet.slnx`.

Minimum tests for `RandomAiMoveStrategy`:

| # | Test | Asserts |
|---|---|---|
| 1 | `PickMove_returns_an_empty_cell` | Over many iterations, every result satisfies `state[result] == '-'`. |
| 2 | `PickMove_is_eventually_all_cells` | Across iterations, every empty index is chosen at least once (guards against degenerate ranges). |
| 3 | `PickMove_single_candidate_returns_it` | A board with exactly one `'-'` at index 5 returns 5. |
| 4 | `PickMove_does_not_mutate_state` | `state` is unchanged after the call. |

These tests are also the **acceptance tests any future strategy must pass** —
copy them as a shared template for the minimax strategy (where test 2 becomes
"every empty cell is chosen from equal boards at least once" or is dropped in
favour of determinism checks).

### Phase 3 — Configuration-driven strategy selection (small, optional)

Swap strategies via `appsettings.json` without recompiling:

```jsonc
// appsettings.json
"TelegramApi": {
    "AiStrategy": "Random"          // "Random" | "Minimax" | ...
}
```

```csharp
// Program.cs — factory singleton instead of AddSingleton<TService, TImpl>
builder.Services.AddSingleton<IAiMoveStrategy>(services =>
{
    var config = services.GetRequiredService<IOptions<TelegramApiConfiguration>>().Value;
    return config.AiStrategy switch
    {
        "Minimax" => new MinimaxAiMoveStrategy(),
        _         => new RandomAiMoveStrategy(),
    };
});
```

This is a **factory** over the strategy and uses only the allowed DI shapes
(`Configure<>` + factory singletons). Keep `AiStrategy` in the existing
`TelegramApiConfiguration` class (add a public property with `internal static`
`ConfigurationKey` pattern intact — no new config section).

### Phase 4 (follow-up feature, own plan) — `MinimaxAiMoveStrategy`

Only possible *because* of this feature. Sketch:

```csharp
internal sealed class MinimaxAiMoveStrategy : IAiMoveStrategy
{
    public int PickMove(char[] state)
    {
        // Bot is 'O' and moves second in this game mode.
        var bestScore = int.MinValue;
        var bestIndex = -1;
        for (var i = 0; i < state.Length; i++)
        {
            if (state[i] != '-')
            {
                continue;
            }

            var copy = (char[])state.Clone();
            copy[i] = 'O';
            var score = Minimax(copy, isMaximizing: false, depth: 0);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static int Minimax(char[] state, bool isMaximizing, int depth)
    {
        var winner = CheckWin(state);           // shared with service, or inline
        if (winner == 'O') return 10 - depth;   // prefer faster wins
        if (winner == 'X') return depth - 10;   // prefer slower losses
        if (state.All(c => c != '-')) return 0;

        // …recurse over empty cells, standard minimax…
    }
}
```

Tie-break on equal scores: first index in row-major order (deterministic,
testable). Tic-tac-toe's 9! game tree is small enough that naive minimax with
no memoization runs in microseconds — no alpha-beta or caching needed at this
board size.

---

## 5. Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Behaviour change sneaks in during extraction | Phase 1 is one PR, diff should be mechanical; smoke-test private + guest games end-to-end before merge. |
| `CA5394` reappears in the service or disappears from the strategy | Build with `<AnalysisMode>All</AnalysisMode>` and require zero warnings. |
| Strategy called on a full board and throws | The service's `emptyCellsIndexes.Count == 0` branch already handles this before calling `PickMove`; add a test that pins the contract. |
| DI style drift (`AddTransient`) | Follow `AGENTS.md`: singletons/factories only. |
| Call-site 1 (forced last move) inconsistency | Explicit decision recorded in the PR (§3.5) with the trade-off visible. |

---

## 6. Definition of Done

- [ ] `IAiMoveStrategy` + `RandomAiMoveStrategy` in `TicTacToeNet.TelegramBot/Ai/`
- [ ] `TelegramBackgroundService` consumes the strategy; no inline bot-move logic remains
- [ ] `CA5394` pragma relocated; build warning-clean
- [ ] DI registration in `Program.cs`
- [ ] Manual smoke test: private-chat game to completion; guest game unaffected
- [ ] (Phase 2) xUnit project with the 4 strategy tests passing
- [ ] (Phase 3, optional) `AiStrategy` config switch proven by running with `"Minimax"` after Phase 4
