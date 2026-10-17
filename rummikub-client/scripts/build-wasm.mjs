// Publishes Rummikub.Wasm and copies its runtime into public/_framework, where the solver worker loads it.
import { execFileSync } from 'node:child_process';
import { cpSync, rmSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const project = path.join(root, 'Rummikub.Wasm');
const out = path.join(project, 'bin', 'publish');
const target = path.join(root, 'rummikub-client', 'public', '_framework');

execFileSync('dotnet', ['publish', project, '-c', 'Release', '-o', out], { stdio: 'inherit' });

rmSync(target, { recursive: true, force: true });
// GitHub Pages compresses on the fly, so the precompressed .br/.gz copies are dead weight.
cpSync(path.join(out, 'wwwroot', '_framework'), target, {
  recursive: true,
  filter: src => !/\.(br|gz)$/.test(src),
});
