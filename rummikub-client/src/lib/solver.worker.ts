/// <reference lib="webworker" />
// Hosts the .NET solver (Rummikub.Wasm) off the main thread so a long solve doesn't freeze the UI.

type SolverExports = { SolverExports: { Solve(requestJson: string): string } };

const exportsReady: Promise<SolverExports> = (async () => {
  const { dotnet } = await import(/* @vite-ignore */ `${import.meta.env.BASE_URL}_framework/dotnet.js`);
  const runtime = await dotnet.create();
  return runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
})();

self.onmessage = async (e: MessageEvent<{ id: number; requestJson: string }>) => {
  const { id, requestJson } = e.data;
  let exports: SolverExports;
  try {
    exports = await exportsReady;
  } catch (error) {
    self.postMessage({ id, error: `Couldn't load the solver: ${describe(error)}.`, runtimeMissing: true });
    return;
  }
  try {
    self.postMessage({ id, responseJson: exports.SolverExports.Solve(requestJson) });
  } catch (error) {
    self.postMessage({ id, error: `The solver failed: ${describe(error)}.` });
  }
};

const describe = (error: unknown) => (error instanceof Error ? error.message : String(error));
