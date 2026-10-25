import { cp, mkdir, rm } from 'node:fs/promises';
import { bundlePlaytest } from './bundle-playtest.js';

await rm('dist', { recursive: true, force: true });
await mkdir('dist', { recursive: true });
await cp('src/presentation', 'dist', { recursive: true });
await cp('src/game', 'dist/game', { recursive: true });
await cp('src/controller', 'dist/controller', { recursive: true });
try { await cp('public/assets', 'dist/assets', { recursive: true }); } catch (error) { if (error.code !== 'ENOENT') throw error; }
await bundlePlaytest();
console.log('Built Recovery Alpha 0.1.1 into dist/.');
