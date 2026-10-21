// `npm run api:fetch`: saves each running service's OpenAPI document to openapi/<service>.json, so the client
// can be generated (`npm run api:generate`) without the services running, for example in CI.
// Development only: it reads the services directly on their fixed development ports, not through the Gateway.
import { mkdirSync, writeFileSync } from 'node:fs';
import { services } from './openapi.ts';

const host = process.env.FLOWZA_SERVICES_HOST ?? 'http://localhost';
mkdirSync('openapi', { recursive: true });

let saved = 0;
for (const service of services) {
  const url = `${host}:${service.port}/openapi/v1.json`;
  try {
    const response = await fetch(url);
    if (!response.ok) {
      console.warn(`${service.name}: ${url} answered ${response.status}; skipped.`);
      continue;
    }
    const document: unknown = await response.json();
    writeFileSync(`openapi/${service.name}.json`, `${JSON.stringify(document, null, 2)}\n`);
    console.warn(`${service.name}: saved openapi/${service.name}.json`);
    saved++;
  } catch {
    console.warn(`${service.name}: ${url} is not reachable (is the AppHost running?); skipped.`);
  }
}

if (saved === 0) {
  process.exitCode = 1;
}
