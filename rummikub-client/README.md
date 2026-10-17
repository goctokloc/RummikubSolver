# rummikub-client

React + Vite + shadcn/ui front end for the Rummikub solver. See the [root README](../README.md) for setup.

| Script | What it does |
|---|---|
| `npm run build:wasm` | Publishes `Rummikub.Wasm` into `public/_framework`. Needed once before `dev`. |
| `npm run dev` | Starts the dev server. |
| `npm run build` | Builds the solver and the client into `dist`. Set `BASE_PATH` to serve from a subpath. |
| `npm run lint` | Runs ESLint. |

By default the solver runs in a Web Worker ([src/lib/solver.worker.ts](src/lib/solver.worker.ts)). Set `VITE_API_URL` to call `Rummikub.Server` over HTTP instead.
