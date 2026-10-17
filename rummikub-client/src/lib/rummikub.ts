export type Color = 'Red' | 'Blue' | 'Black' | 'Yellow';
export type GroupType = 'Set' | 'Run';
export type Objective = 'TileCount' | 'Points';

export type TileDto = { color?: Color; value?: number; joker?: boolean };
export type GroupInputDto = { type: GroupType; tiles: TileDto[] };
export type SolveOptions = { initialMeldDone: boolean; objective: Objective; keepJokers: boolean };
export type SolveRequest = { hand: TileDto[]; board: GroupInputDto[]; options: SolveOptions };
export type SlotDto = { color: Color; value: number; joker: boolean };
export type GroupDto = { type: GroupType; slots: SlotDto[] };
export type SolveResponse = { playedTiles: TileDto[]; board: GroupDto[]; tilesPlayed: number; optimal: boolean };

export const COLORS: Color[] = ['Red', 'Blue', 'Yellow', 'Black'];
export const VALUES = Array.from({ length: 13 }, (_, i) => i + 1);

/** A standard set has two of every numbered tile and two jokers. */
export const COPIES_PER_TILE = 2;

export const JOKER: TileDto = { joker: true };

export const tileKey = (t: TileDto) => (t.joker ? 'J' : `${t.color}-${t.value}`);

export const tileName = (t: TileDto) => (t.joker ? 'joker' : `${t.color?.toLowerCase()} ${t.value}`);

export const countTiles = (tiles: TileDto[]) => {
  const counts = new Map<string, number>();
  for (const t of tiles) counts.set(tileKey(t), (counts.get(tileKey(t)) ?? 0) + 1);
  return counts;
};

/** Removes one copy of each tile in `taken` from `tiles`. */
export const subtractTiles = (tiles: TileDto[], taken: TileDto[]) => {
  const remaining = countTiles(taken);
  return tiles.filter(t => {
    const n = remaining.get(tileKey(t)) ?? 0;
    if (n === 0) return true;
    remaining.set(tileKey(t), n - 1);
    return false;
  });
};

/** For each slot in the solved board, whether it is a tile placed from the hand. */
export const markPlayedSlots = (board: GroupDto[], played: TileDto[]) => {
  const remaining = countTiles(played);
  return board.map(g =>
    g.slots.map(s => {
      const key = s.joker ? 'J' : `${s.color}-${s.value}`;
      const n = remaining.get(key) ?? 0;
      if (n === 0) return false;
      remaining.set(key, n - 1);
      return true;
    }),
  );
};

export const toInputGroups = (board: GroupDto[]): GroupInputDto[] =>
  board.map(g => ({
    type: g.type,
    tiles: g.slots.map(s => (s.joker ? JOKER : { color: s.color, value: s.value })),
  }));

export type SolveOutcome = { ok: true; result: SolveResponse } | { ok: false; errors: string[] };

/** When set, solves go to the Rummikub.Server HTTP API; otherwise the solver runs in the browser as WebAssembly. */
const API_URL: string | undefined = import.meta.env.VITE_API_URL;

export const solve = (request: SolveRequest) => (API_URL ? solveOverHttp(API_URL, request) : solveInBrowser(request));

type WorkerReply = { id: number; responseJson?: string; error?: string; runtimeMissing?: boolean };

let worker: Worker | undefined;
let nextId = 0;
const pending = new Map<number, (reply: WorkerReply) => void>();

function getWorker() {
  if (!worker) {
    worker = new Worker(new URL('./solver.worker.ts', import.meta.url), { type: 'module' });
    worker.onmessage = (e: MessageEvent<WorkerReply>) => {
      pending.get(e.data.id)?.(e.data);
      pending.delete(e.data.id);
    };
  }
  return worker;
}

/** Starts downloading the .NET runtime before the first solve needs it. */
export const warmUpSolver = () => {
  if (!API_URL) getWorker();
};

async function solveInBrowser(request: SolveRequest): Promise<SolveOutcome> {
  const id = nextId++;
  const reply = await new Promise<WorkerReply>(resolve => {
    pending.set(id, resolve);
    getWorker().postMessage({ id, requestJson: JSON.stringify(request) });
  });
  if (reply.responseJson === undefined) {
    const hint = reply.runtimeMissing && import.meta.env.DEV ? ' Run `npm run build:wasm` first.' : '';
    return { ok: false, errors: [`${reply.error}${hint}`] };
  }
  const body: SolveResponse | { errors: Record<string, string> } = JSON.parse(reply.responseJson);
  if ('errors' in body) return { ok: false, errors: Object.entries(body.errors).map(([k, v]) => `${k}: ${v}`) };
  return { ok: true, result: body };
}

async function solveOverHttp(apiUrl: string, request: SolveRequest): Promise<SolveOutcome> {
  let response: Response;
  try {
    response = await fetch(`${apiUrl}/api/solver/solve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
  } catch {
    return { ok: false, errors: [`Can't reach the solver at ${apiUrl}. Check that the server is running.`] };
  }
  if (response.status === 400) {
    const problem: { errors?: Record<string, string[]> } = await response.json();
    return { ok: false, errors: Object.entries(problem.errors ?? {}).map(([k, v]) => `${k}: ${v.join(', ')}`) };
  }
  if (!response.ok) return { ok: false, errors: [`The solver returned an error (${response.status}).`] };
  return { ok: true, result: await response.json() };
}
