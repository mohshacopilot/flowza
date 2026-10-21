import { readFileSync } from 'node:fs';

/** The services with an API behind the Gateway, and their fixed development ports (tasks.md, Phase 0 notes). */
export const services = [
  { name: 'identity', port: 5101 },
  { name: 'design', port: 5102 },
  { name: 'runtime', port: 5103 },
  { name: 'worker', port: 5104 },
] as const;

export type ServiceName = (typeof services)[number]['name'];

/** The parts of an OpenAPI document this tooling touches; everything else passes through unchanged. */
export interface OpenApiDocumentLike {
  openapi: string;
  paths?: Record<string, unknown>;
  servers?: unknown;
  [key: string]: unknown;
}

/**
 * A service describes its own paths (`/api/v1/users`); the browser calls them through the Gateway, which
 * routes `/api/<service>/{**}` to the service's `/api/{**}` (plan 001 §9). Rewrites every path that way and
 * drops `servers`, so the generated client uses relative URLs on the Gateway's own origin.
 */
export function toGatewayDocument(document: OpenApiDocumentLike, service: string): OpenApiDocumentLike {
  const paths: Record<string, unknown> = {};
  for (const [path, item] of Object.entries(document.paths ?? {})) {
    const gatewayPath = path.startsWith('/api/') ? `/api/${service}/${path.slice('/api/'.length)}` : path;
    if (gatewayPath in paths) {
      throw new Error(`Two paths of ${service} map to ${gatewayPath}.`);
    }
    paths[gatewayPath] = item;
  }
  const { servers: _servers, ...rest } = document;
  return { ...rest, paths };
}

export function readOpenApiDocument(file: string): OpenApiDocumentLike {
  return JSON.parse(readFileSync(file, 'utf8')) as OpenApiDocumentLike;
}

/** The orval project for one service document: TanStack Query hooks + models, calling our apiFetch. */
export function apiProject(document: OpenApiDocumentLike, outputDir: string) {
  return {
    input: { target: document as Record<string, unknown> },
    output: {
      mode: 'tags-split' as const,
      target: `${outputDir}/endpoints.ts`,
      schemas: `${outputDir}/model`,
      client: 'react-query' as const,
      httpClient: 'fetch' as const,
      clean: true,
      override: {
        mutator: { path: './src/shared/api/apiFetch.ts', name: 'apiFetch' },
        fetch: { includeHttpResponseReturnType: false },
        query: { signal: true },
      },
    },
  };
}
