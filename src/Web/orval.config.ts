import { existsSync } from 'node:fs';
import { defineConfig } from 'orval';
import { apiProject, readOpenApiDocument, services, toGatewayDocument } from './tooling/openapi.ts';

// Typed TanStack Query hooks per service (tech discussion T10, FE5), generated from the OpenAPI documents saved
// in openapi/<service>.json (`npm run api:fetch` refreshes them from the running services). Services without a
// saved document are skipped. Output: src/shared/api/generated/<service>/ — never edit it by hand.
const projects = Object.fromEntries(
  services
    .filter((service) => existsSync(`openapi/${service.name}.json`))
    .map((service) => [
      service.name,
      apiProject(
        toGatewayDocument(readOpenApiDocument(`openapi/${service.name}.json`), service.name),
        `src/shared/api/generated/${service.name}`,
      ),
    ]),
);

if (Object.keys(projects).length === 0) {
  console.warn('No OpenAPI documents in openapi/ yet: run `npm run api:fetch` while the services run.');
}

export default defineConfig(projects);
