# Rummikub Solver

Lay out the table and your rack, and the solver finds the move that gets the most tiles out of your hand. It rearranges the groups already on the table where that helps.

**Try it:** https://goctokloc.github.io/RummikubSolver/

The solver is written in C# and compiled to WebAssembly, so it runs entirely in your browser. There is no backend.

## Features

- **Exact answers.** The move is found with an integer linear program solved by a hand-written simplex and branch-and-bound. There are no native or third-party solver dependencies. Results are marked *Best possible* when optimality is proven.
- **Table rearrangement.** Existing sets and runs can be split, merged and rebuilt. When several moves score the same, the one that disturbs the table least wins.
- **Initial meld.** Turn off *Opening meld already made* to require a first play of at least 30 points from your rack alone, without touching the table.
- **Objectives.** Maximise the number of tiles played, or their face value (a joker counts 30).
- **Keep jokers.** Optionally prefer holding on to jokers when playing them wouldn't score more.

## Project layout

| Project | What it is |
|---|---|
| `Rummikub.Core` | Domain model: tiles, sets, runs, board and hand. |
| `Rummikub.Solver` | The solver: group catalogue, ILP model, simplex and branch-and-bound. |
| `Rummikub.Wasm` | Browser build of the solver. Exports a JSON `Solve` function to JavaScript. |
| `Rummikub.Server` | ASP.NET Core API (`POST /api/solver/solve`) exposing the same JSON contract. Optional. |
| `Rummikub.Runner` | Small console app for trying the solver from code. |
| `Rummikub.Tests` | NUnit tests, including a brute-force reference solver used as an oracle. |
| `rummikub-client` | React + Vite + shadcn/ui front end. |

Design notes for the solver are in [docs/superpowers/specs](docs/superpowers/specs).

## Running locally

Requirements: .NET 8 SDK and Node.js 24.

```bash
cd rummikub-client
npm ci
npm run build:wasm
npm run dev
```

`build:wasm` publishes `Rummikub.Wasm` and copies the .NET runtime into `public/_framework`. Run it again after changing solver code.

To use the HTTP API instead of WebAssembly, for example to debug the solver in your IDE, start the server and point the client at it:

```bash
dotnet run --project Rummikub.Server --launch-profile http
```

```bash
cd rummikub-client
VITE_API_URL=http://localhost:5155 npm run dev
```

## Tests

```bash
dotnet test
```

## Deployment

Every push to `main` runs [`.github/workflows/pages.yml`](.github/workflows/pages.yml). It runs the tests, builds the WebAssembly solver and the client with the repository name as the base path, and publishes `rummikub-client/dist` to GitHub Pages. In the repository settings, Pages must use **GitHub Actions** as its source.
