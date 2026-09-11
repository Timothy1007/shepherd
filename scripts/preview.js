import { createReadStream } from 'node:fs';
import { stat } from 'node:fs/promises';
import { createServer } from 'node:http';
import { extname, join } from 'node:path';

const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.png': 'image/png' };
const server = createServer(async (request, response) => {
  const pathname = decodeURIComponent(request.url.split('?')[0]);
  const segments = pathname.split('/').filter(Boolean);

  if (segments.some((segment) => segment === '..')) {
    response.writeHead(400);
    response.end('Bad request');
    return;
  }

  const relative = segments.length ? join(...segments) : 'index.html';
  const path = join(process.cwd(), 'dist', relative);

  try {
    if (!(await stat(path)).isFile()) throw new Error('not a file');
    response.writeHead(200, { 'content-type': types[extname(path)] ?? 'application/octet-stream' });
    createReadStream(path).pipe(response);
  } catch {
    response.writeHead(404);
    response.end('Not found');
  }
});

server.listen(Number(process.env.PORT || 4173), '0.0.0.0', () => console.log(`Preview: http://localhost:${process.env.PORT || 4173}`));
